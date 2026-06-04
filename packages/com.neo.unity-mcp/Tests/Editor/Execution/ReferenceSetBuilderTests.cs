using System.IO;
using System.Linq;
using Neo.UnityMcp.Execution;
using NUnit.Framework;

namespace Neo.UnityMcp.Tests.Execution
{
    public sealed class ReferenceSetBuilderTests
    {
        [Test]
        public void ResolvePaths_IncludesCoreBclUnityAndProject_NoDuplicateSimpleNames()
        {
            var paths = new ReferenceSetBuilder().ResolvePaths();
            var names = paths
                .Select(Path.GetFileNameWithoutExtension)
                .ToList();

            Assert.That(names, Does.Contain("mscorlib"), "core BCL present (Unity assemblies bind to it)");
            Assert.That(names.Any(n => n == "UnityEngine.CoreModule"), Is.True, "UnityEngine present");
            Assert.That(names.Any(n => n.StartsWith("UnityEditor")), Is.True, "UnityEditor present");
            Assert.That(names, Does.Contain("Neo.UnityMcp.Editor"), "package assembly present so snippets can use INeoCommand");

            Assert.That(names.Count, Is.EqualTo(names.Distinct().Count()), "no duplicate simple names");
        }
    }
}
