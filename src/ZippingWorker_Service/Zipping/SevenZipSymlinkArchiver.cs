using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Diagnostics.Eventing.Reader;

namespace ZippingWorker_Service.Zipping
{
    public static class SevenZipSymlinkArchiver
    {
        private const string _sevenZipExePath = "7z.exe";
        private const string _compressionArgs = "-mx9";

        /// <summary>
        /// Monitors 7z standard output stream and reports progress based on file operation lines.
        /// The regex matches 7z's file operation indicators: + (add), u (update), U (update header), - (delete), = (set), R (rename), A (set archive attribute), . (skip)
        /// </summary>
        /// <param name="outputStream">Standard output stream from 7z process</param>
        /// <param name="totalFiles">Total number of files to be compressed</param>
        /// <param name="symlinkCount">Number of symlinks already created (for progress offset calculation)</param>
        /// <param name="onProgress">Progress callback (current, total, path)</param>
        /// <param name="onLog">Log callback for each matched line</param>
        private static async Task MonitorSevenZipProgressAsync(
            StreamReader outputStream,
            int totalFiles,
            int symlinkCount,
            string logType,
            ProgressCallback? onProgress,
            Action<string>? onLog)
        {
            // Regex pattern matches 7z file operation lines: "[+uU-=RA.][ ].*"
            // + = adding file, u = updating file, U = updating header, - = deleting, = = setting, R = renaming, A = setting archive attribute, . = skipping
            var progressRegex = new Regex(@"^[+uU\-=RA.][ ]", RegexOptions.Compiled);
            int compressedFileCount = 0;

            string? line;
            while ((line = await outputStream.ReadLineAsync()) != null)
            {
                // Log all output if callback provided
                onLog?.Invoke($"[7z] {line}");

                // Check if line matches file operation pattern
                if (progressRegex.IsMatch(line))
                {
                    compressedFileCount++;

                    // Calculate overall progress: 15% for symlinks, 85% for compression
                    // Total progress = (symlinkCount * 15% / totalFiles) + (compressedFileCount * 85% / totalFiles)



                    // Report progress with ZipAdd type
                    onProgress?.Invoke(compressedFileCount, totalFiles, line.Length > 2 ? line.Substring(2).Trim() : "", logType);
                }
            }
        }
        public static async Task CreateArchiveAsync(List<(string SourcePath, string ArchivePath)> files,
                                                    string stagingDirectory,
                                                    string zipOutputPath,
                                                    string? sevenZipExePath = _sevenZipExePath,
                                                    bool zipperIntegrityCheck = false,
                                                    string? compressionArgs = _compressionArgs,
                                                    ProgressCallback? onProgress = null,
                                                    Action<string>? onLog = null,
                                                    Action<Exception>? onError = null,
                                                    string? symlinkTempDir = null)
        {
            if (sevenZipExePath == null)
                sevenZipExePath = _sevenZipExePath;
            if (compressionArgs == null)
                compressionArgs = _compressionArgs;

            string tempRoot = stagingDirectory;
            Directory.CreateDirectory(tempRoot);
            int symlinkCount = 0; // Track number of symlinks created for progress calculation
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
                            bool success = false;
                            if (isDir)
                            {
                                try
                                {
                                    Directory.CreateDirectory(linkPath);
                                }
                                catch
                                {
                                    var ex = new Exception($"Failed to create directory for symlink: '{linkPath}' —> '{source}'");
                                    onError?.Invoke(ex);
                                    continue;
                                }
                            }
                            else
                            {
                                success = await mklink.CreateLinkAsync(linkPath, source, isDir);

                                if (!success)
                                {
                                    var ex = new Exception($"Failed to create symlink: '{linkPath}' —> '{source}'");
                                    onError?.Invoke(ex);
                                    continue;
                                }
                                // Report detailed info about the link created (full paths)
                                onProgress?.Invoke(index, files.Count, $"{linkPath} -> {source}", "LinkInfo");
                            }
                            index++;
                            // Report progress for this symlink
                            onProgress?.Invoke(index, files.Count, archivePath, "LinkAdd");
                            onLog?.Invoke($" Linked: {archivePath}");
                        }
                        catch (Exception ex)
                        {
                            onError?.Invoke(ex);
                        }
                    }
                    symlinkCount = index; // Capture final symlink count for 7z progress calculation
                }
                finally
                {
                    await mklink.DisposeAsync();
                }

                onLog?.Invoke("[7z] Starting compression...");
                
                string arguments = $"a -ssp -bb {compressionArgs} \"{zipOutputPath}\" \"{tempRoot}\"";

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
                    if (process == null)
                    {
                        throw new Exception("Failed to start 7z process");
                    }

                    // Start monitoring 7z output asynchronously for progress updates
                    var monitorTask = MonitorSevenZipProgressAsync(
                        process.StandardOutput,
                        files.Count,
                        symlinkCount, // Number of symlinks created (15% of work)
                        "ZipAdd", // Use ZipAdd type for compression progress
                        onProgress,
                        onLog);

                    // Read stderr separately
                    string stderr = await process.StandardError.ReadToEndAsync();

                    // Wait for both monitoring and process completion
                    await monitorTask;
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

                if (zipperIntegrityCheck)
                {
                    onLog?.Invoke("[7z] Integrity check...");

                    string arguments2 = $"t -bb \"{zipOutputPath}\"";

                    var psi2 = new ProcessStartInfo
                    {
                        FileName = sevenZipExePath,
                        Arguments = arguments2,
                        WorkingDirectory = tempRoot,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (var process = Process.Start(psi))
                    {
                        if (process == null)
                        {
                            throw new Exception("Failed to start 7z process");
                        }

                        // Start monitoring 7z output asynchronously for progress updates
                        var monitorTask = MonitorSevenZipProgressAsync(
                            process.StandardOutput,
                            files.Count,
                            symlinkCount, // Number of symlinks created (15% of work)
                            "ZipTest", // Use ZipTest type for integrity check progress as well
                            onProgress,
                            onLog);

                        // Read stderr separately
                        string stderr = await process.StandardError.ReadToEndAsync();

                        // Wait for both monitoring and process completion
                        await monitorTask;
                        await Task.Run(() => process.WaitForExit());

                        if (process.ExitCode != 0)
                        {
                            var ex = new Exception($"7z exited with code {process.ExitCode}:\n{stderr}");
                            onError?.Invoke(ex);
                        }
                        else
                        {
                            onLog?.Invoke("[7z] Archive integrity check completed successfully.");
                        }
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
        private static async Task<int> Run7Zip(string exe, string arguments, string workingDir)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = arguments,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process();
            process.StartInfo = psi;

            process.Start();

            string stdout = await process.StandardOutput.ReadToEndAsync();
            string stderr = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            Console.WriteLine(stdout);

            if (!string.IsNullOrWhiteSpace(stderr))
                Console.WriteLine(stderr);

            return process.ExitCode;
        }
    }
}
