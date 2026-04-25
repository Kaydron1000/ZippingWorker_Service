using System.Runtime.InteropServices;
using System.Text;

namespace ZippingWorkerService.Services
{
    /// <summary>
    /// Service for resolving drive letters to UNC paths
    /// </summary>
    public interface IDriveLetterResolver
    {
        /// <summary>
        /// Resolves a path by converting mapped drive letters to their UNC paths
        /// </summary>
        string ResolvePath(string path, Dictionary<string, string>? manualMappings = null);

        /// <summary>
        /// Gets the UNC path for a mapped network drive
        /// </summary>
        string? GetUncPath(string driveLetter);
    }

    public class DriveLetterResolver : IDriveLetterResolver
    {
        private readonly ILogger<DriveLetterResolver> _logger;

        [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int WNetGetConnection(
            [MarshalAs(UnmanagedType.LPTStr)] string localName,
            [MarshalAs(UnmanagedType.LPTStr)] StringBuilder remoteName,
            ref int length);

        public DriveLetterResolver(ILogger<DriveLetterResolver> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Resolves a path by converting drive letters to UNC paths
        /// Priority: Manual mappings > Network drive resolution > Original path
        /// </summary>
        public string ResolvePath(string path, Dictionary<string, string>? manualMappings = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            // Check if path starts with a drive letter (e.g., "C:\..." or "C:/...")
            if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':')
            {
                var driveLetter = path.Substring(0, 2); // "C:"
                var remainingPath = path.Substring(2).TrimStart('\\', '/');

                // Priority 1: Check manual mappings first
                if (manualMappings != null && manualMappings.TryGetValue(driveLetter, out var manualMapping))
                {
                    var resolvedPath = Path.Combine(manualMapping, remainingPath);
                    _logger.LogDebug("Resolved {Original} to {Resolved} using manual mapping", path, resolvedPath);
                    return resolvedPath;
                }

                // Priority 2: Try to resolve network drive
                var uncPath = GetUncPath(driveLetter);
                if (!string.IsNullOrEmpty(uncPath))
                {
                    var resolvedPath = Path.Combine(uncPath, remainingPath);
                    _logger.LogDebug("Resolved {Original} to {Resolved} via UNC", path, resolvedPath);
                    return resolvedPath;
                }

                // Priority 3: Check if it's a local drive
                try
                {
                    var driveInfo = new DriveInfo(driveLetter);
                    if (driveInfo.DriveType == DriveType.Network)
                    {
                        _logger.LogWarning("Drive {Drive} is network drive but UNC path could not be resolved", driveLetter);
                    }
                    else
                    {
                        _logger.LogDebug("Drive {Drive} is local drive ({Type}), keeping original path", 
                            driveLetter, driveInfo.DriveType);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not get drive info for {Drive}", driveLetter);
                }
            }

            return path;
        }

        /// <summary>
        /// Gets the UNC path for a mapped network drive using WNetGetConnection
        /// </summary>
        public string? GetUncPath(string driveLetter)
        {
            // Ensure drive letter format is correct (e.g., "Z:")
            if (string.IsNullOrWhiteSpace(driveLetter))
            {
                return null;
            }

            driveLetter = driveLetter.TrimEnd(':') + ":";

            try
            {
                const int maxPathLength = 260;
                var sb = new StringBuilder(maxPathLength);
                int size = sb.Capacity;

                int error = WNetGetConnection(driveLetter, sb, ref size);

                if (error == 0)
                {
                    var uncPath = sb.ToString();
                    _logger.LogInformation("Resolved drive {Drive} to UNC path: {UNC}", driveLetter, uncPath);
                    return uncPath;
                }
                else if (error == 2250) // ERROR_NOT_CONNECTED
                {
                    _logger.LogDebug("Drive {Drive} is not a network drive", driveLetter);
                    return null;
                }
                else
                {
                    _logger.LogDebug("WNetGetConnection returned error {Error} for drive {Drive}", error, driveLetter);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to get UNC path for drive {Drive}", driveLetter);
                return null;
            }
        }
    }
}
