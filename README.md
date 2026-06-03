# Neo Unity MCP

> A robust MCP bridge for AI-assisted Unity Editor automation, scene inspection, Roslyn-based
> tool execution, and long-running editor workflows.

**Status:** alpha / work in progress.

Neo Unity MCP runs a [Model Context Protocol](https://modelcontextprotocol.io) server inside
the Unity Editor, giving AI agents a small, reliable surface to:

- **Execute C# in-process via Roslyn** — `execute_code` compiles and runs snippets against the
  live editor (generics, LINQ, project types) with a clean, conflict-free reference set.
- **Inspect & observe** — scene hierarchy, GameObjects, components, selection, console logs,
  compilation errors, editor/play state.
- **Drive play-mode workflows** — enter/exit play, screenshots, input simulation.
- **Run long operations without timeouts** — tests and other long tasks run as **jobs**
  (`run_tests` → `job id`; poll `get_job_status` / `get_job_logs` / `cancel_job`).

Designed for large, heavy Unity projects: a metadata-based namespace index (no per-call source
scans), domain-reload-aware operation, and a curated tool set instead of dozens of wrappers.

## Install (UPM)

```
https://github.com/Linkimin/neo-unity-mcp.git?path=packages/com.neo.unity-mcp
```

## License

MIT — see [`LICENSE`](LICENSE). Third-party components (including some editor plumbing adapted
from Funplay MCP for Unity, and vendored Roslyn binaries) are listed in
[`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
