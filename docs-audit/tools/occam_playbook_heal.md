# `occam_playbook_heal` — Deep Capability Audit (Wave 2)

**Source of truth:** current executable code only (`src/FFOccamMcp.Core/Tools/OccamPlaybookHealTool.cs`,
`src/FFOccamMcp.Core/Playbooks/PlaybookHealService.cs`, `PlaybookHealPolicy.cs`, `PlaybookHealModels.cs`,
`src/FFOccamMcp.Core/Workers/DomSkeletonWorker.cs`, `workers/browser-extract/dom-skeleton-capture.mjs`,
`workers/browser-extract/lib/dom-skeleton.mjs`, `workers/browser-extract/lib/session-headers.mjs`).
Docs were **not** read for behavior; every claim below cites a file read directly.

**CAP ID range owned by this audit:** `CAP-530`–`CAP-559` (used: CAP-530…554; remainder reserved).

---

## 0. Entry point and schema

`OccamPlaybookHealTool.Heal` (`Tools/OccamPlaybookHealTool.cs`) — MCP name `occam_playbook_heal`.
Params: `url` (required), `failure_reason` (required string — prior `occam_transcode` failure code),
`session_profile` (optional), `max_skeleton_nodes` (optional int, default 600). No `backend_policy`
parameter exists on this tool at all — see **CAP-548**.

Tool description (as registered): *"When a transcode fails on a hard site with no recipe, capture the
page's DOM skeleton + selector candidates so you can draft a playbook for it (then save with
`occam_playbook_save`). This gathers the evidence; you write the recipe JSON."* — i.e. the tool is
explicitly evidence-gathering only; the LLM caller authors the actual playbook JSON afterward.

Call chain: `OccamPlaybookHealTool.Heal` → `PlaybookHealService.HealAsync` → (validation/policy gates) →
`DomSkeletonWorker.TryCaptureAsync` → browser-pool daemon `/skeleton` HTTP endpoint **or** one-shot
Playwright process (`dom-skeleton-capture.mjs --mode=skeleton`) → JSON mapped back to
`PlaybookHealResult` → `OccamPlaybookHealResponseMapper` → camelCase JSON response.

---

## CAP-530 — `url` + `failure_reason` (required inputs, identity of the heal attempt)

**Evidence:** `PlaybookHealService.HealAsync` (`Playbooks/PlaybookHealService.cs`).

`url` must parse as `Uri.TryCreate(..., UriKind.Absolute, ...)` or the call fails `invalid_url`
(distinct string from `occam_transcode`'s `invalid_arguments`). `failure_reason` must be non-blank or
the call fails `invalid_failure_reason`. Unlike `occam_transcode`, there is **no** `PrivacyClassifier`
call directly inside `HealAsync` itself — the private-URL/SSRF check is deferred to
`DomSkeletonWorker.TryCaptureAsync` → `FetchPreflight.Prepare(request.Url, request.SessionProfile)`,
which reuses the exact same `PrivacyClassifier` (Wave-1 **CAP-100**/**CAP-150**/**CAP-156**) — so a
private/loopback URL still fails `private_url_blocked`, just one call-frame deeper than in transcode.

## CAP-531 — `max_skeleton_nodes` clamp (50–600, default 600)

**Evidence:** `PlaybookHealModels.cs` (`PlaybookHealRequest.MaxSkeletonNodes = 600` default),
`DomSkeletonWorker.cs` (`Math.Clamp(request.MaxSkeletonNodes, 50, 600)` — applied **twice**, once on the
daemon-pool path and once on the one-shot path, so a caller-supplied value outside `[50,600]` is
silently clamped, never rejected as `invalid_arguments`). Code comment in `PlaybookHealModels.cs`
explains the 600 default specifically: on content-heavy pages (e.g. MDN) where nav/sidebar renders
before `<main>`, a lower cap can exhaust the node budget before the DFS reaches main content, yielding
`mainCandidates=0` non-deterministically — 600 was chosen from an 8/8 pilot-verified threshold, not an
arbitrary round number.

## CAP-532 — Terminal-failure gate (`PlaybookHealPolicy.IsTerminalFailure`)

**Evidence:** `Playbooks/PlaybookHealPolicy.cs`. A fixed set — `captcha_or_challenge`,
`private_url_blocked`, `workers_unavailable`, `timeout`, `invalid_arguments`, `invalid_policy`,
`invalid_url`, `invalid_failure_reason` — plus any code starting with `http_4`/`http_5` — is treated as
**non-healable**: `HealAsync` returns that same (normalized) code immediately with message *"Failure is
terminal for self-heal; escalate to user."* without ever reaching the DOM-skeleton worker. This runs
**before** `ShouldOfferHeal` (**CAP-533**), so it is the first, cheapest rejection gate.

## CAP-533 — Heal-eligibility matrix (`ShouldOfferHeal` / `HealFailureCodes`)

**Evidence:** `Playbooks/PlaybookHealPolicy.cs`. After the terminal-failure gate, `ShouldOfferHeal`
requires the (normalized) `failure_reason` to be one of `thin_extract`, `extraction_failed`,
`content_extraction_failed`, `content_selectors_miss`, `playbook_verify_failed`,
`playbook_verify_low_score`, `playbook_verify_high_noise` — otherwise the call fails
`heal_not_applicable` (message names the exact `failure_reason` value back to the caller). This is a
**caller-declared** failure reason from a prior `occam_transcode` call — `occam_playbook_heal` does not
itself re-run transcode or independently verify the claimed failure actually occurred; it trusts the
string the agent passes in (see UNCERTAINTIES).

## CAP-534 — Challenge-URL heuristic reuse blocks heal on anti-bot URLs

**Evidence:** `PlaybookHealPolicy.HasChallengeUrl` / `LooksLikeChallengeUrl`, called from
`ShouldOfferHeal` against **both** `finalUrl` and `requestUrl` (heal only ever has `requestUrl` — no
"final URL after redirect" concept exists on this tool, unlike transcode). Host substring match
(`captcha`, `hcaptcha`, `challenges.cloudflare`), path markers
(`/cdn-cgi/challenge-platform`, `/challenge`, `/js_challenge` segments), and query markers (`__cf_chl`,
`challenge=`) all short-circuit heal to ineligible — this is the **same detection logic family** as
`ChallengePageDetector` (Wave-1 **CAP-095**) but a separate, URL-shape-only implementation (no page
content is fetched to make this decision — it is a pre-fetch heuristic).

## CAP-535 — `requires_login`/`http_401`/`http_403` heal gated on `session_profile` presence

**Evidence:** `ShouldOfferHeal`: for these three codes, heal is offered **only if**
`sessionProfileApplied` is true (i.e. the caller supplied a non-blank `session_profile` on the heal
call) — the policy does not verify the session is *valid*, only that one was *supplied*, mirroring the
same "supplied but not verified effective" pattern noted for transcode's `requires_login`
post-processor (Wave-1 **CAP-096**).

---

## CAP-536 — Browser-pool daemon skeleton-capture path (fast path)

**Evidence:** `DomSkeletonWorker.TryCaptureAsync` (`Workers/DomSkeletonWorker.cs`).

When `browserPool.IsEnabled` (Wave-1 **CAP-204**, N-slot Chromium daemon pool) **and**
`TryEnsureMinimumHealthyAsync` succeeds, the worker acquires a pool slot and calls
`IBrowserDaemonClient.TryCaptureSkeletonJsonAsync(url, maxNodes, timeoutMs=120_000, headersFile, ct,
slot.Port)` — an HTTP POST to `http://127.0.0.1:<port>/skeleton` on the already-running daemon process
(`BrowserDaemonClient.cs`), distinct from the `/extract` endpoint used by transcode's browser backend.
This reuses the pool infrastructure (Wave-1 **CAP-204**/**CAP-227**/**CAP-231**) but hits a **different
route** — the daemon process itself must implement a `/skeleton` handler in addition to `/extract`.
Slot lifecycle: `ReleaseSlot(slot, ok:true, extractMs:...)` only on a **parsed, `Ok:true`** result;
any other outcome falls through to **CAP-537**'s one-shot fallback without failing the whole call yet.

## CAP-537 — One-shot Playwright fallback capture path

**Evidence:** `DomSkeletonWorker.TryCaptureOneShot`. Runs whenever the pool is disabled, unhealthy, or
the daemon call didn't return a parseable `Ok:true` payload. Spawns
`node dom-skeleton-capture.mjs "<url>" --mode=skeleton --max-nodes=<n> [--headers-file="<path>"]` via
`WorkerProcessGroup.Start` (process-group-tracked spawn, Wave-1 **CAP-229** cleanup semantics) — **not**
the same `NodeWorkerProcessSpawner` abstraction transcode/browser-extract use (Wave-1 **CAP-108**); this
is a separate lighter-weight one-shot launcher using `NodeWorkerOutputCapture.RunAsync` +
`TryParseLastJsonLine` directly. Fixed 120s timeout (`timeoutMs = 120_000`, not configurable per-call).
On timeout: failure code `timeout`. On no parseable JSON line: failure code `extraction_failed` with a
message trimmed from stderr/stdout via `TrimWorkerMessage` (looks for "Executable doesn't exist",
"playwright install", or generic "Error" lines, capped at 240 chars) — the **same trimming pattern** as
transcode's worker-crash handling (Wave-1 **CAP-108**) but re-implemented locally rather than shared.

## CAP-538 — `buildDomSkeleton` algorithm: capped DFS + landmark/testid/main-candidate scoring

**Evidence:** `workers/browser-extract/lib/dom-skeleton.mjs`.

A single `page.evaluate` walks `document.body` depth-first, capped at `maxNodes` (clamped 50–600 inside
the browser context too — **CAP-531**'s clamp is redundantly enforced a third time here) and `maxDepth`
(default 12, not exposed as an MCP parameter). Per visited node it records: tag, `id`, up to 3 class
tokens, `role`, `data-testid`, trimmed `aria-label`, trimmed **own-text** (only when the element has
exactly one text-node child — avoids duplicating descendant text into every ancestor), and an
`interactive` flag (`isInteractive`: anchor/button/input/select/textarea/summary tags, `role=button`,
or `onclick`/`tabindex` attributes present). Script/style/noscript/svg/path/link/meta tags are skipped
entirely (`SKIP_TAGS`) and do not count toward the node budget. Landmarks (`main`/`nav`/`role=main`/
`role=navigation`) are collected into a `Set` (dedup by role/tag name only, not by element).
`data-testid` values are collected up to 40. `scoreMain` assigns a 0–1 relevance score per element
(tag/role `main` → +0.4, id containing content/main/readme/article → +0.2, >120 chars of trimmed
`innerText` → +0.25, presence of a nested `article`/`h1`/`h2` → +0.15) and any element scoring ≥0.45 is
recorded as a `mainCandidate` (selector hint via `id` → `data-testid` → `tag.class` fallback chain,
capped at 12 collected / top-8 returned, sorted descending by score).

## CAP-539 — Shadow DOM flattening before skeleton walk

**Evidence:** `dom-skeleton-capture.mjs` imports and calls `flattenOpenShadowRoots(page)`
(`lib/shadow-dom-flatten.mjs`) before `buildDomSkeleton` runs, and `dom-skeleton.mjs`'s own `walk`
additionally recurses into `el.shadowRoot.children` directly as a second-layer safety net. This is the
**same shadow-DOM capability** documented at Wave-1 **CAP-219** for the main browser-extract path,
confirming it is shared infrastructure rather than a heal-only feature — but note it only covers **open**
shadow roots (closed shadow roots are architecturally unobservable from page-context JS, same caveat as
CAP-219).

## CAP-540 — Main-landmark wait heuristic + fixed settle delay

**Evidence:** `dom-skeleton-capture.mjs`. After `page.goto(url, {waitUntil:"domcontentloaded",
timeout:45_000})`, the script does a **best-effort** `page.waitForSelector("main, [role=main], article",
{timeout:8000, state:"attached"})` — swallowed on failure (comment: "pages without an explicit main
landmark fall through") — followed unconditionally by a flat `page.waitForTimeout(800)` before flattening
shadow DOM and walking. This is a deliberate anti-flake measure (comment cites the exact same MDN
race-condition rationale as **CAP-531**'s node-cap choice — both defend the same `mainCandidates=0`
flake) but means every heal call pays a minimum ~800ms tax even on fast-loading pages, and up to ~8.8s
extra on pages that never render a `main`/`article`/`role=main` element.

## CAP-541 — Resource-type blocking during skeleton navigation

**Evidence:** `dom-skeleton-capture.mjs` — `context.route("**/*", ...)` aborts any request whose
`resourceType()` is `image`, `font`, or `media` (`SKELETON_BLOCKED_TYPES`). Comment: "the DOM skeleton
is structural, so these don't affect capture" and blocking them keeps big pages (e.g. MDN) from being
slowed enough to thin the captured skeleton. This is heal-specific tuning — the main browser-extract
worker's resource-blocking policy (if any) was not re-verified here as it is out of this tool's scope.

## CAP-542 — Browser-side per-navigation SSRF re-validation (skeleton worker)

**Evidence:** `dom-skeleton-capture.mjs` — inside the same `context.route` handler, every **navigation**
request (`req.isNavigationRequest()`) is re-validated via `resolveAndValidateHost(new
URL(req.url()).hostname)` from `../shared/lib/private-ip.mjs`, unless `shouldSkipPrivateIpCheck()` is
true. This is the same DNS-rebinding-safe per-navigation guard documented at Wave-1 **CAP-153**/
**CAP-226** for the main browser-extract worker — confirmed independently reimplemented here rather than
imported as one shared route handler, so any future SSRF-guard fix must be applied in **both** places
(`browser-extract.mjs`'s equivalent and this file) to stay in parity.

---

## CAP-543 — HIDDEN: `session_profile` → Cookie-header-only injection; browser `storageState` NOT wired

**Evidence:** `IBrowserDaemonClient.TryCaptureSkeletonJsonAsync` signature (`IBrowserDaemonClient.cs`)
takes `(url, maxNodes, timeoutMs, headersFile, ct, port)` — **no `storageStateFile` parameter**, unlike
the sibling `TryExtractAsync` on the same interface which does accept `storageStateFile`
(`Workers/BrowserDaemonClient.cs` lines 12–22 vs 24–30). `DomSkeletonWorker.TryCaptureAsync` only ever
reads `preflight.ActiveHeadersFile`, never `preflight.ActiveStorageStatePath` (`FetchPreflight.cs`
exposes both). On the one-shot fallback (`TryCaptureOneShot`) the same is true — only
`--headers-file=` is ever passed to the Node process; there is no `--storage-state-file=` argument at
all in this worker's CLI surface (confirmed absent from `dom-skeleton-capture.mjs`'s arg parsing, unlike
`browser-extract.mjs` which does accept one per Wave-1 **CAP-160**/`BrowserExtractRunner.cs`).

Cookie delivery still works, but only through a **different, narrower** mechanism:
`resolveBrowserContextOptions(headersFile)` reads the merged headers file and
`applySessionCookies(context, url, headers)` (`lib/session-headers.mjs`) parses a bare `Cookie` header
string (`headers.Cookie ?? headers.cookie`) into individual Playwright cookies via `parseCookieHeader`
and calls `context.addCookies(...)` — this is the **same mechanism** as Wave-1 **CAP-171** (cookie-header
→ Playwright cookie injection), reused correctly here (a prior bug where the URL was accidentally passed
as the `headers` argument is already fixed per the code comment at `dom-skeleton-capture.mjs:67-70`).

**Net effect:** a `session_profile` that stores its auth as a **Playwright `storageState` file**
(full cookie jar + `localStorage`, produced by `occam-session.mjs export-state`, Wave-1 **CAP-176**) will
have its `localStorage` entries and any cookies not also mirrored into the profile's `Cookie` header
silently **not applied** during `occam_playbook_heal`, even though the identical `session_profile` id
works fully (headers + storageState) on `occam_transcode`. An agent healing a login-walled SPA that
relies on `localStorage`-based auth tokens will see the **anonymous/logged-out** DOM skeleton with no
error or warning that the session was partially ignored.

## CAP-544 — Anchors payload surfaced to caller (landmarks / data-testids / main candidates)

**Evidence:** `OccamPlaybookHealResponseMapper.MapSuccess` (`Tools/OccamPlaybookHealTool.cs`) — maps
`PlaybookHealAnchors` (Landmarks, DataTestIds, MainCandidates with Selector/TextAnchor/Score) straight
through into the JSON response's `anchors` object; this — together with the `domSkeleton` tree
(**CAP-538**) — is the entire evidentiary payload the calling agent is expected to read before hand-
authoring a playbook JSON for `occam_playbook_save`.

## CAP-545 — `agentHints`: fixed `suggested_next` / `do_not` / `max_verify_retries`

**Evidence:** `DomSkeletonWorker.MapSuccess`. Every **successful** heal response carries a constant
agent-hint block: `suggested_next = "occam_playbook_save"`, `do_not = ["compare_skeleton_to_broken_
selectors", "dump_raw_html", "retry_transcode_before_save", "max_verify_retries=3"]`,
`max_verify_retries = PlaybookHealPolicy.MaxVerifyRetries` (constant `3`). This is not computed per-page
— it is the same static advice on every successful capture, distinguishing this tool's "next step"
guidance from `occam_transcode`'s dynamic, failure-code-specific `agentMeta.decisions`
(Wave-1 **CAP-106**).

## CAP-546 — HIDDEN GAP: failure-hint coverage is incomplete for this tool's own failure codes

**Evidence:** `OccamPlaybookHealResponseMapper.MapFailure` calls `FailureAgentHints.ForCode(code)` →
`TranscodeAgentDecisions.ForFailure` (`Agent/TranscodeAgentDecisions.cs`). Of the failure codes this
tool can actually emit (`invalid_url`, `invalid_failure_reason`, every `PlaybookHealPolicy` terminal
code, `heal_not_applicable`, `workers_unavailable`, `timeout`, `extraction_failed`,
`skeleton_capture_failed`, `playwright_missing`), **only** `workers_unavailable` and
`heal_not_applicable` have a matching `ForFailure` branch (confirmed by reading the full decision list —
no case for `invalid_url`, `invalid_failure_reason`, `timeout`, `extraction_failed`,
`skeleton_capture_failed`, or `playwright_missing`). Every other failure returns `agentHints: null` in
the response — the caller gets the raw `failureCode`/`message` strings but no structured next-step
guidance for the majority of this tool's own failure surface, unlike `occam_transcode`'s much more
complete `agentMeta.decisions` coverage (Wave-1 **CAP-106**).

---

## CAP-547 — Profile-gated exposure: `occam_playbook_heal` is **full**-profile-only

**Evidence:** `Transport/OccamToolProfile.cs`. `ReaderTools`, `ResearcherExtra`, and `AuditorExtra` all
explicitly **omit** `occam_playbook_heal` (and its `occam_playbook_save` counterpart) — confirmed by
reading all three arrays directly. `GetExposedToolNames` falls through to the **full**
`OccamMcpServerRegistration.OccamToolNames` catalog only when the resolved `OCCAM_PROFILE` is `full`
(the default) or unset/invalid. This matches the audit brief's own framing: under
`OCCAM_PROFILE=reader|researcher|auditor`, `occam_playbook_heal` is **not registered at all** — an
agent running under any narrower profile has no way to discover or call it, by design (doc comment:
"so agents do not drift into heal/save on a simple read").

## CAP-548 — No `backend_policy` parameter — heal is browser-only by construction

**Evidence:** `OccamPlaybookHealTool.Heal` signature — no `backend_policy` param exists. Verified this
is not merely "defaulted to browser" but **structurally browser-only**: `DomSkeletonWorker` has exactly
two code paths (browser-pool daemon **CAP-536**, one-shot Playwright **CAP-537**) and neither has an
HTTP-only branch — there is no `IExtractBackend`/`HttpExtractBackend` call anywhere in the heal call
chain. If Playwright/Chromium is entirely unavailable (`ResolveScript` returns null, or the daemon and
one-shot spawn both fail), the only possible outcomes are `workers_unavailable` or `extraction_failed` —
heal **cannot** degrade to an HTTP-only DOM capture the way `occam_transcode`'s cascade can degrade to
HTTP-only success. This is intentional (a DOM skeleton requires a live rendered DOM, including
JS-driven structure, which a raw HTTP fetch cannot provide) but is a real capability boundary worth
stating plainly.

## CAP-549 — Bypasses `OccamRouter`/`TranscodePipeline` entirely — confirmed absent subsystems

**Evidence:** absence confirmed by reading `PlaybookHealService.cs` and `DomSkeletonWorker.cs` in full
and grepping for `ManagedExtractBackend`, `OccamRouter`, `TranscodePipeline`, `ITranscodePostProcessor`
references from the `Playbooks`/heal call chain — **none found**. Concretely, `occam_playbook_heal`:

- Does **not** consult `ManagedExtractBackend`/third-party providers (Wave-1 **CAP-054**–**058**) — no
  managed-scraping fallback exists for heal captures.
- Does **not** run any `ITranscodePostProcessor` (challenge / requires-login / thin-extract detection,
  Wave-1 **CAP-094**–**097**) — a DOM skeleton is returned even from a challenge page or login wall
  *unless* the URL-shape heuristic (**CAP-534**) or the `failure_reason`-based gate (**CAP-535**) already
  refused the call before any fetch happened. Once past those gates, there is no in-flight re-detection
  of challenge/login content in the actually-rendered page.
- Does **not** touch `ResponseBudgetPlanner`/`TokenBudget`/`MaterializationKey`/caching/receipts
  (Wave-1 **CAP-060**–**093**, **CAP-300**–**337**) — the DOM skeleton response has its own fixed shape
  with no `max_tokens`, no `if_none_match`, no `cache_ttl_s`, no receipt, and is never persisted or
  content-addressed.
- Does **not** consult `DomainTierRegistry` (Wave-1 **CAP-104**) or `RobotsThrottleService`
  (Wave-1 **CAP-103**/**CAP-190**) — heal captures are not robots.txt-gated or per-host throttled the
  way ordinary transcode/digest traffic is.
- **Does** reuse: `PrivacyClassifier`/SSRF blocking (**CAP-100**/**150**/**156**, via `FetchPreflight`),
  `SessionProfileHeaders` ID hardening (**CAP-069**/**167**), `FetchHeadersScope` (**CAP-170**), the
  browser daemon pool (**CAP-204**/**227**/**231**), shadow-DOM flattening (**CAP-219**), cookie-header
  injection (**CAP-171**), and per-navigation SSRF re-validation (**CAP-153**/**226**) — i.e. it reuses
  the *safety* and *browser-plumbing* layers but none of the *quality/budget/trust* layers.

## CAP-550 — `WorkerPaths.DomSkeletonScript` resolution + env override

**Evidence:** `Workers/WorkerPaths.cs` (`DomSkeletonScript = Path.Combine(root, "workers",
"browser-extract", "dom-skeleton-capture.mjs")`), `DomSkeletonWorker.ResolveScript` — checks
`paths.DomSkeletonScript` first, then `OCCAM_DOM_SKELETON_SCRIPT` env var, then a repo-root-relative
fallback via `WorkerPaths.TryGetRepoRoot()`. Three-tier resolution mirrors the pattern used for
`OCCAM_HTTP_EXTRACT_SCRIPT`/`OCCAM_BROWSER_EXTRACT_SCRIPT` (Wave-1 **CAP-351**) but has its own
dedicated env var not previously catalogued in Wave-1's `config-env.md` sweep.

## CAP-551 — Daemon-path failure silently falls back to one-shot (double-attempt, no combined telemetry)

**Evidence:** `DomSkeletonWorker.TryCaptureAsync` — if the pool is enabled/healthy but
`TryCaptureSkeletonJsonAsync` returns null, empty, or a payload that doesn't parse to `Ok:true`, the
method falls through to `TryCaptureOneShot` with **no record** of the discarded daemon attempt (unlike
transcode's `TranscodeAttempt`/recovery-log, Wave-1 **CAP-098**) — the caller only ever sees the
one-shot's outcome (or lack of one), with no visibility that a faster daemon attempt was tried and
silently discarded first. This means total heal latency can occasionally be "daemon attempt time +
timeout + full one-shot browser launch time" with nothing in the response explaining the delay.

## CAP-552 — DEAD CODE: unused `CreateHeadersScope` helper in `DomSkeletonWorker`

**Evidence:** `DomSkeletonWorker.cs` defines `private static FetchHeadersScope? CreateHeadersScope
(string? sessionProfile)` (lines 183–197) which independently resolves `SessionProfileHeaders.Resolve`
and wraps it in a `FetchHeadersScope` — but grepping the file confirms **no call site** references
`CreateHeadersScope` anywhere; the actual session/header resolution the class uses instead is
`FetchPreflight.Prepare` inside `HealAsync`/`TryCaptureAsync`. This looks like leftover code from an
earlier implementation, superseded but never removed.

## CAP-553 — HIDDEN/DEAD: worker's `--consent-aggressive` flag is unreachable from the MCP tool

**Evidence:** `dom-skeleton-capture.mjs` parses a `--consent-aggressive` CLI flag and, when present,
calls `tryDismissConsent(page)` (generic cookie-banner auto-dismiss, same family as Wave-1 **CAP-178**/
**CAP-211**) before capturing the skeleton. Grepping `DomSkeletonWorker.cs`'s process-arg construction
(`TryCaptureOneShot`) confirms this flag is **never appended** to `args` — there is no
`occam_playbook_heal` parameter or env var that maps to it. The capability exists in the worker script
but has zero live invocation path from the MCP surface; a page whose main content is obscured by an
undismissed cookie banner will capture that banner's DOM into the skeleton with no way for the caller to
ask for consent-dismissal.

## CAP-554 — Pool slot ok/fail bookkeeping tied into shared pool health tracking

**Evidence:** `DomSkeletonWorker.TryCaptureAsync` calls `browserPool.ReleaseSlot(slot, ok:true/false,
extractMs:...)` on every daemon-path attempt (success or failure) — this feeds the **same** pool health/
activity tracking (`BrowserPoolManager.MarkActivity`, Wave-1 **CAP-227**/**CAP-231**) used by ordinary
transcode/digest browser extraction, meaning a spate of failed heal captures against a broken site can
influence the pool's perceived health/recycle decisions for **unrelated** concurrent transcode traffic
sharing the same pool.

---

## Cross-cutting category check (per shared instructions)

| Category | Status |
|---|---|
| proxy | **Reused** — `EgressProxyConfig.ApplyTo(psi)` applied to the one-shot Playwright process spawn (`DomSkeletonWorker.TryCaptureOneShot`); daemon-path capture inherits whatever proxy config the already-running daemon process was launched with (not re-applied per skeleton call). |
| session | **Reused, partially** — headers yes (**CAP-543** cookie-header path), storageState **no** (**CAP-543** gap). |
| cookies | **Reused** — Cookie-header → `context.addCookies` (**CAP-171** family), confirmed bug-fixed inline comment in `dom-skeleton-capture.mjs`. |
| headers | **Reused** — `FetchPreflight`/`RequestHeadersMerger` merged headers written to a temp file, read by the worker (`--headers-file=`). |
| http | **Not used** — no HTTP-only capture path exists for this tool (**CAP-548**). |
| browser | **Core mechanism** — this tool is 100% browser-based (Playwright Chromium), via pool daemon or one-shot (**CAP-536**/**537**). |
| managed | **Not used** — no `ManagedExtractBackend`/third-party provider reference anywhere in the heal call chain (**CAP-549**) — confirms the assignment's suspicion. |
| retry | **Not used** — no explicit retry-on-failure of the same backend; the daemon→one-shot fallback (**CAP-551**) is a cascade, not a same-backend retry, and there is no loop/backoff. |
| cache | **Not used** — no response cache, no `cache_ttl_s`-style parameter, no `MaterializationKey` (**CAP-549**). |
| diff | **Not used** — no `if_none_match`/`diff_against` equivalent. |
| blocks | **Not used** — no `json_blocks`-style DOM block extraction; the DOM skeleton (**CAP-538**) is a structurally different, purpose-built tree, not the blocks sidecar. |
| tables | **Not used**. |
| chunks | **Not used**. |
| budget | **Not used** — no `max_tokens`/token-budget concept; `max_skeleton_nodes` is a **node-count** cap, not a token cap (**CAP-531**). |
| receipts | **Not used** — no signed receipt, no capsule, no Merkle proof on heal output (**CAP-549**). |
| merkle | **Not used** (see above). |
| capsules | **Not used** (see above). |
| playbooks | **Produces evidence for, does not consume** — heal never reads an existing playbook; it feeds the *next* call (`occam_playbook_save`, human/agent-authored JSON in between). |
| datasets | **Not used**. |
| claims | **Not used**. |
| trust tags | **Not used** — no `tag_trust`/suspicious/boilerplate annotation on skeleton nodes. |
| screenshots | **Not used** — no `capture_screenshot` equivalent; output is structural (tag/id/class/text), never a JPEG. |
| translate | **Not used**. |
| llms.txt | **Not used**. |
| feeds | **Not used**. |
| profile | **Gated** — tool itself is exposed **only** under `OCCAM_PROFILE=full` (**CAP-547**); no profile-conditional behavior *within* a call. |
| env | `OCCAM_DOM_SKELETON_SCRIPT` (**CAP-550**), plus every browser-pool/daemon/proxy/session env var inherited from the shared browser-worker subsystem (`OCCAM_BROWSER_*`, `OCCAM_SESSIONS_ROOT`, `OCCAM_HTTP(S)_PROXY`, `OCCAM_ALLOW_PRIVATE_URLS`) since this tool routes through the same `FetchPreflight`/`BrowserPoolManager`/`EgressProxyConfig` plumbing. |

---

## Capability graph edges

```
TOOL:occam_playbook_heal|USES|CAP-530
TOOL:occam_playbook_heal|USES|CAP-531
TOOL:occam_playbook_heal|USES|CAP-532
TOOL:occam_playbook_heal|USES|CAP-533
TOOL:occam_playbook_heal|USES|CAP-534
TOOL:occam_playbook_heal|USES|CAP-535
TOOL:occam_playbook_heal|USES|CAP-536
TOOL:occam_playbook_heal|USES|CAP-537
TOOL:occam_playbook_heal|USES|CAP-538
TOOL:occam_playbook_heal|USES|CAP-539
TOOL:occam_playbook_heal|USES|CAP-540
TOOL:occam_playbook_heal|USES|CAP-541
TOOL:occam_playbook_heal|USES|CAP-542
TOOL:occam_playbook_heal|USES|CAP-543
TOOL:occam_playbook_heal|USES|CAP-544
TOOL:occam_playbook_heal|USES|CAP-545
TOOL:occam_playbook_heal|USES|CAP-546
TOOL:occam_playbook_heal|USES|CAP-547
TOOL:occam_playbook_heal|USES|CAP-548
TOOL:occam_playbook_heal|USES|CAP-549
TOOL:occam_playbook_heal|USES|CAP-550
TOOL:occam_playbook_heal|USES|CAP-551
TOOL:occam_playbook_heal|USES|CAP-552
TOOL:occam_playbook_heal|USES|CAP-553
TOOL:occam_playbook_heal|USES|CAP-554
TOOL:occam_playbook_heal|USES|CAP-100
TOOL:occam_playbook_heal|USES|CAP-150
TOOL:occam_playbook_heal|USES|CAP-069
TOOL:occam_playbook_heal|USES|CAP-167
TOOL:occam_playbook_heal|USES|CAP-170
TOOL:occam_playbook_heal|USES|CAP-171
TOOL:occam_playbook_heal|USES|CAP-204
TOOL:occam_playbook_heal|USES|CAP-219
TOOL:occam_playbook_heal|USES|CAP-226
TOOL:occam_playbook_heal|USES|CAP-229
PARAM:url|ENABLES|CAP-530
PARAM:failure_reason|ENABLES|CAP-530
PARAM:failure_reason|ENABLES|CAP-533
PARAM:max_skeleton_nodes|ENABLES|CAP-531
PARAM:session_profile|ENABLES|CAP-535
PARAM:session_profile|ENABLES|CAP-543
CAP-536|ROUTES_TO|browser_daemon_pool
CAP-537|ROUTES_TO|browser_one_shot_playwright
CAP-536|FALLS_BACK_TO|CAP-537
CAP-532|FALLS_BACK_TO|heal_not_applicable_or_terminal_failure
CAP-538|PRODUCES|domSkeleton
CAP-538|PRODUCES|anchors
CAP-544|PRODUCES|anchors
CAP-545|PRODUCES|agentHints
CAP-543|CONSUMES|session
CAP-536|CONSUMES|session
CAP-537|CONSUMES|session
CAP-549|FALLS_BACK_TO|not_applicable_no_managed_backend
CAP-547|ROUTES_TO|OCCAM_PROFILE=full_only
```

---

## Failure code catalog for `occam_playbook_heal` (consolidated)

| Code | Source | agentHints present? |
|---|---|---|
| `invalid_url` | `PlaybookHealService.HealAsync` (CAP-530) | No |
| `invalid_failure_reason` | `PlaybookHealService.HealAsync` (CAP-530) | No |
| `captcha_or_challenge`, `private_url_blocked`, `workers_unavailable`, `timeout`, `invalid_arguments`, `invalid_policy` (as claimed `failure_reason`) | `PlaybookHealPolicy.IsTerminalFailure` (CAP-532) | Only `workers_unavailable` |
| `http_4xx`/`http_5xx` (as claimed `failure_reason`) | `PlaybookHealPolicy.IsTerminalFailure` (CAP-532) | No |
| `heal_not_applicable` | `PlaybookHealPolicy.ShouldOfferHeal` (CAP-533) | Yes |
| `private_url_blocked` (real, on the heal target itself) | `FetchPreflight.Prepare` via `DomSkeletonWorker` (CAP-530) | No |
| `workers_unavailable` | `WorkerPaths.IsConfigured` false / script missing / spawn failed (CAP-537, CAP-550) | Yes |
| `timeout` | One-shot capture exceeds 120s (CAP-537) | No |
| `extraction_failed` | No parseable JSON from worker, or payload `Ok:false` without a specific code (CAP-537) | No |
| `skeleton_capture_failed` / `playwright_missing` | Worker-reported (`dom-skeleton-capture.mjs` catch block) | No |

---

## Hidden / non-obvious capabilities (a user would never discover from the short MCP description)

1. **CAP-547** — the tool is invisible entirely under `OCCAM_PROFILE=reader|researcher|auditor`; only
   `full` (the default) exposes it.
2. **CAP-543** — `session_profile` support is real but **partial**: only the Cookie *header* is applied;
   a profile whose auth lives in a Playwright `storageState` file (localStorage, richer cookie jar) is
   silently degraded to an anonymous capture with no warning field in the response.
3. **CAP-549** — heal completely bypasses `OccamRouter`, post-processors, budget/cache/receipt
   subsystems, `DomainTierRegistry`, and robots/throttle politeness — none of `occam_transcode`'s
   quality/trust/politeness machinery applies here, only its safety plumbing (SSRF, session hardening).
2. **CAP-546** — most of this tool's own failure codes (`timeout`, `extraction_failed`,
   `skeleton_capture_failed`, `playwright_missing`, `invalid_url`, `invalid_failure_reason`) get **no**
   `agentHints` guidance at all, unlike the rich `agentMeta.decisions` pattern on `occam_transcode`.
3. **CAP-553** — the underlying worker script supports aggressive consent/cookie-banner dismissal via
   `--consent-aggressive`, but the C# host never passes this flag — dead capability from the MCP surface.
4. **CAP-551** — a daemon-pool capture attempt that fails is silently retried via a full one-shot
   Chromium launch with no trace in the response of the discarded first attempt or the extra latency.
5. **CAP-548** — there is no HTTP-only or `backend_policy` degrade path; if Playwright is unavailable,
   heal fails outright rather than returning any partial, non-DOM-derived evidence.

## Uncertainties

- `ShouldOfferHeal`/the terminal-failure gate trust the **caller-declared** `failure_reason` string; the
  service does not independently re-verify against a fresh probe/transcode that the claimed failure is
  still true at heal time — a stale or fabricated `failure_reason` from an agent would still pass the
  gate as long as it names a heal-eligible code.
- Whether the browser daemon's `/skeleton` HTTP handler (server side of `TryCaptureSkeletonJsonAsync`)
  has its own independent SSRF/timeout/error handling beyond what `dom-skeleton-capture.mjs` implements
  was not located/read in this audit (daemon server-side route registration file was out of the assigned
  file set) — assumed to delegate to the same capture logic but not directly confirmed line-by-line.
- Exact behavior when `max_skeleton_nodes` is passed as a value ≤0 or negative was not traced through
  `Math.Clamp` edge cases beyond confirming the clamp call exists (clamp with min 50 makes ≤0 inputs
  resolve to 50, per standard `Math.Clamp` semantics — not separately unit-verified here).

## Unresolved items

- No live/gate execution was performed as part of this audit (static code read only, per Wave 2
  instructions) — behavioral claims about timeouts/fallback ordering are trace-based, not runtime-timed.
