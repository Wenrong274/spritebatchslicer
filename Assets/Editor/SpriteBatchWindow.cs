using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace SpriteBatch
{
    public class SpriteBatchWindow : EditorWindow
    {
        private BatchSettings _settings = new BatchSettings();
        private ReorderableList _folderList;
        private ReorderableList _rectList;

        private List<string> _allTexturePaths = new List<string>();
        private string[]     _previewNames;
        private int          _previewIndex    = 0;
        private Texture2D    _previewTexture;
        private Vector2      _scrollPos;

        [MenuItem("工具 (Tools)/Sprite 批次設定")]
        public static void ShowWindow()
        {
            var window = GetWindow<SpriteBatchWindow>("Sprite 批次設定");
            window.minSize = new Vector2(440, 620);
            window.Show();
        }

        private void OnEnable()
        {
            InitFolderList();
            InitRectList();
        }

        private void InitFolderList()
        {
            _folderList = new ReorderableList(
                _settings.targetFolders, typeof(DefaultAsset), true, true, true, true);
            _folderList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "目標資料夾");
            _folderList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var r = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.BeginChangeCheck();
                _settings.targetFolders[index] = (DefaultAsset)EditorGUI.ObjectField(
                    r, _settings.targetFolders[index], typeof(DefaultAsset), false);
                if (EditorGUI.EndChangeCheck())
                    RefreshTexturePaths();
            };
            _folderList.onAddCallback    = _ => { _settings.targetFolders.Add(null); RefreshTexturePaths(); };
            _folderList.onRemoveCallback = list => { _settings.targetFolders.RemoveAt(list.index); RefreshTexturePaths(); };
            _folderList.onChangedCallback = _ => RefreshTexturePaths();
        }

        private void InitRectList()
        {
            _rectList = new ReorderableList(
                _settings.spriteRects, typeof(SpriteRectDef), true, true, true, true);
            _rectList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "Sprite 切割 (後綴 | x | y | 寬 | 高 | 錨點 X | 錨點 Y)");
            _rectList.elementHeight = EditorGUIUtility.singleLineHeight + 4;
            _rectList.drawElementCallback = DrawRectElement;
            _rectList.onAddCallback = _ => _settings.spriteRects.Add(new SpriteRectDef());
        }

        private void DrawRectElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var def = _settings.spriteRects[index];
            float x = rect.x, y = rect.y + 2, h = EditorGUIUtility.singleLineHeight;

            def.nameSuffix    = EditorGUI.TextField(    new Rect(x,  y, 38, h), def.nameSuffix);    x += 42;
            def.rect.x        = EditorGUI.FloatField(   new Rect(x,  y, 48, h), def.rect.x);        x += 52;
            def.rect.y        = EditorGUI.FloatField(   new Rect(x,  y, 48, h), def.rect.y);        x += 52;
            def.rect.width    = EditorGUI.FloatField(   new Rect(x,  y, 48, h), def.rect.width);    x += 52;
            def.rect.height   = EditorGUI.FloatField(   new Rect(x,  y, 48, h), def.rect.height);   x += 52;
            def.pivot.x       = EditorGUI.FloatField(   new Rect(x,  y, 38, h), def.pivot.x);       x += 42;
            def.pivot.y       = EditorGUI.FloatField(   new Rect(x,  y, 38, h), def.pivot.y);
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            EditorGUILayout.Space(4);

            DrawFolderSection();
            EditorGUILayout.Space(6);
            DrawTextureSettingsSection();
            EditorGUILayout.Space(6);
            DrawRectSection();
            EditorGUILayout.Space(6);
            DrawPreviewSection();
            EditorGUILayout.Space(6);
            DrawApplySection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawFolderSection()   { _folderList.DoLayoutList(); }
        private void DrawRectSection()     { _rectList.DoLayoutList(); }

        private void DrawTextureSettingsSection()
        {
            EditorGUILayout.LabelField("貼圖設定 (Texture Settings)", EditorStyles.boldLabel);
            _settings.maxTextureSize      = EditorGUILayout.IntField(    "最大尺寸 (Max Size)",          _settings.maxTextureSize);
            _settings.filterMode          = (FilterMode)EditorGUILayout.EnumPopup("過濾模式 (Filter Mode)", _settings.filterMode);
            _settings.alphaIsTransparency = EditorGUILayout.Toggle(      "Alpha 透明度",                  _settings.alphaIsTransparency);
            _settings.compression         = (TextureImporterCompression)EditorGUILayout.EnumPopup(
                                                "壓縮 (Compression)", _settings.compression);
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("預覽圖片 (Preview)", EditorStyles.boldLabel);
            if (_allTexturePaths.Count == 0)
            {
                EditorGUILayout.HelpBox("請先新增目標資料夾。", MessageType.Info);
                return;
            }

            var names    = _previewNames ?? System.Array.Empty<string>();
            _previewIndex = Mathf.Clamp(_previewIndex, 0, Mathf.Max(0, _allTexturePaths.Count - 1));
            int newIndex = EditorGUILayout.Popup(_previewIndex, names);
            if (newIndex != _previewIndex || _previewTexture == null)
            {
                _previewIndex   = newIndex;
                _previewTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(_allTexturePaths[_previewIndex]);
            }

            if (_previewTexture == null) return;

            float maxW  = position.width - 24;
            float scale = Mathf.Min(1f, maxW / _previewTexture.width);
            float dispW = _previewTexture.width  * scale;
            float dispH = _previewTexture.height * scale;

            Rect previewRect = GUILayoutUtility.GetRect(dispW, dispH);
            previewRect.width = dispW;
            GUI.DrawTexture(previewRect, _previewTexture, ScaleMode.StretchToFill);

            foreach (var def in _settings.spriteRects)
            {
                float rx = previewRect.x + def.rect.x * scale;
                float ry = previewRect.y + (_previewTexture.height - def.rect.y - def.rect.height) * scale;
                float rw = def.rect.width  * scale;
                float rh = def.rect.height * scale;

                var overlay = new Rect(rx, ry, rw, rh);
                EditorGUI.DrawRect(overlay, new Color(1f, 1f, 0f, 0.15f));
                var border = new Color(1f, 1f, 0f, 0.9f);
                EditorGUI.DrawRect(new Rect(rx,          ry,          rw, 1),  border);
                EditorGUI.DrawRect(new Rect(rx,          ry + rh - 1, rw, 1),  border);
                EditorGUI.DrawRect(new Rect(rx,          ry,          1,  rh), border);
                EditorGUI.DrawRect(new Rect(rx + rw - 1, ry,          1,  rh), border);
            }
        }

        private void DrawApplySection()
        {
            int total       = _allTexturePaths.Count;
            int folderCount = _settings.targetFolders.Count(f => f != null);
            EditorGUILayout.LabelField($"將處理 {total} 張圖片（{folderCount} 個資料夾）");

            if (GUILayout.Button("套用全部 (Apply All)", GUILayout.Height(32)))
                ApplyAll();
        }

        private void RefreshTexturePaths()
        {
            var pathSet = new HashSet<string>();
            foreach (var folder in _settings.targetFolders)
            {
                if (folder == null) continue;
                var folderPath = AssetDatabase.GetAssetPath(folder);
                if (string.IsNullOrEmpty(folderPath)) continue;
                var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
                foreach (var guid in guids)
                    pathSet.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            _allTexturePaths = new List<string>(pathSet);
            _previewNames   = _allTexturePaths.Select(p => Path.GetFileNameWithoutExtension(p)).ToArray();
            _previewIndex   = 0;
            _previewTexture = null;
            Repaint();
        }

        private void ApplyAll()
        {
            var folderPaths = _settings.targetFolders
                .Where(f => f != null)
                .Select(f => AssetDatabase.GetAssetPath(f))
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            var errors = SpriteBatchProcessor.ValidatePreflight(folderPaths, _settings.spriteRects);
            if (errors.Count > 0)
            {
                EditorUtility.DisplayDialog("驗證失敗",
                    string.Join("\n", errors.Select(e => e.message)), "確認");
                return;
            }

            bool cancelled = false;
            SpriteBatchProcessor.ApplyResult result;
            try
            {
                result = SpriteBatchProcessor.ApplyToFolders(
                    _settings,
                    (progress, path) =>
                    {
                        cancelled = EditorUtility.DisplayCancelableProgressBar(
                            "Sprite 批次設定", Path.GetFileName(path), progress);
                    },
                    () => cancelled);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string title  = result.wasCancelled ? "已取消" : "套用完成";
            string report = result.wasCancelled ? "操作已被使用者取消。\n\n" : "";
            report += $"成功：{result.successCount} 張\n" +
                      $"跳過（尺寸不符）：{result.skippedPaths.Count} 張\n" +
                      $"失敗（其他錯誤）：{result.failedPaths.Count} 張";

            if (result.skippedPaths.Count > 0 || result.failedPaths.Count > 0)
            {
                report += "\n\n問題檔案：\n" +
                          string.Join("\n", result.skippedPaths.Concat(result.failedPaths));
            }

            EditorUtility.DisplayDialog(title, report, "確認");
            RefreshTexturePaths();
        }
    }
}
