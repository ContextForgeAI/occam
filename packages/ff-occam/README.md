# ff-occam

Primary npm package for **FF Occam** — the local-first MCP host that turns a URL
into compact, source-linked Markdown or an explicit typed failure.

> npm is an RC channel, not the guarded GA install path. The canonical release
> installer remains the signed GitHub Release bootstrap documented in
> [`INSTALL.md`](../../INSTALL.md).

```bash
npx ff-occam@1.0.0

# Optional global CLI aliases: ff-occam and occam (MCP host only)
npm install -g ff-occam@1.0.0
ff-occam --help
```

Operator commands (`connect`, `doctor`, `disconnect`, …) are **not** part of this
npm package. Use the guarded installer in [`INSTALL.md`](../../INSTALL.md), then
`occam connect`.

This package is a thin CLI wrapper around
[`@ff-occam/mcp`](../occam-mcp) at the same version pin. Use `OCCAM_HOME` to
point at a local checkout and skip release download. Runtime `tools/list` is
authoritative: the default `reader` profile exposes 8 tools; `full` exposes 15;
opt-in flags can add more.

See [docs/getting-started.md](../../docs/getting-started.md).
