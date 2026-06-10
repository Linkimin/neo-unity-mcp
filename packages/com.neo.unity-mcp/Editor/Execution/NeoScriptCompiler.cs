// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Neo.UnityMcp.Execution
{
    // Roslyn replacement for Funplay's CodeDom execute_code compile step.
    // Compiles a full C# compilation unit in memory; identical source returns the same
    // cached Assembly (no recompile, no assembly churn). Curated references via ReferenceSetBuilder.
    //
    // CACHE IS INTENTIONALLY UNBOUNDED. In Mono (Unity's editor runtime) assemblies loaded via
    // Assembly.Load(byte[]) are NEVER unloaded (no collectible AssemblyLoadContext), so evicting a
    // cache entry does not reclaim memory — and worse, re-running evicted code would recompile and
    // load a *new* assembly, increasing the leak. So a size cap would backfire. The cache's job is
    // to AVOID that leak for repeated identical code. For genuinely unique snippets the per-compile
    // assembly cost is inherent to in-process compilation; the real fix is recycling an
    // out-of-process worker (broker, roadmap v0.5), not capping this dictionary.
    internal sealed class NeoScriptCompiler
    {
        private readonly Dictionary<string, Assembly> _cache = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        private readonly ReferenceSetBuilder _references;

        public NeoScriptCompiler(ReferenceSetBuilder references)
        {
            _references = references ?? throw new ArgumentNullException(nameof(references));
        }

        public CompiledScript Compile(string code)
        {
            var key = Sha256(code ?? string.Empty);
            if (_cache.TryGetValue(key, out var cached))
                return CompiledScript.FromCache(cached);

            var tree = CSharpSyntaxTree.ParseText(code ?? string.Empty);
            // Regular compilation (not script): usings are injected textually by ScriptExecutionTool,
            // because CSharpCompilationOptions.WithUsings only applies to SourceCodeKind.Script.
            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

            var compilation = CSharpCompilation.Create(
                "NeoScript_" + key.Substring(0, 8),
                new[] { tree },
                _references.Build(),
                options);

            using (var ms = new MemoryStream())
            {
                var emit = compilation.Emit(ms);
                if (!emit.Success)
                    return CompiledScript.Failed(emit.Diagnostics);

                var assembly = Assembly.Load(ms.ToArray());
                _cache[key] = assembly; // identical source is never recompiled / never spawns a new assembly
                return CompiledScript.Ok(assembly);
            }
        }

        private static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
