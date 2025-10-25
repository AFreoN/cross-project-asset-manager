using System.IO;
using UnityEditor;
using UnityEngine;

namespace CPAM
{
    /// <summary>
    /// EditorWindow for previewing assets from the library.
    /// Displays asset preview with metadata and interactive 3D preview for meshes.
    /// </summary>
    public class AssetPreviewWindow : EditorWindow
    {
        private static AssetPreviewWindow _instance;
        private AssetMetadata _asset;
        private AssetLibraryLoader _loader;
        private Texture2D _previewTexture;
        private Vector2 _scrollPosition = Vector2.zero;
        private Vector3 _meshRotation = Vector3.zero;
        private PreviewRenderUtility _previewRenderer;
        private GameObject _previewMesh;

        private const float PreviewSize = 300f;
        private const float WindowWidth = 700f;
        private const float WindowHeight = 600f;

        /// <summary>
        /// Show the asset preview window.
        /// </summary>
        public static void ShowPreview(AssetMetadata asset, AssetLibraryLoader loader)
        {
            if (asset == null || loader == null)
            {
                EditorUtility.DisplayDialog("Error", "Invalid asset or loader.", "OK");
                return;
            }

            // Close existing window if any
            if (_instance != null)
            {
                _instance.Close();
            }

            // Create new window
            _instance = CreateInstance<AssetPreviewWindow>();
            _instance._asset = asset;
            _instance._loader = loader;
            _instance.titleContent = new GUIContent($"Preview - {asset.name}");

            // Load preview texture
            _instance.LoadPreviewTexture();

            // Center the window
            var rect = EditorGUIUtility.GetMainWindowPosition();
            var x = (rect.width - WindowWidth) / 2 + rect.x;
            var y = (rect.height - WindowHeight) / 2 + rect.y;
            _instance.position = new Rect(x, y, WindowWidth, WindowHeight);

            _instance.Show();
        }

        private void LoadPreviewTexture()
        {
            if (_asset == null || _loader == null)
                return;

            try
            {
                // Try to load thumbnail first
                if (!string.IsNullOrEmpty(_asset.thumbnailPath))
                {
                    var thumbnailData = _loader.GetAssetThumbnail(_asset);
                    if (thumbnailData != null && thumbnailData.Length > 0)
                    {
                        _previewTexture = new Texture2D(1, 1);
                        _previewTexture.LoadImage(thumbnailData);
                        return;
                    }
                }

                // For image assets, try to load the asset itself as preview
                if (IsImageAsset(_asset.type))
                {
                    var assetData = _loader.GetAssetFile(_asset);
                    if (assetData != null && assetData.Length > 0)
                    {
                        _previewTexture = new Texture2D(1, 1);
                        _previewTexture.LoadImage(assetData);
                        return;
                    }
                }
            }
            catch (System.Exception ex)
            {
                LibraryUtilities.LogWarning($"Failed to load preview texture: {ex.Message}");
            }
        }

        private bool IsImageAsset(string assetType)
        {
            return assetType == "Texture2D" || assetType == "Sprite" || assetType == "RenderTexture";
        }

        private void OnGUI()
        {
            if (_asset == null)
            {
                EditorGUILayout.HelpBox("Asset is null.", MessageType.Error);
                return;
            }

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            // Title
            EditorGUILayout.LabelField($"Preview: {_asset.name}", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Layout: Preview on left, metadata on right
            EditorGUILayout.BeginHorizontal();

            // Left panel: Preview
            EditorGUILayout.BeginVertical(GUILayout.Width(PreviewSize + 20));
            DrawPreviewArea();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(20);

            // Right panel: Metadata
            EditorGUILayout.BeginVertical();
            DrawMetadataPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            GUILayout.EndScrollView();
        }

        private void DrawPreviewArea()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            // Draw preview area background
            var previewRect = EditorGUILayout.GetControlRect(GUILayout.Width(PreviewSize), GUILayout.Height(PreviewSize));
            GUI.Box(previewRect, "", GUI.skin.box);

            if (_previewTexture != null)
            {
                // Draw texture with aspect ratio preservation
                DrawTextureWithAspectRatio(previewRect, _previewTexture);
            }
            else if (IsMeshAsset(_asset.type))
            {
                // Draw mesh preview controls
                EditorGUILayout.HelpBox("3D Mesh Preview\nClick and drag to rotate", MessageType.Info);
                EditorGUILayout.Space();
                _meshRotation.x = EditorGUILayout.Slider("Rotation X:", _meshRotation.x, -180, 180);
                _meshRotation.y = EditorGUILayout.Slider("Rotation Y:", _meshRotation.y, -180, 180);
                _meshRotation.z = EditorGUILayout.Slider("Rotation Z:", _meshRotation.z, -180, 180);
            }
            else
            {
                // Draw placeholder
                GUI.Label(previewRect, $"No preview available\n({_asset.type})", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.Space();

            // File size info
            EditorGUILayout.TextField("File Size:", LibraryUtilities.FormatFileSize(_asset.fileSize));
        }

        private void DrawTextureWithAspectRatio(Rect rect, Texture2D texture)
        {
            if (texture == null)
                return;

            float textureAspect = (float)texture.width / texture.height;
            float scaledHeight = rect.height;
            float scaledWidth = scaledHeight * textureAspect;

            if (scaledWidth > rect.width)
            {
                scaledWidth = rect.width;
                scaledHeight = scaledWidth / textureAspect;
            }

            float offsetX = (rect.width - scaledWidth) / 2;
            float offsetY = (rect.height - scaledHeight) / 2;

            var drawRect = new Rect(
                rect.x + offsetX,
                rect.y + offsetY,
                scaledWidth,
                scaledHeight
            );

            GUI.DrawTexture(drawRect, texture, ScaleMode.StretchToFill);
        }

        private void DrawMetadataPanel()
        {
            EditorGUILayout.LabelField("Asset Information", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.TextField("Name:", _asset.name);
            EditorGUILayout.TextField("Type:", _asset.type);
            EditorGUILayout.TextField("Asset ID:", _asset.id);

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Organization", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.TextField("Group:", _asset.group ?? "(None)");

            // Tags
            if (_asset.tags != null && _asset.tags.Count > 0)
            {
                EditorGUILayout.LabelField("Tags:", EditorStyles.label);
                EditorGUI.indentLevel++;
                foreach (var tag in _asset.tags)
                {
                    EditorGUILayout.TextField("", tag);
                }
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.LabelField("Tags:", "(None)");
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.TextArea(_asset.description ?? "(No description)", GUILayout.Height(60));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("File Information", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.TextField("Path:", _asset.relativePath);
            EditorGUILayout.TextField("Size:", LibraryUtilities.FormatFileSize(_asset.fileSize));

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Dates", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.TextField("Added:", _asset.dateAdded);

            EditorGUI.indentLevel--;

            GUILayout.FlexibleSpace();

            // Close button
            EditorGUILayout.Space();
            if (GUILayout.Button("Close", GUILayout.Height(30)))
            {
                Close();
            }
        }

        private bool IsMeshAsset(string assetType)
        {
            return assetType == "Mesh" || assetType == "Model";
        }

        private void OnDestroy()
        {
            // Cleanup
            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
            }

            if (_previewRenderer != null)
            {
                _previewRenderer.Cleanup();
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
