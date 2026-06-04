// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System.Threading.Tasks;
using Neo.UnityMcp.Registry;
using Neo.UnityMcp.Services;
using Newtonsoft.Json;
using UnityEditor;

namespace Neo.UnityMcp.Tools
{
    [NeoToolProvider("Compilation")]
    internal static class CompilationTools
    {
        [NeoTool("request_recompile",
            "Import external file changes and recompile. Call after editing .cs/.asmdef/assets before " +
            "running tests or other tools. May reload the domain; if interrupted, poll get_reload_recovery_status.")]
        public static async Task<object> RequestRecompile(
            [ToolParam("Max seconds to wait for compilation (default 30).", Required = false)] int timeoutSeconds = 30)
        {
            if (EditorApplication.isPlaying)
                return Response.Error("PLAY_MODE", new { hint = "Exit play mode before recompiling." });

            ReloadRecovery.MarkPending("request_recompile");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            if (EditorApplication.isCompiling)
            {
                return Response.Success(
                    "External changes imported; Unity is recompiling and may reload the domain. " +
                    "Poll get_reload_recovery_status / get_compilation_errors after it finishes.",
                    new { compiling = true });
            }

            var completed = await NeoCompilationService.WaitForCompilationAsync(forceRefresh: false, timeoutSeconds);
            if (!completed)
                return Response.Success("External changes imported; still compiling in the background.", new { compiling = true });

            var errors = NeoCompilationService.GetCompilationErrors(50, includeWarnings: true);
            return IsClean(errors)
                ? Response.Success("External changes imported. No compilation errors.", new { compiling = false })
                : Response.Error("COMPILATION_FAILED", new { errors });
        }

        [NeoTool("wait_for_compilation", "Wait (non-blocking) until script compilation completes; reports errors if any.")]
        [ReadOnlyTool]
        public static async Task<object> WaitForCompilation(
            [ToolParam("Force a refresh before waiting (default true).", Required = false)] bool forceRefresh = true,
            [ToolParam("Max seconds to wait (default 30).", Required = false)] int timeoutSeconds = 30)
        {
            var completed = await NeoCompilationService.WaitForCompilationAsync(forceRefresh, timeoutSeconds);
            if (!completed)
                return Response.Error("COMPILATION_TIMEOUT", new { timeoutSeconds });

            var errors = NeoCompilationService.GetCompilationErrors();
            return IsClean(errors)
                ? Response.Success("Compilation complete. No errors detected.", new { compiling = false })
                : Response.Error("COMPILATION_FAILED", new { errors });
        }

        [NeoTool("get_compilation_errors", "Latest script compilation errors (optionally warnings) from the last compile cycle.")]
        [ReadOnlyTool]
        public static object GetCompilationErrors(
            [ToolParam("Max issues to return (default 50).", Required = false)] int maxEntries = 50,
            [ToolParam("Include warnings (default false).", Required = false)] bool includeWarnings = false)
        {
            if (NeoCompilationService.IsCompiling)
                return Response.Success("Currently compiling... try again shortly.", new { compiling = true });

            var errors = NeoCompilationService.GetCompilationErrors(maxEntries, includeWarnings);
            return Response.Success(errors, new { clean = IsClean(errors), compiling = false });
        }

        [NeoTool("get_reload_recovery_status", "Outcome of the last domain-reload recovery (e.g. after request_recompile crossed a reload).")]
        [ReadOnlyTool]
        public static object GetReloadRecoveryStatus(
            [ToolParam("Consume/clear the event after reading (default false).", Required = false)] bool consume = false)
        {
            var json = ReloadRecovery.GetEventJson(consume);
            if (json == null)
                return Response.Success("No reload recovery event recorded.", new { hasEvent = false });

            return Response.Success("Reload recovery event.", JsonConvert.DeserializeObject(json));
        }

        private static bool IsClean(string errors) =>
            errors == "No compilation errors detected." || errors == "No compilation errors or warnings detected.";
    }
}
