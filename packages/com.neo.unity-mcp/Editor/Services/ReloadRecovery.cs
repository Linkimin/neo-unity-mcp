// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using Newtonsoft.Json;
using UnityEditor;

namespace Neo.UnityMcp.Services
{
    // Records the outcome of a recompile that crosses a domain reload, so a client whose
    // request_recompile call was interrupted can poll get_reload_recovery_status for the result.
    internal static class ReloadRecovery
    {
        private const string EventKey = "Neo.Reload.RecoveryEvent";
        private const string PendingKey = "Neo.Reload.Pending";

        [Serializable]
        private sealed class RecoveryEvent
        {
            public string tool;
            public string status;
            public string timeUtc;
            public string summary;
        }

        public static bool HasPending => !string.IsNullOrEmpty(SessionState.GetString(PendingKey, string.Empty));

        public static void MarkPending(string tool)
        {
            SessionState.SetString(PendingKey, string.IsNullOrEmpty(tool) ? "request_recompile" : tool);
        }

        public static void Store(string tool, string status, string summary)
        {
            SessionState.SetString(EventKey, JsonConvert.SerializeObject(new RecoveryEvent
            {
                tool = tool,
                status = status,
                timeUtc = DateTime.UtcNow.ToString("o"),
                summary = summary
            }));
            SessionState.EraseString(PendingKey);
        }

        // Returns the stored event as JSON (or null), optionally clearing it.
        public static string GetEventJson(bool consume)
        {
            var raw = SessionState.GetString(EventKey, null);
            if (string.IsNullOrEmpty(raw))
                return null;
            if (consume)
                SessionState.EraseString(EventKey);
            return raw;
        }
    }

    [InitializeOnLoad]
    internal static class ReloadRecoveryTracker
    {
        static ReloadRecoveryTracker()
        {
            TryComplete();
        }

        private static void TryComplete()
        {
            if (!ReloadRecovery.HasPending)
                return;

            if (!EditorApplication.isCompiling)
            {
                Complete();
                return;
            }

            EditorApplication.update += WaitUntilCompilationEnds;
        }

        private static void WaitUntilCompilationEnds()
        {
            if (EditorApplication.isCompiling)
                return;

            EditorApplication.update -= WaitUntilCompilationEnds;
            Complete();
        }

        private static void Complete()
        {
            if (!ReloadRecovery.HasPending)
                return;

            var errors = NeoCompilationService.GetCompilationErrors(includeWarnings: false);
            var ok = errors == "No compilation errors detected.";
            ReloadRecovery.Store(
                "request_recompile",
                ok ? "Success" : "Error",
                ok
                    ? "External changes imported and compilation finished successfully after domain reload."
                    : "External changes imported, but compilation reported issues.\n" + errors);
        }
    }
}
