using System.IO.Compression;
using System.Security.Cryptography;

namespace ZippingWorkerService.Services
{
    public interface IZipValidationService
    {
        Task<ValidationResult> ValidateZipAsync(string zipPath, List<ZipFileEntry> expectedFiles, CancellationToken cancellationToken = default);
        Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken = default);
    }

    public class ZipValidationService : IZipValidationService
    {
        private readonly ILogger<ZipValidationService> _logger;

        public ZipValidationService(ILogger<ZipValidationService> logger)
        {
            _logger = logger;
        }

        public async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken = default)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        public async Task<ValidationResult> ValidateZipAsync(string zipPath, List<ZipFileEntry> expectedFiles, CancellationToken cancellationToken = default)
        {
            var result = new ValidationResult { IsValid = true };
            string? tempExtractPath = null;

            try
            {
                if (!File.Exists(zipPath))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Zip file not found: {zipPath}");
                    return result;
                }

                // Create temp directory for extraction
                tempExtractPath = Path.Combine(Path.GetTempPath(), $"zipvalidation_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempExtractPath);

                _logger.LogInformation("Extracting zip to temp location for validation: {TempPath}", tempExtractPath);

                // Extract zip
                ZipFile.ExtractToDirectory(zipPath, tempExtractPath);

                // Validate each file
                foreach (var expectedFile in expectedFiles)
                {
                    var extractedFilePath = Path.Combine(tempExtractPath, expectedFile.ArchivePath);

                    if (!File.Exists(extractedFilePath))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"File not found in archive: {expectedFile.ArchivePath}");
                        _logger.LogError("Validation failed: File not extracted: {ArchivePath}", expectedFile.ArchivePath);
                        continue;
                    }

                    // Validate hash
                    var expectedHash = expectedFile.Hash;
                    if (string.IsNullOrWhiteSpace(expectedHash))
                    {
                        // Calculate hash from original file
                        if (File.Exists(expectedFile.SourcePath))
                        {
                            expectedHash = await ComputeFileHashAsync(expectedFile.SourcePath, cancellationToken);
                            _logger.LogDebug("Calculated hash for {SourcePath}: {Hash}", expectedFile.SourcePath, expectedHash);
                        }
                        else
                        {
                            result.Warnings.Add($"Cannot validate hash for {expectedFile.ArchivePath}: Original file not found");
                            _logger.LogWarning("Cannot calculate hash: Source file not found: {SourcePath}", expectedFile.SourcePath);
                            continue;
                        }
                    }

                    var extractedHash = await ComputeFileHashAsync(extractedFilePath, cancellationToken);

                    if (!string.Equals(expectedHash, extractedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Hash mismatch for {expectedFile.ArchivePath}: Expected {expectedHash}, Got {extractedHash}");
                        _logger.LogError("Hash validation failed for {ArchivePath}: Expected {Expected}, Got {Actual}",
                            expectedFile.ArchivePath, expectedHash, extractedHash);
                    }
                    else
                    {
                        result.ValidatedFiles.Add(expectedFile.ArchivePath);
                        _logger.LogDebug("Hash validated for {ArchivePath}: {Hash}", expectedFile.ArchivePath, extractedHash);
                    }
                }

                if (result.IsValid)
                {
                    _logger.LogInformation("Zip validation completed successfully. {Count} files validated.", result.ValidatedFiles.Count);
                }
                else
                {
                    _logger.LogError("Zip validation failed with {ErrorCount} errors", result.Errors.Count);
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Validation exception: {ex.Message}");
                _logger.LogError(ex, "Exception during zip validation");
            }
            finally
            {
                // Cleanup temp directory
                if (tempExtractPath != null && Directory.Exists(tempExtractPath))
                {
                    try
                    {
                        Directory.Delete(tempExtractPath, recursive: true);
                        _logger.LogDebug("Cleaned up temp validation directory: {TempPath}", tempExtractPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cleanup temp directory: {TempPath}", tempExtractPath);
                    }
                }
            }

            return result;
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = [];
        public List<string> Warnings { get; set; } = [];
        public List<string> ValidatedFiles { get; set; } = [];
    }
}
