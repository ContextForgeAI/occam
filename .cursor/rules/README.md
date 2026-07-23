# Cursor rules (project)

Cursor loads `.mdc` files from this folder automatically when **FFOccamMCP** is the opened workspace.

| Rule | When it applies |
|------|-----------------|
| `occam-l0-core.mdc` | **Always** — scope, task order, English |
| `documentation-sync.mdc` | Editing `docs/`, `MCP_API_SPEC.md`, `README.md`, … |
| `csharp-host.mdc` | Editing `src/**/*.cs` |
| `node-workers.mdc` | Editing `workers/**/*` |
| `l0-gate.mdc` | Editing `benchmarks/`, `corpora/` |
| `quality-audit.mdc` | Editing `workers/`, compile/probe/digest/token paths in `src/` |

**Human entry:** [AGENTS.md](../../AGENTS.md) · local `docs-internal/12-cursor-for-contributors.md` (gitignored engineering notes)

**MCP dogfood:** copy [mcp.json.example](mcp.json.example) → `mcp.json`; host via [launch-mcp-host.mjs](../../scripts/launch-mcp-host.mjs) (no machine paths).
