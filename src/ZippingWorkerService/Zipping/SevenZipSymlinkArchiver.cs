using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ZippingWorkerService.Zipping
{
    public static class SevenZipSymlinkArchiver
    {
        private const string _sevenZipExePath = "7z.exe";
        private const string _compressionArgs = "-mx9";
        public static async Task CreateArchiveAsync(List<(string SourcePath, string ArchivePath)> files,
                                                    string archiveOutputPath,
                                                    string? sevenZipExePath = _sevenZipExePath,
                                                    ProgressCallback? onProgress = null,
                                                    string? compressionArgs = _compressionArgs,
                                                    Action<string>? onLog = null,
                                                    Action<Exception>? onError = null,
                                                    string? symlinkTempDir = null)
        {
            if (sevenZipExePath == null)
                sevenZipExePath = _sevenZipExePath;
            if (compressionArgs == null)
                compressionArgs = _compressionArgs;

            string tempRoot;
            if (!string.IsNullOrWhiteSpace(symlinkTempDir) && Directory.Exists(symlinkTempDir))
            {
                tempRoot = Path.Combine(symlinkTempDir, "7zstaging_" + Guid.NewGuid().ToString("N"));
            }
            else
            {
                tempRoot = Path.Combine(Path.GetTempPath(), "7zstaging_" + Guid.NewGuid().ToString("N"));
            }
            Directory.CreateDirectory(tempRoot);
            try
            {
                onLog?.Invoke($"[Symlink] Staging directory: {tempRoot}");
                var mklink = new MklinkSessionAsync();
                try
                {
                    int index = 0;
                    foreach (var (source, archivePath) in files)
                    {
                        try
                        {
                            string linkPath = Path.Combine(tempRoot, archivePath);
                            Directory.CreateDirectory(Path.GetDirectoryName(linkPath) ?? tempRoot);
                            bool isDir = Directory.Exists(source);
                            bool success = await mklink.CreateLinkAsync(linkPath, source, isDir);

                            if (!success)
                            {
                                var ex = new Exception($"Failed to create symlink: '{linkPath}' —> '{source}'");
                                onError?.Invoke(ex);
                                continue;
                            }
                            index++;
                            onProgress?.Invoke(index, files.Count, archivePath);
                            onLog?.Invoke($" Linked: {archivePath}");
                        }
                        catch (Exception ex)
                        {
                            onError?.Invoke(ex);
                        }
                    }
                }
                finally
                {
                    await mklink.DisposeAsync();
                }
                
                onLog?.Invoke("[7z] Starting compression...");
                string arguments = $"a —spf {compressionArgs} \"{archiveOutputPath}\" \"{tempRoot}\"";
                var psi = new ProcessStartInfo
                {
                    FileName = sevenZipExePath,
                    Arguments = arguments,
                    WorkingDirectory = tempRoot,
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
            catch (Exception ex)
            {
                onError?.Invoke(ex);
            }
            finally
            {
                try
                {
                    Directory.Delete(tempRoot, true);
                    onLog?.Invoke("[Cleanup] Temporary folder deleted.");
                }
                catch (Exception cleanupEx)
                {
                    onError?.Invoke(new Exception("Failed to delete temp folder: " + tempRoot, cleanupEx));
                }
            }
        }
    }
}
