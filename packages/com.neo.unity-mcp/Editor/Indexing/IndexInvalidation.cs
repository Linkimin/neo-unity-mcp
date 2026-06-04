// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using UnityEditor;
using UnityEditor.Compilation;

namespace Neo.UnityMcp.Indexing
{
    // Resets the metadata indexes when the project's assemblies may have changed:
    // after a compile, after a domain reload, or on a project (asset) change. The static
    // ctor also re-runs on every domain load (InitializeOnLoad), so caches start clean.
    [InitializeOnLoad]
    internal static class IndexInvalidation
    {
        static IndexInvalidation()
        {
            InvalidateAll();
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            AssemblyReloadEvents.afterAssemblyReload += InvalidateAll;
            EditorApplication.projectChanged += InvalidateAll;
        }

        private static void OnCompilationFinished(object _)
        {
            InvalidateAll();
        }

        private static void InvalidateAll()
        {
            AssemblyDefinitionIndex.Invalidate();
            ProjectSymbolIndex.Invalidate();
            NamespaceUsingsCache.Invalidate();
        }
    }
}
