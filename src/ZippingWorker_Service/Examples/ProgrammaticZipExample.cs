using ZippingWorker_Service.Services;

namespace ZippingWorker_Service.Examples
{
    /// <summary>
    /// Example showing how to submit zip requests programmatically from within the application
    /// </summary>
    public class ProgrammaticZipExample
    {
        private readonly IZipRequestQueue _zipQueue;
        private readonly ILogger<ProgrammaticZipExample> _logger;

        public ProgrammaticZipExample(IZipRequestQueue zipQueue, ILogger<ProgrammaticZipExample> logger)
        {
            _zipQueue = zipQueue;
            _logger = logger;
        }

        /// <summary>
        /// Example: Zip a single file
        /// </summary>
        public async Task ZipSingleFileAsync(string sourceFile, string outputZip)
        {
            var request = new ZipRequest
            {
                Files =
                [
                    new ZipFileEntry
                    {
                        SourcePath = sourceFile,
                        ArchivePath = Path.GetFileName(sourceFile),
                        Hash = null // Will be calculated if validation is enabled
                    }
                ],
                OutputArchivePath = outputZip
            };

            await _zipQueue.EnqueueAsync(request);
            _logger.LogInformation("Queued single file zip request: {OutputPath}", outputZip);
        }

        /// <summary>
        /// Example: Zip multiple files
        /// </summary>
        public async Task ZipMultipleFilesAsync(List<string> sourceFiles, string outputZip)
        {
            var request = new ZipRequest
            {
                Files = sourceFiles.Select(f => new ZipFileEntry
                {
                    SourcePath = f,
                    ArchivePath = Path.GetFileName(f),
                    Hash = null
                }).ToList(),
                OutputArchivePath = outputZip
            };

            await _zipQueue.EnqueueAsync(request);
            _logger.LogInformation("Queued multi-file zip request with {Count} files: {OutputPath}", 
                sourceFiles.Count, outputZip);
        }

        /// <summary>
        /// Example: Zip directory contents with custom archive structure
        /// </summary>
        public async Task ZipDirectoryAsync(string sourceDirectory, string outputZip)
        {
            var files = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
            
            var request = new ZipRequest
            {
                Files = files.Select(f =>
                {
                    var relativePath = Path.GetRelativePath(sourceDirectory, f);
                    return new ZipFileEntry
                    {
                        SourcePath = f,
                        ArchivePath = relativePath,
                        Hash = null
                    };
                }).ToList(),
                OutputArchivePath = outputZip
            };

            await _zipQueue.EnqueueAsync(request);
            _logger.LogInformation("Queued directory zip request with {Count} files: {OutputPath}", 
                files.Length, outputZip);
        }

        /// <summary>
        /// Example: Zip files with custom archive paths (folder structure)
        /// </summary>
        public async Task ZipWithCustomStructureAsync(string outputZip)
        {
            var request = new ZipRequest
            {
                Files =
                [
                    new ZipFileEntry { SourcePath = @"C:\logs\app.log", ArchivePath = "logs/application.log", Hash = null },
                    new ZipFileEntry { SourcePath = @"C:\config\settings.json", ArchivePath = "config/settings.json", Hash = null },
                    new ZipFileEntry { SourcePath = @"C:\data\export.csv", ArchivePath = "data/export.csv", Hash = null }
                ],
                OutputArchivePath = outputZip
            };

            await _zipQueue.EnqueueAsync(request);
            _logger.LogInformation("Queued custom structure zip request: {OutputPath}", outputZip);
        }
    }
}
