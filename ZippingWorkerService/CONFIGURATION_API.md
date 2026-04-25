# Configuration Management API

Remote configuration management endpoint for ZippingWorkerService.

## Endpoint

**Base Path**: `/api/zippingworkerserviceconfiguration`

The endpoint path matches the XSD element name `<zippingworkerserviceconfiguration>`.

## API Reference

### GET - Retrieve Current Configuration

```http
GET /api/zippingworkerserviceconfiguration
```

**Response**: `200 OK`
```json
{
  "servicePort": 5000,
  "sevenZipExePath": "7z.exe",
  "tempDir_SymLink": "%APPPATH%\\tempsymlink",
  "tempDir_ZipStaging": "%APPPATH%\\tempzip",
  "archiver": "sevenzip",
  "compressionLevel": "ultra",
  "resolvedSevenZipExePath": "C:\\Program Files\\7-Zip\\7z.exe",
  "resolvedTempDir_SymLink": "E:\\Code\\CSharp\\ZippingWorkerService\\tempsymlink",
  "resolvedTempDir_ZipStaging": "E:\\Code\\CSharp\\ZippingWorkerService\\tempzip"
}
```

**Response Fields:**
- **servicePort** (int): Port the service listens on
- **sevenZipExePath** (string): Path to 7z.exe (raw, may contain variables)
- **tempDir_SymLink** (string): Temporary symlink directory (raw)
- **tempDir_ZipStaging** (string): Zip staging directory (raw)
- **archiver** (string): Archiver type (`sevenzip` or `dotnetzip`)
- **compressionLevel** (string): Compression level (`nocompression`, `fastest`, `fast`, `normal`, `maximum`, `ultra`)
- **resolvedSevenZipExePath** (string): Resolved path after environment variable expansion
- **resolvedTempDir_SymLink** (string): Resolved symlink directory
- **resolvedTempDir_ZipStaging** (string): Resolved staging directory

---

### PUT - Update Configuration (Partial Update)

Updates only the attributes provided in query parameters. **Omitted attributes retain their current values** (not reset to defaults).

```http
PUT /api/zippingworkerserviceconfiguration?serviceport=8080&archiver=sevenzip
```

**Query Parameters** (all optional, matches XSD attributes):

| Parameter | Type | Default | Valid Values | Description |
|-----------|------|---------|--------------|-------------|
| `serviceport` | integer | 5000 | Any valid port | Service listening port |
| `sevenzipexepath` | string | 7z.exe | Any path | Path to 7z.exe |
| `tempdir_symlink` | string | %APPPATH%\tempsymlink | Any path | Temporary symlink directory |
| `tempdir_zipstaging` | string | %APPPATH%\tempzip | Any path | Zip staging directory |
| `archiver` | string | sevenzip | `sevenzip`, `dotnetzip` | Archiver implementation |
| `compressionlevel` | string | ultra | `nocompression`, `fastest`, `fast`, `normal`, `maximum`, `ultra` | Compression level |

**Response**: `200 OK`
```json
{
  "message": "Configuration updated successfully. Restart service to apply changes.",
  "updatedAttributes": [
    "serviceport=8080",
    "archiver=sevenzip"
  ],
  "currentConfiguration": {
    "servicePort": 8080,
    "sevenZipExePath": "7z.exe",
    "tempDir_SymLink": "%APPPATH%\\tempsymlink",
    "tempDir_ZipStaging": "%APPPATH%\\tempzip",
    "archiver": "sevenzip",
    "compressionLevel": "ultra",
    "resolvedSevenZipExePath": "C:\\Program Files\\7-Zip\\7z.exe",
    "resolvedTempDir_SymLink": "E:\\Code\\CSharp\\ZippingWorkerService\\tempsymlink",
    "resolvedTempDir_ZipStaging": "E:\\Code\\CSharp\\ZippingWorkerService\\tempzip"
  }
}
```

**Error Response**: `400 Bad Request`
```json
{
  "error": "Invalid archiver value: invalid. Valid values: sevenzip, dotnetzip"
}
```

---

## Examples

### PowerShell

**Get current configuration:**
```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/zippingworkerserviceconfiguration" -Method Get
```

**Update service port:**
```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/zippingworkerserviceconfiguration?serviceport=8080" -Method Put
```

**Update multiple settings:**
```powershell
$params = @{
    serviceport = 8080
    archiver = "sevenzip"
    compressionlevel = "maximum"
}
$queryString = ($params.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join "&"
Invoke-RestMethod -Uri "http://localhost:5000/api/zippingworkerserviceconfiguration?$queryString" -Method Put
```

**Update path with spaces (URL encoding):**
```powershell
$path = [System.Uri]::EscapeDataString("C:\Program Files\7-Zip\7z.exe")
Invoke-RestMethod -Uri "http://localhost:5000/api/zippingworkerserviceconfiguration?sevenzipexepath=$path" -Method Put
```

---

### curl

**Get current configuration:**
```bash
curl http://localhost:5000/api/zippingworkerserviceconfiguration
```

**Update service port:**
```bash
curl -X PUT "http://localhost:5000/api/zippingworkerserviceconfiguration?serviceport=8080"
```

**Update multiple settings:**
```bash
curl -X PUT "http://localhost:5000/api/zippingworkerserviceconfiguration?serviceport=8080&archiver=sevenzip&compressionlevel=maximum"
```

**Update path with URL encoding:**
```bash
curl -X PUT "http://localhost:5000/api/zippingworkerserviceconfiguration?sevenzipexepath=C%3A%5CProgram%20Files%5C7-Zip%5C7z.exe"
```

---

### C# / .NET

**Using HttpClient:**
```csharp
using var httpClient = new HttpClient();

// Get configuration
var response = await httpClient.GetAsync("http://localhost:5000/api/zippingworkerserviceconfiguration");
var config = await response.Content.ReadFromJsonAsync<ConfigurationResponse>();

// Update configuration
var updateResponse = await httpClient.PutAsync(
    "http://localhost:5000/api/zippingworkerserviceconfiguration?serviceport=8080&archiver=sevenzip",
    null);
```

---

## Key Features

### 1. Partial Updates
Only attributes specified in query parameters are updated. Omitted attributes **keep their current values**.

**Example:**
```bash
# Only updates port, all other settings remain unchanged
curl -X PUT "http://localhost:5000/api/zippingworkerserviceconfiguration?serviceport=8080"
```

### 2. XSD Schema Alignment
- Endpoint path: `/api/zippingworkerserviceconfiguration` → matches `<zippingworkerserviceconfiguration>` element
- Query parameters: Match XSD attribute names exactly
- Required/Optional: All parameters optional (partial update behavior)
- Types: Match XSD types (integer, string, enums)
- Validation: Enum values validated against XSD restrictions

### 3. Configuration Persistence
Updates are written to `config.xml` in the application directory. The file is locked during read/write operations to prevent corruption.

### 4. Service Restart Required
Configuration changes are written to disk immediately but **require a service restart** to take effect. The API response reminds you of this.

### 5. Resolved Paths
The GET response includes both raw values (may contain `%APPPATH%` variables) and resolved paths (after environment variable expansion).

---

## Thread Safety

The controller uses a `lock` around all configuration file operations to ensure thread-safe reads and writes when multiple API calls occur simultaneously.

---

## Error Handling

**400 Bad Request:**
- Invalid enum values for `archiver` or `compressionlevel`
- No query parameters provided

**500 Internal Server Error:**
- File I/O errors
- XML parsing/validation errors
- Unexpected exceptions

---

## Security Considerations

⚠️ **This endpoint allows remote configuration changes**

Consider adding:
1. **Authentication/Authorization** - Require API keys or OAuth tokens
2. **IP Whitelisting** - Restrict to management network
3. **HTTPS** - Encrypt configuration data in transit
4. **Audit Logging** - Log all configuration changes with user/IP
5. **Configuration Backup** - Backup config.xml before changes

**Example with API key middleware:**
```csharp
[ApiKey] // Custom authorization attribute
[Route("api/zippingworkerserviceconfiguration")]
public class ConfigurationController : ControllerBase
{
    // ...
}
```

---

## OpenAPI / Swagger

The endpoint is automatically documented in Swagger UI when running in development mode:

```
http://localhost:5000/swagger
```

Look for the **ConfigurationController** section to test the API interactively.

---

## Monitoring

Configuration changes are logged via `ILogger<ConfigurationController>`:

```
[2026-04-24 14:23:45] [Information] Configuration updated: serviceport=8080, archiver=sevenzip
```

Consider integrating with your existing metrics:
```csharp
_metricsService.RecordConfigurationChange(updatedAttributes.Count);
```
