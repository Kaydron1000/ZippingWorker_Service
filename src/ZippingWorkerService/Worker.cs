using ZippingWorkerService.Configuration;
using ZippingWorkerService.Services;
using ZippingWorkerService.Zipping;

namespace ZippingWorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IZipRequestQueue _zipQueue;
        private readonly IArchiverFactory _archiverFactory;
        private readonly IZipValidationService _validationService;
        private readonly IMetricsService _metrics;
        private readonly ZippingWorkerServiceConfigurationType _config;

        public Worker(
            ILogger<Worker> logger,
            IZipRequestQueue zipQueue,
            IArchiverFactory archiverFactory,
            IZipValidationService validationService,
            IMetricsService metrics,
            ZippingWorkerServiceConfigurationType config)
        {
            _logger = logger;
            _zipQueue = zipQueue;
            _archiverFactory = archiverFactory;
            _validationService = validationService;
            _metrics = metrics;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Zipping Worker Service started. Waiting for zip requests...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var request = await _zipQueue.DequeueAsync(stoppingToken);
                    await ProcessZipRequestAsync(request, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing zip request");
                }
            }

            _logger.LogInformation("Zipping Worker Service stopped.");
        }

        private async Task ProcessZipRequestAsync(ZipRequest request, CancellationToken stoppingToken)
        {
            var startTime = DateTime.UtcNow;
            _metrics.RecordZipRequestStarted();

            _logger.LogInformation("Processing zip request for output: {OutputPath}", request.OutputArchivePath);

            // Calculate hashes for files that don't have them (if validation is enabled)
            if (request.ValidateZipping)
            {
                _logger.LogInformation("Pre-calculating hashes for validation...");
                var files = request.Files.Where(f => string.IsNullOrWhiteSpace(f.Hash)).ToList();

                await Task.WhenAll(files.Select(async file =>
                {
                    if (File.Exists(file.SourcePath))
                    {
                        file.Hash = await _validationService.ComputeFileHashAsync(file.SourcePath, stoppingToken);
                        _logger.LogDebug("Calculated hash for {SourcePath}: {Hash}", file.SourcePath, file.Hash);
                    }
                    else
                    {
                        _logger.LogWarning("Cannot calculate hash: Source file not found: {SourcePath}", file.SourcePath);
                    }
                }));
            }

            // Use staging directory if validation is enabled
            string stagingPath = request.OutputArchivePath;
            bool useStaging = request.ValidateZipping && !string.IsNullOrWhiteSpace(_config.ResolvedTempDir_ZipStaging);

            if (useStaging)
            {
                Directory.CreateDirectory(_config.ResolvedTempDir_ZipStaging);
                string stagingFileName = $"{Path.GetFileNameWithoutExtension(request.OutputArchivePath)}_{Guid.NewGuid():N}{Path.GetExtension(request.OutputArchivePath)}";
                stagingPath = Path.Combine(_config.ResolvedTempDir_ZipStaging, stagingFileName);
                _logger.LogInformation("Using staging location: {StagingPath}", stagingPath);
            }

            var archiver = _archiverFactory.CreateArchiver();

            // Convert to the format expected by IArchiver
            var fileList = request.Files.Select(f => (f.SourcePath, f.ArchivePath)).ToList();

            await archiver.CreateArchiveAsync(
                fileList,
                stagingPath,
                request.CompressionLevel,
                onProgress: (current, total, path) =>
                {
                    _logger.LogInformation("Progress: {Current}/{Total} - {Path}", current, total, path);
                },
                onLog: (message) =>
                {
                    _logger.LogInformation("Archiver: {Message}", message);
                },
                onError: (exception) =>
                {
                    _logger.LogError(exception, "Archiver error");
                });

            _logger.LogInformation("Completed zip creation at: {Path}", stagingPath);

            if (File.Exists(stagingPath))
            {
                var fileInfo = new FileInfo(stagingPath);
                _logger.LogInformation("Zip file created: {Size} bytes", fileInfo.Length);

                // Validate if requested
                if (request.ValidateZipping)
                {
                    var validationStartTime = DateTime.UtcNow;
                    _logger.LogInformation("Starting zip validation...");
                    var validationResult = await _validationService.ValidateZipAsync(
                        stagingPath,
                        request.Files,
                        stoppingToken);

                    var validationDuration = (DateTime.UtcNow - validationStartTime).TotalSeconds;
                    _metrics.RecordZipValidation(validationResult.IsValid, validationDuration);

                    if (validationResult.IsValid)
                    {
                        _logger.LogInformation("Zip validation PASSED. {Count} files validated successfully.", 
                            validationResult.ValidatedFiles.Count);

                        // Copy from staging to final location with hash verification
                        if (useStaging)
                        {
                            _logger.LogInformation("Computing hash of validated zip package...");
                            string stagingHash = await _validationService.ComputeFileHashAsync(stagingPath, stoppingToken);
                            _logger.LogInformation("Staging zip hash: {Hash}", stagingHash);

                            _logger.LogInformation("Copying validated zip from staging to final location: {OutputPath}", request.OutputArchivePath);
                            Directory.CreateDirectory(Path.GetDirectoryName(request.OutputArchivePath)!);

                            // Check if file exists and find available filename
                            string finalOutputPath = request.OutputArchivePath;
                            if (File.Exists(finalOutputPath))
                            {
                                _logger.LogWarning("File already exists at destination: {OutputPath}", finalOutputPath);
                                finalOutputPath = GetNextAvailableFilename(request.OutputArchivePath);
                                _logger.LogInformation("Using alternate filename: {AlternateFilename}", finalOutputPath);
                            }

                            // Copy the file
                            File.Copy(stagingPath, finalOutputPath, overwrite: false);
                            _logger.LogInformation("Zip file copied to: {OutputPath}", finalOutputPath);

                            // Verify the copy
                            _logger.LogInformation("Verifying copied file integrity...");
                            string copiedHash = await _validationService.ComputeFileHashAsync(finalOutputPath, stoppingToken);
                            _logger.LogInformation("Copied zip hash: {Hash}", copiedHash);

                            if (stagingHash.Equals(copiedHash, StringComparison.OrdinalIgnoreCase))
                            {
                                _metrics.RecordCopyVerification(true);
                                _logger.LogInformation("Copy verification PASSED - hashes match. Deleting staging file.");
                                File.Delete(stagingPath);
                                _logger.LogInformation("Staging file deleted: {StagingPath}", stagingPath);

                                // Delete input files if requested
                                if (request.DeleteInputFiles)
                                {
                                    await DeleteInputFilesAsync(request.Files, stoppingToken);
                                }

                                // Record successful completion
                                var duration = (DateTime.UtcNow - startTime).TotalSeconds;
                                var zipSize = new FileInfo(finalOutputPath).Length;
                                _metrics.RecordZipRequestCompleted(true, duration, zipSize, request.Files.Count);
                            }
                            else
                            {
                                _metrics.RecordCopyVerification(false);
                                _logger.LogError("Copy verification FAILED - hash mismatch! Staging: {StagingHash}, Copied: {CopiedHash}", stagingHash, copiedHash);
                                _logger.LogWarning("Keeping staging file for investigation: {StagingPath}", stagingPath);
                                // Delete the bad copy
                                if (File.Exists(finalOutputPath))
                                {
                                    File.Delete(finalOutputPath);
                                    _logger.LogWarning("Deleted corrupted copy from destination.");
                                }

                                // Record failed completion
                                var duration = (DateTime.UtcNow - startTime).TotalSeconds;
                                _metrics.RecordZipRequestCompleted(false, duration, 0, request.Files.Count);
                            }
                        }
                        else
                        {
                            // No staging used, zip already at final location and validated
                            // Delete input files if requested
                            if (request.DeleteInputFiles)
                            {
                                await DeleteInputFilesAsync(request.Files, stoppingToken);
                            }

                            // Record successful completion
                            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
                            var zipSize = new FileInfo(request.OutputArchivePath).Length;
                            _metrics.RecordZipRequestCompleted(true, duration, zipSize, request.Files.Count);
                        }
                    }
                    else
                    {
                        _logger.LogError("Zip validation FAILED with {ErrorCount} errors:", validationResult.Errors.Count);
                        foreach (var error in validationResult.Errors)
                        {
                            _logger.LogError("  - {Error}", error);
                        }

                        // Record failed completion
                        var duration = (DateTime.UtcNow - startTime).TotalSeconds;
                        _metrics.RecordZipRequestCompleted(false, duration, 0, request.Files.Count);

                        // Clean up failed staging file
                        if (useStaging && File.Exists(stagingPath))
                        {
                            _logger.LogWarning("Deleting failed zip from staging: {StagingPath}", stagingPath);
                            File.Delete(stagingPath);
                        }
                    }

                    if (validationResult.Warnings.Count > 0)
                    {
                        _logger.LogWarning("Validation warnings:");
                        foreach (var warning in validationResult.Warnings)
                        {
                            _logger.LogWarning("  - {Warning}", warning);
                        }
                    }
                }
                else if (useStaging)
                {
                    // No validation, but still copy from staging with hash verification
                    _logger.LogInformation("Computing hash of zip package...");
                    string stagingHash = await _validationService.ComputeFileHashAsync(stagingPath, stoppingToken);
                    _logger.LogInformation("Staging zip hash: {Hash}", stagingHash);

                    _logger.LogInformation("Copying zip from staging to final location: {OutputPath}", request.OutputArchivePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(request.OutputArchivePath)!);

                    string finalOutputPath = request.OutputArchivePath;
                    if (File.Exists(finalOutputPath))
                    {
                        _logger.LogWarning("File already exists at destination: {OutputPath}", finalOutputPath);
                        finalOutputPath = GetNextAvailableFilename(request.OutputArchivePath);
                        _logger.LogInformation("Using alternate filename: {AlternateFilename}", finalOutputPath);
                    }

                    File.Copy(stagingPath, finalOutputPath, overwrite: false);

                    // Verify the copy
                    _logger.LogInformation("Verifying copied file integrity...");
                    string copiedHash = await _validationService.ComputeFileHashAsync(finalOutputPath, stoppingToken);
                    _logger.LogInformation("Copied zip hash: {Hash}", copiedHash);

                    if (stagingHash.Equals(copiedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        _metrics.RecordCopyVerification(true);
                        _logger.LogInformation("Copy verification PASSED - hashes match. Deleting staging file.");
                        File.Delete(stagingPath);

                        // Delete input files if requested
                        if (request.DeleteInputFiles)
                        {
                            await DeleteInputFilesAsync(request.Files, stoppingToken);
                        }

                        // Record successful completion
                        var duration = (DateTime.UtcNow - startTime).TotalSeconds;
                        var zipSize = new FileInfo(finalOutputPath).Length;
                        _metrics.RecordZipRequestCompleted(true, duration, zipSize, request.Files.Count);
                    }
                    else
                    {
                        _metrics.RecordCopyVerification(false);
                        _logger.LogError("Copy verification FAILED - hash mismatch!");
                        _logger.LogWarning("Keeping staging file for investigation: {StagingPath}", stagingPath);
                        if (File.Exists(finalOutputPath))
                        {
                            File.Delete(finalOutputPath);
                        }

                        // Record failed completion
                        var duration = (DateTime.UtcNow - startTime).TotalSeconds;
                        _metrics.RecordZipRequestCompleted(false, duration, 0, request.Files.Count);
                    }
                }
            }
            else
            {
                _logger.LogError("Zip file was not created at: {Path}", stagingPath);
                var duration = (DateTime.UtcNow - startTime).TotalSeconds;
                _metrics.RecordZipRequestCompleted(false, duration, 0, request.Files.Count);
            }
        }

        private static ArchiveCompressionLevel MapCompressionLevel(Configuration.CompressionLevelEnumType level)
        {
            return level switch
            {
                Configuration.CompressionLevelEnumType.ultra => ArchiveCompressionLevel.ultra,
                Configuration.CompressionLevelEnumType.maximum => ArchiveCompressionLevel.maximum,
                Configuration.CompressionLevelEnumType.normal => ArchiveCompressionLevel.normal,
                Configuration.CompressionLevelEnumType.fast => ArchiveCompressionLevel.fast,
                Configuration.CompressionLevelEnumType.fastest => ArchiveCompressionLevel.fastest,
                Configuration.CompressionLevelEnumType.nocompression => ArchiveCompressionLevel.nocompression,
                _ => ArchiveCompressionLevel.ultra
            };
        }

        private static string GetNextAvailableFilename(string originalPath)
        {
            string directory = Path.GetDirectoryName(originalPath) ?? string.Empty;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalPath);
            string extension = Path.GetExtension(originalPath);

            for (int i = 1; i <= 999; i++)
            {
                string candidatePath = Path.Combine(directory, $"{fileNameWithoutExtension}_New{i}{extension}");
                if (!File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            // Fallback if all _New1 through _New999 exist (unlikely scenario)
            return Path.Combine(directory, $"{fileNameWithoutExtension}_New{Guid.NewGuid():N}{extension}");
        }

        private async Task DeleteInputFilesAsync(List<ZipFileEntry> files, CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting deletion of {Count} input files...", files.Count);

            int deletedCount = 0;
            int failedCount = 0;
            var deletedFiles = new List<string>();
            var failedFiles = new List<(string Path, string Error)>();

            foreach (var file in files)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Input file deletion cancelled by user.");
                    break;
                }

                try
                {
                    if (File.Exists(file.SourcePath))
                    {
                        File.Delete(file.SourcePath);
                        deletedFiles.Add(file.SourcePath);
                        deletedCount++;
                        _logger.LogDebug("Deleted input file: {SourcePath}", file.SourcePath);
                    }
                    else
                    {
                        _logger.LogWarning("Input file not found for deletion: {SourcePath}", file.SourcePath);
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    failedFiles.Add((file.SourcePath, ex.Message));
                    _logger.LogError(ex, "Failed to delete input file: {SourcePath}", file.SourcePath);
                }

                // Small delay to avoid overwhelming the file system
                if (deletedCount % 100 == 0)
                {
                    await Task.Delay(10, stoppingToken);
                }
            }

            _logger.LogInformation("Input file deletion completed: {DeletedCount} deleted, {FailedCount} failed", 
                deletedCount, failedCount);

            _metrics.RecordFileDeletion(deletedCount, failedCount);

            if (failedCount > 0)
            {
                _logger.LogWarning("Failed to delete {FailedCount} files:", failedCount);
                foreach (var (path, error) in failedFiles.Take(10))
                {
                    _logger.LogWarning("  - {Path}: {Error}", path, error);
                }
                if (failedFiles.Count > 10)
                {
                    _logger.LogWarning("  ... and {More} more files", failedFiles.Count - 10);
                }
            }
        }
    }
}
