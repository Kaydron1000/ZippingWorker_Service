using ZippingWorkerService.Services;

namespace ZippingWorkerService.Examples
{
    /// <summary>
    /// Examples demonstrating automatic drive letter resolution
    /// </summary>
    public class DriveLetterResolutionExample
    {
        private readonly IDriveLetterResolver _resolver;
        private readonly ILogger<DriveLetterResolutionExample> _logger;

        public DriveLetterResolutionExample(
            IDriveLetterResolver resolver,
            ILogger<DriveLetterResolutionExample> logger)
        {
            _resolver = resolver;
            _logger = logger;
        }

        /// <summary>
        /// Example 1: Automatic network drive resolution
        /// If Z: is mapped to \\server\share, this automatically resolves it
        /// </summary>
        public void AutomaticNetworkDriveResolution()
        {
            _logger.LogInformation("=== Automatic Network Drive Resolution ===");

            // These will automatically resolve to UNC paths if they're network drives
            var paths = new[]
            {
                @"Z:\projects\archive.zip",
                @"M:\documents\file.txt",
                @"C:\local\data.txt"  // Local drive - stays as-is
            };

            foreach (var path in paths)
            {
                var resolved = _resolver.ResolvePath(path);
                _logger.LogInformation("{Original} => {Resolved}", path, resolved);
            }
        }

        /// <summary>
        /// Example 2: Manual override of automatic resolution
        /// </summary>
        public void ManualOverrideResolution()
        {
            _logger.LogInformation("=== Manual Override Resolution ===");

            // Manual mapping takes precedence over automatic resolution
            var manualMappings = new Dictionary<string, string>
            {
                { "C:", @"\\test-server\testdata" },
                { "Z:", @"\\override-server\share" }
            };

            var paths = new[]
            {
                @"C:\source\file.txt",  // Will use manual mapping
                @"Z:\data\file.txt",    // Will use manual mapping instead of auto-resolution
                @"D:\backup\file.txt"   // No mapping - uses automatic resolution or stays as-is
            };

            foreach (var path in paths)
            {
                var resolved = _resolver.ResolvePath(path, manualMappings);
                _logger.LogInformation("{Original} => {Resolved}", path, resolved);
            }
        }

        /// <summary>
        /// Example 3: Check if a specific drive is a network drive
        /// </summary>
        public void CheckNetworkDrives()
        {
            _logger.LogInformation("=== Network Drive Detection ===");

            var drivesToCheck = new[] { "C:", "Z:", "M:", "X:" };

            foreach (var drive in drivesToCheck)
            {
                var uncPath = _resolver.GetUncPath(drive);

                if (uncPath != null)
                {
                    _logger.LogInformation("{Drive} is a network drive: {UNC}", drive, uncPath);
                }
                else
                {
                    _logger.LogInformation("{Drive} is not a network drive (or not accessible)", drive);
                }
            }
        }

        /// <summary>
        /// Example 4: Real-world scenario - processing files from multiple drives
        /// </summary>
        public async Task ProcessMultiDriveArchiveAsync(IZipRequestQueue queue)
        {
            _logger.LogInformation("=== Multi-Drive Archive Processing ===");

            // Files might be on different drives - some network, some local
            var sourceFiles = new[]
            {
                @"Z:\projects\src\app.cs",        // Network drive (auto-resolved)
                @"M:\libraries\lib.dll",          // Network drive (auto-resolved)
                @"C:\local\config.xml"            // Local drive (stays as-is)
            };

            // Optional: Provide manual overrides for specific scenarios
            var manualOverrides = new Dictionary<string, string>
            {
                // Override Z: for testing environment
                { "Z:", @"\\test-environment\projects" }
            };

            var request = new ZipRequest
            {
                Files = sourceFiles.Select(f => new ZipFileEntry
                {
                    SourcePath = _resolver.ResolvePath(f, manualOverrides),
                    ArchivePath = Path.GetFileName(f),
                    Hash = null
                }).ToList(),
                OutputArchivePath = @"C:\output\multi-drive-archive.zip",
                ValidateZipping = true
            };

            await queue.EnqueueAsync(request);
            _logger.LogInformation("Multi-drive archive request queued");
        }

        /// <summary>
        /// Example usage of all scenarios
        /// </summary>
        public static async Task RunAllExamplesAsync(DriveLetterResolutionExample example, IZipRequestQueue queue)
        {
            example.AutomaticNetworkDriveResolution();
            Console.WriteLine();

            example.ManualOverrideResolution();
            Console.WriteLine();

            example.CheckNetworkDrives();
            Console.WriteLine();

            await example.ProcessMultiDriveArchiveAsync(queue);
        }
    }
}
