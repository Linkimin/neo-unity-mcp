// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System.Collections.Generic;
using Neo.UnityMcp.Registry;
using UnityEngine.SceneManagement;

namespace Neo.UnityMcp.Tools
{
    [NeoToolProvider("Scene")]
    internal static class SceneQueryTools
    {
        [NeoTool("get_scene_info", "Active scene + all loaded scenes (name, path, loaded/dirty state, root count).")]
        [ReadOnlyTool]
        public static object GetSceneInfo()
        {
            var active = SceneManager.GetActiveScene();
            var scenes = new List<object>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid())
                    continue;

                scenes.Add(new
                {
                    name = scene.name,
                    path = scene.path,
                    buildIndex = scene.buildIndex,
                    isLoaded = scene.isLoaded,
                    isDirty = scene.isDirty,
                    rootCount = scene.isLoaded ? scene.rootCount : 0
                });
            }

            return Response.Success("Scene info.", new
            {
                activeScene = active.name,
                activeScenePath = active.path,
                sceneCount = SceneManager.sceneCount,
                scenes
            });
        }
    }
}
