// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System.Text.RegularExpressions;

namespace Neo.UnityMcp.Execution
{
    // Conservative blocklist for execute_code. Covers the common "AI accidentally
    // destroyed something / hung the editor" cases without locking down legitimate
    // automation (System.Net, reflection, etc. stay allowed). Defensive layer only.
    internal static class SafetyChecks
    {
        private static readonly (string Pattern, string Reason)[] Block =
        {
            (@"\bFile\.Delete\b",                 "File.Delete"),
            (@"\bDirectory\.Delete\b",            "Directory.Delete"),
            (@"\bSystem\.IO\.File\.Delete\b",     "System.IO.File.Delete"),
            (@"\bProcess\.Start\b",               "Process.Start"),
            (@"\bSystem\.Diagnostics\.Process\b", "System.Diagnostics.Process"),
            (@"\bEnvironment\.Exit\b",            "Environment.Exit"),
            (@"\bApplication\.Quit\b",            "Application.Quit"),
            (@"\bAssetDatabase\.DeleteAsset\b",   "AssetDatabase.DeleteAsset"),
            (@"\bwhile\s*\(\s*true\s*\)",         "while(true)"),
            (@"\bfor\s*\(\s*;\s*;\s*\)",          "for(;;)"),
        };

        public static bool IsBlocked(string code, out string reason)
        {
            if (!string.IsNullOrEmpty(code))
            {
                foreach (var (pattern, why) in Block)
                {
                    if (Regex.IsMatch(code, pattern))
                    {
                        reason = why;
                        return true;
                    }
                }
            }

            reason = null;
            return false;
        }
    }
}
