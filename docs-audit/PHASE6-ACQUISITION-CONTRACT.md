# Phase 6 — Acquisition ladder contract (EF-056)

**Status:** intentional / locked — **not a runtime bug**. Analysis only; no acquisition behavior changes in this patch wave.

**Sources:** `docs-audit/ACQUISITION-ROUTING-MODEL.md`, `docs-audit/canonical/acquisition-routing.md`, `PHASE6-INTENDED-CONTRACT.md` §Acquisition contract.

## Intent

Public and internal prose must describe the **code-derived** `http_then_browser` cascade. Older CAP-052/104 text that claimed density-ranked dual-fail or “managed always wins last” is **wrong** (EF-056 / GAP-001).

## Locked behaviors → acceptance → code sites

| # | Behavior | Acceptance | Code |
|---|----------|------------|------|
| 1 | HTTP usable success terminates | Non-empty MD, not EQM-thin, not short challenge → surface HTTP | `OccamRouter.cs` success gate (~188–199), cascade (~134–182) |
| 2 | Thin / short-challenge HTTP escalates | Thin or ≤2000-char challenge body → browser rung | same success gate + escalate branch |
| 3 | Browser escalation under cascade | After non-terminal HTTP fail (not 404/410, not public-ref) | `OccamRouter.cs` ~139–161 |
| 4 | 404/410 short-circuit | No browser, no managed | `IsTerminalHttpFailure` ~144–147, 216–218 |
| 5 | Public-reference short-circuit | Failed HTTP on public-ref page → no browser | `DomainTierRegistry.IsPublicReferencePage` + router ~149–152 |
| 6 | Dual-fail by `FailureRanking` | Surface = max informativeness(http, browser); **not** markdown density | `ChooseRawFallback` ~206–213; `FailureRanking.cs` ~10–21 |
| 7 | Managed only after both locals fail | Only on `http_then_browser`; never a `backend_policy` value | router ~81–86, 163–175; `ManagedExtractBackend` |
| 8 | Managed fail never wins surface | Managed success may surface; managed fail recorded only | router ~171–182 |
| 9 | `ok:false` = unknown content | Agents must not invent page body from memory | product trust rule / tool responses |
| 10 | Private-IP reject on http/browser | Preflight / SSRF guards block private targets (unless allow-listed) | `FetchPreflight` / `OutboundHttpGuard` / worker DNS-pin |
| 11 | Session tiers 1/2/3 | Operator session_profile / storageState tiers as documented | session profile path (not router enum) |

## Explicit non-goals

- No CAPTCHA solving; no fingerprint rotation.
- `managed` is **not** a public `backend_policy` enum value.
- Domain-tier `http_only` is probe-advisory — does **not** skip browser escalation in the router.
- Do **not** change ladder behavior to match obsolete docs; fix docs/tests to match this contract.

## Regression posture

Prefer unit/router fixtures that assert short-circuits (4–5), ranking (6), and managed surface rules (7–8). Live gates remain the integration proof for HTTP→browser escalation.
