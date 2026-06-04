// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Neo.UnityMcp.Indexing;

namespace Neo.UnityMcp.Execution
{
    // Builds the Roslyn MetadataReference set for compiling user snippets.
    //
    // Strategy: reference the project's ACTUAL runtime assembly set — every loaded,
    // file-backed assembly in the AppDomain (mono mscorlib/System + netstandard facade +
    // UnityEngine.* + UnityEditor.* + project ScriptAssemblies + packages), deduped by
    // simple name.
    //
    // Why not a standalone netstandard 2.1 reference assembly (as for mcs/CodeDom): Unity's
    // assemblies are built against mono mscorlib. Referencing a separate netstandard ref gives
    // System.Object a different identity, so implicit conversions (e.g. GameObject ->
    // UnityEngine.Object -> System.Object) fail with CS0012. Roslyn — unlike mcs — references
    // nothing implicitly, so feeding it exactly the loaded set is conflict-free (the netstandard
    // facade type-forwards into mscorlib rather than re-defining types).
    //
    // Project assemblies come from the authoritative AssemblyDefinitionIndex (Task 5); the
    // loaded domain supplies the BCL/Unity/package surface. Deduped by simple name.
    internal sealed class ReferenceSetBuilder
    {
        public IReadOnlyList<MetadataReference> Build()
        {
            return ResolvePaths()
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
                .ToList();
        }

        // Deduped assembly file paths for the reference set. Kept separate from Build() so it
        // can be inspected/tested without depending on Roslyn types, and reused for diagnostics.
        public IReadOnlyList<string> ResolvePaths()
        {
            var paths = new List<string>();
            var seenSimpleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Authoritative project assemblies from the metadata index (Task 5), added first so
            // they are present even if not currently loaded into the AppDomain.
            foreach (var outputPath in AssemblyDefinitionIndex.ProjectOutputPaths)
            {
                if (string.IsNullOrEmpty(outputPath) || !File.Exists(outputPath))
                    continue;

                var simpleName = Path.GetFileNameWithoutExtension(outputPath);
                if (seenSimpleNames.Add(simpleName))
                    paths.Add(outputPath);
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                    continue;

                string name;
                string location;
                try
                {
                    name = assembly.GetName().Name;
                    location = assembly.Location;
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrEmpty(location) || !File.Exists(location))
                    continue;

                if (string.IsNullOrEmpty(name))
                    name = Path.GetFileNameWithoutExtension(location);

                if (!seenSimpleNames.Add(name))
                    continue; // dedup by assembly simple name

                paths.Add(location);
            }

            return paths;
        }
    }
}
