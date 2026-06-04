// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Neo.UnityMcp.Protocol
{
    internal static class NeoProjectIdentity
    {
        public const int IdentityVersion = 1;

        public static string FromProjectPath(string projectPath)
        {
            var normalized = string.IsNullOrWhiteSpace(projectPath)
                ? Application.dataPath
                : Path.GetFullPath(projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized.ToUpperInvariant()));
                return "neo-" + BitConverter.ToString(bytes).Replace("-", string.Empty).Substring(0, 16).ToLowerInvariant();
            }
        }
    }
}
