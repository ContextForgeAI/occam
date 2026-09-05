# ff-occam

Primary npm package for **FF Occam** — an experimental launcher for the
local-first MCP host that turns a URL into compact, source-linked Markdown
or an explicit typed failure.

> npm is an RC channel, not the guarded GA install path. This package starts
> the MCP host only. It does **not** install the `occam` operator CLI
> (`connect`, `doctor`, `smoke`, …). For host + PATH + connect in one step,
> use the signed bootstrap in [`INSTALL.md`](../../INSTALL.md).

```bash
# MCP-only trial (stdio). Does not provide `occam connect`.
npx ff-occam@1.0.1

# Optional global MCP launcher. Command name is `ff-occam`, not `occam`.
# On Windows, `npm bin -g` must be on PATH or PowerShell will not see it.
npm install -g ff-occam@1.0.1
ff-occam --help
```

Full install (recommended):

```powershell
# Windows
irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
```

```bash
# Linux x64 / macOS Apple Silicon
curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash
```

This package is a thin CLI wrapper around
[`@ff-occam/mcp`](../occam-mcp) at the same version pin. Use `OCCAM_HOME` to
point at a local checkout and skip release download. Runtime `tools/list` is
authoritative: the default `reader` profile exposes 8 tools; `full` exposes 15;
opt-in flags can add more.

See [docs/getting-started.md](../../docs/getting-started.md).
