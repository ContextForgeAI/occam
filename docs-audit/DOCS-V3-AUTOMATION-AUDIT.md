# DOCS-V3-AUTOMATION-AUDIT (Phase 8H)

**Branch:** `docs/v3-canonical`  
**Date:** 2026-07-26  
**Scope:** Publicly relevant automatic behaviors — what triggers them, whether users must know, where documented, gaps, and fixes applied.

**Sources:** `docs-audit/AUTOMATIC-BEHAVIORS.md`, `docs-audit/STATE-MODEL.md`, public `docs/`, `docs/handbook/20-automatic-behaviors.md`, code hints.

---

## Summary

| Verdict | Count |
|---------|------:|
| Documented adequately (user/agent can find limits) | 9 |
| Documented with minor discoverability gap | 2 |
| Gap fixed this phase | 1 (handbook index stale “planned” blocked ch 15–27) |
| Residual gap (acceptable / operator-only) | 1 (onboard env merge depth) |

Most high-surprise automatics (key mint, save-always-sign, bypassCSP, cache, connect mutation, profile/exposure model) are now covered in public docs and handbook ch. 20. The largest remaining discoverability hole from **README-only** entry is state/automation depth — reachable via `llms.txt` or docs hub, not first-hop from README.

---

## Behavior matrix

| Behavior | TRIGGER | USER MUST KNOW? | WHERE DOCUMENTED | GAP? | FIX |
|----------|---------|-----------------|------------------|------|-----|
| **Playbook auto resolve / overlay** | `playbook_policy=auto` (default on transcode, digest, watch, batch submit, claim_check, attest, dataset_export) | **Yes** — silent recipe merge changes extract path | [playbooks.md](../docs/playbooks.md) · [handbook/11](../docs/handbook/11-playbooks-resolution.md) · [occam_transcode](../docs/tools/occam_transcode.md) | No | — |
| **Auto signing key mint** | Every MCP host start (`ReceiptSigner.LoadOrCreate`) | **Yes** — CRITICAL disk secret even when `OCCAM_RECEIPTS=off` | [receipts.md](../docs/receipts.md) · [configuration.md](../docs/configuration.md) · [handbook/20](../docs/handbook/20-automatic-behaviors.md) · [handbook/21](../docs/handbook/21-state-and-footprint.md) | No | — |
| **Playbook save always signs** | `occam_playbook_save` success | **Yes** — `OCCAM_RECEIPTS` does not apply | [receipts.md](../docs/receipts.md) · [playbooks.md](../docs/playbooks.md) · [handbook/20](../docs/handbook/20-automatic-behaviors.md) | No | — |
| **Browser escalation** | `http_then_browser` when HTTP unusable (thin/challenge/etc.); **not** on 404/410/public-reference short-circuit | **Yes** — cost/latency + different failure semantics | [acquisition.md](../docs/acquisition.md) · [handbook/05](../docs/handbook/05-acquisition-ladder.md) · [how-occam-works.md](../docs/how-occam-works.md) | Minor — README “difficult pages” linked only to sessions | **Fixed:** README now splits JS-heavy vs login-wall links |
| **Managed escalation** | Both local backends fail on cascade; operator configured provider | **Yes** — third-party URL/content egress; managed failure never surfaces as result | [acquisition.md](../docs/acquisition.md) · [experimental.md](../docs/experimental.md) · [trust/local-first.md](../docs/trust/local-first.md) · [handbook/06](../docs/handbook/06-when-acquisition-is-hard.md) | No | — |
| **Response cache (opt-in)** | `cache_ttl_s > 0` on eligible transcode success | **Yes** — full signed envelope on disk; replays prior bytes | [materialization.md](../docs/materialization.md) · [concepts.md](../docs/concepts.md) · [handbook/08](../docs/handbook/08-structured-differential-output.md) · [handbook/20](../docs/handbook/20-automatic-behaviors.md) | No | — |
| **Session persistence** | `occam session` import/export; `session_profile` on supported tools | **Yes** — secrets on disk; tier differs by tool | [sessions.md](../docs/sessions.md) · [guides/sessions.md](../docs/guides/sessions.md) · [handbook/06](../docs/handbook/06-when-acquisition-is-hard.md) · [handbook/21](../docs/handbook/21-state-and-footprint.md) | No | — |
| **Host config mutation (connect)** | `occam connect` / installer connect step on live-validated hosts | **Yes** — writes host MCP JSON | [connect/automatic.md](../docs/connect/automatic.md) · [mcp-hosts.md](../docs/mcp-hosts.md) · [trust/installation-safety.md](../docs/trust/installation-safety.md) | No | — |
| **Connect backups (`*.occam-bak`)** | Connect adapter before config write | **Yes** — rollback limits on restart-required hosts | [mcp-hosts.md](../docs/mcp-hosts.md) · [trust/installation-safety.md](../docs/trust/installation-safety.md) · [connect/index.md](../docs/connect/index.md) · [handbook/19](../docs/handbook/19-operating-an-install.md) | No | — |
| **Profile filtering (`OCCAM_PROFILE`)** | Host startup / `tools/list` registration | **Yes** — tool subset changes; opt-ins bypass profile | [choosing-a-tool.md](../docs/choosing-a-tool.md) · [configuration.md](../docs/configuration.md) · [handbook/18-exposure.md](../docs/handbook/18-exposure.md) | No | — |
| **Opt-in tool exposure** | `OCCAM_BATCH_MCP`, `OCCAM_WATCH_MCP`, `OCCAM_CONSENSUS_MCP`, `OCCAM_ATLAS_MCP` | **Yes** — not default promises | [experimental.md](../docs/experimental.md) · per-tool pages · [handbook/17](../docs/handbook/17-opt-in-surfaces.md) · [llms.txt](../llms.txt) | No | — |
| **Consent dismiss / DOM mutation** | Browser extract path (consent.mjs, CSS-hide, aggressive retry) | **Yes** — silent page mutation; not CAPTCHA bypass | [handbook/20-automatic-behaviors.md](../docs/handbook/20-automatic-behaviors.md) · [acquisition.md](../docs/acquisition.md) (limits) · `llms.txt` `access-consent` row | Minor — no dedicated public task page (by design; advanced family) | Acceptable; handbook + llms route sufficient |
| **Onboard env merge** | Every `launch-mcp-host.mjs` invocation | **Yes (operators)** — `~/.occam/onboard.json` env injected silently | [connect/after-install.md](../docs/connect/after-install.md) · [handbook/20](../docs/handbook/20-automatic-behaviors.md) · [configuration.md](../docs/configuration.md) | Minor — easy to miss from agent task docs | Note in journey; operators handbook primary |
| **Post-processors (thin/login/challenge)** | Every successful extract path | Partial — visible via `failure.code` / `quality` | [failure-codes.md](../docs/failure-codes.md) · [handbook/02](../docs/handbook/02-honesty-contract.md) | No | — |
| **HTTP daemon prewarm** | Host start (default on) | No — performance only | [configuration.md](../docs/configuration.md) (`OCCAM_HTTP_DAEMON_PREWARM`) | No | — |
| **WS/Remote pool kill (`InstallShared`)** | New WebSocket/Remote DI session | **Yes (operators)** — latency spike | [handbook/20](../docs/handbook/20-automatic-behaviors.md) · [transports.md](../docs/transports.md) | No | — |
| **bypassCSP + playbook `page.evaluate`** | Browser extract / heal with interaction plan | **Yes** — page script surface | [handbook/20](../docs/handbook/20-automatic-behaviors.md) · [handbook/23](../docs/handbook/23-security-posture.md) | No | — |
| **`occam refresh` name-wide kill** | Operator refresh/stop | **Yes** — kills all `OccamMcp.Core` on machine | [operators.md](../docs/operators.md) · [handbook/20](../docs/handbook/20-automatic-behaviors.md) | No | — |

---

## Must-disclose checklist (public docs duty)

Aligned with handbook ch. 20 “must-disclose automatics”:

| # | Behavior | Public surface reachable? |
|---|----------|---------------------------|
| 1 | Key mint on start | Yes — receipts, configuration, handbook 20/21 |
| 2 | Save always signs | Yes — receipts, playbooks, handbook 20 |
| 3 | `OCCAM_RECEIPTS` not master switch | Yes — receipts.md table |
| 4 | bypassCSP unconditional | Yes — handbook 20/23 |
| 5 | Opt-in disk cache | Yes — materialization, handbook 20 |
| 6 | Refresh kill-all | Yes — operators, handbook 20 |
| 7 | Onboard env merge | Yes — connect/after-install, handbook 20 |
| 8 | WS pool kill | Yes — handbook 20, transports |
| 9 | Managed provider egress | Yes — acquisition, experimental, local-first |
| 10 | Marketplace auto-merge risk | Yes — playbooks.md, handbook 20, trust |

---

## Phase 8H verdict

**PASS** for automation documentation coverage. Residual discoverability from README-only entry (state/automation without visiting hub or llms.txt) tracked in [DOCS-V3-USER-JOURNEY.md](DOCS-V3-USER-JOURNEY.md).
