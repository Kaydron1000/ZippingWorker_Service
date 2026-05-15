# Command-Line Arguments

## Log Level Configuration

You can control the logging verbosity at startup using the `--loglevel` argument.

### Usage

```bash
dotnet run --loglevel=<level>
```

or with a published executable:

```bash
ZippingWorker_Service.exe --loglevel=<level>
```

### Available Log Levels

| Level | Description | Use Case |
|-------|-------------|----------|
| `Trace` | Most verbose - shows all log messages including trace details | Deep debugging, seeing every operation |
| `Debug` | Detailed debugging information | Development and troubleshooting |
| `Information` | General informational messages (default) | Normal production operation |
| `Warning` | Warning messages only | Production with reduced noise |
| `Error` | Error messages only | Production - only see failures |
| `Critical` | Critical errors only | Production - only catastrophic issues |
| `None` | No logging | Not recommended |

### Examples

**Development with detailed logs:**
```bash
dotnet run --loglevel=Debug
```

**Production with minimal logging:**
```bash
dotnet run --loglevel=Warning
```

**Troubleshooting with maximum verbosity:**
```bash
dotnet run --loglevel=Trace
```

**Silent operation (not recommended):**
```bash
dotnet run --loglevel=None
```

### Running as Published Executable

**Windows:**
```powershell
# Run directly
.\ZippingWorker_Service.exe --loglevel=Debug

# Run as Windows Service with log level
sc config "ZippingWorkerService" binPath= "C:\path\to\ZippingWorker_Service.exe --loglevel=Debug"
sc start "ZippingWorkerService"
```

**Linux/macOS:**
```bash
# Run directly
./ZippingWorker_Service --loglevel=Debug

# Run as systemd service (add to service file)
# ExecStart=/path/to/ZippingWorker_Service --loglevel=Information
```

### Visual Studio / Rider

In Visual Studio or Rider, you can set command-line arguments:

**Visual Studio:**
1. Right-click project → Properties
2. Debug → General → Command line arguments
3. Enter: `--loglevel=Debug`

**Rider:**
1. Run → Edit Configurations
2. Program arguments: `--loglevel=Debug`

### Docker

```dockerfile
# In your Dockerfile CMD or when running
docker run myimage --loglevel=Information
```

### Invalid Log Level

If an invalid log level is provided, the service will:
- Display a warning message
- List valid log levels
- Fall back to the default (`Information`)

Example output:
```
Warning: Invalid log level 'Verbose'. Using default: Information
Valid levels: Trace, Debug, Information, Warning, Error, Critical, None
```

## Future Enhancements

Additional command-line arguments may be added in future versions, such as:
- `--port` - Override the service port
- `--config` - Specify custom config file path
- `--help` - Display help information
