using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;

namespace ZippingWorkerService.Zipping
{
    public class DotNetZipArchiver : IArchiver
    {
        public async Task CreateArchiveAsync(List<(string SourcePath, string ArchivePath)> files,
                                            string archiveOutputPath,
                                            ArchiveCompressionLevel compressionLevel = ArchiveCompressionLevel.ultra,
                                            ProgressCallback? onProgress = null,
                                            Action<string>? onLog = null,
                                            Action<Exception>? onError = null)
        {
            int count = 0;
            try
            {
                using (var fs = new FileStream(archiveOutputPath, FileMode.Create))
                using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    foreach (var (sourcePath, archivePath) in files)
                    {
                        string fixedPath = archivePath.Replace('\\', '/');
                        if (File.Exists(sourcePath))
                        {
                            archive.CreateEntryFromFile(sourcePath, fixedPath, ConvertCompression(compressionLevel));
                        }
                        else if (Directory.Exists(sourcePath))
                        {
                            AddDirectoryRecursive(archive, sourcePath, fixedPath, compressionLevel);
                        }
                        else
                        {
                            onError?.Invoke(new FileNotFoundException("File not found", sourcePath));
                            continue;
                        }

                        count++;
                        onProgress?.Invoke(count, files.Count, fixedPath);
                        onLog?.Invoke($"Added: {fixedPath}");

                        await Task.Yield(); // yield for UI responsiveness
                    }
                }
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
            }
        }

        private CompressionLevel ConvertCompression(ArchiveCompressionLevel level)
        {
            switch (level)
            {
                case ArchiveCompressionLevel.nocompression: return CompressionLevel.NoCompression;
                case ArchiveCompressionLevel.fastest: return CompressionLevel.Fastest;
                case ArchiveCompressionLevel.normal: return CompressionLevel.Optimal;
                case ArchiveCompressionLevel.maximum: return CompressionLevel.SmallestSize;
                case ArchiveCompressionLevel.ultra: return CompressionLevel.SmallestSize;
                default: return CompressionLevel.Optimal;
            }
        }

        private void AddDirectoryRecursive(ZipArchive archive, string sourceDir, string archiveRoot, ArchiveCompressionLevel level)
        {
            foreach (string file in Directory.GetFiles(sourceDir, "#", SearchOption.AllDirectories))
            {
                string relative = file.Substring(sourceDir.Length).TrimStart('\\', '/');
                string zipPath = Path.Combine(archiveRoot, relative).Replace('\\', '/');
                archive.CreateEntryFromFile(file, zipPath, ConvertCompression(level));
            }
        }
    }
}
