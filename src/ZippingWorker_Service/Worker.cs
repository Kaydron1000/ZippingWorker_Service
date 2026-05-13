using System.Diagnostics;
using ZippingWorker_Service.Configuration;
using ZippingWorker_Service.Services;
using ZippingWorker_Service.Zipping;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ZippingWorker_Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IZipRequestQueue _zipQueue;
        private readonly IArchiverFactory _archiverFactory;
        private readonly IZipValidationService _validationService;
        private readonly IMetricsService _metrics;
        private readonly ZippingWorker_ServiceConfigurationType _config;

        public Worker(
            ILogger<Worker> logger,
            IZipRequestQueue zipQueue,
            IArchiverFactory archiverFactory,
            IZipValidationService validationService,
            IMetricsService metrics,
            ZippingWorker_ServiceConfigurationType config)
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

            try
            {
                // Use configuration snapshot from the request
                var config = request.Configuration;

                _logger.LogInformation("Processing zip request for output: {OutputPath}", request.OutputArchivePath);

            // Calculate hashes for files that don't have them (if validation is enabled)
            if (request.ValidateZipping)
            {
                _logger.LogInformation("Pre-calculating hashes for validation...");
                var files = request.Files.Where(f => string.IsNullOrWhiteSpace(f.Hash)).ToList();

                await Task.WhenAll(files.Select(async file =>
                {
                    if (Directory.Exists(file.SourcePath))
                        _logger.LogWarning("Source path is not a file but a directory. List all full file paths");
                    else if (File.Exists(file.SourcePath))
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

            // Getting final zip path and ensure it has the correct extension based on the archiver type
            FilePathManger filePathManger = new FilePathManger(config, _logger, request.OutputArchivePath);

            _logger.LogInformation("Using file staging location for zipping: {StageDirectory}", filePathManger.StageFilesDirectory);

            //_logger.LogInformation("Zipping to location: {ZipPath}", zipPath);
            // stage yes no
            // validate yes no
            // validate move yes no
            string zipPath = filePathManger.ArchiveFilePath;
            if (config.usestaging)
                zipPath = filePathManger.StageZipPath;

            var archiver = _archiverFactory.CreateArchiver();

            // Convert to the format expected by IArchiver
            var fileList = request.Files.Select(f => (f.SourcePath, f.ArchivePath)).ToList();

            // Track archiver errors
            Exception? archiverException = null;

            await archiver.CreateArchiveAsync(
                fileList,
                filePathManger.StageFilesDirectory,
                zipPath,
                request.CompressionLevel,
                onProgress: (current, total, path, logType) =>
                {
                    _metrics.UpdateZipProgress(current, total);

                    // Log differently based on operation type
                    switch (logType)
                    {
                        case "LinkAdd":
                            _logger.LogInformation("Symlink Created: {Current}/{Total} ({Percent:F1}%) - {Path}", 
                                current, total, (double)current / total * 100.0, path);
                            break;
                        case "LinkInfo":
                            _logger.LogDebug("Symlink Info: {Path}", path);
                            break;
                        case "ZipAdd":
                            _logger.LogInformation("Compressing: {Current}/{Total} ({Percent:F1}%) - {Path}", 
                                current, total, (double)current / total * 100.0, path);
                            break;
                        case "ZipInfo":
                            _logger.LogDebug("7z Info: {Path}", path);
                            break;
                        default:
                            _logger.LogInformation("Progress: {Current}/{Total} ({Percent:F1}%) - {Path} [{Type}]", 
                                current, total, (double)current / total * 100.0, path, logType);
                            break;
                    }
                },
                onLog: (message) =>
                {
                    _logger.LogInformation("Archiver: {Message}", message);
                },
                onError: (exception) =>
                {
                    archiverException = exception;
                    _logger.LogError(exception, "Archiver error occurred during archive creation");
                });

            // Check if archiver failed
            if (archiverException != null)
            {
                _logger.LogError("Archive creation failed due to archiver error");
                throw new Exception("Archive creation failed", archiverException);
            }

            _logger.LogInformation("Completed zip creation at: {Path}", zipPath);

            if (File.Exists(zipPath))
            {
                // Ensure file is not locked and is ready for reading
                bool fileReady = await WaitForFileReady(zipPath, _logger, stoppingToken);
                if (!fileReady)
                    throw new Exception("Zip file is not ready or invalid");

                // Validate if requested
                if (request.ValidateZipping)
                {
                    // Ensure file is not locked and is ready for reading
                    fileReady = await WaitForFileReady(zipPath, _logger, stoppingToken);
                    if (!fileReady)
                        throw new Exception("Zip file is not ready or invalid");

                    var validationStartTime = DateTime.UtcNow;
                    _logger.LogInformation("Starting zip validation...");
                    var validationResult = await _validationService.ValidateZipAsync(
                        zipPath,
                        request.Files,
                        stoppingToken);

                    var validationDuration = (DateTime.UtcNow - validationStartTime).TotalSeconds;
                    _metrics.RecordZipValidation(validationResult.IsValid, validationDuration);

                    if (validationResult.IsValid)
                    {
                        _logger.LogInformation("Zip validation PASSED. {Count} files validated successfully.", 
                            validationResult.ValidatedFiles.Count);
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

                await RenameZippedItem(sevenZipExePath: config.sevenzipexepath, zipFilePath: zipPath,
                                       origfldFileName: filePathManger.StageFilesFolder, newfldFileName: filePathManger.RenameTopInternalZipFolderName,
                                       onLog: (message) =>
                                       {
                                           _logger.LogInformation("Archiver Name Change: {Message}", message);
                                       },
                                       onError: (exception) =>
                                       {
                                           archiverException = exception;
                                           _logger.LogError(exception, "Archiver error occurred during archive name change");
                                       });

                // copy staged zip to final location if staging is enabled and validate copy, otherwise it's already at final location
                // If staging is not used, the zip is already at the final location and can skip the copy and verification steps
                if (config.usestaging)
                {
                    // Copy from staging to final location with hash verification
                    _logger.LogInformation("Computing hash of zip package...");
                    string stagingHash = await _validationService.ComputeFileHashAsync(zipPath, stoppingToken);
                    _logger.LogInformation("Staging zip hash: {Hash}", stagingHash);

                    _logger.LogInformation("Copying zip from staging to final location: {OutputPath}", filePathManger.ArchiveFilePath);
                    Directory.CreateDirectory(filePathManger.ArchiveDirectory);

                    // Copy the file
                    File.Copy(zipPath, filePathManger.ArchiveFilePath, overwrite: false);
                    _logger.LogInformation("Zip file copied to: {OutputPath}", filePathManger.ArchiveFilePath);

                    // Verify the copy
                    _logger.LogInformation("Verifying copied file integrity...");
                    string copiedHash = await _validationService.ComputeFileHashAsync(filePathManger.ArchiveFilePath, stoppingToken);
                    _logger.LogInformation("Copied zip hash: {Hash}", copiedHash);

                    if (stagingHash.Equals(copiedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        _metrics.RecordCopyVerification(true);
                        _logger.LogInformation("Copy verification PASSED - hashes match. Deleting staging file.");
                        File.Delete(zipPath);
                        _logger.LogInformation("Staging file deleted: {StagingPath}", zipPath);

                        // Delete input files if requested
                        if (request.DeleteInputFiles)
                        {
                            await DeleteInputFilesAsync(request.Files, stoppingToken);
                        }

                        // Record successful completion
                        var duration = (DateTime.UtcNow - startTime).TotalSeconds;
                        var zipSize = new FileInfo(filePathManger.ArchiveFilePath).Length;
                        _metrics.RecordZipRequestCompleted(true, duration, zipSize, request.Files.Count);
                    }
                    else
                    {
                        _metrics.RecordCopyVerification(false);
                        _logger.LogError("Copy verification FAILED - hash mismatch! Staging: {StagingHash}, Copied: {CopiedHash}", stagingHash, copiedHash);
                        _logger.LogWarning("Keeping staging file for investigation: {StagingPath}", zipPath);
                        // Delete the bad copy
                        if (File.Exists(filePathManger.ArchiveFilePath))
                        {
                            File.Delete(filePathManger.ArchiveFilePath);
                            _logger.LogWarning("Deleted corrupted copy from destination.");
                        }

                        // Record failed completion
                        var duration = (DateTime.UtcNow - startTime).TotalSeconds;
                        _metrics.RecordZipRequestCompleted(false, duration, 0, request.Files.Count);
                    }
                }
            }
            else
            {
                _logger.LogError("Zip file was not created at: {Path}", zipPath);
                var duration = (DateTime.UtcNow - startTime).TotalSeconds;
                _metrics.RecordZipRequestCompleted(false, duration, 0, request.Files.Count);
            }
            }
            finally
            {
                // Reset progress gauge when request completes (success or failure)
                _metrics.ResetZipProgress();
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

        private static async Task<bool> WaitForFileReady(string filePath, ILogger logger, CancellationToken stoppingToken)
        {
            int retryCount = 0;
            const int maxRetries = 5;
            bool fileReady = false;

            // Wait a moment for file handles to be released and file system to stabilize
            await Task.Delay(500, stoppingToken);

            while (retryCount < maxRetries & !fileReady)
            {
                try
                {
                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        if (stream.Length > 22)
                        {
                            fileReady = true;
                            return fileReady;
                        }
                    }
                }
                catch (IOException)
                {
                    retryCount++;
                    logger.LogWarning("File {FilePath} is not ready yet. Retrying {Retry}/{MaxRetries}...", filePath, retryCount, maxRetries);
                    await Task.Delay(1000, stoppingToken);
                }
            }
            logger.LogError("File {FilePath} was not ready after {MaxRetries} attempts.", filePath, maxRetries);

            return fileReady;
        }

        private static async Task RenameZippedItem(string sevenZipExePath, string zipFilePath, string origfldFileName, string newfldFileName, 
                                                   Action<string>? onLog = null, Action<Exception>? onError = null)
        {
            string arguments = $"rn \"{zipFilePath}\" \"{origfldFileName}\" \"{newfldFileName}\"";
            var psi = new ProcessStartInfo
            {
                FileName = sevenZipExePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                string stdout = await process.StandardOutput.ReadToEndAsync();
                string stderr = await process.StandardError.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit());
                if (process.ExitCode != 0)
                {
                    var ex = new Exception($"7z exited with code {process.ExitCode}:\n{stderr}");
                    onError?.Invoke(ex);
                }
                else
                {
                    onLog?.Invoke("[7z] Archive created successfully.");
                }
            }
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
    public class FilePathManger
    {
        private readonly ZippingWorker_ServiceConfigurationType _config;
        private readonly ILogger<Worker> _logger;
        private readonly string _archiveOutputPath;
        /// <summary>
        /// Gets the directory path where archive zip file will be saved. 
        /// This is derived from the OutputArchivePath provided in the zip request and is used as the final destination for the archive after staging and validation. 
        /// </summary>
        public string ArchiveDirectory { get; private set; } // Archive output directory
        /// <summary>
        /// Gets the name of the archive file without its file extension.
        /// </summary>
        public string ArchiveFileNameWithoutExtension { get; private set; } // Archive file name without extension
        /// <summary>
        /// Gets the archive file name including its extension.
        /// </summary>
        public string ArchiveFileName => ArchiveFileNameWithoutExtension + ArchiveExtension; // Archive file name with extension
        /// <summary>
        /// Gets the file extension used for archive files.
        /// </summary>
        /// <remarks>The extension includes the leading period (for example, ".zip" or ".7z").</remarks>
        public string ArchiveExtension { get; private set; } // Archive file extension (e.g. .zip or .7z)
        /// <summary>
        /// Gets the full path to the final archive file, including the directory and file name.
        /// </summary>
        public string ArchiveFilePath { get; private set; } // Full path to the final archive file (ArchiveDirectory + ArchiveFileName)
        /// <summary>
        /// Gets the root directory used for staging files.
        /// </summary>
        /// <remarks>The directory path is typically loaded from configuration settings. This property is
        /// read-only.</remarks>
        public string StageFilesRootDirectory { get; private set; } // Root directory for staging files (from configuration)
        /// <summary>
        /// Gets the unique folder name used for staging files during processing.
        /// </summary>
        public string StageFilesFolder { get; private set; } // Unique folder name for staging files (e.g. ArchiveFileNameWithoutExtension + GUID)
        /// <summary>
        /// Gets the full path to the staging directory used for temporary file storage.
        /// </summary>
        public string StageFilesDirectory { get; private set; } // Full path to the staging directory (StageFilesRootDirectory + StageFilesFolder)
        /// <summary>
        /// Gets the unique identifier for this zip operation.
        /// </summary>
        /// <remarks>This identifier is used to create unique staging paths for each operation. It can be
        /// used to distinguish between multiple concurrent or sequential zip operations.</remarks>
        /// <summary>
        /// Gets the unique 6-character identifier for this zip operation.
        /// </summary>
        /// <remarks>This short alphanumeric ID (e.g., "A3K9M2") is used to create unique staging paths 
        /// and prevent conflicts between concurrent operations. While not a true GUID, it provides 
        /// sufficient uniqueness (36^6 ≈ 2.2 billion combinations) for temporary file operations.</remarks>
        public string GUID { get; private set; } // Unique identifier for this zip operation, used to create unique staging paths
        /// <summary>
        /// Gets the directory path where the zip file is created during the staging process.
        /// </summary>
        /// <remarks>The directory is typically specified through configuration. This property is
        /// read-only and is set internally by the application.</remarks>
        public string StageZipDirectory { get; private set; } // Directory where the zip file will be created during staging (from configuration)
        /// <summary>
        /// Gets the name of the zip file used during the staging process.
        /// </summary>
        /// <remarks>The staged zip file name typically includes the original archive name, a unique
        /// identifier, and the archive extension to ensure uniqueness during processing.</remarks>
        public string StageZipName { get; private set; } // Name of the zip file during staging (e.g. ArchiveFileNameWithoutExtension + GUID + ArchiveExtension)
        /// <summary>
        /// Gets the full path to the zip file used during the staging process.
        /// </summary>
        public string StageZipPath { get; private set; } // Full path to the zip file during staging (StageZipDirectory + StageZipName)
        /// <summary>
        /// Gets the name to which the top-level folder inside the zip archive will be renamed.
        /// </summary>
        /// <remarks>The returned name matches the archive file name without its extension. This is useful
        /// when extracting or repackaging zip files to ensure consistent folder naming.</remarks>
        public string RenameTopInternalZipFolderName => ArchiveFileNameWithoutExtension; // The name to which the top-level folder inside the zip will be renamed (same as archive file name without extension)

        public FilePathManger(ZippingWorker_ServiceConfigurationType config, ILogger<Worker> logger, string archiveOutputPath)
        {
            _config = config;
            _archiveOutputPath = archiveOutputPath;
            _logger = logger;

            if (config.archiver == ArchiverEnumType.sevenzip)
                ArchiveExtension = ".7z";
            else if (config.archiver == ArchiverEnumType.dotnetzip)
                ArchiveExtension = ".zip";
            else
                logger.LogError("Unsupported archiver type configured: {ArchiverType}", config.archiver);

            ArchiveDirectory = Path.GetDirectoryName(archiveOutputPath);
            if (ArchiveDirectory == null)
            {
                logger.LogError("Failed to determine archive directory from output path: {OutputPath}", archiveOutputPath);
                ArchiveDirectory = string.Empty;
            }

            ArchiveFileNameWithoutExtension = Path.GetFileName(archiveOutputPath);
            if (ArchiveFileNameWithoutExtension.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
                ArchiveFileNameWithoutExtension = ArchiveFileNameWithoutExtension.Substring(0, ArchiveFileNameWithoutExtension.Length - 3);
            else if (ArchiveFileNameWithoutExtension.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                ArchiveFileNameWithoutExtension = ArchiveFileNameWithoutExtension.Substring(0, ArchiveFileNameWithoutExtension.Length - 4);

            ArchiveFilePath = Path.Combine(ArchiveDirectory, ArchiveFileName);
            if (File.Exists(ArchiveFilePath))
            {
                string finalOutputPath = ArchiveFilePath;
                if (File.Exists(finalOutputPath))
                {
                    _logger.LogWarning("File already exists at destination: {OutputPath}", finalOutputPath);
                    finalOutputPath = finalOutputPath.GetNextAvailableFilePath();
                    _logger.LogInformation("Using alternate filename: {AlternateFilename}", finalOutputPath);
                }
                // Update ArchiveFilePath to the new alternate path to avoid conflicts later in the process
                ArchiveFileNameWithoutExtension = Path.GetFileNameWithoutExtension(finalOutputPath);
                ArchiveFilePath = finalOutputPath;
            }

            StageFilesRootDirectory = _config.ResolvedTempDir_SymLink;
            StageZipDirectory = _config.ResolvedTempDir_ZipStaging;

            GUID = Extensions.GenerateShortId();
            StageZipName = ArchiveFileNameWithoutExtension + "_" + GUID + ArchiveExtension;
            StageZipPath = Path.Combine(StageZipDirectory, StageZipName);
            StageFilesFolder = ArchiveFileNameWithoutExtension + "_" + GUID;
            StageFilesDirectory = Path.Combine(StageFilesRootDirectory, StageFilesFolder);
        }
    }

}
