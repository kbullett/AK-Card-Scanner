using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AKCardScanner
{
    /// <summary>
    /// Downloads an installer file from a GitHub release asset URL and launches it.
    /// Intended to be used from the "Download" button on the update-available form:
    /// download the installer, run it, then exit this app so the installer can
    /// overwrite the currently-running files.
    /// </summary>
    public class UpdateDownloader
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5) // installers can be a few dozen MB; give it room
        };

        static UpdateDownloader()
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("AKCardScanner-Updater");
        }

        /// <summary>Downloads the file at downloadUrl to a temp path and returns that path.</summary>
        public async Task<string> DownloadInstallerAsync(string downloadUrl, IProgress<int> progress = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(downloadUrl))
                throw new ArgumentException("No download URL was provided.", nameof(downloadUrl));

            string fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
            if (string.IsNullOrWhiteSpace(fileName)) fileName = "AKCardScannerSetup.exe";
            string tempPath = Path.Combine(Path.GetTempPath(), fileName);

            using (var response = await _http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                long? totalBytes = response.Content.Headers.ContentLength;

                using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

                var buffer = new byte[81920];
                long totalRead = 0;
                int read;
                while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                    totalRead += read;

                    if (totalBytes.HasValue && progress != null)
                    {
                        int percent = (int)(totalRead * 100 / totalBytes.Value);
                        progress.Report(percent);
                    }
                }
            }

            return tempPath;
        }

        /// <summary>
        /// Launches the installer (showing its normal UI) and closes this application
        /// so the installer isn't blocked trying to overwrite a running exe.
        /// </summary>
        public void RunInstallerAndExit(string installerPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true // lets Windows show the UAC prompt if the installer needs elevation
            };

            Process.Start(startInfo);

            // Give the installer a brief head start before we release our file locks.
            // Most installers (Inno Setup, WiX, etc.) also detect and wait for a
            // running instance to close, but this avoids an unnecessary race.
            Thread.Sleep(500);

            Environment.Exit(0);
        }

        /// <summary>
        /// Runs an NSIS installer silently (/S), waits for it to finish, then relaunches
        /// the newly-installed app before exiting. Use this instead of
        /// RunInstallerAndExit when you want a fully click-free update: NSIS's normal
        /// "launch app" finish-page option is skipped in silent mode, so this method
        /// does that step manually instead.
        /// </summary>
        /// <param name="installerPath">Path to the downloaded NSIS installer.</param>
        /// <param name="relaunchExePath">
        /// Path to relaunch after install completes - typically Application.ExecutablePath.
        /// This assumes your NSIS script installs to the same directory the app is
        /// already running from (e.g. by reading the existing install location from the
        /// registry on upgrade). If your script always installs to a fresh default path,
        /// this path may no longer be correct after the update.
        /// </param>
        public async Task RunSilentInstallerAndRelaunchAsync(string installerPath, string relaunchExePath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/S",
                UseShellExecute = true // still needed so Windows can show the UAC prompt if required
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();

                // NSIS returns 0 on success. Only relaunch if the install actually succeeded.
                if (process.ExitCode == 0 && !string.IsNullOrEmpty(relaunchExePath) && File.Exists(relaunchExePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = relaunchExePath,
                        UseShellExecute = true
                    });
                }
            }

            Environment.Exit(0);
        }
    }
}