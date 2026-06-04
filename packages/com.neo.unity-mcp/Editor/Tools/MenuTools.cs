// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using Neo.UnityMcp.Registry;
using UnityEditor;

namespace Neo.UnityMcp.Tools
{
    // Invoke editor menu items — the cheap-and-broad fallback when no dedicated tool exists.
    [NeoToolProvider("Menu")]
    internal static class MenuTools
    {
        [NeoTool("execute_menu_item",
            "Execute an editor menu item by full path, e.g. 'GameObject/2D Object/Sprites/Square' or 'Edit/Undo'.")]
        [SceneEditingTool]
        public static object ExecuteMenuItem(
            [ToolParam("Full menu path ('/'-separated, case sensitive).")] string menu_path)
        {
            if (string.IsNullOrWhiteSpace(menu_path))
                return Response.Error("MENU_PATH_REQUIRED");

            try
            {
                var ok = EditorApplication.ExecuteMenuItem(menu_path);
                if (!ok)
                    return Response.Error("MENU_ITEM_NOT_FOUND",
                        new { menu_path, hint = "Verify the path matches the editor menu exactly." });
                return Response.Success("Executed menu item '" + menu_path + "'.", new { menu_path });
            }
            catch (Exception ex)
            {
                return Response.Error("MENU_EXECUTION_FAILED", new { menu_path, error = ex.Message });
            }
        }

        [NeoTool("validate_menu_item",
            "Note that validating a menu item without side effects is not supported by Unity; " +
            "use execute_menu_item and inspect the success flag.")]
        [ReadOnlyTool]
        public static object ValidateMenuItem(
            [ToolParam("Full menu path to validate.")] string menu_path)
        {
            if (string.IsNullOrWhiteSpace(menu_path))
                return Response.Error("MENU_PATH_REQUIRED");

            return Response.Success(
                "Unity has no side-effect-free validation API; use execute_menu_item and read the success flag.",
                new { menu_path });
        }
    }
}
