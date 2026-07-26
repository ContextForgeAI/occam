# Acquisition routing

**Slug:** `acquisition-routing` · **Product system:** PS-1 Acquisition · **CAPs:** 6 · **Public relevance:** HIGH

**Member CAPs:** CAP-050, CAP-051, CAP-052, CAP-088, CAP-104, CAP-112  
**Product capability:** CAP-052  
**Engineering findings:** EF-056 (model correction — cascade prose was wrong)

## What it is

The decision layer that chooses **which extract backend runs**, when to **stop escalating**, and **which failed attempt is surfaced** when locals disagree. Implemented primarily by `OccamRouter` after `TranscodePipeline` preflight. It is **not** a universal product spine — only callers that enter `TranscodePipeline` → `OccamRouter` use this ladder (`PRODUCT-ARCHITECTURE.md` §2–3).

## Why it exists

Cheap HTTP extract first; escalate to browser only when HTTP is non-terminal and not a public-reference short-circuit; optionally try a managed provider only after both locals fail. Surface the **most informative** failure so agents get actionable codes (`http_403`, `captcha_or_challenge`) instead of a generic mask (`OccamRouter.cs:177-182`; `FailureRanking.cs:10-21`).

## User-visible entrypoints

| Entrypoint | How routing is reached | Evidence |
|------------|------------------------|----------|
| `occam_transcode` | Default path; `backend_policy` param | `OccamTranscodeTool.cs:47,168` |
| `occam_digest` / `occam_claim_check` / `occam_attest` / `occam_dataset_export` | Per-URL `pipeline.TranscodeAsync` | `PRODUCT-ARCHITECTURE.md` §3 |
| `occam_watch` / `occam_crosscheck` / batch (opt-in) | Same pipeline | same |
| `occam_verify` live mode | Partial — re-fetch via pipeline | same |
| Probe / map / search / extract_knowledge / heal | **Bypass** router | `PRODUCT-ARCHITECTURE.md` §3 |

Required input identity: **`url`** (CAP-050). Policy string: **`backend_policy`** (CAP-051).

## Core behavior

### Policy set (CAP-051)

Parsed values: `http` | `browser` | `http_then_browser` (also hyphenated form). Default on `occam_transcode`: **`http_then_browser`** (`OccamTranscodeTool.cs:47`). Unknown → `invalid_arguments` at tool / `invalid_policy` at router (`OccamRouter.cs:89`).

| Policy | Behavior | Evidence |
|--------|----------|----------|
| `http` | HTTP backend only | `OccamRouter.cs:83-84` |
| `browser` | Browser backend only | `:85-86` |
| `http_then_browser` | Cascade ladder below | `:87-88,103-182` |

**Readiness:** `HttpThenBrowser` requires **both** HTTP and browser backends ready; managed cannot salvage a missing browser (`OccamRouter.cs:96-101`).

### Corrected cascade (CAP-052 / EF-056 / C1)

**Do not use** older CAP-052/104 prose that claimed density ranking or “managed always wins last.” Code:

```
HTTP extract
  → usable success? STOP (HTTP)
  → 404/410 terminal? STOP (HTTP) — no browser, no managed
  → IsPublicReferencePage(url)? STOP (HTTP) — no browser
  → Browser extract
    → usable success? STOP (Browser)
  → managed configured && ShouldAttempt(url)?
    → managed success? STOP (Managed)
    → managed fail? (recorded only; not surface winner)
  → ChooseRawFallback(http, browser) by FailureRanking
```

Evidence: `OccamRouter.cs:134-182`; peer model `ACQUISITION-ROUTING-MODEL.md`.

### Success gate before escalation

Usable extract = `ok` + non-empty markdown + **not** EQM-thin + **not** short challenge (≤2000 chars with challenge keywords) (`OccamRouter.cs:188-199`). Thin/challenge HTTP therefore escalates under cascade.

### Dual-fail surface pick (`ChooseRawFallback`)

Ranks by `FailureRanking.Informativeness`, **not** markdown length/density (`OccamRouter.cs:206-213`). Raw ok-but-unusable body ranks as `thin_extract` (60). Tie → **browser** (`RawRank(browser) >= RawRank(http)`).

| Rank | Codes (`FailureRanking.cs:10-21`) |
|------|-----------------------------------|
| 100 | `http_401`, `http_403`, `requires_login` |
| 90 | `captcha_or_challenge`, `anti_bot_blocked` |
| 85 | `tls_error` |
| 80 | other `http_4*` |
| 70 | `http_5*` |
| 60 | `thin_extract` |
| 50 | `timeout`, `network_error`, `dns_error` |
| 40 | `content_selectors_miss` |
| 10 | else |

**Managed failure never enters `ChooseRawFallback`** (`OccamRouter.cs:171-182`; GAP-014).

### Domain tiers (CAP-104) — corrected role

`DomainTierRegistry.IsPublicReferencePage` short-circuits browser after failed HTTP (`OccamRouter.cs:149-152`; `DomainTierRegistry.cs:98-124`). Tier flag `http_only` is **probe-advisory** (`PreferHttpOnlyRoute`) and does **not** skip router browser escalation (`ACQUISITION-ROUTING-MODEL.md` corrections #2–3).

## Advanced behavior

| Behavior | Notes | Evidence |
|----------|-------|----------|
| Playbook `preferredBackend` | Overrides policy **only when** request policy is `HttpThenBrowser` | `TranscodePipeline.cs:87-104` |
| Browser missing + auto-provision off | Tool may force effective `Http` + warning | `OccamTranscodeTool.cs:135-145` |
| `prefer_llms_txt` | Pre-ladder HTTP-only substitute attempt | `OccamTranscodeTool.cs:147-164` |
| Opt-in `cache_ttl_s` | Skips live acquisition on hit | FLOW-019; family `response-cache` |
| Recovery attempt log | `recovery[]` records each rung | CAP-098; `OccamRouter.cs:107-125` |
| `tag_trust` (CAP-088) | Post-materialization trust tags; not a routing determinant | `OccamTranscodeTool.cs:66,267` |
| Feature scope (CAP-112) | `OccamFeaturesScope` propagates worker feature flags | tools report |

## Automatic / silent behavior

| Silent decision | Effect | Evidence |
|-----------------|--------|----------|
| Public-reference skip | No browser after failed HTTP | AUTOMATION #5; `OccamRouter.cs:149-152` |
| Thin/challenge auto-escalate | Second rung without caller flag | router `:154-161` |
| Managed attempt when env set | May run after dual fail; fail invisible as winner | `:163-175`; GAP-014 |
| Internal always-on `json_blocks,json_tables` features | Worker flags for planner; public sidecars still opt-in | `TranscodePipeline.cs:44-55` |

## Parameters

| Name | Default | Effect when set |
|------|---------|-----------------|
| `url` | required | Operation identity (CAP-050) |
| `backend_policy` | `http_then_browser` | Selects single backend or cascade; never a `managed` value |
| `playbook_policy` | `auto` | Soft overlay; may set preferred backend under cascade |
| `prefer_llms_txt` | `false` | HTTP llms.txt probe first |
| `cache_ttl_s` | omit/≤0 | Skip fetch on eligible hit |
| `session_profile` | unset | Preflight headers/storage; not a policy selector |
| `if_none_match` / `diff_against` | unset | Post-fetch / cache-eligibility gates |

## Configuration

| Knob | Default | When flipped |
|------|---------|--------------|
| `OCCAM_MANAGED_*` | unset | Enables last rung under cascade only |
| `OCCAM_DOMAIN_TIERS_PATH` | built-in JSON | Extends tier hints; does not set router `http_only` skip |
| `OCCAM_BROWSER_AUTOINSTALL` | on | Affects whether tool downgrades policy when browser missing |
| `OCCAM_RESPECT_ROBOTS` / `OCCAM_HOST_THROTTLE_MS` | off / 0 | Pre-router gate (family `network-safety`) |

## Backends

HTTP → Browser → (optional) Managed. See families `http-acquisition`, `browser-acquisition`, `managed-acquisition`. Router never treats managed as a `backend_policy` enum value (`ACQUISITION-ROUTING-MODEL.md`).

## Sessions / state

Routing itself is **stateless** per call. Session profiles affect preflight and RequiresLogin PP (family `session-fetch`). Recovery log is per-response, not durable.

## Network behavior

Preflight SSRF/session (`FetchPreflight`) and robots/throttle run **before** router (`TranscodePipeline.cs:116-143`). Cascade short-circuits avoid unnecessary network on 404/410 and public-ref fails.

## Artifacts produced

| Artifact | When |
|----------|------|
| Markdown / failure envelope | Always from chosen rung |
| `recovery[]` attempts | Cascade path |
| `backend` string | Winning attempt (incl. managed **success**) |
| Receipts / cache | Downstream PS-2/PS-6 — not owned here |

## Trust / provenance properties

Routing does not prove page authenticity. Managed success can surface third-party markdown without OutboundHttpGuard on the managed client (**EF-003**). Dual-fail ranking prefers actionable codes over silence — honesty about *which attempt won*, not about content truth.

## Failure / fallback behavior

Align with `FAILURE-BEHAVIOR-MAP.md` and `ACQUISITION-ROUTING-MODEL.md`:

| Condition | Surfaced | Escalates? |
|-----------|----------|------------|
| HTTP usable | HTTP ok | no |
| HTTP 404/410 | `http_404`/`http_410` | **no** |
| HTTP fail + public ref | HTTP failure | **no** |
| HTTP thin/challenge/other | → browser → optional managed | yes |
| Both fail + managed fail/skip | Ranked(http,browser) | no further |
| Workers missing | `workers_unavailable` | — |
| Unknown policy | `invalid_policy` / tool `invalid_arguments` | — |

## Platform differences

None for cascade short-circuits or `FailureRanking` (`ACQUISITION-ROUTING-MODEL.md` §Platform). Path separators for `OCCAM_DOMAIN_TIERS_PATH` are OS-dependent (config only).

## Composition with other capabilities

- **Upstream:** preflight, robots, playbook overlay, cache eligibility, llms.txt gate.
- **Downstream:** post-processors (challenge → login → thin) then PS-2 `FinishMaterialize`.
- **Peer families:** all other PS-1 families implement rungs/guards this family orders.

## Known limitations

- Name “cascade” historically overstated managed/density behavior — **EF-056**.
- `dns_error` / `timeout` are **not** terminal; they escalate under cascade (correction #1).
- Probe/map never use this ladder — do not document as universal.
- Domain tier `http_only` does not mean “HTTP only forever” in the router.

## Engineering findings

| ID | Finding (not a feature) |
|----|-------------------------|
| **EF-056** | Cascade model correction: 404/410 + public-ref short-circuit; `FailureRanking`; managed fail never wins surface |
| GAP-001 | Prior CAP-052/104 prose wrong |
| GAP-014 | Managed fail invisible as surface winner |

## Code evidence

- `src/FFOccamMcp.Core/Routing/OccamRouter.cs:81-218`
- `src/FFOccamMcp.Core/Routing/FailureRanking.cs:10-21`
- `src/FFOccamMcp.Core/Routing/TranscodePipeline.cs:87-156`
- `src/FFOccamMcp.Core/Tools/OccamTranscodeTool.cs:47,117-168`
- Peer: `docs-audit/ACQUISITION-ROUTING-MODEL.md`, `FAILURE-BEHAVIOR-MAP.md`

## Public-doc relevance

**HIGH.** Any public explanation of “how Occam fetches” must use the corrected ladder. Forbidden: density-ranked dual-fail; managed as `backend_policy`; claiming all tools cascade.

## Handbook relevance

**Reference narrative** for “read a page” after preflight. Handbook must show terminal 404/410 and public-ref stops before browser.
