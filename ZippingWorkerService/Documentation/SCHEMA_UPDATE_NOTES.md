# ZipInfoSchema Update Notes

## Summary of Changes

The ZipInfoSchema.xsd was updated with the following structural changes:

### 1. FileInfoType - Element to Attribute Conversion
**Before (element-based):**
```xml
<zipinfo>
  <filelocation>C:\source\file.txt</filelocation>
  <filehash>abc123...</filehash>
  <internalziplocation>folder/file.txt</internalziplocation>
</zipinfo>
```

**After (attribute-based):**
```xml
<fileinfo filelocation="C:\source\file.txt" filehash="abc123..." internalziplocation="folder/file.txt" />
```

### 2. New DriveLetterType
Added new type for drive letter path mapping:
```xml
<driveletters DriveLetter="C:" DrivePath="E:\TestData" />
```

This allows mapping drive letters to alternative paths. For example:
- If `DriveLetter="C:"` and `DrivePath="E:\TestData"`
- Then `C:\source\file.txt` resolves to `E:\TestData\source\file.txt`

### 3. Type Rename
- `ZipFileInfoType` → `FileInfoType`

## Code Changes Made

### Controller Updates (ZipInfoController.cs)
1. **Added drive letter mapping support**
   - Created `Dictionary<string, string>` for drive mappings
   - Added `ReplaceDriveLetter()` helper method
   - Applied drive letter replacement to both source paths and output path

2. **Processing logic**
   - Extracts `driveletters` from ZipInfoType
   - Applies mapping when building file paths
   - Logs drive letter configuration

### Examples Updated
1. **ZipInfoSerializationExample.cs**
   - Changed `ZipFileInfoType` to `FileInfoType`
   - Added `DriveLetterType` initialization

2. **test-zipinfo-xml.ps1**
   - Updated XML to use attribute-based `<fileinfo>` elements
   - Added `<driveletters>` element

3. **test-zipinfo-binary.ps1**
   - Changed PowerShell object creation from `ZipFileInfoType` to `FileInfoType`
   - Added `DriveLetterType` object creation

### Documentation Updates (README.md)
Updated all XML examples throughout to reflect:
- Attribute-based `<fileinfo>` syntax
- New `<driveletters>` element
- Drive letter mapping explanation

## Breaking Changes

### For API Users
- **XML Structure**: File info must now use attributes instead of nested elements
- **Type Names**: `ZipFileInfoType` is now `FileInfoType` in serialized objects

### Migration Guide

**Old XML format:**
```xml
<zippingfiles xmlns="http://tempuri.org/ZipInfoSchema.xsd">
  <zipfiles>
    <zipinfo>
      <filelocation>C:\file.txt</filelocation>
      <filehash></filehash>
      <internalziplocation>file.txt</internalziplocation>
    </zipinfo>
  </zipfiles>
</zippingfiles>
```

**New XML format:**
```xml
<zippingfiles xmlns="http://tempuri.org/ZipInfoSchema.xsd">
  <driveletters DriveLetter="" DrivePath="" />
  <zipfiles>
    <fileinfo filelocation="C:\file.txt" filehash="" internalziplocation="file.txt" />
  </zipfiles>
</zippingfiles>
```

**Note:** The `<driveletters>` element is required by the schema. Leave attributes empty if not using drive mapping.

## New Features

### Automatic UNC Path Resolution

The service now **automatically resolves network-mapped drive letters to their UNC paths** using Windows API (`WNetGetConnection`). This means:

1. **Network Drive Auto-Resolution**: If you have a drive mapped (e.g., `Z:` → `\\server\share`), the service automatically resolves `Z:\folder\file.txt` to `\\server\share\folder\file.txt`

2. **Manual Override Support**: The `driveletters` element allows manual path remapping that takes precedence over automatic resolution

3. **Resolution Priority**:
   - **First**: Manual mappings from XML (`driveletters` element)
   - **Second**: Automatic network drive resolution via Windows API
   - **Third**: Original path (for local drives or when resolution fails)

### Drive Letter Path Mapping

Manual mapping via XML:

```xml
<driveletters DriveLetter="C:" DrivePath="\\network\share" />
```

Use cases:
- Override automatic resolution for specific scenarios
- Map local drives to network shares
- Cross-environment path translation (e.g., container/VM remapping)
- Testing with different root directories

**Example Behavior:**

| Input Path | Manual Mapping | Automatic Resolution | Final Path |
|------------|----------------|---------------------|------------|
| `Z:\data\file.txt` | None | `Z:` → `\\server\data` | `\\server\data\data\file.txt` |
| `C:\temp\file.txt` | `C:` → `E:\Test` | N/A (local) | `E:\Test\temp\file.txt` |
| `M:\docs\file.txt` | None | Not mapped | `M:\docs\file.txt` (unchanged) |
| `C:\source\file.txt` | None | N/A (local) | `C:\source\file.txt` (unchanged) |

The mapping applies to:
- All file source paths in `<fileinfo>` elements
- The output `<zipfilelocation>` path

**Empty driveletters element** (automatic resolution only):
```xml
<driveletters DriveLetter="" DrivePath="" />
```

## Backward Compatibility

⚠️ **Breaking Change**: This update is NOT backward compatible with the old schema.

Clients using the old XML format will receive deserialization errors. All API consumers must update their XML structure to use the new attribute-based format.

## Build Status

✅ Build successful - All changes compile and integrate correctly
