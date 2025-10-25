using System;
using System.IO;
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
        private string _currentDragTempDir = "";
        private System.DateTime _lastDragStartTime = System.DateTime.MinValue;

        // Asset type colors for placeholders
        private static readonly Dictionary<string, Color> AssetTypeColors = new Dictionary<string, Color>
        {
            { "Texture2D", new Color(0.8f, 0.6f, 0.2f, 1f) },      // Gold
            { "Sprite", new Color(0.8f, 0.6f, 0.2f, 1f) },         // Gold
            { "RenderTexture", new Color(0.8f, 0.6f, 0.2f, 1f) },  // Gold
            { "GameObject", new Color(0.4f, 0.6f, 0.8f, 1f) },     // Light blue
            { "MonoScript", new Color(0.2f, 0.7f, 0.2f, 1f) },     // Green
            { "Material", new Color(0.7f, 0.5f, 0.8f, 1f) },       // Purple
            { "Shader", new Color(0.7f, 0.5f, 0.8f, 1f) },         // Purple
            { "ComputeShader", new Color(0.7f, 0.5f, 0.8f, 1f) },  // Purple
            { "AudioClip", new Color(0.8f, 0.4f, 0.4f, 1f) },      // Red
            { "Mesh", new Color(0.4f, 0.7f, 0.8f, 1f) },           // Cyan
            { "AnimationClip", new Color(0.8f, 0.7f, 0.3f, 1f) },  // Yellow-gold
            { "AnimatorController", new Color(0.8f, 0.7f, 0.3f, 1f) }, // Yellow-gold
            { "SceneAsset", new Color(0.6f, 0.4f, 0.8f, 1f) },     // Violet
            { "ScriptableObject", new Color(0.2f, 0.7f, 0.2f, 1f) }, // Green
        };

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
            // Check if we should clean up old drag-drop temp files
            CleanupOldDragTempFilesIfNeeded();

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

            // ===== DRAG-AND-DROP AND CLICK HANDLING =====
            int controlID = GUIUtility.GetControlID(FocusType.Passive, thumbnailRect);
            Event evt = Event.current;
            EventType eventType = evt.GetTypeForControl(controlID);

            switch (eventType)
            {
                case EventType.MouseDown:
                    if (thumbnailRect.Contains(evt.mousePosition))
                    {
                        if (evt.button == 0)
                        {
                            // Left-click: take control ownership for drag-and-drop
                            GUIUtility.hotControl = controlID;
                            evt.Use();
                        }
                        else if (evt.button == 1)
                        {
                            // Right-click: show context menu
                            AssetContextMenu.Show(asset, _loader, _libraryPath, thumbnailRect);
                            evt.Use();
                        }
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlID && !_isDragging)
                    {
                        // Get selected assets to drag
                        var selectedAssets = _displayedAssets
                            .Where(a => _selectedAssetIds.Contains(a.id))
                            .ToList();

                        if (selectedAssets.Count > 0)
                        {
                            // Extract assets to temporary files
                            var tempFilePaths = ExtractAssetsToTempForDragDrop(selectedAssets);

                            if (tempFilePaths.Length > 0)
                            {
                                _isDragging = true;

                                // Start drag operation with file paths
                                DragAndDrop.PrepareStartDrag();
                                DragAndDrop.paths = tempFilePaths;
                                DragAndDrop.StartDrag($"Dragging {selectedAssets.Count} asset(s)");

                                evt.Use();
                            }
                        }
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlID)
                    {
                        GUIUtility.hotControl = 0;

                        // Handle as click if we didn't start a drag
                        if (!_isDragging)
                        {
                            bool wasSelected = _selectedAssetIds.Contains(asset.id);

                            if (evt.control || evt.command)
                            {
                                // Ctrl+click: toggle selection
                                if (wasSelected)
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
                                // Regular click:
                                // - If clicking unselected → select it (clear others)
                                // - If clicking selected and only one selected → deselect it
                                // - If clicking selected but multiple selected → select only it
                                if (wasSelected && _selectedAssetIds.Count == 1)
                                {
                                    // Deselect if it's the only selected item
                                    _selectedAssetIds.Remove(asset.id);
                                }
                                else
                                {
                                    // Select only this item
                                    _selectedAssetIds.Clear();
                                    _selectedAssetIds.Add(asset.id);
                                }
                            }
                        }

                        _isDragging = false;
                        evt.Use();
                    }
                    break;

                case EventType.Repaint:
                    // Draw thumbnail or placeholder
                    if (thumbnail != null)
                    {
                        DrawTextureWithAspectRatio(thumbnailRect, thumbnail);
                    }
                    else
                    {
                        DrawAssetTypePlaceholder(thumbnailRect, asset.type);
                    }

                    // Draw selection border if selected
                    if (isSelected)
                    {
                        Handles.BeginGUI();
                        Handles.color = Color.green;
                        Handles.DrawSolidRectangleWithOutline(thumbnailRect, Color.clear, Color.green);
                        Handles.EndGUI();
                    }
                    break;

                case EventType.DragExited:
                    if (GUIUtility.hotControl == controlID)
                    {
                        GUIUtility.hotControl = 0;
                        _isDragging = false;
                    }
                    break;
            }

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

        /// <summary>
        /// Draw a texture while preserving its aspect ratio within the given rect.
        /// The texture is centered and scaled to fit without stretching.
        /// </summary>
        private void DrawTextureWithAspectRatio(Rect rect, Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            // Calculate texture aspect ratio
            float textureAspect = (float)texture.width / texture.height;

            // Start by fitting to height
            float scaledHeight = rect.height;
            float scaledWidth = scaledHeight * textureAspect;

            // If the width exceeds the rect, fit to width instead
            if (scaledWidth > rect.width)
            {
                scaledWidth = rect.width;
                scaledHeight = scaledWidth / textureAspect;
            }

            // Center the texture within the rect
            float offsetX = (rect.width - scaledWidth) / 2;
            float offsetY = (rect.height - scaledHeight) / 2;

            Rect scaledRect = new Rect(rect.x + offsetX, rect.y + offsetY, scaledWidth, scaledHeight);

            GUI.DrawTexture(scaledRect, texture);
        }

        /// <summary>
        /// Draw a colored placeholder for assets without thumbnails.
        /// Shows the asset type with a distinctive color.
        /// </summary>
        private void DrawAssetTypePlaceholder(Rect rect, string assetType)
        {
            // Get color for this asset type
            Color placeholderColor = Color.gray;
            if (AssetTypeColors.TryGetValue(assetType, out var color))
            {
                placeholderColor = color;
            }

            // Draw colored background
            GUI.color = placeholderColor;
            GUI.Box(rect, "");
            GUI.color = Color.white;

            // Draw asset type text in the center
            var labelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                fontSize = 9
            };

            // Abbreviate long type names
            string displayType = AbbreviateAssetType(assetType);
            GUI.Label(rect, displayType, labelStyle);
        }

        /// <summary>
        /// Get a short abbreviation for an asset type.
        /// </summary>
        private string AbbreviateAssetType(string assetType)
        {
            return assetType switch
            {
                "Texture2D" => "TEX",
                "Sprite" => "SPR",
                "RenderTexture" => "RTEX",
                "GameObject" => "PREFAB",
                "MonoScript" => "C#",
                "Material" => "MAT",
                "Shader" => "SHDR",
                "ComputeShader" => "CSHDR",
                "AudioClip" => "SND",
                "Mesh" => "MESH",
                "AnimationClip" => "ANIM",
                "AnimatorController" => "CTRL",
                "SceneAsset" => "SCENE",
                "ScriptableObject" => "SO",
                _ => assetType.Length > 4 ? assetType.Substring(0, 4).ToUpper() : assetType.ToUpper()
            };
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
        /// Extract selected assets to temporary files for drag-and-drop.
        /// Returns an array of file paths that can be dragged to the Project window.
        /// </summary>
        private string[] ExtractAssetsToTempForDragDrop(List<AssetMetadata> assets)
        {
            try
            {
                // Create a unique temp directory for this drag operation
                _currentDragTempDir = Path.Combine(
                    Path.GetTempPath(),
                    "CPAM_DragDrop",
                    System.Guid.NewGuid().ToString()
                );

                if (!Directory.Exists(_currentDragTempDir))
                {
                    Directory.CreateDirectory(_currentDragTempDir);
                }

                var tempFilePaths = new List<string>();

                // Extract each selected asset to the temp directory
                foreach (var asset in assets)
                {
                    try
                    {
                        var assetData = _loader.GetAssetFile(asset);
                        if (assetData == null || assetData.Length == 0)
                        {
                            LibraryUtilities.LogWarning($"Could not extract asset data for: {asset.name}");
                            continue;
                        }

                        // Preserve the folder structure from the library in the temp directory
                        // Example: library has "assets/ui/RoundRect_50.png"
                        // Extract to: temp/ui/RoundRect_50.png
                        // This way Unity will create Assets/ui/ folder automatically during import

                        var relativePath = asset.relativePath;

                        // Remove the leading "assets/" or "assets\" folder if present
                        if (relativePath.StartsWith("assets/", System.StringComparison.OrdinalIgnoreCase))
                        {
                            relativePath = relativePath.Substring("assets/".Length);
                        }
                        else if (relativePath.StartsWith("assets\\", System.StringComparison.OrdinalIgnoreCase))
                        {
                            relativePath = relativePath.Substring("assets\\".Length);
                        }

                        // Normalize path separators for this OS
                        relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

                        var tempFilePath = Path.Combine(_currentDragTempDir, relativePath);
                        var tempFileDir = Path.GetDirectoryName(tempFilePath);

                        // Create subdirectories if they don't exist
                        if (!Directory.Exists(tempFileDir))
                        {
                            Directory.CreateDirectory(tempFileDir);
                        }

                        // Write the asset file to temp location with preserved structure
                        File.WriteAllBytes(tempFilePath, assetData);
                        tempFilePaths.Add(tempFilePath);

                        LibraryUtilities.Log($"Extracted for drag: {Path.GetFileName(tempFilePath)}");
                    }
                    catch (Exception ex)
                    {
                        LibraryUtilities.LogWarning($"Failed to extract asset {asset.name}: {ex.Message}");
                    }
                }

                // Record when this drag started for cleanup timing
                if (tempFilePaths.Count > 0)
                {
                    _lastDragStartTime = System.DateTime.Now;
                }

                return tempFilePaths.ToArray();
            }
            catch (Exception ex)
            {
                LibraryUtilities.LogError($"Failed to prepare assets for drag-drop: {ex.Message}");
                return new string[0];
            }
        }

        /// <summary>
        /// Check if old drag-drop temp files should be cleaned up.
        /// Called from OnGUI to clean up after drag-drop operation completes.
        /// </summary>
        private void CleanupOldDragTempFilesIfNeeded()
        {
            // Only attempt cleanup if we have a temp directory to clean
            if (string.IsNullOrEmpty(_currentDragTempDir) || !Directory.Exists(_currentDragTempDir))
            {
                return;
            }

            // Wait at least 3 seconds after drag started before cleaning up
            // This gives Unity plenty of time to copy/import the files
            if ((System.DateTime.Now - _lastDragStartTime).TotalSeconds < 3.0)
            {
                return; // Not ready to clean up yet
            }

            // Attempt cleanup
            try
            {
                Directory.Delete(_currentDragTempDir, recursive: true);
                LibraryUtilities.Log($"Cleaned up drag-drop temp files: {_currentDragTempDir}");
                _currentDragTempDir = "";
            }
            catch (Exception ex)
            {
                // Silently ignore cleanup failures
                LibraryUtilities.LogWarning($"Failed to cleanup temp drag files: {ex.Message}");
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
