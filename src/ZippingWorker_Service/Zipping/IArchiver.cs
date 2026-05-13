using System;
using System.Collections.Generic;
using System.Text;

namespace ZippingWorker_Service.Zipping
{
    /// <summary>
    /// Progress callback for archive operations.
    /// </summary>
    /// <param name="currentIndex">Current item index being processed</param>
    /// <param name="totalFiles">Total number of items to process</param>
    /// <param name="archivePath">Path or description of the item being processed</param>
    /// <param name="logType">Type of operation: LinkAdd, LinkInfo, ZipAdd, ZipInfo</param>
    public delegate void ProgressCallback(int currentIndex, int totalFiles, string archivePath, string logType);

    public interface IArchiver
    {
        Task CreateArchiveAsync(
                    List<(string SourcePath, string ArchivePath)> files,
                    string stagingDirectory,
                    string zipOutputPath,
                    ArchiveCompressionLevel compressionLevel = ArchiveCompressionLevel.ultra,
                    ProgressCallback onProgress = null,
                    Action<string> onLog = null,
                    Action<Exception> onError = null);
    }

}
