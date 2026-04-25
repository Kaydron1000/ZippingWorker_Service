# Automatic Drive Letter Resolution

## Overview

The ZippingWorkerService now includes **automatic resolution of network-mapped drive letters to UNC paths** using the Windows API. This eliminates the need for manual path configuration in most scenarios.

## How It Works

### Automatic Resolution Process

1. **Detection**: When a path like `Z:\folder\file.txt` is encountered, the service checks if `Z:` is a network drive
2. **Resolution**: If it's a network drive, the service uses Windows API (`WNetGetConnection`) to get the UNC path (e.g., `\\server\share`)
3. **Reconstruction**: The original path is rebuilt using the UNC path: `\\server\share\folder\file.txt`
4. **Fallback**: If resolution fails or it's a local drive, the original path is used

### Resolution Priority (in order)

1. **Manual Mappings** (from XML `<driveletters>` element) - Highest priority
2. **Automatic Network Drive Resolution** (via Windows API)
3. **Original Path** (for local drives or when resolution fails)

## Usage Examples

### Example 1: Fully Automatic (No Manual Mappings)

**XML:**
```xml
<zippingfiles xmlns="http://tempuri.org/ZipInfoSchema.xsd">
  <zipfilename>archive.zip</zipfilename>
  <zipfilelocation>C:\output</zipfilelocation>
  <zipcompressionlevel>ultra</zipcompressionlevel>
  <validatezipping>true</validatezipping>
  <driveletters DriveLetter="" DrivePath="" />
  <zipfiles>
    <fileinfo filelocation="Z:\projects\file.txt" filehash="" internalziplocation="file.txt" />
  </zipfiles>
</zippingfiles>
```

**Behavior:**
- If `Z:` is mapped to `\\server\projects`, the path becomes `\\server\projects\projects\file.txt`
- Automatically works across all environments where `Z:` is mapped

### Example 2: Manual Override

**XML:**
```xml
<driveletters DriveLetter="Z:" DrivePath="\\custom-server\testdata" />
<zipfiles>
  <fileinfo filelocation="Z:\projects\file.txt" filehash="" internalziplocation="file.txt" />
</zipfiles>
```

**Behavior:**
- Ignores automatic resolution
- Uses manual path: `\\custom-server\testdata\projects\file.txt`
- Useful for testing or overriding specific drives

### Example 3: Mixed Drives

**XML:**
```xml
<driveletters DriveLetter="C:" DrivePath="\\test\data" />
<zipfiles>
  <fileinfo filelocation="C:\local\file1.txt" filehash="" internalziplocation="file1.txt" />
  <fileinfo filelocation="Z:\network\file2.txt" filehash="" internalziplocation="file2.txt" />
</zipfiles>
```

**Behavior:**
- `C:\local\file1.txt` → `\\test\data\local\file1.txt` (manual override)
- `Z:\network\file2.txt` → `\\server\share\network\file2.txt` (automatic resolution)

## Technical Details

### Windows API Integration

The service uses P/Invoke to call the Windows `mpr.dll` function:

```csharp
[DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
private static extern int WNetGetConnection(
    string localName,
    StringBuilder remoteName,
    ref int length);
```

### Drive Type Detection

The service uses `DriveInfo` to determine drive types:
- **Network**: Automatically resolved to UNC
- **Fixed**: Left as-is (e.g., `C:`, `D:`)
- **Removable**: Left as-is
- **CDRom**: Left as-is

### Logging

The service provides detailed logging for troubleshooting:

```
[Debug] Drive Z: is not a network drive
[Info] Resolved drive Z: to UNC path: \\server\share
[Debug] Resolved C:\source\file.txt to \\server\share\source\file.txt via UNC
[Debug] Drive C: is local drive (Fixed), keeping original path
[Warning] Drive X: is network drive but UNC path could not be resolved
```

## Benefits

### 1. **Environment Independence**
```xml
<!-- Same XML works in all environments -->
<fileinfo filelocation="Z:\projects\file.txt" ... />
```
- Dev environment: `Z:` → `\\dev-server\projects`
- Prod environment: `Z:` → `\\prod-server\projects`
- No XML changes needed!

### 2. **Automatic Recursive Resolution**
If your network path contains mapped drives, they're all resolved:
- `Z:\data\file.txt` where `Z:` → `\\server\share`
- Result: `\\server\share\data\file.txt`
- The UNC path can now be accessed from any machine on the network

### 3. **Service Account Compatibility**
When running as a Windows Service with a service account:
- Service accounts often have different drive mappings than users
- Automatic UNC resolution ensures paths work regardless of drive letter assignments
- No hardcoded drive letters in your data

### 4. **Container/VM Scenarios**
- Host has `Z:` mapped to `\\server\share`
- Container/VM might not have drive mappings
- Service resolves to UNC, which works across both

## Limitations

1. **Windows-Only**: Uses Windows API - won't work on Linux/Mac
2. **Requires Network Access**: UNC paths require network connectivity
3. **Credentials**: Assumes the service account has access to network shares
4. **Disconnected Drives**: If a drive is mapped but disconnected, resolution may fail

## Configuration

### Recommended XML Template

```xml
<zippingfiles xmlns="http://tempuri.org/ZipInfoSchema.xsd">
  <zipfilename>archive.zip</zipfilename>
  <zipfilelocation>C:\output</zipfilelocation>
  <zipcompressionlevel>ultra</zipcompressionlevel>
  <validatezipping>true</validatezipping>
  <!-- Leave empty for full automatic resolution -->
  <driveletters DriveLetter="" DrivePath="" />
  <zipfiles>
    <!-- Network drives automatically resolved -->
    <fileinfo filelocation="Z:\path\file.txt" filehash="" internalziplocation="file.txt" />
  </zipfiles>
</zippingfiles>
```

### When to Use Manual Mappings

Only provide manual `driveletters` mappings when:
- Testing with alternate paths
- Overriding automatic resolution for specific scenarios
- Dealing with drives that can't be automatically resolved
- Need deterministic behavior regardless of network mapping

## Troubleshooting

### Issue: Path not resolving
**Check:**
1. Is the drive actually a network drive? (Check `DriveInfo.DriveType`)
2. Is the drive currently connected?
3. Check service logs for resolution attempts
4. Try `net use Z:` in command prompt to verify mapping

### Issue: Access denied on UNC path
**Check:**
1. Service account has permissions to the network share
2. Service is running with correct credentials
3. Firewall rules allow network access

### Issue: Need to disable automatic resolution
**Solution:**
Provide manual mapping with original path:
```xml
<driveletters DriveLetter="Z:" DrivePath="Z:\" />
```

## See Also

- `DriveLetterResolver.cs` - Core resolution service
- `DriveLetterResolutionExample.cs` - Usage examples
- `ZipInfoController.cs` - Integration point
