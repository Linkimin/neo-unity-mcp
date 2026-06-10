# Third-Party Notices

Neo Unity MCP is licensed under the MIT License (see [`LICENSE`](LICENSE)). It includes
or adapts third-party components listed below. Each remains under its own license.

---

## Funplay MCP for Unity

- **License:** MIT — see [`licenses/FUNPLAY-MIT-LICENSE.txt`](licenses/FUNPLAY-MIT-LICENSE.txt)
- **Copyright:** © 2026 Funplay
- **Source:** https://github.com/FunplayAI/funplay-unity-mcp

Neo Unity MCP **adapts a subset of code** from Funplay MCP for Unity — primarily editor
plumbing: the MCP HTTP transport, JSON-RPC request/response handling, the attribute-based
tool registry, and some builtin tool implementations.
The execution core (Roslyn), the job system, the namespace/symbol index, and the overall
tool surface are new and specific to Neo Unity MCP.

Files that adapt Funplay code carry a per-file header:
`// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.`

---

## Roslyn (.NET Compiler Platform) — vendored

- **Packages:** `Microsoft.CodeAnalysis`, `Microsoft.CodeAnalysis.CSharp`
- **License:** MIT — © .NET Foundation and Contributors
- **Source:** https://github.com/dotnet/roslyn

Vendored as managed DLLs under `packages/com.neo.unity-mcp/Editor/Plugins/` to provide the
in-process C# compilation used by the execution core.

## .NET runtime support libraries — vendored

- **Packages:** `System.Collections.Immutable`, `System.Reflection.Metadata`,
  `System.Text.Encoding.CodePages`, `System.Threading.Tasks.Extensions`
- **License:** MIT — © .NET Foundation and Contributors
- **Source:** https://github.com/dotnet/runtime

Vendored as managed DLLs alongside Roslyn (transitive dependencies not already provided by
the Unity runtime).
