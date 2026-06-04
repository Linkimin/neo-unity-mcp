// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System.Collections.Generic;

namespace Neo.UnityMcp.Indexing
{
    // Default snippet usings (framework + Unity + Neo execution API) plus all project
    // namespaces from ProjectSymbolIndex. Fed into CSharpCompilationOptions.WithUsings so
    // execute_code snippets can reference project types without explicit using directives.
    // Cached; reset by IndexInvalidation.
    internal static class NamespaceUsingsCache
    {
        private static readonly string[] Defaults =
        {
            "System",
            "System.Collections",
            "System.Collections.Generic",
            "System.Linq",
            "System.Text",
            "System.IO",
            "UnityEngine",
            "UnityEngine.SceneManagement",
            "UnityEditor",
            "UnityEditor.SceneManagement",
            "Neo.UnityMcp.Execution",
        };

        private static string[] _usings;

        public static IReadOnlyList<string> GetUsings()
        {
            if (_usings != null)
                return _usings;

            var list = new List<string>(Defaults);
            foreach (var ns in ProjectSymbolIndex.ProjectNamespaces)
            {
                if (!list.Contains(ns))
                    list.Add(ns);
            }

            _usings = list.ToArray();
            return _usings;
        }

        public static void Invalidate()
        {
            _usings = null;
        }
    }
}
