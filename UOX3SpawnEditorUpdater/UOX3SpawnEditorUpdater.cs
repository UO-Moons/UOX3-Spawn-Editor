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
            string logFilePath = Path.Combine(Path.GetTempPath(), "UOX3SpawnEditorUpdaterLog.txt");

            try
            {
                Log(logFilePath, "Updater started.");

                if (args.Length < 2)
                {
                    Log(logFilePath, "Not enough arguments.");
                    return;
                }

                string targetExePath = args[0];
                string releaseZipUrl = args[1];

                Log(logFilePath, "Target EXE: " + targetExePath);
                Log(logFilePath, "Release ZIP URL: " + releaseZipUrl);

                if (string.IsNullOrWhiteSpace(targetExePath) || string.IsNullOrWhiteSpace(releaseZipUrl))
                {
                    Log(logFilePath, "Arguments were empty.");
                    return;
                }

                string installFolderPath = Path.GetDirectoryName(targetExePath);
                if (string.IsNullOrWhiteSpace(installFolderPath))
                {
                    Log(logFilePath, "Install folder path was empty.");
                    return;
                }

                string tempFolderPath = Path.Combine(Path.GetTempPath(), "UOX3SpawnEditorUpdate");
                string zipFilePath = Path.Combine(tempFolderPath, "update.zip");
                string extractFolderPath = Path.Combine(tempFolderPath, "extracted");

                if (Directory.Exists(tempFolderPath))
                    Directory.Delete(tempFolderPath, true);

                Directory.CreateDirectory(tempFolderPath);
                Directory.CreateDirectory(extractFolderPath);

                Log(logFilePath, "Waiting for target process to exit...");
                if (!WaitForProcessToExit(targetExePath, logFilePath))
                {
                    Log(logFilePath, "Target process did not exit in time.");
                    return;
                }

                Log(logFilePath, "Downloading release zip...");
                DownloadReleaseZip(releaseZipUrl, zipFilePath);

                Log(logFilePath, "Extracting zip...");
                ZipFile.ExtractToDirectory(zipFilePath, extractFolderPath);

                Log(logFilePath, "Copying updated files...");
                CopyReleaseFilesToInstallFolder(extractFolderPath, installFolderPath, logFilePath);

                Thread.Sleep(1500);

                if (!File.Exists(targetExePath))
                {
                    Log(logFilePath, "Target EXE missing after copy: " + targetExePath);
                    return;
                }

                Log(logFilePath, "Launching updated EXE...");
                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.FileName = targetExePath;
                processStartInfo.WorkingDirectory = installFolderPath;
                processStartInfo.UseShellExecute = true;

                Process.Start(processStartInfo);
                Log(logFilePath, "Launch command sent successfully.");
            }
            catch (Exception exception)
            {
                File.WriteAllText(logFilePath, exception.ToString());
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

        private static void CopyReleaseFilesToInstallFolder(string extractFolderPath, string installFolderPath, string logFilePath)
        {
            foreach (string sourceFilePath in Directory.GetFiles(extractFolderPath, "*", SearchOption.AllDirectories))
            {
                string relativePath = sourceFilePath.Substring(extractFolderPath.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (string.IsNullOrWhiteSpace(relativePath))
                    continue;

                if (relativePath.Equals("UOX3SpawnEditorUpdater.exe", StringComparison.OrdinalIgnoreCase))
                {
                    Log(logFilePath, "Skipping updater replacement: " + relativePath);
                    continue;
                }

                string destinationFilePath = Path.Combine(installFolderPath, relativePath);
                string destinationFolderPath = Path.GetDirectoryName(destinationFilePath);

                if (!string.IsNullOrWhiteSpace(destinationFolderPath) && !Directory.Exists(destinationFolderPath))
                    Directory.CreateDirectory(destinationFolderPath);

                File.Copy(sourceFilePath, destinationFilePath, true);
                Log(logFilePath, "Copied: " + relativePath);
            }
        }

        private static bool WaitForProcessToExit(string exePath, string logFilePath)
        {
            string processName = Path.GetFileNameWithoutExtension(exePath);

            for (int attemptIndex = 0; attemptIndex < 150; attemptIndex++)
            {
                Process[] runningProcesses = Process.GetProcessesByName(processName);
                if (runningProcesses.Length == 0)
                {
                    Log(logFilePath, "Target process exited.");
                    return true;
                }

                Thread.Sleep(200);
            }

            return false;
        }

        private static void Log(string logFilePath, string message)
        {
            File.AppendAllText(
                logFilePath,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + message + Environment.NewLine
            );
        }
    }
}