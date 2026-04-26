using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace ZippingWorker_Service.Zipping
{
    public class MklinkSessionAsync
    {
        private readonly Process _cmdProcess;
        private readonly StreamWriter _stdin;
        private readonly StringBuilder _outputBuffer = new StringBuilder();
        private readonly StringBuilder _errorBuffer = new StringBuilder();
        public event Action<string> OnOutput;
        public event Action<string> OnError;
        public MklinkSessionAsync()
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            _cmdProcess = new Process { StartInfo = psi };
            _cmdProcess.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    _outputBuffer.AppendLine(e.Data);
                    OnOutput?.Invoke(e.Data);
                }
            };
            _cmdProcess.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    _errorBuffer.AppendLine(e.Data);
                    OnError?.Invoke(e.Data);
                }
            };
            _cmdProcess.Start();
            _cmdProcess.BeginOutputReadLine();
            _cmdProcess.BeginErrorReadLine();

            _stdin = _cmdProcess.StandardInput;
        }

        public async Task<bool> CreateLinkAsync(string linkPath, string targetPath, bool isDirectory)
        {

            _outputBuffer.Clear();
            _errorBuffer.Clear();

            string escapedLink = linkPath.Replace("\"", "\"\"");
            string escapedTarget = targetPath.Replace("\"", "\"\"");

            string cmd = isDirectory ? $"mklink /D \"{escapedLink}\" \"{escapedTarget}\""
                    : $"mklink \"{escapedLink}\" \"{escapedTarget}\"";

            // Write command + end marker
            await _stdin.WriteLineAsync(cmd);
            await _stdin.WriteLineAsync("echo [END]");

            // Wait for end marker
            bool endSeen = await WaitForMarkerAsync("[END]", timeoutMs: 3000);

            string output = _outputBuffer.ToString();
            string errors = _errorBuffer.ToString();
            bool success = output.Contains("symbolic link") && endSeen && string.IsNullOrWhiteSpace(errors);

            return success;
        }

        private async Task<bool> WaitForMarkerAsync(string marker, int timeoutMs)
        {
            int interval = 50;
            int waited = 0;
            while (waited < timeoutMs)
            {
                if (_outputBuffer.ToString().Contains(marker))
                    return true;

                await Task.Delay(interval);
                waited += interval;
            }

            return false;
        }

        public async ValueTask DisposeAsync()
        {
            try { await _stdin.WriteLineAsync("exit"); } catch { }
            try { _stdin.Dispose(); } catch { }
            try { await Task.Run(() => _cmdProcess.WaitForExit()); } catch { }
            _cmdProcess.Dispose();
        }

    }
}
