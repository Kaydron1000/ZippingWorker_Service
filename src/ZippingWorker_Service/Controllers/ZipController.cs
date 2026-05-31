using Microsoft.AspNetCore.Mvc;
using ZippingWorker_Service.Services;
using ZippingWorker_Service.Configuration;

namespace ZippingWorker_Service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ZipController : ControllerBase
    {
        private readonly IZipRequestQueue _zipQueue;
        private readonly ILogger<ZipController> _logger;
        private readonly ZippingWorker_ServiceConfigurationType _config;

        public ZipController(
            IZipRequestQueue zipQueue, 
            ILogger<ZipController> logger,
            ZippingWorker_ServiceConfigurationType config)
        {
            _zipQueue = zipQueue;
            _logger = logger;
            _config = config;
        }

        [HttpPost]
        public async Task<IActionResult> CreateZip([FromBody] ZipRequestDto request)
        {
            if (request.Files == null || request.Files.Count == 0)
            {
                return BadRequest("No files specified");
            }

            if (string.IsNullOrWhiteSpace(request.OutputArchivePath))
            {
                return BadRequest("Output archive path is required");
            }

            // Early validation: Check output directory accessibility (if enabled)
            if (_config.validateonrequest_directorycheck)
            {
                var outputDirectory = Path.GetDirectoryName(request.OutputArchivePath);
                if (!string.IsNullOrEmpty(outputDirectory))
                {
                    try
                    {
                        if (!Directory.Exists(outputDirectory))
                        {
                            _logger.LogWarning("Output directory does not exist, attempting to create: {OutputDir}", outputDirectory);
                            Directory.CreateDirectory(outputDirectory);
                        }

                        // Test write access by attempting to create a temp file
                        var testFile = Path.Combine(outputDirectory, $".access_test_{Guid.NewGuid():N}.tmp");
                        try
                        {
                            System.IO.File.WriteAllText(testFile, "test");
                            System.IO.File.Delete(testFile);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            return StatusCode(500, new { error = "Access denied", message = $"No write permission for output directory: {outputDirectory}" });
                        }
                        catch (IOException ex)
                        {
                            return StatusCode(500, new { error = "Output directory not accessible", message = $"Cannot write to output directory: {outputDirectory}. {ex.Message}" });
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        return StatusCode(500, new { error = "Access denied", message = $"No permission to access output directory: {outputDirectory}" });
                    }
                    catch (Exception ex)
                    {
                        return StatusCode(500, new { error = "Output directory error", message = $"Cannot access output directory: {outputDirectory}. {ex.Message}" });
                    }
                }
            }

            // Early validation: Check source file locations (if enabled)
            if (_config.validateonrequest_directorycheck || _config.validateonrequest_filecheck)
            {
                // Group files by directory to optimize checking (common case: many files in same directory)
                var filesByDirectory = request.Files
                    .GroupBy(f => Path.GetDirectoryName(f.SourcePath) ?? string.Empty)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var inaccessibleFiles = new List<string>();
                var inaccessibleDirectories = new List<string>();

                foreach (var dirGroup in filesByDirectory)
                {
                    var directory = dirGroup.Key;
                    var filesInDir = dirGroup.Value;

                    // Check directory accessibility first (faster than checking each file)
                    bool directoryAccessible = true;
                    if (_config.validateonrequest_directorycheck && !string.IsNullOrEmpty(directory))
                    {
                        try
                        {
                            if (!Directory.Exists(directory))
                            {
                                inaccessibleDirectories.Add(directory);
                                inaccessibleFiles.AddRange(filesInDir.Select(f => f.SourcePath));
                                _logger.LogWarning("Source directory does not exist: {Directory}", directory);
                                directoryAccessible = false;
                                continue;
                            }

                            // Test read access to directory (fast - doesn't read file contents)
                            _ = Directory.GetFiles(directory);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            inaccessibleDirectories.Add(directory);
                            inaccessibleFiles.AddRange(filesInDir.Select(f => f.SourcePath));
                            _logger.LogWarning("No read permission for directory: {Directory}", directory);
                            directoryAccessible = false;
                            continue;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Cannot access directory: {Directory}", directory);
                            // Don't fail directory-level check, still check individual files
                        }
                    }

                    // Only check file existence if enabled and directory is accessible
                    if (_config.validateonrequest_filecheck && directoryAccessible)
                    {
                        var totalFilesInDir = filesInDir.Count;
                        var filesChecked = 0;
                        var maxFiles = _config.filecheck_maxfilestocheckexistence;
                        var sampleRate = _config.filecheck_sampleeverynthfile;

                        for (int i = 0; i < totalFilesInDir; i++)
                        {
                            var fileEntry = filesInDir[i];

                            // Performance optimization: Check first maxFiles, then sample
                            bool shouldCheck = filesChecked < maxFiles ||
                                              (sampleRate > 0 && i % sampleRate == 0);

                            if (shouldCheck)
                            {
                                if (!System.IO.File.Exists(fileEntry.SourcePath))
                                {
                                    inaccessibleFiles.Add(fileEntry.SourcePath);
                                    _logger.LogWarning("Source file does not exist: {FilePath}", fileEntry.SourcePath);
                                }
                                filesChecked++;
                            }
                        }

                        // Log if we skipped some files
                        if (totalFilesInDir > maxFiles)
                        {
                            _logger.LogInformation("Checked {Checked} of {Total} files in {Directory} (sampled after {Max})",
                                filesChecked, totalFilesInDir, directory, maxFiles);
                        }
                    }
                }

                // If there are inaccessible files, return error with details
                if (inaccessibleFiles.Count > 0)
                {
                    var errorDetails = new
                    {
                        error = "Source files not accessible",
                        message = $"{inaccessibleFiles.Count} file(s) cannot be accessed",
                        inaccessibleFiles = inaccessibleFiles.Take(10).ToList(), // Limit to first 10 for response size
                        totalInaccessible = inaccessibleFiles.Count,
                        inaccessibleDirectories = inaccessibleDirectories.Distinct().ToList()
                    };

                    _logger.LogError("Zip request rejected: {Count} inaccessible files", inaccessibleFiles.Count);
                    return StatusCode(500, errorDetails);
                }
            }

            _logger.LogInformation("Pre-flight validation passed. Queuing zip request for: {OutputPath}", request.OutputArchivePath);

            var zipRequest = new ZipRequest
            {
                Files = request.Files.Select(f => new ZipFileEntry
                {
                    SourcePath = f.SourcePath,
                    ArchivePath = f.ArchivePath,
                    Hash = null
                }).ToList(),
                OutputArchivePath = request.OutputArchivePath
            };

            await _zipQueue.EnqueueAsync(zipRequest);

            _logger.LogInformation("Zip request queued for: {OutputPath}", request.OutputArchivePath);

            return Accepted(new { Message = "Zip request queued successfully", OutputPath = request.OutputArchivePath });
        }
    }

    public class ZipRequestDto
    {
        public List<FileEntry> Files { get; set; } = new List<FileEntry>();
        public string OutputArchivePath { get; set; } = string.Empty;
    }

    public class FileEntry
    {
        public string SourcePath { get; set; } = string.Empty;
        public string ArchivePath { get; set; } = string.Empty;
    }
}
