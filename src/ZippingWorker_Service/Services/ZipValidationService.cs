using System.IO.Compression;
using System.Security.Cryptography;
using System.Diagnostics;
using ZippingWorker_Service.Configuration;

namespace ZippingWorker_Service.Services
{
    public interface IZipValidationService
    {
        Task<ValidationResult> ValidateZipAsync(string zipPath, List<ZipFileEntry> expectedFiles, CancellationToken cancellationToken = default);
        Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken = default);
    }

    public class ZipValidationService : IZipValidationService
    {
        private readonly ILogger<ZipValidationService> _logger;
        private readonly ZippingWorker_ServiceConfigurationType _config;

        public ZipValidationService(ILogger<ZipValidationService> logger, ZippingWorker_ServiceConfigurationType config)
        {
            _logger = logger;
            _config = config;
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

                // Check if file is empty or too small to be a valid archive
                var fileInfo = new FileInfo(zipPath);
                if (fileInfo.Length == 0)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Archive file is empty (0 bytes): {zipPath}");
                    _logger.LogError("Archive file is empty: {ZipPath}", zipPath);
                    return result;
                }

                // Minimum size check - 22 bytes for ZIP, 32 bytes for 7z
                var extension = Path.GetExtension(zipPath).ToLowerInvariant();
                int minSize = extension == ".7z" ? 32 : 22;

                if (fileInfo.Length < minSize)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Archive file is too small to be valid ({fileInfo.Length} bytes): {zipPath}");
                    _logger.LogError("Archive file too small: {ZipPath} ({Size} bytes, minimum {MinSize})", zipPath, fileInfo.Length, minSize);
                    return result;
                }

                // Only pre-validate ZIP files (not 7z files which require 7-Zip library)
                if (extension == ".zip")
                {
                    try
                    {
                        using (var zipArchive = ZipFile.OpenRead(zipPath))
                        {
                            _logger.LogDebug("Zip file opened successfully, contains {EntryCount} entries", zipArchive.Entries.Count);
                        }
                    }
                    catch (InvalidDataException ex)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Zip file is corrupted or incomplete: {ex.Message}");
                        _logger.LogError(ex, "Zip file is corrupted: {ZipPath}. This usually means the file was not completely written or was damaged during creation.", zipPath);
                        return result;
                    }
                }
                else if (extension == ".7z")
                {
                    _logger.LogDebug("7z file detected, skipping pre-validation (7z format requires extraction for validation)");
                }
                else
                {
                    _logger.LogWarning("Unknown archive format: {Extension}. Proceeding with extraction-based validation.", extension);
                }

                // Create temp directory for extraction
                tempExtractPath = Path.Combine(Path.GetDirectoryName(zipPath), $"zipvalidation_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempExtractPath);

                _logger.LogInformation("Extracting archive to temp location for validation: {TempPath}", tempExtractPath);

                // Extract based on file type
                if (extension == ".7z")
                {
                    await Extract7zArchiveAsync(zipPath, tempExtractPath, cancellationToken);
                }
                else // .zip or other
                {
                    ZipFile.ExtractToDirectory(zipPath, tempExtractPath);
                }

                // Parent directory internal zip file (same as zip file name)
                string parentFolder = Path.GetFileNameWithoutExtension(zipPath);

                // Validate each file
                foreach (var expectedFile in expectedFiles)
                {
                    var inernalFilePath = Path.Combine(parentFolder, expectedFile.ArchivePath);
                    var extractedFilePath = Path.Combine(tempExtractPath, inernalFilePath);
                    if (!File.Exists(extractedFilePath))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"File not found in archive: {inernalFilePath}");
                        _logger.LogError("Validation failed: File not extracted: {ArchivePath}", inernalFilePath);
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
                            result.Warnings.Add($"Cannot validate hash for {inernalFilePath}: Original file not found");
                            _logger.LogWarning("Cannot calculate hash: Source file not found: {SourcePath}", expectedFile.SourcePath);
                            continue;
                        }
                    }

                    var extractedHash = await ComputeFileHashAsync(extractedFilePath, cancellationToken);

                    if (!string.Equals(expectedHash, extractedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Hash mismatch for {inernalFilePath}: Expected {expectedHash}, Got {extractedHash}");
                        _logger.LogError("Hash validation failed for {ArchivePath}: Expected {Expected}, Got {Actual}",
                            inernalFilePath, expectedHash, extractedHash);
                    }
                    else
                    {
                        result.ValidatedFiles.Add(inernalFilePath);
                        _logger.LogDebug("Hash validated for {ArchivePath}: {Hash}", inernalFilePath, extractedHash);
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

        private async Task Extract7zArchiveAsync(string archivePath, string extractPath, CancellationToken cancellationToken)
        {
            var sevenZipExePath = _config.ResolvedSevenZipExePath;

            // 7z x "archive.7z" -o"outputdir" -y
            // x = extract with full paths
            // -o = output directory (no space between -o and path)
            // -y = answer yes to all prompts
            var arguments = $"x \"{archivePath}\" -o\"{extractPath}\" -y";

            var psi = new ProcessStartInfo
            {
                FileName = sevenZipExePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _logger.LogDebug("Extracting 7z archive with command: {FileName} {Arguments}", sevenZipExePath, arguments);

            using var process = Process.Start(psi);
            if (process == null)
            {
                throw new Exception("Failed to start 7-Zip process");
            }

            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                _logger.LogError("7-Zip extraction failed with exit code {ExitCode}. StdErr: {StdErr}", process.ExitCode, stderr);
                throw new Exception($"7-Zip extraction failed with exit code {process.ExitCode}: {stderr}");
            }

            _logger.LogDebug("7z extraction completed successfully");
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
