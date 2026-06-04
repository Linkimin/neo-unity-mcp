using Neo.UnityMcp.Indexing;
using NUnit.Framework;

namespace Neo.UnityMcp.Tests.Indexing
{
    public sealed class NamespaceIndexTests
    {
        [Test]
        public void AssemblyIndex_IncludesPackageAndTestAssemblies()
        {
            var names = AssemblyDefinitionIndex.ProjectAssemblyNames;

            Assert.That(names, Does.Contain("Neo.UnityMcp.Editor"));
            Assert.That(names, Does.Contain("Neo.UnityMcp.Editor.Tests"));
        }

        [Test]
        public void ProjectNamespaces_ComeFromMetadata_IncludePackageNamespaces()
        {
            // These namespaces live in the compiled Neo.UnityMcp.Editor assembly (a UPM package),
            // not under dev-project/Assets — so finding them proves a metadata source, not a .cs scan.
            // Only namespaces with PUBLIC (exported) types appear — internal-only namespaces such as
            // Neo.UnityMcp.Indexing are intentionally excluded (snippets can't use internal types).
            var namespaces = ProjectSymbolIndex.ProjectNamespaces;

            Assert.That(namespaces, Does.Contain("Neo.UnityMcp.Execution"));
            Assert.That(namespaces, Does.Contain("Neo.UnityMcp.Registry"));
        }

        [Test]
        public void ProjectNamespaces_AreCached_SameInstanceUntilInvalidated()
        {
            var first = ProjectSymbolIndex.ProjectNamespaces;
            var second = ProjectSymbolIndex.ProjectNamespaces;
            Assert.That(second, Is.SameAs(first), "cached: O(1) on repeat");

            ProjectSymbolIndex.Invalidate();
            var rebuilt = ProjectSymbolIndex.ProjectNamespaces;
            Assert.That(rebuilt, Is.Not.SameAs(first));
            Assert.That(rebuilt, Is.EquivalentTo(first));
        }

        [Test]
        public void Usings_IncludeDefaultsAndProjectNamespaces()
        {
            var usings = NamespaceUsingsCache.GetUsings();

            Assert.That(usings, Does.Contain("System"));
            Assert.That(usings, Does.Contain("UnityEngine"));
            Assert.That(usings, Does.Contain("UnityEditor"));
            Assert.That(usings, Does.Contain("Neo.UnityMcp.Execution"));
        }
    }
}
