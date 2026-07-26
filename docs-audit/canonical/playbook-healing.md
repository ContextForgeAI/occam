# Playbook healing

**Slug:** `playbook-healing` · **Product system:** PS-5 Playbooks · **CAPs:** 25 · **Public relevance:** HIGH.

## What it is

`occam_playbook_heal` captures a rendered DOM skeleton and candidate anchors after selected extraction failures so an agent/human can draft a recipe. It gathers evidence; it does not write or automatically repair a playbook (CAP-530–554).

It is browser-only and bypasses the transcode/router/materialization/trust spine (CAP-548/549; `PRODUCT-ARCHITECTURE.md:88`).

## Why it exists

- Turn a heal-eligible failure into bounded structural evidence (CAP-533/538/544).
- Reuse browser pool or one-shot Playwright to see JS-rendered structure (CAP-536/537).
- Identify landmarks, test ids, and likely content roots for recipe authors (CAP-538/544).
- Keep terminal/challenge/private failures from being mislabeled as selector-repair problems (CAP-532–535).

## User-visible entrypoints

MCP `occam_playbook_heal`, exposed only under `OCCAM_PROFILE=full` (CAP-547). Successful hints point to `occam_playbook_save` (CAP-545).

## Core behavior

1. Validate absolute URL and required caller-declared `failure_reason` (CAP-530).
2. Reject terminal and non-healable reasons before network activity (CAP-532/533).
3. Apply challenge-URL and session-presence eligibility rules (CAP-534/535).
4. Resolve preflight/header session and clamp skeleton nodes 50–600 (CAP-531/543).
5. Try browser pool daemon `/skeleton`; on any unusable daemon outcome, silently fall back to one-shot Playwright (CAP-536/537/551).
6. Navigate, wait for main landmarks, block heavy resources, flatten open shadow roots, and walk bounded DOM (CAP-538–542).
7. Return skeleton, anchors, and fixed authoring hints (CAP-544/545).

## Advanced behavior

| Mechanism | Detail | Evidence |
|---|---|---|
| Eligibility | Only thin/extraction/selectors/verify failures, plus login/401/403 when session supplied | CAP-532/533/535 |
| Node walk | DFS depth 12, 50–600 nodes, skip script/style/SVG metadata | `dom-skeleton.mjs`; CAP-538 |
| Main scoring | main/role, id hints, text length, nested article/headings; top 8 | CAP-538 |
| Shadow DOM | Flatten/open-root recursion only | CAP-539 |
| Readiness | `domcontentloaded`, best-effort main wait up to 8s, then 800ms settle | CAP-540 |
| Resource block | Images/fonts/media aborted | CAP-541 |
| Navigation SSRF | DNS/private-host revalidation per navigation | CAP-542 |
| Anchors | Landmarks, up to 40 test ids, main selector/text/score candidates | CAP-544 |

## Automatic / silent behavior

- `max_skeleton_nodes` is silently clamped, not rejected (CAP-531).
- The service trusts the caller's prior `failure_reason`; it does not reproduce the original failure (CAP-533).
- Daemon failure is discarded and one-shot retried without attempt telemetry (CAP-551).
- StorageState/localStorage is ignored; only headers and Cookie-header injection work (CAP-543).
- Pool capture failures affect shared browser pool health for unrelated traffic (CAP-554).
- Every successful response emits the same suggested next step and max verify retries (CAP-545).
- Worker supports `--consent-aggressive`, but host never passes it (CAP-553).

## Parameters

| Name | Default | Effect | Evidence |
|---|---|---|---|
| `url` | required | Target for rendered skeleton | CAP-530 |
| `failure_reason` | required | Eligibility input; not independently verified | CAP-530/532/533 |
| `session_profile` | `null` | Headers/Cookie only; also gates selected auth failures | CAP-535/543 |
| `max_skeleton_nodes` | `600` | Clamp 50–600 | CAP-531 |

No backend policy, HTTP-only mode, token budget, cache, screenshot, receipt, or automatic-save parameter exists.

## Configuration

`OCCAM_DOM_SKELETON_SCRIPT` overrides worker script (CAP-550). `OCCAM_BROWSER_*`, proxy variables, `OCCAM_SESSIONS_ROOT`, and `OCCAM_ALLOW_PRIVATE_URLS` affect shared browser/preflight plumbing (CAP-536/537/543/550).

`OCCAM_PROFILE` controls exposure (CAP-547).

## Backends

Browser pool daemon is preferred; one-shot Playwright is fallback (CAP-536/537/551). There is no HTTP backend, `OccamRouter`, managed provider, or browser-policy selector (CAP-548/549).

## Sessions / state

Merged headers are passed to daemon/one-shot and Cookie header is converted to Playwright cookies. Playwright storageState and localStorage are not wired (CAP-543).

Skeleton/anchors are ephemeral. No cache or playbook file is written. Browser pool is shared process state and heal outcomes feed its slot health (CAP-554; ST-20).

## Network behavior

- Browser navigation only; fixed 120s host capture timeout, with 45s `goto` plus waits inside worker (CAP-537/540).
- Images/fonts/media blocked; navigation requests SSRF-checked (CAP-541/542).
- One-shot spawn receives static proxy environment; daemon inherits launch-time proxy (CAP-537).
- No robots/throttle, domain tiers, post-processors, managed providers, or retries beyond daemon→one-shot fallback (CAP-549/551).

## Artifacts produced

ART-016 ephemeral heal skeleton/candidates (`ARTIFACT-ONTOLOGY.md:84`). Payload includes structural DOM tree, landmarks, test ids, scored main candidates, and fixed hints (CAP-538/544/545).

No playbook, receipt, hash, screenshot, raw HTML, or persistent store is produced.

## Trust / provenance properties

Heal output is unsigned and not Receipt-v1-backed. It is a browser observation that may represent a login wall, challenge, consent overlay, or mutated DOM because no post-processors reclassify actual rendered content (CAP-549).

It must not be described as verified evidence or an automatically safe recipe. `failure_reason` is caller-asserted (CAP-533).

## Failure / fallback behavior

| Code/class | Behavior | Evidence |
|---|---|---|
| terminal reasons | Echo reason; no capture | CAP-532 |
| non-healable reason | `heal_not_applicable` | CAP-533 |
| challenge URL heuristic | Refuses before fetch | CAP-534 |
| auth failures without session | Refuses | CAP-535 |
| pool failure | Silent one-shot fallback | CAP-551 |
| `workers_unavailable`, `timeout`, `extraction_failed`, `skeleton_capture_failed`, `playwright_missing` | Capture failure; most lack agent hints | CAP-537/546 |

No HTTP/managed degrade path exists (CAP-548).

## Platform differences

Worker process groups use Win32 Job Objects vs POSIX groups; Playwright cache paths differ (`PLATFORM-DIFFERENCES.md`). Browser semantics are intended to match. Session path checks differ in case sensitivity by OS.

## Composition with other capabilities

- Starts from a prior acquisition/save-verification failure code (CAP-530/533).
- Produces authoring evidence for a human/agent draft, then `playbook-authoring` persists it (CAP-545).
- Does not consume an existing playbook (CAP-549).
- Shares browser pool with transcode and can influence pool health (CAP-554).
- Lint may inspect the resulting draft, but no automated link exists.

## Known limitations

- Full profile only (CAP-547).
- Browser-only; fails if Playwright unavailable (CAP-548).
- Caller-declared failure is not verified (CAP-533).
- StorageState ignored (CAP-543).
- No challenge/login/quality post-processing after capture (CAP-549).
- Consent-aggressive mode unreachable (CAP-553).
- No receipt, persistence, screenshot, raw HTML, cache, robots, or managed fallback.
- Closed shadow roots cannot be flattened (CAP-539).

## Engineering findings

- CAP-543: silent session downgrade.
- CAP-546: incomplete failure-hint coverage.
- CAP-551: invisible double-attempt/latency.
- CAP-552: unused `CreateHeadersScope` dead code (`DEAD-OR-UNREACHABLE.md:39`).
- CAP-553: unreachable consent flag (`DEAD-OR-UNREACHABLE.md:40`).
- CAP-554: shared pool health coupling.

## Code evidence

- `src/FFOccamMcp.Core/Tools/OccamPlaybookHealTool.cs`
- `src/FFOccamMcp.Core/Playbooks/PlaybookHealService.cs`
- `src/FFOccamMcp.Core/Playbooks/PlaybookHealPolicy.cs`
- `src/FFOccamMcp.Core/Workers/DomSkeletonWorker.cs`
- `workers/browser-extract/dom-skeleton-capture.mjs`
- `workers/browser-extract/lib/dom-skeleton.mjs`
- CAP-530–554; ART-016.

## Public-doc relevance

High. Document eligibility, caller-asserted failure, browser-only behavior, exact node bounds, daemon fallback, session limitation, unsigned output, and manual draft/save step. Do not imply automatic repair or verified DOM provenance.

## Handbook relevance

Use as the troubleshooting-to-authoring bridge. Include “do not heal terminal/challenge failures,” how to read main candidates, session caveats, and the explicit human/agent drafting step before lint/save.
