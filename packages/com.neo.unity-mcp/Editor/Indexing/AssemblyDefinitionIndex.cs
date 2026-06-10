// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;

namespace Neo.UnityMcp.Indexing
{
    // Authoritative set of the PROJECT's (editor) assemblies via CompilationPipeline — names and
    // compiled output paths. "Project" = code under Assets/ plus Embedded/Local packages; Unity
    // Registry/BuiltIn/Git packages are excluded (CompilationPipeline returns all ~115 of them,
    // which would flood the namespace index and risk ambiguous usings).
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
                if (!IsProjectAssembly(assembly))
                    continue;

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

        // Project = under Assets/ (no owning package) or an Embedded/Local package.
        // External (Registry/BuiltIn/Git) packages are not "project" code.
        private static bool IsProjectAssembly(Assembly assembly)
        {
            var sources = assembly.sourceFiles;
            if (sources == null || sources.Length == 0)
                return false;

            PackageInfo package;
            try
            {
                package = PackageInfo.FindForAssetPath(sources[0]);
            }
            catch
            {
                return false;
            }

            if (package == null)
                return true; // under Assets/

            return package.source == PackageSource.Embedded || package.source == PackageSource.Local;
        }
    }
}
