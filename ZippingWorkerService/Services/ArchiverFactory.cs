using ZippingWorkerService.Configuration;
using ZippingWorkerService.Zipping;

namespace ZippingWorkerService.Services
{
    public interface IArchiverFactory
    {
        IArchiver CreateArchiver();
    }

    public class ArchiverFactory : IArchiverFactory
    {
        private readonly ZippingWorkerServiceConfigurationType _config;
        private readonly ILogger<ArchiverFactory> _logger;

        public ArchiverFactory(ZippingWorkerServiceConfigurationType config, ILogger<ArchiverFactory> logger)
        {
            _config = config;
            _logger = logger;
        }

        public IArchiver CreateArchiver()
        {
            return _config.archiver switch
            {
                ArchiverEnumType.sevenzip => new SevenZipArchiverAdapter(_config.ResolvedSevenZipExePath, _config.ResolvedTempDir_SymLink, _config.ResolvedTempDir_ZipStaging),
                ArchiverEnumType.dotnetzip => new DotNetZipArchiver(),
                _ => throw new NotSupportedException($"Archiver type '{_config.archiver}' is not supported.")
            };
        }
    }

    internal class SevenZipArchiverAdapter : IArchiver
    {
        private readonly string _sevenZipExePath;
        private readonly string _symlinkTempDir;
        private readonly string _zipStagingDir;

        public SevenZipArchiverAdapter(string sevenZipExePath, string symlinkTempDir, string zipStagingDir)
        {
            _sevenZipExePath = sevenZipExePath;
            _symlinkTempDir = symlinkTempDir;
            _zipStagingDir = zipStagingDir;
        }

        public Task CreateArchiveAsync(List<(string SourcePath, string ArchivePath)> files,
                                      string archiveOutputPath,
                                      ArchiveCompressionLevel compressionLevel = ArchiveCompressionLevel.ultra,
                                      ProgressCallback? onProgress = null,
                                      Action<string>? onLog = null,
                                      Action<Exception>? onError = null)
        {
            string compressionArgs = compressionLevel switch
            {
                ArchiveCompressionLevel.ultra => "-mx9",
                ArchiveCompressionLevel.maximum => "-mx7",
                ArchiveCompressionLevel.normal => "-mx5",
                ArchiveCompressionLevel.fast => "-mx3",
                ArchiveCompressionLevel.fastest => "-mx1",
                ArchiveCompressionLevel.nocompression => "-mx0",
                _ => "-mx9"
            };

            return SevenZipSymlinkArchiver.CreateArchiveAsync(
                files,
                archiveOutputPath,
                _sevenZipExePath,
                onProgress,
                compressionArgs,
                onLog,
                onError,
                _symlinkTempDir
            );
        }
    }
}
