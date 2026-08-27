# Experimental features

Features that ship but are **not** default product promises. Experimental ≠ invisible.

**Status vocabulary:** EXPERIMENTAL — enable deliberately; expect sharper edges.

| Feature | Enablement | What it does | Limitations | Why not default |
|---------|------------|--------------|-------------|-----------------|
| **Watch** | `OCCAM_WATCH_MCP=1` | Re-check a URL over time; history chain | No daemon (agent polls); store races; unsigned history ≠ `history_verified` | Costly; persistence/concurrency limits |
| **Crosscheck** | `OCCAM_CONSENSUS_MCP=1` | Multi-source / multi-vantage **comparison** (source agreement) | Verdict is computed, not a “consensus proof”; same-process/egress limits | Expensive (2+ extracts); easy to overread |
| **Batch** | `OCCAM_BATCH_MCP=1` | Queue many URLs | No Receipt v1 on the batch envelope; store races; retention limits | Operator/server mode; not a casual agent default |
| **Failure atlas** | `OCCAM_ATLAS_MCP=1` | Session-local failure telemetry | Not proof a host is a “dead end” | Diagnostic, not a trust layer |
| **Browser interact** | `OCCAM_BROWSER_ACTIONS_MCP=1` | Declarative click/type/scroll then materialize | Max 16 steps; typed text redacted; no raw page JS; never cached | Automation surface; keep off unless needed |
| **Managed acquisition** | Operator-configured providers | Third-party fetch after local failure | Privacy (URL leaves the machine); not a `backend_policy` value; failure never surfaces as the result | Opt-in cost/privacy; local-first default |

## Forbidden readings

- Crosscheck agreement is **not** proof of correctness (“consensus proof” forbidden).  
- Watch `history_verified` requires **every** entry signed and verified; hash-chain integrity alone is a different signal.  
- Managed success may help you read a wall; it does **not** make Occam a CAPTCHA bypass.

## Related

- [Choosing a tool](choosing-a-tool.md)
- [Transports](transports.md) (batch server)
- Tool pages under Reference → Opt-in
