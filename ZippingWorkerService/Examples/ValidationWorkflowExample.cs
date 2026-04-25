using System.Security.Cryptography;
using ZippingWorkerService.Services;

namespace ZippingWorkerService.Examples
{
    /// <summary>
    /// Example demonstrating the complete validation workflow
    /// </summary>
    public class ValidationWorkflowExample
    {
        private readonly IZipRequestQueue _zipQueue;
        private readonly IZipValidationService _validationService;
        private readonly ILogger<ValidationWorkflowExample> _logger;

        public ValidationWorkflowExample(
            IZipRequestQueue zipQueue,
            IZipValidationService validationService,
            ILogger<ValidationWorkflowExample> logger)
        {
            _zipQueue = zipQueue;
            _validationService = validationService;
            _logger = logger;
        }

        /// <summary>
        /// Example 1: Zip with automatic hash calculation and validation
        /// </summary>
        public async Task ZipWithAutoValidationAsync(List<string> sourceFiles, string outputZip)
        {
            _logger.LogInformation("Creating zip request with automatic validation...");

            var request = new ZipRequest
            {
                Files = sourceFiles.Select(f => new ZipFileEntry
                {
                    SourcePath = f,
                    ArchivePath = Path.GetFileName(f),
                    Hash = null // Service will calculate automatically
                }).ToList(),
                OutputArchivePath = outputZip,
                ValidateZipping = true // Enable validation
            };

            await _zipQueue.EnqueueAsync(request);
            _logger.LogInformation("Request queued with auto-validation enabled");
        }

        /// <summary>
        /// Example 2: Pre-calculate hashes for better performance with large files
        /// </summary>
        public async Task ZipWithPreCalculatedHashesAsync(List<string> sourceFiles, string outputZip)
        {
            _logger.LogInformation("Pre-calculating hashes for {Count} files...", sourceFiles.Count);

            var filesWithHashes = new List<ZipFileEntry>();

            foreach (var file in sourceFiles)
            {
                string hash;
                if (File.Exists(file))
                {
                    hash = await _validationService.ComputeFileHashAsync(file);
                    _logger.LogDebug("Calculated hash for {File}: {Hash}", file, hash);
                }
                else
                {
                    hash = string.Empty;
                    _logger.LogWarning("File not found: {File}", file);
                }

                filesWithHashes.Add(new ZipFileEntry
                {
                    SourcePath = file,
                    ArchivePath = Path.GetFileName(file),
                    Hash = hash
                });
            }

            var request = new ZipRequest
            {
                Files = filesWithHashes,
                OutputArchivePath = outputZip,
                ValidateZipping = true
            };

            await _zipQueue.EnqueueAsync(request);
            _logger.LogInformation("Request queued with pre-calculated hashes");
        }

        /// <summary>
        /// Example 3: Zip without validation (faster for trusted operations)
        /// </summary>
        public async Task ZipWithoutValidationAsync(List<string> sourceFiles, string outputZip)
        {
            _logger.LogInformation("Creating zip request without validation...");

            var request = new ZipRequest
            {
                Files = sourceFiles.Select(f => new ZipFileEntry
                {
                    SourcePath = f,
                    ArchivePath = Path.GetFileName(f),
                    Hash = null
                }).ToList(),
                OutputArchivePath = outputZip,
                ValidateZipping = false // Disable validation for speed
            };

            await _zipQueue.EnqueueAsync(request);
            _logger.LogInformation("Request queued without validation");
        }

        /// <summary>
        /// Example 4: Complex directory structure with custom paths and validation
        /// </summary>
        public async Task ZipDirectoryWithValidationAsync(string sourceDir, string outputZip)
        {
            var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
            _logger.LogInformation("Found {Count} files in {Directory}", files.Length, sourceDir);

            var zipEntries = new List<ZipFileEntry>();

            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(sourceDir, file);
                var hash = await _validationService.ComputeFileHashAsync(file);

                zipEntries.Add(new ZipFileEntry
                {
                    SourcePath = file,
                    ArchivePath = relativePath.Replace('\\', '/'), // Use forward slashes in zip
                    Hash = hash
                });

                _logger.LogDebug("Added {File} with hash {Hash}", relativePath, hash);
            }

            var request = new ZipRequest
            {
                Files = zipEntries,
                OutputArchivePath = outputZip,
                CompressionLevel = Zipping.ArchiveCompressionLevel.ultra,
                ValidateZipping = true
            };

            await _zipQueue.EnqueueAsync(request);
            _logger.LogInformation("Complex directory zip request queued with validation");
        }

        /// <summary>
        /// Standalone validation of an existing zip file
        /// </summary>
        public async Task<ValidationResult> ValidateExistingZipAsync(string zipPath, List<ZipFileEntry> expectedFiles)
        {
            _logger.LogInformation("Validating existing zip: {ZipPath}", zipPath);

            var result = await _validationService.ValidateZipAsync(zipPath, expectedFiles);

            if (result.IsValid)
            {
                _logger.LogInformation("Validation PASSED: {Count} files validated", result.ValidatedFiles.Count);
            }
            else
            {
                _logger.LogError("Validation FAILED with {ErrorCount} errors", result.Errors.Count);
                foreach (var error in result.Errors)
                {
                    _logger.LogError("  - {Error}", error);
                }
            }

            return result;
        }

        /// <summary>
        /// Example usage demonstrating all scenarios
        /// </summary>
        public static async Task RunExamplesAsync(ValidationWorkflowExample example)
        {
            var sourceFiles = new List<string>
            {
                @"C:\temp\document1.txt",
                @"C:\temp\document2.pdf",
                @"C:\temp\image.jpg"
            };

            // Scenario 1: Auto-validation (simplest)
            await example.ZipWithAutoValidationAsync(
                sourceFiles,
                @"C:\output\auto-validated.zip");

            // Scenario 2: Pre-calculated hashes (faster for large files)
            await example.ZipWithPreCalculatedHashesAsync(
                sourceFiles,
                @"C:\output\precalc-validated.zip");

            // Scenario 3: No validation (fastest)
            await example.ZipWithoutValidationAsync(
                sourceFiles,
                @"C:\output\no-validation.zip");

            // Scenario 4: Full directory with validation
            await example.ZipDirectoryWithValidationAsync(
                @"C:\temp\my-project",
                @"C:\output\project-backup.zip");

            // Scenario 5: Validate existing zip
            var expectedFiles = sourceFiles.Select(f => new ZipFileEntry
            {
                SourcePath = f,
                ArchivePath = Path.GetFileName(f),
                Hash = null
            }).ToList();

            await example.ValidateExistingZipAsync(
                @"C:\output\existing-archive.zip",
                expectedFiles);
        }
    }
}
