# Zipping Worker Service

A background service for zipping files with REST API support.

## Features

- Background worker that processes zip requests asynchronously
- Multiple REST API endpoints:
  - Simple JSON-based API for basic zip requests
  - **ZipInfo XML/Binary API for large-scale file zipping with structured data**
- Support for multiple archiver types (SevenZip, DotNetZip)
- Configurable compression levels (nocompression, fastest, fast, normal, maximum, ultra)
- **Comprehensive validation system**:
  - SHA256 hash calculation for all files before zipping
  - Extract and validate zip integrity after creation
  - Compare hashes of original files vs extracted files
  - Option to provide pre-calculated hashes or auto-calculate
- Progress tracking and logging

## Configuration

Edit `config.xml` to configure the service:

```xml
<ZippingWorkerServiceConfiguration 
    xmlns="http://tempuri.org/ZippingWorkerServiceConfigurationSchema.xsd"
    sevenzipexepath="7z.exe"
    symlinktempdir=""
    archiver="sevenzip"
    compressionlevel="optimal">
</ZippingWorkerServiceConfiguration>
```

### Configuration Options

- **sevenzipexepath**: Path to 7z.exe (default: "7z.exe")
- **symlinktempdir**: Temporary directory for symlinks (optional)
- **archiver**: Archiver type - "sevenzip" or "dotnetzip" (default: sevenzip)
- **compressionlevel**: Compression level - "optimal", "fastest", or "nocompression" (default: optimal)

## Usage

### Starting the Service

```bash
dotnet run
```

The service will start on `http://localhost:5000` (or the configured port).

---

## API Endpoints

### Health Check - `/api/health/ping`

**Endpoint**: `GET /api/health/ping`

Simple health check endpoint to verify the server is responsive. Useful for monitoring and client connectivity checks.

**Response**: Plain text `"pong"`

**Example with curl**:
```bash
curl http://localhost:5000/api/health/ping
# Response: pong
```

**Example with PowerShell**:
```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/health/ping" -Method Get
# Response: pong
```

---

### 1. Simple JSON API - `/api/zip`

**Endpoint**: `POST /api/zip`

**Request Body**:
```json
{
  "files": [
    {
      "sourcePath": "C:\\temp\\file1.txt",
      "archivePath": "file1.txt"
    },
    {
      "sourcePath": "C:\\temp\\folder\\file2.txt",
      "archivePath": "folder/file2.txt"
    }
  ],
  "outputArchivePath": "C:\\output\\archive.zip"
}
```

**Example with curl**:
```bash
curl -X POST http://localhost:5000/api/zip \
  -H "Content-Type: application/json" \
  -d '{
    "files": [
      {
        "sourcePath": "C:\\temp\\file1.txt",
        "archivePath": "file1.txt"
      }
    ],
    "outputArchivePath": "C:\\output\\archive.zip"
  }'
```

**Example with PowerShell**:
```powershell
$body = @{
    files = @(
        @{
            sourcePath = "C:\temp\file1.txt"
            archivePath = "file1.txt"
        }
    )
    outputArchivePath = "C:\output\archive.zip"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/zip" -Method Post -Body $body -ContentType "application/json"
```

---

### 2. ZipInfo Binary API - `/api/zipinfo/binary`

**For large-scale file zipping using structured XSD-based data**

**Endpoint**: `POST /api/zipinfo/binary`  
**Content-Type**: `application/octet-stream`

This endpoint accepts a byte array of XML-serialized `ZipInfoType` objects, allowing you to define complex zip operations with many files.

#### ZipInfoType Structure

```xml
<?xml version="1.0" encoding="utf-8"?>
<zippingfiles xmlns="http://tempuri.org/ZipInfoSchema.xsd">
  <zipfilename>output.zip</zipfilename>
  <zipfilelocation>C:\output</zipfilelocation>
  <zipcompressionlevel>ultra</zipcompressionlevel>
  <validatezipping>true</validatezipping>
  <driveletters DriveLetter="C:" DrivePath="E:\MappedPath" />
  <zipfiles>
    <fileinfo filelocation="C:\source\file1.txt" filehash="" internalziplocation="folder/file1.txt" />
    <fileinfo filelocation="C:\source\file2.txt" filehash="" internalziplocation="folder/file2.txt" />
  </zipfiles>
</zippingfiles>
```

**Compression Levels**: `nocompression`, `fastest`, `fast`, `normal`, `maximum`, `ultra`

**Drive Letter Resolution**: The service automatically resolves network-mapped drive letters to their UNC paths (`\\server\share`). The `driveletters` element allows you to provide manual overrides:

- **Automatic Resolution**: Network drives (e.g., `Z:` mapped to `\\server\share`) are automatically resolved to UNC paths
- **Manual Override**: If `DriveLetter="C:"` and `DrivePath="E:\TestData"`, then `C:\source\file.txt` will be resolved to `E:\TestData\source\file.txt`
- **Priority**: Manual mappings take precedence over automatic resolution
- **Local Drives**: Local drives (C:, D:, etc.) are left as-is unless manually overridden

Leave `driveletters` attributes empty if you want fully automatic resolution:
```xml
<driveletters DriveLetter="" DrivePath="" />
```

**Example with PowerShell**:
```powershell
# See test-zipinfo-binary.ps1 for complete example
Add-Type -Path ".\bin\Debug\net10.0\ZippingWorkerService.dll"

$zipInfo = New-Object ZippingWorkerService.ZipInfoType
$zipInfo.zipfilename = "archive.zip"
$zipInfo.zipfilelocation = "C:\temp"
$zipInfo.zipcompressionlevel = [ZippingWorkerService.CompressionLevelEnumType]::ultra
$zipInfo.validatezipping = $true

# Add drive letter mapping
$driveLetter = New-Object ZippingWorkerService.DriveLetterType
$driveLetter.DriveLetter = "C:"
$driveLetter.DrivePath = "E:\TestData"
$zipInfo.driveletters = $driveLetter

# Add files...
$file = New-Object ZippingWorkerService.FileInfoType
$file.filelocation = "C:\source\file.txt"
$file.internalziplocation = "file.txt"
$file.filehash = ""
$zipInfo.zipfiles = @($file)

# Serialize and send
$serializer = New-Object System.Xml.Serialization.XmlSerializer([ZippingWorkerService.ZipInfoType])
$ms = New-Object System.IO.MemoryStream
$serializer.Serialize($ms, $zipInfo)
$bytes = $ms.ToArray()

Invoke-RestMethod -Uri "http://localhost:5000/api/zipinfo/binary" `
    -Method Post -Body $bytes -ContentType "application/octet-stream"
```

**Example with C#**:
```csharp
var zipInfo = new ZipInfoType
{
    zipfilename = "archive.zip",
    zipfilelocation = @"C:\output",
    zipcompressionlevel = CompressionLevelEnumType.ultra,
    validatezipping = true,
    driveletters = new DriveLetterType
    {
        DriveLetter = "C:",
        DrivePath = @"E:\TestData"
    },
    zipfiles = new[]
    {
        new FileInfoType
        {
            filelocation = @"C:\source\file.txt",
            internalziplocation = "file.txt",
            filehash = ""
        }
    }
};

// Serialize
var serializer = new XmlSerializer(typeof(ZipInfoType));
using var ms = new MemoryStream();
serializer.Serialize(ms, zipInfo);
var bytes = ms.ToArray();

// Send
using var client = new HttpClient();
var content = new ByteArrayContent(bytes);
content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
var response = await client.PostAsync("http://localhost:5000/api/zipinfo/binary", content);
```

---

### 3. ZipInfo XML API - `/api/zipinfo/xml`

**Alternative to binary API - accepts XML directly**

**Endpoint**: `POST /api/zipinfo/xml`  
**Content-Type**: `application/xml` or `text/xml`

Send the XML structure directly without serialization to bytes.

**Example with PowerShell**:
```powershell
# See test-zipinfo-xml.ps1
$xml = @"
<?xml version="1.0" encoding="utf-8"?>
<zippingfiles xmlns="http://tempuri.org/ZipInfoSchema.xsd">
  <zipfilename>archive.zip</zipfilename>
  <zipfilelocation>C:\output</zipfilelocation>
  <zipcompressionlevel>ultra</zipcompressionlevel>
  <validatezipping>true</validatezipping>
  <driveletters DriveLetter="C:" DrivePath="E:\TestData" />
  <zipfiles>
    <fileinfo filelocation="C:\source\file.txt" filehash="" internalziplocation="file.txt" />
  </zipfiles>
</zippingfiles>
"@

Invoke-RestMethod -Uri "http://localhost:5000/api/zipinfo/xml" `
    -Method Post -Body $xml -ContentType "application/xml"
```

---

## Response Format

All endpoints return:

```json
{
  "message": "Zip request queued successfully",
  "outputPath": "C:\\output\\archive.zip",
  "fileCount": 2,
  "compressionLevel": "ultra",
  "validateZipping": true
}
```

**Status Codes**:
- `202 Accepted` - Request queued successfully
- `400 Bad Request` - Invalid input
- `500 Internal Server Error` - Processing error

---

## Zip Validation Feature

The service includes comprehensive validation to ensure zip integrity:

### How Validation Works

1. **Pre-Zip Hash Calculation**
   - For each file, calculate SHA256 hash before zipping
   - If hash is provided in XML, use that; otherwise auto-calculate

2. **Create Archive**
   - Zip files using the configured archiver

3. **Post-Zip Validation**
   - Extract zip to temporary location
   - Calculate hash of each extracted file
   - Compare with original hashes
   - Report success/failure with detailed errors

### Enabling Validation

Set `validatezipping="true"` in your ZipInfoType XML:

```xml
<zippingfiles xmlns="http://tempuri.org/ZipInfoSchema.xsd">
  <zipfilename>validated-archive.zip</zipfilename>
  <zipfilelocation>C:\output</zipfilelocation>
  <validatezipping>true</validatezipping>
  <driveletters DriveLetter="C:" DrivePath="" />
  <zipfiles>
    <fileinfo filelocation="C:\source\file.txt" filehash="" internalziplocation="file.txt" />
  </zipfiles>
</zippingfiles>
```

### Pre-Calculated Hashes (Optional)

You can provide SHA256 hashes to avoid recalculation:

```xml
<fileinfo filelocation="C:\source\file.txt" filehash="a3b5c7d9e1f2..." internalziplocation="file.txt" />
```

**Calculate hash in PowerShell:**
```powershell
$hash = Get-FileHash -Path "C:\file.txt" -Algorithm SHA256
$hash.Hash.ToLower()
```

**Calculate hash in C#:**
```csharp
using var sha256 = SHA256.Create();
using var stream = File.OpenRead(filePath);
var hashBytes = await sha256.ComputeHashAsync(stream);
var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
```

### Validation Logging

Check service logs for validation results:

```
[INFO] Pre-calculating hashes for validation...
[DEBUG] Calculated hash for C:\source\file.txt: a3b5c7d9...
[INFO] Archiver: Adding file.txt
[INFO] Completed zip request: C:\output\archive.zip
[INFO] Starting zip validation...
[INFO] Extracting zip to temp location for validation: C:\Temp\zipvalidation_abc123
[DEBUG] Hash validated for file.txt: a3b5c7d9...
[INFO] Zip validation PASSED. 1 files validated successfully.
```

### Disable Validation for Speed

For faster processing without validation:

```xml
<validatezipping>false</validatezipping>
```

---

## Test Scripts

- **test-api.ps1** - Test simple JSON API
- **test-zipinfo-binary.ps1** - Test ZipInfo binary endpoint
- **test-zipinfo-xml.ps1** - Test ZipInfo XML endpoint
- **test-validation.ps1** - Test validation with and without pre-calculated hashes

## Architecture

- **Worker**: Background service that processes zip requests from the queue
- **ZipRequestQueue**: Thread-safe queue for managing zip requests
- **ArchiverFactory**: Creates appropriate archiver instance based on configuration
- **ZipValidationService**: SHA256 hash calculation and zip integrity validation
- **ZipController**: Simple JSON-based REST API endpoint
- **ZipInfoController**: Advanced XSD-based XML/Binary API for large-scale operations

## Logging

The service logs all operations including:
- Queue status
- Pre-zip hash calculations
- Progress updates
- Archiver operations
- Post-zip validation results (PASSED/FAILED)
- Detailed error messages for validation failures
- Errors and warnings

Check the console output or configure logging to file in `appsettings.json`.
