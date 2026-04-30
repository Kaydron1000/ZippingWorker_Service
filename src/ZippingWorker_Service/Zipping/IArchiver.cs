using System;
using System.Collections.Generic;
using System.Text;

namespace ZippingWorker_Service.Zipping
{
    public delegate void ProgressCallback(int currentIndex, int totalFiles, string archivePath);
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
