# Wave 1 assignment — CAP ID ranges

**Branch:** `docs/site-overhaul`  
**SoT:** current executable code only. Docs = UNTRUSTED.  
**No public doc rewrites in Wave 1.**

| Agent | Target | Report path | CAP ID range |
|-------|--------|-------------|--------------|
| S0 | Runtime / MCP | `docs-audit/subsystems/runtime-mcp.md` | CAP-001 … CAP-049 |
| S1 | occam_transcode | `docs-audit/tools/occam_transcode.md` | CAP-050 … CAP-149 |
| S17 | Network / fetch / proxy | `docs-audit/subsystems/network-fetch-proxy.md` | CAP-150 … CAP-199 |
| S18 | Browser / workers | `docs-audit/subsystems/browser-workers.md` | CAP-200 … CAP-249 |
| S19 | Trust / receipts | `docs-audit/subsystems/trust-receipts.md` | CAP-250 … CAP-299 |
| S20 | Materialization | `docs-audit/subsystems/materialization.md` | CAP-300 … CAP-349 |
| S24 | Config / env | `docs-audit/ENVIRONMENT-VARIABLES.md` + `docs-audit/subsystems/config-env.md` | CAP-350 … CAP-399 |

## Envelope format (return ONLY this to orchestrator)

```
TARGET:
REPORT_PATH:
FILES_INSPECTED_COUNT:
CAPABILITY_COUNT:
CAPABILITY_IDS: CAP-xxx … CAP-yyy (list)
HIDDEN_ADVANCED: (short bullets)
UNRESOLVED: (short bullets)
COMPLETENESS: COMPLETE | NEEDS SECOND PASS | BLOCKED
```

Full evidence stays in the report file. Do not paste the full report into the agent return message.
