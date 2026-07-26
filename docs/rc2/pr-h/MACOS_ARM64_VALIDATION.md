# macOS ARM64 validation package

Target: Mac mini M1/M4-class. Local Ollama-compatible workflows may consume Occam over MCP stdio; this
package validates Occam itself, not a specific Ollama model.

## Prerequisites

1. Checkout at base commit `a535705c03a9bf483cbbb23aefe8c4e60cf7b48f` or the owner-approved RC.2 tree.
2. `OCCAM_HOME` set to the repo root.
3. Node.js + `dotnet` SDK capable of Native AOT for `osx-arm64`.
4. Workers installed (`./scripts/occam-doctor.sh` or equivalent).

## Build the artifact on macOS

```bash
export OCCAM_HOME="$(pwd)"
dotnet publish src/FFOccamMcp.Core/FFOccamMcp.Core.csproj -c Release -r osx-arm64 -o artifacts/rc2/osx-arm64
chmod +x artifacts/rc2/osx-arm64/OccamMcp.Core
shasum -a 256 artifacts/rc2/osx-arm64/OccamMcp.Core
```

Record RID, file name, size, SHA-256, build command, and timestamp into
`artifacts/rc2/manifest.json` (see [RC2_RELEASE_ARTIFACTS.md](RC2_RELEASE_ARTIFACTS.md)).

## Automated helper

```bash
export OCCAM_HOME="$(pwd)"
export OCCAM_RC2_HOST="$(pwd)/artifacts/rc2/osx-arm64/OccamMcp.Core"
# optional: export RC2_EXPECTED_SHA256='<hash from manifest>'
chmod +x scripts/rc2-remote-macos-arm64.sh
./scripts/rc2-remote-macos-arm64.sh
```

Outputs land in `artifacts/rc2/remote-macos/` (gitignored).

## Manual checklist

| Step | Expected |
|---|---|
| Artifact verification | SHA-256 matches manifest entry |
| Executable bit | `chmod +x` applied; binary launches |
| Launch | `OCCAM_HOME=… OCCAM_BANNER=0 $OCCAM_RC2_HOST` over stdio MCP, or launcher |
| MCP initialize | `initialize` + `notifications/initialized` succeed |
| `tools/list` | Includes `occam_digest`, `occam_probe`, `occam_transcode` |
| Digest native array | `urls: ["https://example.com/"]` accepted |
| Public terminology page | Probe/transcode of a public auth-terminology page is not a hard login FP |
| Focus/fragment | Focused transcode returns structural `focus` / non-empty markdown when content fits |
| Constrained budget | Low `max_tokens` does not silently expand; completeness reflects truncation honestly |
| Semantic envelope | Additive fields present beside legacy `ok` / `confidence` / `focusMatched` |
| Lifecycle self/diagnose | CLI returns `ok:true` identity JSON; diagnose is read-only |
| Shutdown/orphan | Exact-child stop; no leftover `OccamMcp` hosts after soak/helper |
| Offline gates | `--pr-g` and `--regression` both `"failed":0` |

## Log collection

Collect:

- `artifacts/rc2/remote-macos/`
- host SHA-256
- `uname -a`
- soak or helper stdout/stderr
- any MCP client logs (Cursor/Ollama wrapper) without secrets

## Failure reporting

Use [REMOTE_VALIDATION_RESULT_TEMPLATE.md](REMOTE_VALIDATION_RESULT_TEMPLATE.md).

## Status in PR-H

**Prepared, not executed** on remote macOS hardware during this PR-H session.
