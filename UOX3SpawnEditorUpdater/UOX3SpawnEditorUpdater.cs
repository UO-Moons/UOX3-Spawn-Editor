using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;

namespace UOX3SpawnEditorUpdater
{
    internal class UpdateManifest
    {
        public string LatestVersion { get; set; }
        public string Changelog { get; set; }
        public bool Mandatory { get; set; }
        public string BaseUrl { get; set; }
        public List<string> Files { get; set; }
    }

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
                string manifestUrl = args[1];

                string installFolderPath = Path.GetDirectoryName(targetExePath);
                if (string.IsNullOrWhiteSpace(installFolderPath))
                    return;

                string tempFolderPath = Path.Combine(Path.GetTempPath(), "UOX3SpawnEditorUpdate");

                if (Directory.Exists(tempFolderPath))
                    Directory.Delete(tempFolderPath, true);

                Directory.CreateDirectory(tempFolderPath);

                if (!WaitForProcessToExit(targetExePath))
                    return;

                UpdateManifest manifest = DownloadManifest(manifestUrl);
                if (manifest == null)
                    return;

                if (string.IsNullOrWhiteSpace(manifest.BaseUrl))
                    return;

                if (manifest.Files == null || manifest.Files.Count == 0)
                    return;

                DownloadAllFiles(manifest, tempFolderPath);
                CopyAllFilesToInstallFolder(manifest, tempFolderPath, installFolderPath);

                Thread.Sleep(500);

                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.FileName = targetExePath;
                processStartInfo.WorkingDirectory = Path.GetDirectoryName(targetExePath);
                processStartInfo.UseShellExecute = true;

                Process.Start(processStartInfo);
            }
            catch (Exception exception)
            {
                File.WriteAllText( Path.Combine(Path.GetTempPath(), "UOX3SpawnEditorUpdaterError.txt"), exception.ToString() );
                return;
            }
        }

        private static UpdateManifest DownloadManifest(string manifestUrl)
        {
            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add("Cache-Control", "no-cache");
                string json = webClient.DownloadString(manifestUrl);
                return JsonConvert.DeserializeObject<UpdateManifest>(json);
            }
        }

        private static void DownloadAllFiles(UpdateManifest manifest, string tempFolderPath)
        {
            string cleanBaseUrl = manifest.BaseUrl.TrimEnd('/', '\\');

            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add("Cache-Control", "no-cache");

                foreach (string fileName in manifest.Files)
                {
                    if (string.IsNullOrWhiteSpace(fileName))
                        continue;

                    string cleanFileName = fileName.Replace("/", "\\").TrimStart('\\');
                    string fileUrl = cleanBaseUrl + "/" + cleanFileName.Replace("\\", "/");
                    string tempFilePath = Path.Combine(tempFolderPath, cleanFileName);
                    string tempFileFolderPath = Path.GetDirectoryName(tempFilePath);

                    if (!string.IsNullOrWhiteSpace(tempFileFolderPath) && !Directory.Exists(tempFileFolderPath))
                        Directory.CreateDirectory(tempFileFolderPath);

                    webClient.DownloadFile(fileUrl, tempFilePath);
                }
            }
        }

        private static void CopyAllFilesToInstallFolder(UpdateManifest manifest, string tempFolderPath, string installFolderPath)
        {
            foreach (string fileName in manifest.Files)
            {
                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                string cleanFileName = fileName.Replace("/", "\\").TrimStart('\\');

                if (cleanFileName.Equals("UOX3SpawnEditorUpdater.exe", StringComparison.OrdinalIgnoreCase))
                    continue;

                string tempFilePath = Path.Combine(tempFolderPath, cleanFileName);
                string destinationFilePath = Path.Combine(installFolderPath, cleanFileName);
                string destinationFolderPath = Path.GetDirectoryName(destinationFilePath);

                if (!File.Exists(tempFilePath))
                    throw new FileNotFoundException("Downloaded file not found.", tempFilePath);

                if (!string.IsNullOrWhiteSpace(destinationFolderPath) && !Directory.Exists(destinationFolderPath))
                    Directory.CreateDirectory(destinationFolderPath);

                File.Copy(tempFilePath, destinationFilePath, true);
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