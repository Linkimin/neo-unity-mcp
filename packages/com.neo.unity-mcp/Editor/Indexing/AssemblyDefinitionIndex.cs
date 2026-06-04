// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Compilation;

namespace Neo.UnityMcp.Indexing
{
    // Authoritative set of project (editor) assemblies via CompilationPipeline — names and
    // compiled output paths. Replaces ad-hoc .cs scanning / "whatever happens to be loaded".
    // O(assemblies), cached; reset by IndexInvalidation on compile/reload/project change.
    internal static class AssemblyDefinitionIndex
    {
        private static HashSet<string> _names;
        private static List<string> _outputPaths;

        public static IReadOnlyCollection<string> ProjectAssemblyNames
        {
            get { EnsureBuilt(); return _names; }
        }

        public static IReadOnlyList<string> ProjectOutputPaths
        {
            get { EnsureBuilt(); return _outputPaths; }
        }

        public static void Invalidate()
        {
            _names = null;
            _outputPaths = null;
        }

        private static void EnsureBuilt()
        {
            if (_names != null)
                return;

            var names = new HashSet<string>(StringComparer.Ordinal);
            var outputs = new List<string>();

            foreach (var assembly in CompilationPipeline.GetAssemblies(AssembliesType.Editor))
            {
                names.Add(assembly.name);
                try
                {
                    outputs.Add(Path.GetFullPath(assembly.outputPath));
                }
                catch
                {
                }
            }

            _names = names;
            _outputPaths = outputs;
        }
    }
}
