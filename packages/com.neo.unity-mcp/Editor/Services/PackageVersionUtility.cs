// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor.PackageManager;

namespace Neo.UnityMcp.Services
{
    internal static class PackageVersionUtility
    {
        public static string CurrentVersion
        {
            get
            {
                try
                {
                    var packageInfo = PackageInfo.FindForAssembly(typeof(PackageVersionUtility).Assembly);
                    if (!string.IsNullOrWhiteSpace(packageInfo?.version))
                        return packageInfo.version;
                }
                catch
                {
                }

                try
                {
                    var packageJson = Path.GetFullPath(Path.Combine("Packages", "com.neo.unity-mcp", "package.json"));
                    if (File.Exists(packageJson))
                        return (string)JObject.Parse(File.ReadAllText(packageJson))["version"] ?? "0.0.0";
                }
                catch
                {
                }

                return "0.0.0";
            }
        }
    }
}
