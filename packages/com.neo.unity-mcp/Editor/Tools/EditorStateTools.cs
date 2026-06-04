// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using System.Linq;
using Neo.UnityMcp.Registry;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Neo.UnityMcp.Tools
{
    [NeoToolProvider("EditorState")]
    internal static class EditorStateTools
    {
        [NeoTool("get_editor_state", "High-level editor runtime state: play mode, paused, compiling, updating, version.")]
        [ReadOnlyTool]
        public static object GetEditorState()
        {
            return Response.Success("Editor state", new
            {
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating,
                isPlayingOrWillChange = EditorApplication.isPlayingOrWillChangePlaymode,
                applicationPath = EditorApplication.applicationPath,
                timeSinceStartup = EditorApplication.timeSinceStartup,
                unityVersion = Application.unityVersion
            });
        }

        [NeoTool("get_selection", "Current hierarchy selection: instance ids, names, and paths.")]
        [ReadOnlyTool]
        public static object GetSelection()
        {
            var gos = Selection.gameObjects ?? Array.Empty<GameObject>();
            var items = gos.Select(go => new
            {
                instanceId = ObjectIdHelper.GetSerializableId(go),
                name = go.name,
                path = ObjectsHelper.GetGameObjectPath(go)
            }).ToList();

            return Response.Success(items.Count + " object(s) selected.", new
            {
                count = items.Count,
                activeInstanceId = ObjectIdHelper.GetSerializableId(Selection.activeGameObject),
                items
            });
        }

        [NeoTool("set_selection",
            "Replace the hierarchy selection. Pass comma-separated instance ids, names, or paths; " +
            "find_method controls resolution (by_id, by_name, by_path, by_id_or_name_or_path).")]
        [SceneEditingTool]
        public static object SetSelection(
            [ToolParam("Comma-separated instance ids, names, or paths.")] string targets,
            [ToolParam("Resolution method (default by_id_or_name_or_path).", Required = false)] string find_method = null)
        {
            if (string.IsNullOrWhiteSpace(targets))
            {
                Selection.objects = Array.Empty<UnityObject>();
                return Response.Success("Cleared selection.");
            }

            var picked = new List<GameObject>();
            var missing = new List<string>();

            foreach (var raw in targets.Split(','))
            {
                var token = raw.Trim();
                if (token.Length == 0)
                    continue;

                var go = ObjectsHelper.FindObject(token, find_method);
                if (go != null)
                    picked.Add(go);
                else
                    missing.Add(token);
            }

            Selection.objects = picked.Cast<UnityObject>().ToArray();
            if (picked.Count > 0)
                Selection.activeGameObject = picked[0];

            return Response.Success(
                "Selected " + picked.Count + " object(s)" + (missing.Count > 0 ? ", " + missing.Count + " not found" : "") + ".",
                new
                {
                    selected = picked.Select(g => new { instanceId = ObjectIdHelper.GetSerializableId(g), name = g.name }).ToList(),
                    notFound = missing
                });
        }
    }
}
