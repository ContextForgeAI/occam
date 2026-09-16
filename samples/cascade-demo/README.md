# Cascade demo — one call, five steps

Shows the progressive page reader without an MCP client: selftest (no network) then an optional
live run against a public URL.

## Selftest (no workers required)

```bash
dotnet build src/FFOccamMcp.Core -c Release
dotnet run --project src/FFOccamMcp.Core -c Release --no-build -- cascade selftest
```

Expect:

```
CASCADE_SELFTEST_OK
```

That marker means: HTTP success skips browser, HTTP failure degrades to browser, HTTP timeout
marks `partial` + `omitted`, invalid URL/budget are typed failures, and `mode=advanced` emits a hint.

## Live run (needs OCCAM_HOME + workers)

```bash
export OCCAM_HOME="$(pwd)"   # or set in PowerShell
dotnet run --project src/FFOccamMcp.Core -c Release --no-build -- \
  cascade run --url=https://example.com --task=example --budget=512
```

JSON on stdout includes `steps[]` and optional `omitted[]`. Exit 0 only when `ok:true`.

## MCP

With `OCCAM_PROFILE=minimal` the host exposes a single tool: `occam(url, task?, budget?)`.
See [ADR-0017](../../docs/adr/0017-cascade-facade.md) and [docs/tools/occam.md](../../docs/tools/occam.md).
