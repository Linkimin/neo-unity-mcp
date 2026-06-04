// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using Neo.UnityMcp.Registry;
using UnityEditor;

namespace Neo.UnityMcp.Tools
{
    // Enter/exit play mode. Entering play mode reloads the domain, so the HTTP response may be
    // cut off — clients should poll get_editor_state / get_reload_recovery_status afterwards.
    [NeoToolProvider("PlayMode")]
    internal static class PlayModeTools
    {
        [NeoTool("enter_play_mode", "Enter play mode. The domain reloads, so poll editor state after calling.")]
        [SceneEditingTool]
        public static object EnterPlayMode()
        {
            if (EditorApplication.isPlaying)
                return Response.Success("Already in play mode.", new { isPlaying = true });

            EditorApplication.EnterPlaymode();
            return Response.Success("Entering play mode (the domain may reload).", new { isPlaying = true });
        }

        [NeoTool("exit_play_mode", "Exit play mode and return to edit mode.")]
        [SceneEditingTool]
        public static object ExitPlayMode()
        {
            if (!EditorApplication.isPlaying)
                return Response.Success("Not in play mode.", new { isPlaying = false });

            EditorApplication.ExitPlaymode();
            return Response.Success("Exiting play mode.", new { isPlaying = false });
        }
    }
}
