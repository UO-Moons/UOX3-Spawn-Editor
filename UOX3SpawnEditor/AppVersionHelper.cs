using System.Reflection;

namespace UOX3SpawnEditor
{
    public static class AppVersionHelper
    {
        public static string GetCurrentVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();

            var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (info != null && !string.IsNullOrWhiteSpace(info.InformationalVersion))
                return info.InformationalVersion;

            var version = assembly.GetName().Version;
            if (version != null)
                return version.Major + "." + version.Minor + "." + version.Build;

            return "0.0.0";
        }
    }
}