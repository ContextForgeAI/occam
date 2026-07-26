# Chapter 26 — Architecture internals

**Status:** INTERNAL (handbook-only depth) · **Prerequisites:** [Chapter 4](04-request-path.md), [Chapter 5](05-acquisition-ladder.md), [Chapter 20](20-automatic-behaviors.md), [Chapter 21](21-state-and-footprint.md)

---

## Mental model

**Nine product systems, six orchestration spines, one host process, several worker process families.** Shipped ≠ reachable ≠ documentable — the AOT binary contains types with zero live callers.

---

## Explanation

### Layer model (L0 host)

| Layer | Responsibility |
|-------|----------------|
| Transport | stdio / WebSocket / Remote JSON-RPC |
| Tools | MCP handlers — validate params, serialize responses |
| Services | Probe, digest, map, playbook, claim, attest, verify, watch, batch, consensus |
| Routing | `TranscodePipeline`, `OccamRouter`, backends |
| Post-processors | Challenge, login, thin extract, quality |
| Workers | Spawn Node: http-extract, browser-extract, css-extract |
| Compile | Token budget, focus, FitMarkdown |
| Trust | Sign, verify, Merkle, cache, watch chain |
| DI | `AddOccamCore()` singleton wiring |

### Six spines (not one universal pipeline)

| Spine | Tools |
|-------|-------|
| Transcode family | transcode (reference 14-step narrative) |
| Probe | probe — HTTP only, no worker extract path |
| Map | map — link listing, capped |
| Search | search — provider-gated |
| Playbook resolve/heal/lint/save | playbook_* |
| Extract knowledge | extract_knowledge — separate worker, bypasses router/post-processors/receipt v1 |

Nine of twenty-one registered tool names bypass the full transcode pipeline.

### Process topology

- **Host:** .NET Native AOT `FFOccamMcp.Core` — one process per invocation mode.
- **HTTP daemon:** persistent Node helper for http-extract.
- **Browser daemon / pool:** Playwright chromium pool; per-call contexts for session/header paths.
- **Ephemeral workers:** css-extract, dom-skeleton for heal — spawned per need.

### Side-effect entry points

Key mint (DI start), playbook save (disk + sign), watch/batch stores, response cache write, connect/onboard (host configs), session import (disk secrets), process kill (refresh).

### Dead-but-shipped register (not extension points)

Types compile into the binary but have no product callers — do not document as capabilities:

- `IWorkerProcessSpawner` / `BrowserConcurrencyGate.Run`
- `MaterializedProvenanceResolver` / `ProvenanceTrace`
- Alternate codecs / `TableSemanticMaterializer`
- `ResponseBudgetMode.Unchanged` / `DeltaOnly`
- Canonical IR pipeline (built then discarded on transcode)
- `PlaybookCommunitySanitizer` on local save path
- Paywall negative-receipt branch (unreachable producer)

Contributors: grep before assuming a public extension seam.

### Gate boundary

`OCCAM_GATE` conditional compilation in gate bench — not active in normal publish. Worker timeouts: HTTP ~35s, browser default 60s, css-extract hardcoded 45s.

---

## CHECK

**LOCAL.** Grep the tree for one dead-register item (e.g. `MaterializedProvenanceResolver`) and confirm no caller outside tests and gate — shipped but unreachable.

---

## Common misconception

**"Dead code does not ship."** Core uses default compile glob — every type under `src/FFOccamMcp.Core/**` is in the AOT binary. **Shipped ≠ reachable ≠ documentable.**

---

## Limitations

- This chapter is handbook-only — public docs describe supported behavior, not internal type names.
- Architecture map drifts with refactors — verify against `PRODUCT-ARCHITECTURE.md` and code.
- Worker internals live in `workers/` — separate from C# host concerns.
- No claim here about future extension APIs.

---

## Links

- [Chapter 4 — Request path](04-request-path.md)
- [Chapter 5 — Acquisition ladder](05-acquisition-ladder.md)
- [Chapter 21 — State](21-state-and-footprint.md)
- Engineering (gitignored): `docs-internal/` code maps
- Audit: `docs-audit/PRODUCT-ARCHITECTURE.md` · `docs-audit/CODE-MAP.md` · `docs-audit/PRODUCT-VS-ENGINEERING.md` §6
