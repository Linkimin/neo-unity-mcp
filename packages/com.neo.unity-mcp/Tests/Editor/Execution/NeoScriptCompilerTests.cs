using Neo.UnityMcp.Execution;
using NUnit.Framework;

namespace Neo.UnityMcp.Tests.Execution
{
    public sealed class NeoScriptCompilerTests
    {
        private const string GoodCode = @"
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CompilerProbe
{
    public static int Count()
    {
        var go = new GameObject(""compiler_probe"");
        var list = new List<int> { 1, 2, 3 };
        return list.Where(x => x > 1).Count();
    }
}";

        [Test]
        public void Compile_GenericsAndLinqAndUnity_Succeeds()
        {
            var compiler = new NeoScriptCompiler(new ReferenceSetBuilder());

            var result = compiler.Compile(GoodCode);

            Assert.That(result.Success, Is.True, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assembly, Is.Not.Null);
        }

        [Test]
        public void Compile_IdenticalCode_ReturnsSameCachedAssembly()
        {
            var compiler = new NeoScriptCompiler(new ReferenceSetBuilder());

            var first = compiler.Compile(GoodCode);
            var second = compiler.Compile(GoodCode);

            Assert.That(first.Success, Is.True);
            Assert.That(second.FromCacheHit, Is.True);
            Assert.That(second.Assembly, Is.SameAs(first.Assembly));
        }

        [Test]
        public void Compile_CountsUniqueAssemblies_NotCacheHits()
        {
            var compiler = new NeoScriptCompiler(new ReferenceSetBuilder());

            compiler.Compile(GoodCode);                                              // unique -> 1
            compiler.Compile("public static class A { public static int X() => 1; }"); // unique -> 2
            compiler.Compile(GoodCode);                                              // cache hit -> still 2

            Assert.That(compiler.UniqueCompileCount, Is.EqualTo(2));
        }

        [Test]
        public void Compile_InvalidCode_ReturnsDiagnostics()
        {
            var compiler = new NeoScriptCompiler(new ReferenceSetBuilder());

            var result = compiler.Compile("public class Broken { this is not valid C# }");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Diagnostics, Is.Not.Empty);
        }
    }
}
