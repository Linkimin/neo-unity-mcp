// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Neo.UnityMcp.Tools
{
    // Fixed-shape structured payloads for GameObjects/Components (serialized via Newtonsoft).
    // The instanceId field lets agents chain by_id lookups instead of re-resolving by name.
    // (Component property reading is out of this tool group's scope.)
    internal static class GameObjectSerializer
    {
        public static object Describe(GameObject go, bool includeComponents = true, bool includeChildren = false)
        {
            if (go == null)
                return null;

            return new
            {
                instanceId = ObjectIdHelper.GetSerializableId(go),
                name = go.name,
                path = ObjectsHelper.GetGameObjectPath(go),
                activeSelf = go.activeSelf,
                activeInHierarchy = go.activeInHierarchy,
                tag = go.tag,
                layer = LayerMask.LayerToName(go.layer),
                isStatic = go.isStatic,
                scene = go.scene.IsValid() ? go.scene.name : null,
                transform = new
                {
                    position = ToObj(go.transform.position),
                    localPosition = ToObj(go.transform.localPosition),
                    eulerAngles = ToObj(go.transform.eulerAngles),
                    localScale = ToObj(go.transform.localScale)
                },
                components = includeComponents ? DescribeComponents(go) : null,
                childCount = go.transform.childCount,
                children = includeChildren ? DescribeChildren(go) : null
            };
        }

        public static List<object> DescribeMany(IEnumerable<GameObject> gos)
        {
            return gos.Select(go => Describe(go, includeComponents: false)).ToList();
        }

        private static List<object> DescribeComponents(GameObject go)
        {
            var list = new List<object>();
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null)
                {
                    list.Add(new { type = "<missing>", instanceId = "0" });
                    continue;
                }

                list.Add(new
                {
                    instanceId = ObjectIdHelper.GetSerializableId(c),
                    type = c.GetType().Name,
                    fullType = c.GetType().FullName
                });
            }
            return list;
        }

        private static List<object> DescribeChildren(GameObject go)
        {
            var list = new List<object>();
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i).gameObject;
                list.Add(new
                {
                    instanceId = ObjectIdHelper.GetSerializableId(child),
                    name = child.name,
                    activeSelf = child.activeSelf,
                    childCount = child.transform.childCount
                });
            }
            return list;
        }

        private static object ToObj(Vector3 v) => new { x = v.x, y = v.y, z = v.z };
    }
}
