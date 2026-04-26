using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;

namespace ZippingWorker_Service.Zipping
{
    using ZippingWorker_Service;
    using ZippingWorker_Service.Model;
    internal class SevenZipSymlinkArchiverWrapper
    {
        public Task CreateArchiveAsync(ZipInfoType zipinfo,
                                       ProgressCallback? onProgress = null,
                                       Action<string>? onLog = null,
                                       Action<Exception>? onError = null)
        {
            string? levelArg = null;
            switch (zipinfo.zipcompressionlevel)
            {
                case CompressionLevelEnumType.ultra:
                    levelArg = "-mx9";
                    break;
                case CompressionLevelEnumType.maximum:
                    levelArg = "-mx7";
                    break;
                case CompressionLevelEnumType.normal:
                    levelArg = "-mx5";
                    break;
                case CompressionLevelEnumType.fast:
                    levelArg = "-mx3";
                    break;
                case CompressionLevelEnumType.fastest:
                    levelArg = "-mx1";
                    break;
                case CompressionLevelEnumType.nocompression:
                    levelArg = "-mx0";
                    break;
                default:
                    break;
            }

            return SevenZipSymlinkArchiver.CreateArchiveAsync(
                        zipinfo.zipfiles.Select(o => (o.filelocation, o.internalziplocation)).ToList(),
                        System.IO.Path.Combine(zipinfo.zipfilelocation, zipinfo.zipfilename),
                        "7z.exe",
                        onProgress,
                        levelArg,
                        onLog,
                        onError
                        );
        }
        public Task CreateArchiveAsync(List<(string SourcePath, string ArchivePath)> files,
                                       string archiveOutputPath,
                                       ArchiveCompressionLevel compressionLevel = ArchiveCompressionLevel.ultra,
                                       ProgressCallback? onProgress = null,
                                       Action<string>? onLog = null,
                                       Action<Exception>? onError = null)
        {
            string? levelArg = null;
            switch (compressionLevel)
            {
                case ArchiveCompressionLevel.ultra:
                    levelArg = "-mx9";
                    break;
                case ArchiveCompressionLevel.maximum:
                    levelArg = "-mx7";
                    break;
                case ArchiveCompressionLevel.normal:
                    levelArg = "-mx5";
                    break;
                case ArchiveCompressionLevel.fast:
                    levelArg = "-mx3";
                    break;
                case ArchiveCompressionLevel.fastest:
                    levelArg = "-mx1";
                    break;
                case ArchiveCompressionLevel.nocompression:
                    levelArg = "-mx0";
                    break;
                default:
                    break;
            }
            return SevenZipSymlinkArchiver.CreateArchiveAsync(
                        files,
                        archiveOutputPath,
                        "7z.exe",
                        onProgress,
                        levelArg,
                        onLog,
                        onError
                        );
        }

    }
}
