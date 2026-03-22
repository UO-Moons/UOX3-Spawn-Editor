using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace UOX3SpawnEditorUpdater
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                if (args.Length < 2)
                    return;

                string targetExePath = args[0];
                string releaseZipUrl = args[1];

                if (string.IsNullOrWhiteSpace(targetExePath) || string.IsNullOrWhiteSpace(releaseZipUrl))
                    return;

                string installFolderPath = Path.GetDirectoryName(targetExePath);
                if (string.IsNullOrWhiteSpace(installFolderPath))
                    return;

                string tempFolderPath = Path.Combine(Path.GetTempPath(), "UOX3SpawnEditorUpdate");
                string zipFilePath = Path.Combine(tempFolderPath, "update.zip");
                string extractFolderPath = Path.Combine(tempFolderPath, "extracted");

                if (Directory.Exists(tempFolderPath))
                    Directory.Delete(tempFolderPath, true);

                Directory.CreateDirectory(tempFolderPath);
                Directory.CreateDirectory(extractFolderPath);

                if (!WaitForProcessToExit(targetExePath))
                    return;

                DownloadReleaseZip(releaseZipUrl, zipFilePath);
                ZipFile.ExtractToDirectory(zipFilePath, extractFolderPath);

                CopyReleaseFilesToInstallFolder(extractFolderPath, installFolderPath);

                Thread.Sleep(500);

                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.FileName = targetExePath;
                processStartInfo.WorkingDirectory = installFolderPath;
                processStartInfo.UseShellExecute = true;

                Process.Start(processStartInfo);
            }
            catch (Exception exception)
            {
                File.WriteAllText(
                    Path.Combine(Path.GetTempPath(), "UOX3SpawnEditorUpdaterError.txt"),
                    exception.ToString()
                );
            }
        }

        private static void DownloadReleaseZip(string releaseZipUrl, string zipFilePath)
        {
            using (System.Net.WebClient webClient = new System.Net.WebClient())
            {
                webClient.Headers.Add("Cache-Control", "no-cache");
                webClient.Headers.Add("User-Agent", "UOX3SpawnEditor-Updater");
                webClient.DownloadFile(releaseZipUrl, zipFilePath);
            }
        }

        private static void CopyReleaseFilesToInstallFolder(string extractFolderPath, string installFolderPath)
        {
            foreach (string sourceFilePath in Directory.GetFiles(extractFolderPath, "*", SearchOption.AllDirectories))
            {
                string relativePath = sourceFilePath.Substring(extractFolderPath.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (string.IsNullOrWhiteSpace(relativePath))
                    continue;

                if (relativePath.Equals("UOX3SpawnEditorUpdater.exe", StringComparison.OrdinalIgnoreCase))
                    continue;

                string destinationFilePath = Path.Combine(installFolderPath, relativePath);
                string destinationFolderPath = Path.GetDirectoryName(destinationFilePath);

                if (!string.IsNullOrWhiteSpace(destinationFolderPath) && !Directory.Exists(destinationFolderPath))
                    Directory.CreateDirectory(destinationFolderPath);

                File.Copy(sourceFilePath, destinationFilePath, true);
            }
        }

        private static bool WaitForProcessToExit(string exePath)
        {
            string processName = Path.GetFileNameWithoutExtension(exePath);

            for (int attemptIndex = 0; attemptIndex < 100; attemptIndex++)
            {
                Process[] runningProcesses = Process.GetProcessesByName(processName);
                if (runningProcesses.Length == 0)
                    return true;

                Thread.Sleep(200);
            }

            return false;
        }
    }
}