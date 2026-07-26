# Linux x64 Hermes / OpenRouter validation package

## Integration boundary (important)

Hermes and OpenRouter are **external** MCP clients/gateways. Occam does **not** invent Hermes APIs in
RC.2. This package validates:

- linux-x64 Native AOT host
- stdio MCP contract
- lifecycle identity CLI
- exact-child shutdown via the Occam launcher
- semantic/digest/access/focus behaviors already proven offline

It does **not** claim a Hermes-native lease/callback API.

Reference notes: [../pr-g/HERMES_LIFECYCLE_NOTES.md](../pr-g/HERMES_LIFECYCLE_NOTES.md).

## Prerequisites

1. Linux x64 host with `dotnet` Native AOT support.
2. Repo checkout + `OCCAM_HOME`.
3. Node.js workers via doctor/install scripts.
4. Optional: Hermes or OpenRouter configured as a generic MCP stdio client pointing at Occam.

## Build the artifact on Linux

```bash
export OCCAM_HOME="$(pwd)"
dotnet publish src/FFOccamMcp.Core/FFOccamMcp.Core.csproj -c Release -r linux-x64 -o artifacts/rc2/linux-x64
chmod +x artifacts/rc2/linux-x64/OccamMcp.Core
sha256sum artifacts/rc2/linux-x64/OccamMcp.Core
```

## Automated helper

```bash
export OCCAM_HOME="$(pwd)"
export OCCAM_RC2_HOST="$(pwd)/artifacts/rc2/linux-x64/OccamMcp.Core"
# optional: export RC2_EXPECTED_SHA256='<hash from manifest>'
chmod +x scripts/rc2-remote-linux-x64.sh
./scripts/rc2-remote-linux-x64.sh
```

Outputs: `artifacts/rc2/remote-linux/`.

## Neutral MCP client configuration

Example stdio MCP server entry (client-specific wrapping may differ):

```json
{
  "mcpServers": {
    "occam": {
      "command": "/absolute/path/to/artifacts/rc2/linux-x64/OccamMcp.Core",
      "args": [],
      "env": {
        "OCCAM_HOME": "/absolute/path/to/repo",
        "OCCAM_BANNER": "0"
      }
    }
  }
}
```

Launcher alternative:

```bash
OCCAM_HOME="$(pwd)" node scripts/launch-mcp-host.mjs
```

The launcher stamps `OCCAM_RUNTIME_ID`, `OCCAM_SESSION_ID`, `OCCAM_PARENT_PID`, and
`OCCAM_PARENT_LABEL`, and forwards termination to the exact child only.

## Checklist

| Step | Expected |
|---|---|
| Artifact verification | SHA-256 matches |
| Launch command | Direct host or `launch-mcp-host.mjs` |
| MCP configuration | Stdio JSON as above; Hermes/OpenRouter use their own MCP server stanza without Occam inventing APIs |
| Environment | `OCCAM_HOME` required; banner off for clean JSON |
| Representative tool calls | probe / digest native array / focused budgeted transcode |
| Lifecycle diagnostics | `lifecycle self`, `lifecycle diagnose` |
| Process-tree checks | Record `pgrep -af OccamMcp` / `pstree` before and after |
| Exact-child shutdown | SIGTERM/launcher stop leaves no orphan Occam host for that runtime id |
| Semantic result verification | Additive envelope fields present; legacy aliases unchanged |
| Offline gates | `--pr-g` and `--regression` green against `OCCAM_RC2_HOST` |

## Environment variables (Occam-owned)

| Variable | Role |
|---|---|
| `OCCAM_HOME` | Worker/script root |
| `OCCAM_BANNER` | Set `0` for clean stdio |
| `OCCAM_RUNTIME_ID` | Stamped by launcher when used |
| `OCCAM_SESSION_ID` | Stamped by launcher when used |
| `OCCAM_PARENT_PID` / `OCCAM_PARENT_LABEL` | Parent identity for diagnostics |
| `OCCAM_RC2_HOST` | Regression harness candidate override |

OpenRouter/Hermes API keys and gateway URLs are **external** and are not Occam RC.2 configuration.

## Log collection

Collect `artifacts/rc2/remote-linux/`, host hash, `uname -a`, process-tree snapshots, and client logs
with secrets redacted.

## Failure reporting

Use [REMOTE_VALIDATION_RESULT_TEMPLATE.md](REMOTE_VALIDATION_RESULT_TEMPLATE.md).

## Status in PR-H

**Prepared, not executed** on remote Linux/Hermes hardware during this PR-H session.
