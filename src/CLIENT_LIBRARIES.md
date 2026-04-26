# ZippingWorkerService Client Libraries

Client libraries for interacting with the ZippingWorkerService API. These libraries provide a fluent, type-safe way to create zip requests based on the `ZipInfoSchema.xsd` schema.

## 🎯 Key Features

- ✅ **Automatic Drive Detection** - No need to manually specify drive letters! Both libraries auto-detect drives from your system
- ✅ **Fluent Builder API** - Easy-to-use builder pattern for creating requests
- ✅ **Type-Safe** - Compile-time checking (C#) and runtime validation (Python)
- ✅ **Cross-Platform** - Works on Windows, Linux, and macOS
- ✅ **Async Support** - Full async/await support in both libraries
- ✅ **Manual Override** - Option to customize drive mappings when needed

## Available Client Libraries

### 1. C# Client Library (.NET 10.0)

Located in: `ZippingWorkerService.Client/`

A .NET library that provides:
- Type-safe classes generated from the XSD schema
- **Automatic drive detection** using `DriveInfo.GetDrives()`
- Fluent builder pattern for creating zip requests
- Synchronous and asynchronous HTTP client
- XML serialization/deserialization utilities

**Quick Start (C#):**

```csharp
using ZippingWorkerService.Client;

using var client = new ZippingServiceClient("http://localhost:5000");

// Drives are automatically detected - no need to specify them!
var zipRequest = ZipRequestBuilder.Create()
    .WithZipFileName("my-archive.zip")
    .WithZipFileLocation(@"C:\output")
    .WithCompressionLevel(CompressionLevelEnumType.ultra)
    // No AddDriveLetter() needed - auto-detected!
    .AddFile(@"C:\file.txt", "file.txt")
    .Build();

await client.SubmitZipRequestAsync(zipRequest);
```

[Full C# Documentation](ZippingWorkerService.Client/README.md)

### 2. Python Client Library (Python 3.8+)

Located in: `ZippingWorkerService.Client.Python/`

A Python package that provides:
- Dataclasses matching the XSD schema
- **Automatic drive detection** (Windows via ctypes, Unix via root filesystem)
- Fluent builder pattern for creating zip requests
- Both sync and async HTTP client
- XML serialization/deserialization using ElementTree

**Quick Start (Python):**

```python
from zipping_client import ZippingServiceClient, ZipRequestBuilder, CompressionLevelEnum

with ZippingServiceClient("http://localhost:5000") as client:
    zip_request = (
        ZipRequestBuilder.create()
        .with_zip_filename("my-archive.zip")
        .with_zip_file_location(r"C:\output")
        .with_compression_level(CompressionLevelEnum.ULTRA)
        .add_drive_letter("C:", r"E:\Data")
        .add_file(r"C:\file.txt", "file.txt")
        .build()
    )

    client.submit_zip_request(zip_request)
```

[Full Python Documentation](ZippingWorkerService.Client.Python/README.md)

## Schema Overview

Both libraries are based on the `ZipInfoSchema.xsd` schema which defines:

### Required Elements:
- **zipfilename**: Name of the output zip file
- **driveletters**: Array of drive letter mappings (at least one required)
  - `driveLetter`: The virtual drive letter (e.g., "C:")
  - `drivePath`: The actual path it maps to
- **zipfiles**: Array of files to include (at least one required)
  - `filelocation`: Source file path
  - `internalziplocation`: Path within the zip archive
  - `filehash`: Optional hash for verification

### Optional Elements:
- **zipfilelocation**: Output directory for the zip file
- **zipcompressionlevel**: Compression level (default: ultra)
  - Options: nocompression, fastest, fast, normal, maximum, ultra
- **validatezipping**: Whether to validate the zip (default: true)
- **deleteinputfiles**: Whether to delete source files after zipping (default: false)

## API Endpoints

The ZippingWorkerService exposes the following endpoints:

### POST /api/zipinfo/binary
Accepts XML-serialized `ZipInfoType` as binary content.

**Content-Type:** `application/octet-stream`

**Example using curl:**
```bash
curl -X POST http://localhost:5000/api/zipinfo/binary \
  -H "Content-Type: application/octet-stream" \
  --data-binary @zipinfo.xml
```

### POST /api/zipinfo/xml
Alternative endpoint accepting XML content.

**Content-Type:** `application/xml` or `text/xml`

## Installation

### C# Client

Add the project reference to your solution:

```bash
dotnet add reference path/to/ZippingWorkerService.Client.csproj
```

Or build and reference the DLL directly:

```bash
cd ZippingWorkerService.Client
dotnet build -c Release
# Reference bin/Release/net10.0/ZippingWorkerService.Client.dll
```

### Python Client

Install from source:

```bash
pip install -e ZippingWorkerService.Client.Python/
```

Or install as a package:

```bash
cd ZippingWorkerService.Client.Python
pip install .
```

## Examples

### C# Example Files

See `ZippingWorkerService.Client/Examples.cs` for complete examples including:
- Basic usage with builder pattern
- Multiple files handling
- Manual object construction
- Custom HttpClient configuration

### Python Example Files

See `ZippingWorkerService.Client.Python/examples.py` for complete examples including:
- Synchronous usage
- Asynchronous usage
- Multiple files handling
- Manual object construction
- XML serialization/deserialization

## Development

### Building the C# Library

```bash
cd ZippingWorkerService.Client
dotnet build
dotnet pack  # Creates NuGet package
```

### Building the Python Library

```bash
cd ZippingWorkerService.Client.Python
pip install build
python -m build
```

### Running Tests

**C#:**
```bash
cd ZippingWorkerService.Client
dotnet test
```

**Python:**
```bash
cd ZippingWorkerService.Client.Python
pip install -e ".[dev]"
pytest
```

## Schema Generation

If the XSD schema is updated, regenerate the C# classes:

**C# (using xsd.exe):**
```bash
xsd ZipInfoSchema.xsd /classes /namespace:ZippingWorkerService.Client
```

**Python:**
The Python models are manually maintained to provide a more Pythonic interface. Update `models.py` to match schema changes.

## Architecture

Both client libraries follow the same architectural pattern:

1. **Models/Schema Classes**: Type-safe representations of the XSD schema
2. **Builder**: Fluent API for constructing requests
3. **Client**: HTTP client for submitting requests to the service
4. **Serialization**: XML serialization utilities

```
┌──────────────────────┐
│   User Application   │
└──────────┬───────────┘
           │
           v
┌──────────────────────┐
│   Builder Pattern    │  (Fluent API)
└──────────┬───────────┘
           │
           v
┌──────────────────────┐
│  Schema Models/DTOs  │  (ZipInfoType, etc.)
└──────────┬───────────┘
           │
           v
┌──────────────────────┐
│  XML Serialization   │
└──────────┬───────────┘
           │
           v
┌──────────────────────┐
│   HTTP Client        │
└──────────┬───────────┘
           │
           v
┌──────────────────────┐
│ ZippingWorkerService │  (API)
└──────────────────────┘
```

## Service Configuration

Before using the clients, ensure the ZippingWorkerService is running:

1. Configure the service port in `config.xml` (default: 5000)
2. Start the service:
   ```bash
   cd ZippingWorkerService
   dotnet run
   ```
3. Verify the service is accessible at `http://localhost:5000`

## License

See the main project license.

## Contributing

Contributions are welcome! Please ensure:

1. Both C# and Python libraries stay in sync with schema changes
2. Examples are updated for new features
3. Tests are added for new functionality
4. Documentation is updated

## Support

For issues, questions, or contributions, please visit:
https://github.com/Kaydron1000/ZippingWorkerService
