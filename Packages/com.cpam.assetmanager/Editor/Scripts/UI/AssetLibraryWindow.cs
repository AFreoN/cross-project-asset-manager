using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CPAM
{
    /// <summary>
    /// Main editor window for browsing and importing assets from the library.
    /// </summary>
    public class AssetLibraryWindow : EditorWindow
    {
        // Constants
        private const float ThumbnailSize = 80f;
        private const float CardPadding = 10f;
        private const int ColumnsPerRow = 3;

        // Data
        private AssetLibraryLoader _loader;
        private List<AssetMetadata> _displayedAssets;
        private HashSet<string> _selectedAssetIds;
        private bool _isDragging = false;

        // UI state
        private string _libraryPath = "";
        private string _searchText = "";
        private string _selectedTypeFilter = "All Types";
        private string _selectedTagFilter = "";
        private string _selectedGroupFilter = "";
        private Vector2 _gridScrollPosition = Vector2.zero;
        private Dictionary<string, Texture2D> _thumbnailCache;
        private bool _isLoadingThumbnails = false;

        // Asset card rendering
        private struct AssetCard
        {
            public AssetMetadata Metadata;
            public Texture2D Thumbnail;
            public bool IsSelected;
            public Rect Rect;
        }

        [MenuItem("Window/Asset Library")]
        public static void ShowWindow()
        {
            GetWindow<AssetLibraryWindow>("Asset Library");
        }

        private void OnEnable()
        {
            _loader = new AssetLibraryLoader();
            _displayedAssets = new List<AssetMetadata>();
            _selectedAssetIds = new HashSet<string>();
            _thumbnailCache = new Dictionary<string, Texture2D>();

            // Load last used library path from preferences
            _libraryPath = EditorPrefs.GetString("CPAM.LastLibraryPath", "");

            if (!string.IsNullOrEmpty(_libraryPath) && LibraryUtilities.IsValidLibraryFile(_libraryPath))
            {
                LoadLibrary(_libraryPath);
            }
        }

        private void OnDisable()
        {
            if (_loader != null)
            {
                _loader.Dispose();
            }

            // Clear thumbnail cache
            foreach (var thumb in _thumbnailCache.Values)
            {
                DestroyImmediate(thumb);
            }
            _thumbnailCache.Clear();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space();

            if (!_loader.IsLoaded)
            {
                DrawNoLibraryUI();
                HandleProjectToLibraryDragDrop();
                return;
            }

            DrawSearchAndFilter();
            EditorGUILayout.Space();
            DrawAssetGrid();
            EditorGUILayout.Space();
            DrawFooter();

            // Handle drag-and-drop from project to library
            HandleProjectToLibraryDragDrop();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUILayout.LabelField("Library:", GUILayout.Width(50));
            _libraryPath = EditorGUILayout.TextField(_libraryPath);

            if (GUILayout.Button("Browse", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                var path = EditorUtility.OpenFilePanel("Select Asset Library", "", "unitylib");
                if (!string.IsNullOrEmpty(path))
                {
                    _libraryPath = path;
                    LoadLibrary(path);
                }
            }

            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                if (!string.IsNullOrEmpty(_libraryPath))
                {
                    LoadLibrary(_libraryPath);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawNoLibraryUI()
        {
            EditorGUILayout.HelpBox("No library loaded. Please select a .unitylib file to get started.", MessageType.Info);

            EditorGUILayout.Space();

            if (GUILayout.Button("Select Library File", GUILayout.Height(40)))
            {
                var path = EditorUtility.OpenFilePanel("Select Asset Library", "", "unitylib");
                if (!string.IsNullOrEmpty(path))
                {
                    _libraryPath = path;
                    LoadLibrary(path);
                }
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Create New Library", GUILayout.Height(40)))
            {
                CreateNewLibraryDialog.ShowDialog();
            }
        }

        private void DrawSearchAndFilter()
        {
            EditorGUILayout.BeginHorizontal();

            // Search field
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            var newSearchText = EditorGUILayout.TextField(_searchText);
            if (newSearchText != _searchText)
            {
                _searchText = newSearchText;
                RefreshDisplayedAssets();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            // Type filter
            EditorGUILayout.LabelField("Type:", GUILayout.Width(50));
            var types = new List<string> { "All Types" };
            types.AddRange(_loader.GetAssetTypes().OrderBy(t => t));
            var typeIndex = types.IndexOf(_selectedTypeFilter);
            if (typeIndex < 0) typeIndex = 0;

            var newTypeIndex = EditorGUILayout.Popup(typeIndex, types.ToArray());
            var newTypeFilter = types[newTypeIndex];
            if (newTypeFilter != _selectedTypeFilter)
            {
                _selectedTypeFilter = newTypeFilter;
                RefreshDisplayedAssets();
            }

            // Tag filter
            EditorGUILayout.LabelField("Tag:", GUILayout.Width(40));
            var tags = new List<string> { "All Tags" };
            tags.AddRange(_loader.GetTags().OrderBy(t => t));
            var tagIndex = tags.IndexOf(_selectedTagFilter);
            if (tagIndex < 0) tagIndex = 0;

            var newTagIndex = EditorGUILayout.Popup(tagIndex, tags.ToArray());
            var newTagFilter = tags[newTagIndex];
            if (newTagFilter != _selectedTagFilter)
            {
                _selectedTagFilter = newTagFilter;
                RefreshDisplayedAssets();
            }

            // Group filter
            EditorGUILayout.LabelField("Group:", GUILayout.Width(45));
            var groups = new List<string> { "All Groups" };
            groups.AddRange(_loader.GetGroups().OrderBy(g => g));
            var groupIndex = groups.IndexOf(_selectedGroupFilter);
            if (groupIndex < 0) groupIndex = 0;

            var newGroupIndex = EditorGUILayout.Popup(groupIndex, groups.ToArray());
            var newGroupFilter = groups[newGroupIndex];
            if (newGroupFilter != _selectedGroupFilter)
            {
                _selectedGroupFilter = newGroupFilter;
                RefreshDisplayedAssets();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAssetGrid()
        {
            EditorGUILayout.LabelField($"Assets ({_displayedAssets.Count})", EditorStyles.boldLabel);

            _gridScrollPosition = GUILayout.BeginScrollView(_gridScrollPosition);

            if (_displayedAssets.Count == 0)
            {
                EditorGUILayout.HelpBox("No assets match your search/filter criteria.", MessageType.Info);
                GUILayout.EndScrollView();
                return;
            }

            var rowAssets = new List<AssetMetadata>();

            for (int i = 0; i < _displayedAssets.Count; i++)
            {
                rowAssets.Add(_displayedAssets[i]);

                if (rowAssets.Count == ColumnsPerRow || i == _displayedAssets.Count - 1)
                {
                    DrawAssetRow(rowAssets);
                    rowAssets.Clear();
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawAssetRow(List<AssetMetadata> assets)
        {
            EditorGUILayout.BeginHorizontal();

            foreach (var asset in assets)
            {
                DrawAssetCard(asset);
            }

            // Add empty cards to fill the row
            for (int i = assets.Count; i < ColumnsPerRow; i++)
            {
                GUILayout.Space(ThumbnailSize + CardPadding * 2);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAssetCard(AssetMetadata asset)
        {
            var isSelected = _selectedAssetIds.Contains(asset.id);

            // Change card background color based on selection
            var originalBgColor = GUI.backgroundColor;
            if (isSelected)
            {
                GUI.backgroundColor = new Color(0.2f, 0.5f, 0.2f, 1f); // Soft green highlight
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(ThumbnailSize + CardPadding * 2));

            // Restore original color for content
            GUI.backgroundColor = originalBgColor;

            // Get or load thumbnail
            Texture2D thumbnail = null;
            if (!_thumbnailCache.TryGetValue(asset.id, out thumbnail))
            {
                thumbnail = LoadThumbnail(asset);
                _thumbnailCache[asset.id] = thumbnail;
            }

            // Draw thumbnail (clickable, draggable)
            EditorGUILayout.BeginHorizontal();

            var thumbnailRect = GUILayoutUtility.GetRect(ThumbnailSize, ThumbnailSize);

            // Draw thumbnail button with selection border
            var buttonStyle = new GUIStyle(EditorStyles.label)
            {
                border = new RectOffset(isSelected ? 3 : 0, isSelected ? 3 : 0, isSelected ? 3 : 0, isSelected ? 3 : 0)
            };

            if (GUI.Button(thumbnailRect, thumbnail ?? Texture2D.whiteTexture, buttonStyle))
            {
                if (Event.current.control || Event.current.command)
                {
                    // Ctrl+click: toggle selection
                    if (isSelected)
                    {
                        _selectedAssetIds.Remove(asset.id);
                    }
                    else
                    {
                        _selectedAssetIds.Add(asset.id);
                    }
                }
                else
                {
                    // Regular click: toggle if already selected, otherwise select only this
                    if (isSelected && _selectedAssetIds.Count == 1)
                    {
                        // Deselect if clicking the same item
                        _selectedAssetIds.Remove(asset.id);
                    }
                    else
                    {
                        // Select only this asset
                        _selectedAssetIds.Clear();
                        _selectedAssetIds.Add(asset.id);
                    }
                }
            }

            // Draw selection border if selected
            if (isSelected)
            {
                GUI.color = Color.green;
                GUI.Box(thumbnailRect, "", new GUIStyle(GUI.skin.box) { border = new RectOffset(2, 2, 2, 2) });
                GUI.color = Color.white;
            }

            // Handle drag-and-drop from library to project
            HandleLibraryToProjectDragDrop(thumbnailRect, asset);

            EditorGUILayout.EndHorizontal();

            // Asset info
            EditorGUILayout.LabelField(asset.name, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(asset.type, EditorStyles.miniLabel);

            // Tags
            if (asset.tags.Count > 0)
            {
                var tagText = string.Join(", ", asset.tags.Take(2));
                if (asset.tags.Count > 2)
                {
                    tagText += "...";
                }
                EditorGUILayout.LabelField(tagText, EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();

            // Restore original background color
            GUI.backgroundColor = originalBgColor;
        }

        private Texture2D LoadThumbnail(AssetMetadata asset)
        {
            if (string.IsNullOrEmpty(asset.thumbnailPath))
            {
                return null;
            }

            try
            {
                var thumbnailData = _loader.GetAssetThumbnail(asset);
                if (thumbnailData == null || thumbnailData.Length == 0)
                {
                    return null;
                }

                var texture = new Texture2D(1, 1);
                texture.LoadImage(thumbnailData);
                return texture;
            }
            catch (Exception ex)
            {
                LibraryUtilities.LogWarning($"Failed to load thumbnail for {asset.name}: {ex.Message}");
                return null;
            }
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField($"{_selectedAssetIds.Count} asset(s) selected");

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Import Selected", GUILayout.Height(30), GUILayout.Width(150)))
            {
                ImportSelectedAssets();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void LoadLibrary(string libraryPath)
        {
            EditorUtility.DisplayProgressBar("Loading Library", "Loading asset library...", 0.5f);

            try
            {
                if (_loader.LoadLibrary(libraryPath))
                {
                    EditorPrefs.SetString("CPAM.LastLibraryPath", libraryPath);
                    _selectedAssetIds.Clear();
                    RefreshDisplayedAssets();
                    EditorUtility.DisplayProgressBar("Loading Library", "Loading thumbnails...", 0.8f);
                    PreloadThumbnails();
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Failed to load library. Check the console for details.", "OK");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void RefreshDisplayedAssets()
        {
            _displayedAssets.Clear();

            var assets = _loader.GetAllAssets();

            // Apply search filter
            if (!string.IsNullOrEmpty(_searchText))
            {
                assets = _loader.SearchAssetsByName(_searchText);
            }

            // Apply type filter
            if (!string.IsNullOrEmpty(_selectedTypeFilter) && _selectedTypeFilter != "All Types")
            {
                assets = assets.Where(a => a.type == _selectedTypeFilter).ToList();
            }

            // Apply tag filter
            if (!string.IsNullOrEmpty(_selectedTagFilter) && _selectedTagFilter != "All Tags")
            {
                assets = assets.Where(a => a.tags.Contains(_selectedTagFilter)).ToList();
            }

            // Apply group filter
            if (!string.IsNullOrEmpty(_selectedGroupFilter) && _selectedGroupFilter != "All Groups")
            {
                assets = assets.Where(a => a.group == _selectedGroupFilter).ToList();
            }

            _displayedAssets = assets;
            _gridScrollPosition = Vector2.zero;
        }

        private void PreloadThumbnails()
        {
            // Simple preload: load first few thumbnails
            for (int i = 0; i < Mathf.Min(9, _displayedAssets.Count); i++)
            {
                var asset = _displayedAssets[i];
                if (!_thumbnailCache.ContainsKey(asset.id))
                {
                    _thumbnailCache[asset.id] = LoadThumbnail(asset);
                }
            }
        }

        private void ImportSelectedAssets()
        {
            if (_selectedAssetIds.Count == 0)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select at least one asset to import.", "OK");
                return;
            }

            var selectedAssets = _displayedAssets.Where(a => _selectedAssetIds.Contains(a.id)).ToList();
            AssetImporter.ImportAssets(_loader, selectedAssets);

            _selectedAssetIds.Clear();
        }

        /// <summary>
        /// Handle drag-and-drop from library window to project window.
        /// Initiates drag operation when asset card is dragged.
        /// </summary>
        private void HandleLibraryToProjectDragDrop(Rect cardRect, AssetMetadata asset)
        {
            var evt = Event.current;

            switch (evt.type)
            {
                case EventType.MouseDown:
                    if (cardRect.Contains(evt.mousePosition))
                    {
                        // Ensure this asset is selected when starting drag
                        if (!_selectedAssetIds.Contains(asset.id))
                        {
                            _selectedAssetIds.Clear();
                            _selectedAssetIds.Add(asset.id);
                        }
                        _isDragging = false;
                    }
                    break;

                case EventType.MouseDrag:
                    if (!_isDragging && cardRect.Contains(evt.mousePosition))
                    {
                        // Start drag
                        var selectedAssets = _displayedAssets.Where(a => _selectedAssetIds.Contains(a.id)).ToList();

                        if (selectedAssets.Count > 0)
                        {
                            _isDragging = true;

                            // Prepare drag operation
                            DragAndDrop.PrepareStartDrag();
                            DragAndDrop.SetGenericData("CPAM_AssetImport", selectedAssets);
                            DragAndDrop.StartDrag($"Dragging {selectedAssets.Count} asset(s)");
                            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                            evt.Use();
                        }
                    }
                    break;

                case EventType.DragExited:
                case EventType.MouseUp:
                    _isDragging = false;
                    break;
            }
        }

        /// <summary>
        /// Handle drag-and-drop from project window to library window.
        /// Opens the "Add to Asset Library" dialog when assets are dropped.
        /// </summary>
        private void HandleProjectToLibraryDragDrop()
        {
            var evt = Event.current;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                    // Check if dragging from project
                    if (DragAndDrop.objectReferences.Length > 0)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                        evt.Use();
                    }
                    break;

                case EventType.DragPerform:
                    // Drop assets from project
                    if (DragAndDrop.objectReferences.Length > 0)
                    {
                        DragAndDrop.AcceptDrag();

                        // Convert dragged objects to selected assets
                        var selectedAssets = new List<ContextMenuExtension.SelectedAsset>();

                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            var assetPath = AssetDatabase.GetAssetPath(obj);

                            if (string.IsNullOrEmpty(assetPath) || AssetDatabase.IsValidFolder(assetPath))
                            {
                                continue; // Skip folders and non-assets
                            }

                            var assetType = ContextMenuExtension.GetAssetTypeFromPath(assetPath);
                            var assetName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                            var fullPath = System.IO.Path.Combine(
                                System.IO.Path.GetDirectoryName(Application.dataPath),
                                assetPath
                            );

                            selectedAssets.Add(new ContextMenuExtension.SelectedAsset
                            {
                                Path = assetPath,
                                Name = assetName,
                                Type = assetType,
                                FullPath = fullPath
                            });
                        }

                        // Open Add Asset Dialog with dropped assets
                        if (selectedAssets.Count > 0)
                        {
                            AddAssetDialog.ShowDialog(selectedAssets);
                        }

                        evt.Use();
                    }
                    break;
            }
        }
    }
}
