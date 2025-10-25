# CPAM - Test Guide

This guide provides step-by-step instructions for testing the Cross Project Asset Manager functionality.

## Pre-Test Checklist

- [ ] Unity project is open (2020.3 LTS or newer)
- [ ] CPAM package is imported into `Assets/Packages/com.cpam.assetmanager/`
- [ ] No compilation errors in the console
- [ ] Test assets are available (textures, scripts, prefabs, etc.)

## Test Scenarios

### Test 1: Create a New Library

1. Open `Window → Asset Library`
2. Verify the "No library loaded" message appears
3. Click "Create New Library"
4. Enter library name: "TestLibrary"
5. Click "Browse" and select a save location
6. Click "Create"
7. **Expected Result**: Dialog shows success message, .unitylib file created at selected location

### Test 2: Add Asset to Library

1. In the Project window, find a texture asset (or any image)
2. Right-click on it
3. Verify "Add to Asset Library" appears in context menu
4. Click it
5. Fill in metadata:
   - Group: "Test Assets"
   - Tags: "test, sample"
   - Description: "This is a test asset"
6. Click "Browse" and select the library created in Test 1
7. Click "Add to Library"
8. **Expected Result**: Success dialog appears, asset is added to library

### Test 3: Add Multiple Assets

1. Select multiple assets in the Project window (Ctrl+Click)
2. Right-click and select "Add to Asset Library"
3. Fill in shared metadata
4. Click "Add to Library"
5. **Expected Result**: All selected assets are added to library

### Test 4: Load Library and Browse Assets

1. Open `Window → Asset Library`
2. Click "Browse" and select the test library from Test 1
3. **Expected Result**:
   - Library loads successfully
   - Asset count displays correctly
   - Thumbnails load for image assets
   - Asset names, types, and tags are visible

### Test 5: Search Functionality

1. With library loaded, type in the search field
2. Enter search term (e.g., asset name prefix)
3. **Expected Result**: Only matching assets display, count updates

### Test 6: Type Filtering

1. Click the "Type" dropdown (currently "All Types")
2. Select a specific asset type (e.g., "Texture2D")
3. **Expected Result**: Only assets of that type display

### Test 7: Tag Filtering

1. Click the "Tag" dropdown (currently "All Tags")
2. Select a tag (e.g., "test")
3. **Expected Result**: Only assets with that tag display

### Test 8: Group Filtering

1. Click the "Group" dropdown (currently "All Groups")
2. Select a group (e.g., "Test Assets")
3. **Expected Result**: Only assets in that group display

### Test 9: Multi-Select and Import

1. In the asset grid, click on an asset
2. Hold Ctrl and click on another asset to multi-select
3. Click "Import Selected"
4. Select a destination folder in the dialog
5. **Expected Result**:
   - Assets import to selected location
   - Import dialog shows success message
   - Assets appear in Project window
   - No errors in console

### Test 10: Verify Imported Assets Work

1. If a prefab was imported, try to instantiate it
2. If a script was imported, try to attach it to a GameObject
3. If a texture was imported, try to apply it to a material
4. **Expected Result**: Imported assets function correctly in the project

## Edge Cases to Test

### Test 11: Handle Duplicate Filenames

1. Import the same asset twice
2. **Expected Result**: Second import creates "AssetName (1).ext" format

### Test 12: Large Library Performance

1. Add 50+ assets to a library
2. Open library and scroll through grid
3. **Expected Result**: UI remains responsive, thumbnails load progressively

### Test 13: Special Characters in Asset Names

1. Create assets with special characters in names
2. Try to add them to library
3. **Expected Result**: Assets are sanitized and added successfully

### Test 14: Missing Thumbnail Handling

1. Add a non-image asset (script, prefab) without custom thumbnail
2. View in library
3. **Expected Result**: Placeholder displays, no errors

### Test 15: Library File Validation

1. Try to load a non-.unitylib file
2. **Expected Result**: Error message appears, file rejected

## Expected Issues and Fixes

### Issue: Compilation Errors

**Solution**:
- Ensure all files are in the correct directories
- Check that assembly definition path is correct
- Verify no typos in namespace declarations (should be "CPAM")

### Issue: Context Menu Not Appearing

**Solution**:
- Ensure ContextMenuExtension.cs is in Editor/Scripts/UI/
- Restart Unity after adding the file
- Verify assembly definition is set to Editor platform only

### Issue: Thumbnails Not Loading

**Solution**:
- Check that asset files exist in the extracted library
- Verify AssetLibraryLoader is correctly reading files
- Check console for specific error messages

### Issue: Import Fails

**Solution**:
- Ensure destination folder is accessible and writable
- Check that assets exist in library
- Verify AssetDatabase.Refresh() is called after import

## Performance Metrics

Expected performance on standard hardware:

- Library creation: < 1 second
- Adding 10 assets: < 2 seconds
- Loading library with 50+ assets: < 3 seconds
- Searching/filtering: < 100ms
- Importing 5 assets: < 2 seconds

## Success Criteria

After running all tests, verify:

- [x] All features work without crashes
- [x] No console errors during normal operation
- [x] Imported assets function correctly in the project
- [x] UI is responsive even with large libraries
- [x] File handling is robust (special characters, duplicates, etc.)
- [x] Error messages are clear and helpful

---

## Bug Reporting

If you encounter issues:

1. Note the exact steps to reproduce
2. Check the Unity console for error messages
3. Include:
   - Unity version
   - OS (Windows/Mac/Linux)
   - Asset types involved
   - Error messages from console

---
