using Newtonsoft.Json;
using System;
using System.Net;

namespace UOX3SpawnAtlas
{
    public static class UpdateService
    {
        public static UpdateManifest GetManifest(string manifestUrl)
        {
            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add("Cache-Control", "no-cache");
                string json = webClient.DownloadString(manifestUrl);
                return JsonConvert.DeserializeObject<UpdateManifest>(json);
            }
        }

        public static bool IsUpdateAvailable(string currentVersionText, string latestVersionText)
        {
            Version currentVersion;
            Version latestVersion;

            if (!Version.TryParse(NormalizeVersion(currentVersionText), out currentVersion))
                return false;

            if (!Version.TryParse(NormalizeVersion(latestVersionText), out latestVersion))
                return false;

            return latestVersion > currentVersion;
        }

        private static string NormalizeVersion(string versionText)
        {
            if (string.IsNullOrWhiteSpace(versionText))
                return "0.0.0.0";

            string[] parts = versionText.Split('.');
            while (parts.Length < 4)
            {
                versionText += ".0";
                parts = versionText.Split('.');
            }

            return versionText;
        }
    }
}