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
        private const float PreviewMaxHeight = 240f;
        private static readonly Color[] RectOverlayColors =
        {
            new Color(0.298f, 0.686f, 0.314f), // Material Green 500
            new Color(0.129f, 0.588f, 0.953f), // Material Blue 500
            new Color(1.000f, 0.596f, 0.000f), // Material Orange 700
            new Color(0.957f, 0.263f, 0.212f), // Material Red 500
        };
        private static readonly int[]    MaxTextureSizeValues = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };
        private static readonly string[] MaxTextureSizeNames  = { "32", "64", "128", "256", "512", "1024", "2048", "4096", "8192" };

        private BatchSettings _settings = new();
        private ReorderableList _folderList;
        private ReorderableList _rectList;

        private List<string> _allTexturePaths = new();
        private string[] _previewNames;
        private int _previewIndex = 0;
        private Texture2D _previewTexture;
        private Vector2 _scrollPos;
        private GUIStyle _rectLabelStyle;
        private GUIStyle _folderHintStyle;

        [MenuItem("Tools/Sprite 批次設定")]
        public static void ShowWindow()
        {
            var window = GetWindow<SpriteBatchWindow>("Sprite 批次設定");
            window.minSize = new Vector2(520, 620);
            window.Show();
        }

        private void OnEnable()
        {
            LoadState();
            InitFolderList();
            InitRectList();
        }

        private void OnDisable()
        {
            SaveState();
        }

        private void InitFolderList()
        {
            _folderList = new ReorderableList(
                _settings.TargetFolders, typeof(DefaultAsset), true, true, true, true)
            {
                drawHeaderCallback = rect =>
                    EditorGUI.LabelField(rect, "目標資料夾"),
                drawElementCallback = (rect, index, isActive, isFocused) =>
                    {
                        var r = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);
                        EditorGUI.BeginChangeCheck();
                        _settings.TargetFolders[index] = (DefaultAsset)EditorGUI.ObjectField(
                            r, _settings.TargetFolders[index], typeof(DefaultAsset), false);
                        if (EditorGUI.EndChangeCheck())
                        {
                            RefreshTexturePaths();
                        }
                    },
                onAddCallback = _ => { _settings.TargetFolders.Add(null); RefreshTexturePaths(); },
                onRemoveCallback = list => { _settings.TargetFolders.RemoveAt(list.index); RefreshTexturePaths(); }
            };
        }

        private void InitRectList()
        {
            _rectList = new ReorderableList(
                _settings.SpriteRects, typeof(SpriteRectDef), true, true, true, true)
            {
                drawHeaderCallback = rect =>
                    EditorGUI.LabelField(
                        new Rect(rect.x + 20, rect.y, rect.width - 20, rect.height),
                        "Sprite 切割 (後綴 | X | Y | W | H | Pivot X | Pivot Y | 對齊)"),
                elementHeight = EditorGUIUtility.singleLineHeight + 4,
                drawElementCallback = DrawRectElement,
                onAddCallback = _ => _settings.SpriteRects.Add(new SpriteRectDef
                {
                    NameSuffix = $"_{_settings.SpriteRects.Count}"
                })
            };
        }

        private void DrawRectElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var def = _settings.SpriteRects[index];
            float x = rect.x, y = rect.y + 2, h = EditorGUIUtility.singleLineHeight;

            def.NameSuffix = EditorGUI.TextField(new Rect(x, y, 38, h), def.NameSuffix);
            x += 42;
            def.Rect.x = EditorGUI.FloatField(new Rect(x, y, 48, h), def.Rect.x);
            x += 52;
            def.Rect.y = EditorGUI.FloatField(new Rect(x, y, 48, h), def.Rect.y);
            x += 52;
            def.Rect.width = EditorGUI.FloatField(new Rect(x, y, 48, h), def.Rect.width);
            x += 52;
            def.Rect.height = EditorGUI.FloatField(new Rect(x, y, 48, h), def.Rect.height);
            x += 52;
            def.Pivot.x = EditorGUI.FloatField(new Rect(x, y, 38, h), def.Pivot.x);
            x += 42;
            def.Pivot.y = EditorGUI.FloatField(new Rect(x, y, 38, h), def.Pivot.y);
            x += 42;
            var newAlignment = (SpriteAlignment)EditorGUI.EnumPopup(new Rect(x, y, 84, h), def.Alignment);
            if (newAlignment != def.Alignment)
            {
                def.Alignment = newAlignment;
                def.Pivot = AlignmentToPivot(newAlignment, def.Pivot);
            }
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

        private void DrawFolderSection()
        {
            float listHeight = _folderList.GetHeight();
            Rect listRect = GUILayoutUtility.GetRect(0f, listHeight, GUILayout.ExpandWidth(true));
            _folderList.DoList(listRect);
            HandleFolderDrop(listRect);

            if (_settings.TargetFolders.Count == 0)
            {
                _folderHintStyle ??= new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    wordWrap = false,
                    normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
                };
                var hintRect = new Rect(listRect.x + 4, listRect.y + 21, listRect.width - 8, 36);
                EditorGUI.DrawRect(hintRect, new Color(1f, 1f, 1f, 0.03f));
                GUI.Label(hintRect, "將資料夾從 Project 窗口拖曳至此", _folderHintStyle);
            }
        }

        private void HandleFolderDrop(Rect dropRect)
        {
            var evt = Event.current;
            if (!dropRect.Contains(evt.mousePosition))
            {
                return;
            }

            if (evt.type == EventType.DragUpdated)
            {
                bool hasFolder = DragAndDrop.objectReferences.Any(obj =>
                    obj is DefaultAsset &&
                    AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj)));
                DragAndDrop.visualMode = hasFolder
                    ? DragAndDropVisualMode.Copy
                    : DragAndDropVisualMode.Rejected;
                evt.Use();
            }
            else if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                var newFolders = FilterNewFolders(_settings.TargetFolders, DragAndDrop.objectReferences);
                if (newFolders.Count > 0)
                {
                    _settings.TargetFolders.AddRange(newFolders);
                    RefreshTexturePaths();
                }
                evt.Use();
            }
        }

        private void DrawRectSection() { _rectList.DoLayoutList(); }

        private void DrawTextureSettingsSection()
        {
            EditorGUILayout.LabelField("貼圖設定 (Texture Settings)", EditorStyles.boldLabel);
            _settings.MaxTextureSize = EditorGUILayout.IntPopup(
                "最大尺寸 (Max Size)", _settings.MaxTextureSize, MaxTextureSizeNames, MaxTextureSizeValues);
            _settings.FilterMode = (FilterMode)EditorGUILayout.EnumPopup("過濾模式 (Filter Mode)", _settings.FilterMode);
            _settings.AlphaIsTransparency = EditorGUILayout.Toggle("Alpha 透明度", _settings.AlphaIsTransparency);
            _settings.Compression = (TextureImporterCompression)EditorGUILayout.EnumPopup(
                                                "壓縮 (Compression)", _settings.Compression);
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("預覽圖片 (Preview)", EditorStyles.boldLabel);
            if (_allTexturePaths.Count == 0)
            {
                EditorGUILayout.HelpBox("請先新增目標資料夾。", MessageType.Info);
                return;
            }

            string[] names = _previewNames ?? System.Array.Empty<string>();
            _previewIndex = Mathf.Clamp(_previewIndex, 0, Mathf.Max(0, _allTexturePaths.Count - 1));
            int newIndex = EditorGUILayout.Popup(_previewIndex, names);
            if (newIndex != _previewIndex || _previewTexture == null)
            {
                _previewIndex = newIndex;
                _previewTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(_allTexturePaths[_previewIndex]);
            }

            if (_previewTexture == null)
            {
                return;
            }

            float maxW = position.width - 24;
            float scale = Mathf.Min(1f, maxW / _previewTexture.width, PreviewMaxHeight / _previewTexture.height);
            float dispW = _previewTexture.width * scale;
            float dispH = _previewTexture.height * scale;

            Rect previewRect = GUILayoutUtility.GetRect(dispW, dispH);
            previewRect.width = dispW;
            GUI.DrawTexture(previewRect, _previewTexture, ScaleMode.StretchToFill);

            _rectLabelStyle ??= new GUIStyle(EditorStyles.miniLabel);
            var labelStyle = _rectLabelStyle;
            for (int i = 0; i < _settings.SpriteRects.Count; i++)
            {
                var def = _settings.SpriteRects[i];
                Color c = RectOverlayColors[i % RectOverlayColors.Length];

                float rx = previewRect.x + def.Rect.x * scale;
                float ry = previewRect.y + (_previewTexture.height - def.Rect.y - def.Rect.height) * scale;
                float rw = def.Rect.width * scale;
                float rh = def.Rect.height * scale;

                var overlay = new Rect(rx, ry, rw, rh);
                EditorGUI.DrawRect(overlay, new Color(c.r, c.g, c.b, 0.15f));
                EditorGUI.DrawRect(new Rect(rx,          ry,          rw,  1), c);
                EditorGUI.DrawRect(new Rect(rx,          ry + rh - 1, rw,  1), c);
                EditorGUI.DrawRect(new Rect(rx,          ry,          1,  rh), c);
                EditorGUI.DrawRect(new Rect(rx + rw - 1, ry,          1,  rh), c);
                if (rw > 6 && rh > 14)
                {
                    labelStyle.normal.textColor = c;
                    GUI.Label(new Rect(rx + 2, ry + 1, rw - 4, 14), def.NameSuffix, labelStyle);
                }
            }
        }

        private void DrawApplySection()
        {
            int total = _allTexturePaths.Count;
            int folderCount = _settings.TargetFolders.Count(f => f != null);
            EditorGUILayout.LabelField($"將處理 {total} 張圖片（{folderCount} 個資料夾）");

            if (GUILayout.Button("套用全部 (Apply All)", GUILayout.Height(32)))
            {
                ApplyAll();
            }
        }

        private void LoadState()
        {
            var s = SpriteBatchWindowState.instance;
            _settings.MaxTextureSize = s.MaxTextureSize;
            _settings.FilterMode = s.FilterMode;
            _settings.AlphaIsTransparency = s.AlphaIsTransparency;
            _settings.Compression = s.Compression;
            _settings.SpriteRects = new List<SpriteRectDef>(s.SpriteRects);
            _settings.TargetFolders.Clear();
            foreach (var path in s.FolderPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                if (asset != null)
                {
                    _settings.TargetFolders.Add(asset);
                }
            }
        }

        private void SaveState()
        {
            var s = SpriteBatchWindowState.instance;
            s.MaxTextureSize = _settings.MaxTextureSize;
            s.FilterMode = _settings.FilterMode;
            s.AlphaIsTransparency = _settings.AlphaIsTransparency;
            s.Compression = _settings.Compression;
            s.SpriteRects = new List<SpriteRectDef>(_settings.SpriteRects);
            s.FolderPaths.Clear();
            foreach (var folder in _settings.TargetFolders)
            {
                if (folder != null)
                {
                    s.FolderPaths.Add(AssetDatabase.GetAssetPath(folder));
                }
            }
            s.Save();
        }

        private void RefreshTexturePaths()
        {
            string preservedPath = _previewIndex < _allTexturePaths.Count
                ? _allTexturePaths[_previewIndex]
                : null;

            var pathSet = new HashSet<string>();
            foreach (var folder in _settings.TargetFolders)
            {
                if (folder == null)
                {
                    continue;
                }

                string folderPath = AssetDatabase.GetAssetPath(folder);
                if (string.IsNullOrEmpty(folderPath))
                {
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
                foreach (string guid in guids)
                {
                    _ = pathSet.Add(AssetDatabase.GUIDToAssetPath(guid));
                }
            }

            _allTexturePaths = new List<string>(pathSet);
            _previewNames = _allTexturePaths
                .Select(p =>
                {
                    string ext = Path.GetExtension(p);
                    return p.StartsWith("Assets/")
                        ? (p["Assets/".Length..^ext.Length] is { Length: > 0 } rel ? rel : Path.GetFileNameWithoutExtension(p))
                        : Path.GetFileNameWithoutExtension(p);
                })
                .ToArray();

            int restored = preservedPath != null ? _allTexturePaths.IndexOf(preservedPath) : -1;
            int newIndex = restored >= 0 ? restored : 0;
            if (newIndex != _previewIndex)
                _previewTexture = null;
            _previewIndex = newIndex;
            Repaint();
        }

        public static List<DefaultAsset> FilterNewFolders(
            List<DefaultAsset> existing, IEnumerable<Object> dragged)
        {
            var result = new List<DefaultAsset>();
            foreach (var obj in dragged)
            {
                if (obj is not DefaultAsset asset)
                {
                    continue;
                }
                if (!AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj)))
                {
                    continue;
                }
                if (existing.Contains(asset) || result.Contains(asset))
                {
                    continue;
                }
                result.Add(asset);
            }
            return result;
        }

        public static Vector2 AlignmentToPivot(SpriteAlignment alignment, Vector2 current) =>
            alignment switch
            {
                SpriteAlignment.TopLeft      => new Vector2(0f,   1f),
                SpriteAlignment.TopCenter    => new Vector2(0.5f, 1f),
                SpriteAlignment.TopRight     => new Vector2(1f,   1f),
                SpriteAlignment.LeftCenter   => new Vector2(0f,   0.5f),
                SpriteAlignment.Center       => new Vector2(0.5f, 0.5f),
                SpriteAlignment.RightCenter  => new Vector2(1f,   0.5f),
                SpriteAlignment.BottomLeft   => new Vector2(0f,   0f),
                SpriteAlignment.BottomCenter => new Vector2(0.5f, 0f),
                SpriteAlignment.BottomRight  => new Vector2(1f,   0f),
                _                            => current,
            };

        private void ApplyAll()
        {
            var folderPaths = _settings.TargetFolders
                .Where(f => f != null)
                .Select(f => AssetDatabase.GetAssetPath(f))
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            var errors = SpriteBatchProcessor.ValidatePreflight(folderPaths, _settings.SpriteRects);
            if (errors.Count > 0)
            {
                _ = EditorUtility.DisplayDialog("驗證失敗",
                    string.Join("\n", errors.Select(e => e.Message)), "確認");
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

            string title = result.WasCancelled ? "已取消" : "套用完成";
            string report = result.WasCancelled ? "操作已被使用者取消。\n\n" : "";
            report += $"成功：{result.SuccessCount} 張\n" +
                        $"跳過（尺寸不符）：{result.SkippedPaths.Count} 張\n" +
                        $"失敗（其他錯誤）：{result.FailedPaths.Count} 張";

            if (result.SkippedPaths.Count > 0 || result.FailedPaths.Count > 0)
            {
                report += "\n\n問題檔案：\n" +
                            string.Join("\n", result.SkippedPaths.Concat(result.FailedPaths));
            }

            _ = EditorUtility.DisplayDialog(title, report, "確認");
            RefreshTexturePaths();
        }
    }
}
