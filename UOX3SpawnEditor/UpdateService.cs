using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net;

namespace UOX3SpawnEditor
{
    public static class UpdateService
    {
        private class GitHubReleaseAsset
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("browser_download_url")]
            public string BrowserDownloadUrl { get; set; }

            [JsonProperty("download_count")]
            public int DownloadCount { get; set; }
        }

        private class GitHubReleaseResponse
        {
            [JsonProperty("tag_name")]
            public string TagName { get; set; }

            [JsonProperty("body")]
            public string Body { get; set; }

            [JsonProperty("html_url")]
            public string HtmlUrl { get; set; }

            [JsonProperty("draft")]
            public bool Draft { get; set; }

            [JsonProperty("prerelease")]
            public bool Prerelease { get; set; }

            [JsonProperty("assets")]
            public GitHubReleaseAsset[] Assets { get; set; }
        }

        public static UpdateManifest GetManifest(string latestReleaseApiUrl)
        {
            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add("Cache-Control", "no-cache");
                webClient.Headers.Add("User-Agent", "UOX3SpawnEditor-Updater");
                webClient.Headers.Add("Accept", "application/vnd.github+json");

                string json = webClient.DownloadString(latestReleaseApiUrl);
                GitHubReleaseResponse release = JsonConvert.DeserializeObject<GitHubReleaseResponse>(json);

                if (release == null || release.Draft)
                    return null;

                GitHubReleaseAsset zipAsset = null;

                if (release.Assets != null && release.Assets.Length > 0)
                {
                    zipAsset = release.Assets
                        .Where(asset => asset != null &&
                                        !string.IsNullOrWhiteSpace(asset.Name) &&
                                        !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl) &&
                                        asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(asset => asset.Name.IndexOf("updater", StringComparison.OrdinalIgnoreCase) < 0)
                        .ThenBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase)
                        .FirstOrDefault();
                }

                return new UpdateManifest
                {
                    LatestVersion = CleanVersionTag(release.TagName),
                    Changelog = release.Body ?? string.Empty,
                    ReleasePageUrl = release.HtmlUrl ?? string.Empty,
                    DownloadUrl = zipAsset != null ? zipAsset.BrowserDownloadUrl : string.Empty,
                    AssetName = zipAsset != null ? zipAsset.Name : string.Empty,
                    DownloadCount = zipAsset != null ? zipAsset.DownloadCount : 0,
                    Mandatory = false
                };
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

        private static string CleanVersionTag(string versionText)
        {
            if (string.IsNullOrWhiteSpace(versionText))
                return "0.0.0";

            versionText = versionText.Trim();

            if (versionText.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                versionText = versionText.Substring(1);

            return versionText;
        }

        private static string NormalizeVersion(string versionText)
        {
            versionText = CleanVersionTag(versionText);

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