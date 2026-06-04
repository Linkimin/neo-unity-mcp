// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using Neo.UnityMcp.Registry;
using Neo.UnityMcp.Services;
using UnityEngine;

namespace Neo.UnityMcp.Tools
{
    [NeoToolProvider("Console")]
    internal static class ConsoleTools
    {
        [NeoTool("get_console_logs", "Recent Unity console log entries captured in-process (ring buffer).")]
        [ReadOnlyTool]
        public static object GetConsoleLogs(
            [ToolParam("Max entries to return (default 50).", Required = false)] int count = 50,
            [ToolParam("Filter: all, error, warning, log, assert, exception (default all).", Required = false)] string logType = "all")
        {
            var filter = ParseFilter(logType);
            var entries = ConsoleLogService.GetRecent(count, filter);

            return Response.Success(entries.Count + " log entr" + (entries.Count == 1 ? "y" : "ies") + ".", new
            {
                count = entries.Count,
                filter = string.IsNullOrWhiteSpace(logType) ? "all" : logType,
                entries
            });
        }

        private static LogType? ParseFilter(string logType)
        {
            if (string.IsNullOrWhiteSpace(logType))
                return null;

            switch (logType.Trim().ToLowerInvariant())
            {
                case "error": return LogType.Error;
                case "warning": return LogType.Warning;
                case "log":
                case "info": return LogType.Log;
                case "assert": return LogType.Assert;
                case "exception": return LogType.Exception;
                default: return null; // "all" / unknown
            }
        }
    }
}
