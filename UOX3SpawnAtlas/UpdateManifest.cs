using System.Collections.Generic;

namespace UOX3SpawnAtlas
{
    public class UpdateManifest
    {
        public string LatestVersion { get; set; }
        public string Changelog { get; set; }
        public bool Mandatory { get; set; }
        public string BaseUrl { get; set; }
        public List<string> Files { get; set; }
    }
}