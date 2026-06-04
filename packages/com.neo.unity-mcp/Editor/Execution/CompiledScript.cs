// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Neo.UnityMcp.Execution
{
    // Result of NeoScriptCompiler.Compile: either a usable Assembly (freshly emitted
    // or served from the hash cache) or a list of compiler diagnostics.
    internal sealed class CompiledScript
    {
        public bool Success { get; private set; }
        public bool FromCacheHit { get; private set; }
        public Assembly Assembly { get; private set; }
        public IReadOnlyList<string> Diagnostics { get; private set; } = Array.Empty<string>();

        public static CompiledScript Ok(Assembly assembly)
        {
            return new CompiledScript { Success = true, FromCacheHit = false, Assembly = assembly };
        }

        public static CompiledScript FromCache(Assembly assembly)
        {
            return new CompiledScript { Success = true, FromCacheHit = true, Assembly = assembly };
        }

        public static CompiledScript Failed(IEnumerable<Diagnostic> diagnostics)
        {
            return new CompiledScript
            {
                Success = false,
                Diagnostics = (diagnostics ?? Enumerable.Empty<Diagnostic>())
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString())
                    .ToArray()
            };
        }
    }
}
