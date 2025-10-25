# Unity Asset Library - MVP Development Plan

## Project Overview

**Name**: Unity Asset Library (working title)

**Purpose**: A Unity Editor extension that provides a personal cross-project asset library, allowing developers to quickly import frequently used assets without manual folder navigation.

**Target**: Solo Unity developers and small teams who reuse assets across multiple projects

**License**: Open Source (MIT recommended)

* * *

## Core Concept

The tool enables a bidirectional workflow:

1.  **Add**: Right-click any asset in Unity → Add to library with metadata
2.  **Browse**: Open library window to view all saved assets with filtering
3.  **Import**: Click or drag-drop assets from library into current project

The library is stored as a single compressed file (.zip) on local storage, making it portable and easy to backup/share.

* * *

## MVP Features (Essential Only)

### 1\. Library File Management

- **Library file format**: ZIP archive containing assets and a JSON manifest
- **File selection**: User selects library file via file picker
- **Library structure**:
    - `manifest.json` - metadata for all assets
    - `assets/` folder - organized by asset type (textures, prefabs, scripts, materials, etc.)
    - `thumbnails/` folder - optional custom preview images for non-image assets

### 2\. Add Assets to Library

- **Context menu**: Right-click on any asset in Project window → "Add to Asset Library"
- **Metadata dialog**: Opens window where user inputs:
    - Group/Category name
    - Tags (comma-separated)
    - Description
    - Custom thumbnail (optional, for non-image assets)
    - Library file to add to
- **Automatic thumbnail**: Images (textures) use themselves as thumbnails
- **Asset storage**: Copy raw asset files into library (no .meta files, no GUID preservation)

### 3\. Asset Browser Window

- **Main window**: Custom Editor Window accessible via Unity menu (Window → Asset Library)
- **Library loading**: Select library file, extract and parse manifest
- **Asset display**: Grid/card view showing:
    - Thumbnail preview
    - Asset name
    - Asset type
    - Tags
- **Search functionality**: Text search by asset name
- **Filtering**:
    - Filter by asset type (Texture, Prefab, Script, Material, etc.)
    - Filter by tags
    - Filter by group/category

### 4\. Import Assets

- **Click to import**: Select asset(s) → Click "Import Selected" button
- **Import location**: Default to `Assets/Imported/` or let user choose folder
- **Drag-and-drop**: Drag asset from library window directly to Project window
- **Simple copy**: Copy raw asset files, let Unity generate new GUIDs (like drag-drop from file explorer)

* * *

## Technical Architecture

### Technology Stack

- **Language**: C#
- **Platform**: Unity Editor (EditorWindow, MenuItem, AssetDatabase APIs)
- **Libraries**:
    - `System.IO.Compression` for ZIP handling
    - `JsonUtility` for manifest serialization
    - Unity's `EditorGUI` for UI

### Component Breakdown

#### 1\. **Data Layer**

**LibraryManifest.cs**

- Serializable class representing the entire library
- Contains library metadata (name, version) and list of assets
- Serializes to/from JSON

**AssetMetadata.cs**

- Serializable class for individual asset information
- Fields:
    - Unique ID (GUID string)
    - Asset name
    - Relative path in library
    - Asset type (Texture2D, GameObject/Prefab, MonoScript, Material, etc.)
    - Group/category name
    - Tags (list of strings)
    - Description
    - Thumbnail path (relative)
    - Optional: File size, date added

#### 2\. **Library Management Layer**

**AssetLibraryLoader.cs**

- Loads library ZIP file from disk
- Extracts to temporary cache folder
- Parses manifest.json
- Provides asset list to UI
- Handles library file validation and error cases

**LibraryWriter.cs**

- Adds new assets to existing library
- Extracts library → modifies → recompresses
- Handles:
    - Copying asset files into correct category folders
    - Generating/copying thumbnails
    - Updating manifest with new entries
    - Repackaging as ZIP
- Ensures library file integrity

**AssetImporter.cs**

- Handles importing assets from library to project
- Simple file copy from extracted library to Assets folder
- Triggers AssetDatabase refresh
- Provides import location selection
- Handles batch imports

#### 3\. **UI Layer**

**AssetLibraryWindow.cs** (Main Window)

- EditorWindow subclass
- UI sections:
    - **Toolbar**: Library file selector, refresh button
    - **Search bar**: Text input for searching
    - **Filter panel**: Dropdowns/toggles for type and tag filtering
    - **Asset grid**: ScrollView with asset cards
    - **Footer**: Import button, status text
- Manages state (selected assets, filters, search text)
- Handles asset selection (single/multiple)
- Coordinates with loader and importer

**AddAssetDialog.cs**

- Modal EditorWindow for adding assets
- Input fields:
    - Library file browser
    - Group/category text field
    - Tags text field
    - Description text area
    - Custom thumbnail object field
- Preview of assets being added
- Validation before adding
- Calls LibraryWriter to persist changes

**ContextMenuExtension.cs**

- Adds context menu item to Project window
- MenuItem attribute: "Assets/Add to Asset Library"
- Validation function to show only when assets selected
- Opens AddAssetDialog with selected assets

**DragAndDropHandler.cs** (Utility)

- Handles drag-and-drop events from library window to Project window
- Uses Unity's DragAndDrop API
- Provides visual feedback during drag
- Triggers import on drop

#### 4\. **Utility Layer**

**LibraryUtilities.cs**

- Helper functions:
    - Determine asset category from type
    - Generate unique IDs
    - Path manipulation (cross-platform)
    - Thumbnail generation from textures
    - File validation
    - Error logging

* * *

## Data Flow

### Adding Asset Flow

1.  User right-clicks asset in Project window
2.  Context menu appears with "Add to Asset Library"
3.  AddAssetDialog opens with selected asset(s)
4.  User fills metadata fields and selects library file
5.  LibraryWriter extracts library, copies asset files, updates manifest
6.  Library recompressed, success dialog shown

### Importing Asset Flow

1.  User opens AssetLibraryWindow
2.  Selects library file → AssetLibraryLoader extracts and parses
3.  Assets displayed in grid with thumbnails
4.  User searches/filters to find desired asset
5.  User clicks "Import" or drags asset to Project window
6.  AssetImporter copies files from temp cache to Assets folder
7.  AssetDatabase refresh triggered, asset appears in project

* * *

## Library File Structure

```
MyAssetLibrary.zip
│
├── manifest.json
│   {
│     "libraryName": "My Personal Library",
│     "version": "1.0.0",
│     "assets": [
│       {
│         "id": "550e8400-e29b-41d4-a716-446655440000",
│         "name": "WhiteDot",
│         "relativePath": "assets/textures/WhiteDot.png",
│         "type": "Texture2D",
│         "group": "UI Elements",
│         "tags": ["ui", "pixel-art", "texture"],
│         "description": "4x4 white pixel texture",
│         "thumbnailPath": "assets/textures/WhiteDot.png"
│       },
│       {
│         "id": "650e8400-e29b-41d4-a716-446655440001",
│         "name": "PlayerController",
│         "relativePath": "assets/scripts/PlayerController.cs",
│         "type": "MonoScript",
│         "group": "Player Systems",
│         "tags": ["script", "player", "controller"],
│         "description": "Basic FPS player controller",
│         "thumbnailPath": "thumbnails/PlayerController.png"
│       }
│     ]
│   }
│
├── assets/
│   ├── textures/
│   │   ├── WhiteDot.png
│   │   └── GradientBackground.png
│   ├── prefabs/
│   │   └── UIButton.prefab
│   ├── scripts/
│   │   ├── PlayerController.cs
│   │   └── CameraFollow.cs
│   ├── materials/
│   │   └── StandardMaterial.mat
│   └── audio/
│       └── ButtonClick.wav
│
└── thumbnails/
    ├── PlayerController.png
    ├── UIButton.png
    └── StandardMaterial.png
```

* * *

## Asset Type Categories (Auto-organization)

When adding assets, automatically determine category based on type:

- **Textures**: Texture2D, Sprite, RenderTexture
- **Prefabs**: GameObject (if prefab)
- **Scripts**: MonoScript
- **Materials**: Material
- **Audio**: AudioClip
- **Models**: Mesh
- **Animations**: AnimationClip, Animator, AnimatorController
- **Shaders**: Shader, ComputeShader
- **Other**: Fallback category

* * *

## UI/UX Design Principles

### Asset Library Window Layout

```
┌─────────────────────────────────────────────────────────┐
│  Asset Library                                    [✕]   │
├─────────────────────────────────────────────────────────┤
│  Library: [MyLibrary.zip           ] [Browse] [Refresh] │
├─────────────────────────────────────────────────────────┤
│  Search: [____________] 🔍                               │
│  Type: [All Types ▼]  Tags: [____________]              │
├─────────────────────────────────────────────────────────┤
│  ┌──────────┐  ┌──────────┐  ┌──────────┐              │
│  │  [IMG]   │  │  [IMG]   │  │  [IMG]   │              │
│  │ WhiteDot │  │ UIButton │  │  Player  │              │
│  │ Texture  │  │  Prefab  │  │  Script  │              │
│  │ ui, px   │  │ ui, btn  │  │ player   │              │
│  └──────────┘  └──────────┘  └──────────┘              │
│                                                          │
│  [Grid view continues with scroll...]                   │
│                                                          │
├─────────────────────────────────────────────────────────┤
│  3 assets selected              [Import Selected]       │
└─────────────────────────────────────────────────────────┘
```

### Add Asset Dialog Layout

```
┌─────────────────────────────────────────────────────────┐
│  Add to Asset Library                             [✕]   │
├─────────────────────────────────────────────────────────┤
│  Selected Assets: 1                                      │
│  ○ WhiteDot (Texture2D)                                 │
│                                                          │
│  Library File: [MyLibrary.zip        ] [Browse]         │
│                                                          │
│  Group/Category: [UI Elements_______________]           │
│  Tags: [ui, pixel-art, texture_______________]          │
│  Description:                                            │
│  ┌────────────────────────────────────────────────┐     │
│  │ 4x4 white pixel texture for UI elements       │     │
│  │                                                │     │
│  └────────────────────────────────────────────────┘     │
│                                                          │
│  Custom Thumbnail: [None              ] [Select]        │
│  (Optional - images use themselves)                     │
│                                                          │
├─────────────────────────────────────────────────────────┤
│                           [Cancel]  [Add to Library]    │
└─────────────────────────────────────────────────────────┘
```

* * *

## Development Phases

### Phase 1: Foundation (Week 1-2)

**Goal**: Basic infrastructure and file handling

**Tasks**:

- Set up Unity package structure (folder hierarchy, assembly definitions)
- Create data classes (LibraryManifest, AssetMetadata)
- Implement ZIP extraction/compression utilities
- Build manifest JSON serialization/deserialization
- Create basic AssetLibraryLoader (load library, parse manifest, provide asset list)
- Unit testing for data layer

**Deliverable**: Can load a manually-created library file and read its contents

### Phase 2: Add Assets Feature (Week 2-3)

**Goal**: Users can add assets to library

**Tasks**:

- Implement ContextMenuExtension (right-click menu)
- Build AddAssetDialog UI
- Create LibraryWriter (add assets to library)
- Implement thumbnail handling:
    - Auto-use for images
    - Custom thumbnail support
- Handle asset file copying and organization
- Manifest updating logic
- Test with various asset types

**Deliverable**: Can right-click assets and add them to library with metadata

### Phase 3: Browse and Display (Week 3-4)

**Goal**: Users can view library contents

**Tasks**:

- Build AssetLibraryWindow main UI
- Implement library file selection
- Create asset grid/card display
- Load and display thumbnails
- Implement search functionality (filter by name)
- Add type filtering (dropdown)
- Add tag filtering (text input)
- Handle multi-selection
- Display asset details on selection

**Deliverable**: Functional library browser with search and filters

### Phase 4: Import Feature (Week 4-5)

**Goal**: Users can import assets from library

**Tasks**:

- Implement AssetImporter (file copying)
- Add "Import Selected" button functionality
- Target folder selection/configuration
- Implement drag-and-drop handler
- AssetDatabase refresh integration
- Batch import support
- Error handling (duplicate files, locked files, etc.)
- Import confirmation feedback

**Deliverable**: Full import workflow working via button and drag-drop

### Phase 5: Polish and Testing (Week 5-6)

**Goal**: Production-ready MVP

**Tasks**:

- Comprehensive error handling and validation
- Loading indicators for long operations
- User-friendly error messages
- Edge case testing:
    - Large libraries (100+ assets)
    - Corrupted library files
    - Missing thumbnails
    - Special characters in names
    - Cross-platform path issues
- Performance optimization (thumbnail loading)
- Code cleanup and documentation
- Create README with usage guide
- Prepare example library file

**Deliverable**: Stable, tested MVP ready for release

* * *

## Technical Considerations

### 1\. File Handling

- **Temp folder management**: Extract library to `Application.temporaryCachePath`, clean up after use
- **Cross-platform paths**: Always use `Path.Combine()`, never hardcode separators
- **File locking**: Handle cases where library file is locked or in use
- **Large files**: Consider streaming or chunking for very large assets

### 2\. Performance

- **Thumbnail loading**: Load thumbnails asynchronously to avoid UI freezing
- **Large libraries**: Consider pagination or virtual scrolling for 100+ assets
- **Search optimization**: Index asset names and tags for faster filtering
- **Caching**: Keep extracted library in cache between operations during same session

### 3\. Error Handling

- **Invalid library files**: Detect and handle corrupted ZIPs or malformed JSON
- **Missing files**: Handle references to non-existent thumbnails or assets
- **Write failures**: Handle disk full, permission denied scenarios
- **User feedback**: Always provide clear error messages and recovery options

### 4\. Data Integrity

- **Atomic operations**: Ensure library modifications are atomic (temp copy → modify → replace)
- **Backup**: Consider creating .bak file before modifying library
- **Validation**: Validate manifest schema on load
- **Unique IDs**: Ensure asset IDs are truly unique (use System.Guid)

### 5\. Unity Integration

- **Asset database**: Always call `AssetDatabase.Refresh()` after importing
- **Editor utilities**: Use `EditorUtility.DisplayProgressBar()` for long operations
- **Undo system**: Not necessary for MVP (library is external to project)
- **Preferences**: Store last used library path in EditorPrefs

* * *

## Package Structure

```
com.yourname.assetlibrary/
├── package.json                    # Unity package manifest
├── README.md                       # Usage guide
├── LICENSE.md                      # Open source license
├── CHANGELOG.md                    # Version history
│
├── Editor/
│   ├── Scripts/
│   │   ├── Data/
│   │   │   ├── LibraryManifest.cs
│   │   │   └── AssetMetadata.cs
│   │   ├── Core/
│   │   │   ├── AssetLibraryLoader.cs
│   │   │   ├── LibraryWriter.cs
│   │   │   └── AssetImporter.cs
│   │   ├── UI/
│   │   │   ├── AssetLibraryWindow.cs
│   │   │   ├── AddAssetDialog.cs
│   │   │   ├── DragAndDropHandler.cs
│   │   │   └── ContextMenuExtension.cs
│   │   └── Utilities/
│   │       └── LibraryUtilities.cs
│   │
│   └── Resources/
│       └── Icons/                  # UI icons and graphics
│           ├── WindowIcon.png
│           └── DefaultThumbnail.png
│
├── Documentation~/                 # Unity package docs folder
│   ├── UserGuide.md
│   └── Images/
│       └── screenshots/
│
└── Samples~/                       # Optional samples
    └── ExampleLibrary.zip
```

* * *

## Configuration and Settings

### User Preferences (EditorPrefs)

- `AssetLibrary.LastLibraryPath` - Last opened library file path
- `AssetLibrary.DefaultImportPath` - Default import location
- `AssetLibrary.WindowPosition` - Remember window position/size

### Library Manifest Metadata

- Library name (user-friendly identifier)
- Version number (for future compatibility)
- Creation date
- Last modified date
- Asset count (for quick info)

* * *

## Known Limitations (MVP Scope)

### Explicitly OUT of Scope for MVP

- ❌ Cloud sync or remote storage
- ❌ Team collaboration features
- ❌ Git integration
- ❌ Asset versioning or update detection
- ❌ Dependency tracking between assets
- ❌ Asset preview in 3D (for models/prefabs)
- ❌ Bulk operations (delete multiple, export)
- ❌ Library merging or splitting
- ❌ Asset usage analytics
- ❌ Multiple library support (open multiple libraries simultaneously)
- ❌ Library encryption or protection

### Acceptable MVP Limitations

- Single library file at a time
- No GUID preservation (new GUID on import)
- Basic thumbnail support (static images only)
- Simple search (name only, case-insensitive)
- Manual library file selection each session
- No dependency resolution for complex prefabs
- Limited to file-based assets (no scene assets, no ScriptableObject instances embedded in scenes)

* * *

## Success Criteria

### Functional Requirements

✅ User can add any supported asset type to library with metadata  
✅ User can browse library contents with thumbnails  
✅ User can search and filter assets effectively  
✅ User can import assets via button or drag-drop  
✅ Imported assets work correctly in project (no broken references for standalone assets)  
✅ Library file remains valid after multiple add operations  
✅ Works on Windows, macOS, and Linux

### Quality Requirements

✅ No crashes or Unity console errors during normal use  
✅ Clear error messages for user mistakes  
✅ Operations complete in reasonable time (<5s for typical actions)  
✅ UI is intuitive and follows Unity's design patterns  
✅ Code is documented and maintainable

### Documentation Requirements

✅ README with installation and basic usage  
✅ Example library file demonstrating features  
✅ Inline code comments for complex logic  
✅ GitHub repository with clear contributing guidelines

* * *

## Development Guidelines

### Code Standards

- Follow Unity C# style guide
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep methods focused and small (<50 lines)
- Avoid deep nesting (max 3 levels)

### Testing Strategy

- Manual testing for UI workflows
- Test with various asset types (textures, prefabs, scripts, materials, audio)
- Test edge cases (empty library, corrupted files, special characters)
- Test on all target platforms before release
- Create reproducible test scenarios

* * *

## Conclusion

This MVP focuses on the core workflow: **add assets → browse with metadata → import quickly**. By keeping the scope tight and implementation simple (no GUID preservation, single library, local storage), we can deliver a working tool that provides real value to Unity developers.

The modular architecture allows for future enhancements without major refactoring, and the open-source approach will help build community and gather feedback for improvements.

The tool solves a genuine problem (asset reuse friction) in a simple, intuitive way that fits naturally into Unity's existing workflow patterns.