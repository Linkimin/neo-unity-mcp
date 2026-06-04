// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System.Text;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Neo.UnityMcp.Tools
{
    // GameObject path + resolution helpers shared by the tool surface.
    internal static class ObjectsHelper
    {
        public static string GetGameObjectPath(GameObject go)
        {
            if (go == null)
                return null;

            var sb = new StringBuilder("/" + go.name);
            var t = go.transform.parent;
            while (t != null)
            {
                sb.Insert(0, "/" + t.name);
                t = t.parent;
            }
            return sb.ToString();
        }

        // Resolve a token to a GameObject. find_method: by_id, by_name, by_path, or
        // by_id_or_name_or_path (default).
        public static GameObject FindObject(string token, string findMethod = null)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            token = token.Trim();
            var method = string.IsNullOrWhiteSpace(findMethod) ? "by_id_or_name_or_path" : findMethod.Trim();

            switch (method)
            {
                case "by_id":
                    return AsGameObject(ObjectIdHelper.ToObject(token));
                case "by_name":
                    return GameObject.Find(token);
                case "by_path":
                    return GameObject.Find(token);
                default:
                    return AsGameObject(ObjectIdHelper.ToObject(token)) ?? GameObject.Find(token);
            }
        }

        private static GameObject AsGameObject(UnityObject obj)
        {
            switch (obj)
            {
                case null:
                    return null;
                case GameObject go:
                    return go;
                case Component component:
                    return component.gameObject;
                default:
                    return null;
            }
        }
    }
}
