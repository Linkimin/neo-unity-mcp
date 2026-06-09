# Changelog

All notable changes to this package are documented here. Format follows
[Keep a Changelog](https://keepachangelog.com/); this package adheres to SemVer.

## [0.1.0] — 2026-06-10

First release — Neo Unity MCP v1 (walking skeleton of the bridge). A standalone, drop-in
MCP server for the Unity Editor: same `:8765` transport and tool names as the workflow it
replaces, but a new Roslyn execution core, job system, and metadata-based indexing.

### Added
- **Transport & protocol** — HTTP JSON-RPC server on `:8765` (`initialize` / `tools/list` /
  `tools/call`), editor main-thread marshalling, domain-reload-aware lifecycle.
- **Tool registry** — attribute-based discovery (`[NeoToolProvider]` / `[NeoTool]` / `[ToolParam]`)
  with constructor/member DI (`[Inject]`); tool-body failures map to `isError`, protocol problems
  to JSON-RPC errors.
- **Execution core (Roslyn)** — `execute_code`: in-memory compile with SHA-256 hash cache, a
  curated reference set (no `System.Object` identity conflicts), a safety blocklist, and the
  `INeoCommand` template (auto-Undo, change tracking, structured logs).
- **Namespace index** — default `using`s and the compile reference set are sourced from assembly
  metadata (no per-call `.cs` scan), invalidated on compile / reload / project change.
- **Jobs** — file-backed job manager (`Library/NeoMcp/jobs/`) surviving domain reloads;
  `get_job_status` / `cancel_job` / `get_job_logs`.
- **Tool surface (~28)** — compilation (`request_recompile`, `wait_for_compilation`,
  `get_compilation_errors`, `get_reload_recovery_status`), editor state & selection, console logs,
  hierarchy / GameObject / find, scene info, packages, play mode, screenshots, menu items,
  input simulation (New Input System), and `run_tests` (runs as a job — **no timeouts**).

### Notes
- **Self-contained:** Roslyn and its support libraries are vendored under `Editor/Plugins/`;
  consumers do **not** need NuGetForUnity.
- **Optional dependency:** Unity Input System is wired via `versionDefines` — the package compiles
  on legacy-only projects (input simulation reports `INPUT_BACKEND_UNSUPPORTED` there).
- Adapts editor plumbing from Funplay MCP for Unity (MIT) — see
  [`THIRD_PARTY_NOTICES.md`](../../THIRD_PARTY_NOTICES.md).
