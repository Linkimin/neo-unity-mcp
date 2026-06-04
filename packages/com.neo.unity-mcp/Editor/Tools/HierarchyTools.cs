// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using System.Linq;
using Neo.UnityMcp.Registry;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Neo.UnityMcp.Tools
{
    [NeoToolProvider("Hierarchy")]
    internal static class HierarchyTools
    {
        [NeoTool("get_hierarchy", "Scene hierarchy tree (roots + children to a depth) with instance ids.")]
        [ReadOnlyTool]
        public static object GetHierarchy(
            [ToolParam("Max child depth (default 3).", Required = false)] int maxDepth = 3,
            [ToolParam("Include inactive objects (default true).", Required = false)] bool includeInactive = true)
        {
            maxDepth = Mathf.Clamp(maxDepth, 1, 12);
            var scenes = new List<object>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid())
                    continue;

                var roots = scene.isLoaded ? scene.GetRootGameObjects() : Array.Empty<GameObject>();
                scenes.Add(new
                {
                    name = scene.name,
                    path = scene.path,
                    isLoaded = scene.isLoaded,
                    rootCount = roots.Length,
                    roots = roots
                        .Where(r => includeInactive || r.activeInHierarchy)
                        .Select(r => Node(r, 1, maxDepth, includeInactive))
                        .ToList()
                });
            }

            return Response.Success(scenes.Count + " scene(s).", new { sceneCount = scenes.Count, scenes });
        }

        [NeoTool("get_game_object_info", "Full info for one GameObject (components + children) resolved by id, name, or path.")]
        [ReadOnlyTool]
        public static object GetGameObjectInfo(
            [ToolParam("Instance id, name, or path.")] string target,
            [ToolParam("Resolution method (default by_id_or_name_or_path).", Required = false)] string find_method = null)
        {
            var go = ObjectsHelper.FindObject(target, find_method);
            if (go == null)
                return Response.Error("GAME_OBJECT_NOT_FOUND", new { target, find_method });

            return Response.Success("GameObject info.", GameObjectSerializer.Describe(go, includeComponents: true, includeChildren: true));
        }

        [NeoTool("find_game_objects", "Find scene GameObjects by name substring and/or exact tag.")]
        [ReadOnlyTool]
        public static object FindGameObjects(
            [ToolParam("Name substring (case-insensitive). Omit to match any.", Required = false)] string nameQuery = null,
            [ToolParam("Exact tag to match. Omit to ignore.", Required = false)] string tag = null,
            [ToolParam("Include inactive objects (default true).", Required = false)] bool includeInactive = true,
            [ToolParam("Max results (default 50).", Required = false)] int limit = 50)
        {
            limit = Mathf.Clamp(limit, 1, 500);

            IEnumerable<GameObject> candidates = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go != null && go.scene.IsValid());

            if (!includeInactive)
                candidates = candidates.Where(go => go.activeInHierarchy);

            if (!string.IsNullOrWhiteSpace(nameQuery))
            {
                var q = nameQuery.Trim();
                candidates = candidates.Where(go => go.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (!string.IsNullOrWhiteSpace(tag))
            {
                var t = tag.Trim();
                candidates = candidates.Where(go => go.tag == t);
            }

            var matches = candidates.Take(limit).ToList();
            return Response.Success(matches.Count + " match(es).", new
            {
                count = matches.Count,
                items = GameObjectSerializer.DescribeMany(matches)
            });
        }

        private static object Node(GameObject go, int depth, int maxDepth, bool includeInactive)
        {
            List<object> children = null;
            if (depth < maxDepth)
            {
                children = new List<object>();
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    var child = go.transform.GetChild(i).gameObject;
                    if (includeInactive || child.activeInHierarchy)
                        children.Add(Node(child, depth + 1, maxDepth, includeInactive));
                }
            }

            return new
            {
                instanceId = ObjectIdHelper.GetSerializableId(go),
                name = go.name,
                activeSelf = go.activeSelf,
                childCount = go.transform.childCount,
                children
            };
        }
    }
}
