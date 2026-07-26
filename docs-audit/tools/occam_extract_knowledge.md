# `occam_extract_knowledge` — Deep Capability Audit (Wave 2)

**Source of truth:** current executable code only (`src/FFOccamMcp.Core/**`, `workers/css-extract/**`,
`workers/browser-extract/lib/browser-session.mjs`). Docs (`docs/*.md`, `MCP_API_SPEC.md`, `AGENTS.md`)
were **not** used as evidence — every claim cites a file/line read directly.

**CAP ID range owned by this audit:** `CAP-590`–`CAP-619` (used: CAP-590…CAP-602; remainder reserved).

---

## 0. Entry point and schema

`OccamExtractKnowledgeTool` (`src/FFOccamMcp.Core/Tools/OccamExtractKnowledgeTool.cs:12-54`) is the MCP
handler. Only **three** parameters exist — by far the smallest schema of any core tool:

```
url (required), backend_policy = "http_then_browser", session_profile = null
```

There is **no** `max_tokens`, `fit_markdown`, `focus_query`, `json_blocks`, `playbook_policy`,
`if_none_match`, `capture_screenshot`, receipts opt-ins, etc. — none of `occam_transcode`'s ~19 sidecar
params apply here, because this tool does **not** run through `TranscodePipeline`/`OccamRouter` at all
(see CAP-591). It delegates straight to `KnowledgeExtractService.Extract`
(`Services/KnowledgeExtractService.cs:15-147`).

---

## CAP-590 — Recipe D: resolve → schema-match → CSS-extract (the actual pipeline)

**Evidence:** `Services/KnowledgeExtractService.cs:15-147`, `Playbooks/KnowledgeSchemaPlanner.cs`,
`Extract/FieldSpecParser.cs`, `Workers/CssExtractWorker.cs`.

The live call sequence, distinct from every other core tool:

1. `FetchPreflight.Prepare(url, sessionProfile)` — same SSRF/private-URL guard and `session_profile`
 resolution as `occam_transcode` (`CAP-050`/`CAP-068`/`CAP-069` from Wave 1 — reused, not reimplemented).
2. `PlaybookSeedResolver.ResolveExtended(new PlaybookResolveOptions(url))` — the **same** tiered playbook
 resolver `occam_playbook_resolve`/`occam_transcode(playbook_policy=auto)` use (local → `WT_PLAYBOOKS_PATH`
 → community → seeds). Failure here → `playbook_not_found`.
3. `KnowledgeSchemaPlanner.TryMatch(playbookRoot, url, …)` — requires the resolved playbook to have a
 non-empty `knowledge_schema` object (`KnowledgeSchemaPlanner.cs:26-32` → `knowledge_schema_missing`),
 then matches the URL path against `genome.page_classes` patterns (longest-pattern-first) to pick a
 schema class, falling back to a `"default"` class if present (`page_class_unmatched` if neither matches;
 `knowledge_schema_empty` if the matched class has zero fields).
4. `FieldSpecParser.ParseFromSchemaFields` — converts the matched class's JSON field specs
 (`selector`, `attr`, `multiple`, `divide`) into a `FieldExtractionPlan`. A field with no/blank `selector`
 throws `ArgumentException` → surfaced as `invalid_arguments`.
5. `CssExtractWorker.Extract` spawns `workers/css-extract/css-extract.mjs` as a **fresh Node process per
 call** — no daemon variant exists for this worker (unlike HTTP/browser extract).
6. Facts are rebuilt host-side by `BuildFacts` matching plan field keys against the worker's returned
 JSON object, formatting arrays/booleans/numbers to strings — **not** the raw worker JSON passed through
 verbatim.

This is a materially different, narrower pipeline than `occam_transcode`'s — no `ThinExtractPostProcessor`,
no `ChallengePagePostProcessor`, no `RequiresLoginPostProcessor`/`AccessClassifier`, no
`DomainTierRegistry`, no `RobotsThrottleService`, no `ResponseBudgetPlanner`, no `OmittedManifest`, no
media-ref collection, no managed-backend escalation tier. None of those Wave-1 subsystems are reachable
from this tool (confirmed by absence of any call site in `KnowledgeExtractService`/`CssExtractWorker`).

## CAP-591 — `occam_extract_knowledge` bypasses `TranscodePipeline`/`OccamRouter` entirely

**Evidence:** `Services/KnowledgeExtractService.cs` has no reference to `OccamRouter`, `TranscodePipeline`,
`IExtractBackend`, `HttpExtractBackend`, or `BrowserExtractBackend`. It talks directly to a dedicated
`CssExtractWorker` → `workers/css-extract/css-extract.mjs`, a **third, independent** worker script beside
`http-extract/extract.mjs` and `browser-extract/browser-extract.mjs`.

Practical consequence: quality/safety infrastructure that a user might assume is universal across "any
Occam tool that fetches a URL" (challenge detection, thin-extract detection, robots politeness, domain
tier hints, receipts) simply does not apply to structured field extraction. A page behind a Cloudflare
challenge will not be reported as `captcha_or_challenge` here — it will most likely surface as
`http_403`/`extraction_failed`/empty facts with no dedicated challenge signal.

## CAP-592 — `css-extract.mjs`: HTTP-only by default, no daemon, 45s hardcoded worker-side timeout

**Evidence:** `workers/css-extract/css-extract.mjs:39-46` (`AbortSignal.timeout(45_000)`, hardcoded, not
env-configurable at the worker level); `Workers/CssExtractWorker.cs:13` (`timeoutMs = 45_000` default
parameter, clamped `Math.Clamp(timeoutMs, 5000, 120_000)` at the process-capture layer, but the caller
(`KnowledgeExtractService`) never passes a non-default value, so the effective timeout is always 45s
end-to-end). `EgressProxyConfig.ApplyTo(psi)` (`CssExtractWorker.cs:64`) means proxy env vars
(`OCCAM_HTTP_PROXY`/`OCCAM_HTTPS_PROXY`/`OCCAM_NO_PROXY`, Wave-1 CAP-157/159) reach the worker's
`egressFetch` call transparently — proxy support is inherited, not reimplemented.

## CAP-593 — Browser-fallback bypasses the browser pool/daemon entirely (per-call throwaway Playwright session)

**Evidence:** `workers/css-extract/css-extract.mjs:111-128` (`fetchHtmlViaBrowser`) —
`await import("../browser-extract/lib/browser-session.mjs")` then `createBrowserSession()` with **zero
arguments**, `page.goto(url, {waitUntil:"domcontentloaded", timeout:45_000})`, a flat
`page.waitForTimeout(1500)`, `page.content()`, then `session.close()` — a brand-new Chromium context is
launched, used once, and torn down, per fallback invocation. This is confirmed to bypass:
- `BrowserPoolManager` / the persistent multi-slot browser daemon (Wave-1 CAP-204/227/231/232) —
 no pool slot is acquired, no daemon RPC is made.
- `BrowserConcurrencyGate`/`BrowserConcurrencyLimiter` (Wave-1 CAP-230/248b) — the concurrency-limiting
 layer that gates `BrowserExtractBackend`'s own calls is never consulted for this path, so a burst of
 `occam_extract_knowledge` browser-fallback calls can launch N simultaneous throwaway Chromium processes
 with no shared-limit backpressure from Occam's own concurrency control (OS/machine resource limits are
 the only remaining ceiling).
- Playbook-driven browser interaction (Wave-1 CAP-220), consent-banner auto-dismiss (CAP-211/212),
 shadow-DOM flattening (CAP-219), and challenge/CAPTCHA detection (CAP-210) — none of these
 `browser-session.mjs`-adjacent behaviors are invoked by `fetchHtmlViaBrowser`; it is a bare
 goto+wait+content() call, the thinnest possible use of the browser worker's session primitive.

This matches and extends Wave-1's `CAP-236` finding — confirmed independently here from the tool-usage
side: `occam_extract_knowledge` is the **only** caller that ever sets `browserFallback: true` into
`css-extract.mjs` (`KnowledgeExtractService.cs:96,109-113` — only fires after an initial HTTP attempt
returns `http_401`/`http_403`/`http_429`/`timeout`/`extraction_failed` **and** the effective policy allows
browser).

## CAP-594 — HIDDEN: `session_profile` silently does not reach the browser-fallback leg

**Evidence:** `Services/KnowledgeExtractService.cs:92-114` — `preflight.ActiveHeadersFile` (the resolved
`session_profile` headers/cookies temp file) is passed to **every** `cssExtractWorker.Extract(...,
headersFile: headersFile)` call, including the retry-with-browser-fallback call
(line 109-113: `browserFallback: true, headersFile: headersFile`). But inside
`css-extract.mjs`, the `--headers-file=` argument is only read and applied (`requestHeaders`, merged into
the `egressFetch` call's `headers`) in the **initial plain-HTTP fetch branch** (lines 31-46) — the
`fetchHtmlViaBrowser(url)` function (lines 111-128, invoked when that initial fetch returns
401/403/429 and `--browser-fallback` was passed) takes only a bare `url` argument and calls
`createBrowserSession()` with **no** `headersFile`/`extraHTTPHeaders`/`storageState` option at all.

Net effect: a caller who configured `session_profile` specifically to get past a login wall will have
that session applied to the doomed-to-401/403 first HTTP attempt, but **not** to the browser fallback that
is supposed to succeed where HTTP failed — the browser session that actually renders the page is always
anonymous/logged-out. This is a genuine, non-obvious functional gap: nothing in the tool's schema or
description suggests `session_profile` only half-applies depending on which internal leg ends up serving
the request.

## CAP-595 — `confidence` is a dead field: always `0.0` on success, never assigned

**Evidence:** `Services/KnowledgeExtractService.cs:135-145` — the success-path construction of
`KnowledgeExtractResult` sets `Ok`, `Url`, `PlaybookId`, `PageClass`, `Facts`, `Meta`, `LatencyMs`,
`Backend` — **`Confidence` is never set**. `KnowledgeExtractResult.Confidence` (`double`, line 235) has
no default other than CLR `0.0`, and a repo-wide search confirms it is assigned nowhere in the codebase
outside its own declaration. `OccamExtractKnowledgeTool.cs:87` passes `result.Confidence` into both the
top-level `Confidence` field and the `Receipt.Confidence` field of the success response — both of which
carry `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]` (`OccamExtractKnowledgeTool.cs:
113,127-128`). Consequence: **the `confidence` field never appears in a real `occam_extract_knowledge`
success response at all** (it is always the JSON-ignored default), despite being declared in the schema
and despite the doc-comment on `OccamExtractKnowledgeReceiptInfo` (line 111) explicitly calling it
"AF-3: receipt for knowledge extract" as if it carried a real confidence measurement. This looks like a
scaffolded-but-never-wired feature (the field, the mapper plumbing, and the JSON-ignore-when-default
convention all exist; only the one line that would compute and assign a real value is missing).

## CAP-596 — Fake "Receipt": `OccamExtractKnowledgeReceiptInfo` is not a signed `ReceiptEnvelope`

**Evidence:** `Tools/OccamExtractKnowledgeTool.cs:88,111-115`. `Receipt` on the success response is
`OccamExtractKnowledgeReceiptInfo(Confidence, ElapsedMs)` — two numbers, no `ReceiptEnvelope`
(Wave-1 `CAP-250`), no ECDSA signature, no `keyId`, no `contentHash`, no Merkle root. Given `Confidence`
is always `0.0`/omitted (**CAP-595**), the field that actually survives to the wire in practice is just
`{"elapsedMs": N}`. This is a **reuse of Wave-1 CAP-287** ("`occam_extract_knowledge`'s Receipt is NOT a
signed Receipt v1"), confirmed independently here at the exact same file/lines, with the added finding
that the one numeric field it does carry (`confidence`) is itself dead (CAP-595). A caller who sees a
field literally named `receipt` on a "core Occam tool" response and assumes — by analogy with
`occam_transcode`/`occam_digest`/`occam_claim_check`/`occam_dataset_export`, which all attach a real
`ReceiptEnvelope` when `OCCAM_RECEIPTS=1` — that it can hand this to `occam_verify` will get
`invalid_receipt`: there is nothing cryptographic to verify. `OCCAM_RECEIPTS` (Wave-1 CAP-090/280) has
**zero** effect on this tool either way — receipts are unconditionally this fake shape whether the global
receipts policy is on or off.

## CAP-597 — Per-call fresh Node process, no daemon, for the CSS-extract worker

**Evidence:** `Workers/CssExtractWorker.cs:66` (`WorkerProcessGroup.Start(psi)` inside `Extract`, called
fresh on every invocation — no persistent-process/daemon variant exists for CSS extract, unlike
`http-extract`'s daemon (Wave-1 CAP-201) or `browser-extract`'s pool (CAP-204)). Each call also writes and
best-effort-deletes a temp JSON field-spec file (`occam-fields-<guid>.json`, line 23, deleted in `finally`
at line 130) — a real filesystem round-trip per extraction call, present because the field spec is handed
to the worker as a file path argument rather than via stdin/env, for reasons not evidenced in code (kept
consistent with the also-file-based `--headers-file=` mechanism).

## CAP-598 — `readNuxtPath`: `eval()` over page-controlled Nuxt SSR state (attr=`nuxt`)

**Evidence:** `workers/css-extract/lib/css-schema-extract.mjs:131-163` (`readNuxtPath`) — for a field spec
with `attr: "nuxt"`, the worker regex-extracts the raw JS expression assigned to `window.__NUXT__` out of
the fetched HTML and evaluates it via `(0, eval)(match[1])` (line 139) to get a real JS object, then walks
a dotted/indexed `path` (from the playbook's field spec) into that object. The evaluated string originates
from the **target page's own HTML** — i.e. attacker-controlled if the target site is hostile or
compromised — executed in the Node worker process with no sandboxing beyond a bare `try/catch`. This is a
genuine code-execution surface distinct from (and arguably worse than) the `attr:"regex"` path
(**CAP-599**), which only builds a `RegExp` (ReDoS risk, not RCE) from a playbook-supplied pattern
template. Scope note: exploitability requires (a) a playbook with a `nuxt`-typed field pointed at the
attacker's page, and (b) the page actually serving a `window.__NUXT__=...</script>` blob shaped to trigger
malicious evaluated code — not reachable from a bare `url` call with no matching playbook, but real once a
`knowledge_schema` with a `nuxt` field exists for a host (whether from a local, community, or seed
playbook).

## CAP-599 — `attr: "regex"` / `attr: "const"` field extraction modes (undocumented on the MCP surface)

**Evidence:** `workers/css-extract/lib/css-schema-extract.mjs:59,84-94,124-129`. Beyond the "obvious"
CSS-selector + attribute (`text`/`html`/`href`/`src`/other-attr) extraction, the field spec format
(driven entirely by the resolved playbook's `knowledge_schema`, never by the MCP caller directly) supports:
- `attr: "regex"` — `selector` is treated as a regex **pattern template** with a `{id}` placeholder
 substituted from an item ID captured out of the page URL path (`/item/(\d+)/`), then matched against the
 **raw HTML** (not the DOM) with the `s` (dotall) flag — i.e. an escape hatch for data embedded in
 `<script>` blobs or comments that isn't reachable via CSS selectors at all.
- `attr: "const"` — `selector`'s literal string value is returned unmodified as the field's value (a
 static/constant fact, e.g. a hardcoded category label), with no page interaction at all.
- `divide` — for `nuxt`/`regex`/CSS-derived numeric-looking strings, `applyDivide` parses the value as a
 number (comma-as-decimal-tolerant) and divides it, reformatting with `formatDecimal` — a unit-conversion
 primitive (e.g. cents→currency-units) baked into the extraction spec itself.
None of these modes are visible from the `occam_extract_knowledge` MCP schema (which has no `attr`/`mode`
parameter at all) — they only exist inside a resolved playbook's `knowledge_schema` JSON, authored via
`occam_playbook_heal`/`occam_playbook_save` or hand-written community/local playbooks.

## CAP-600 — `base_selector` row-mode: structured list/table extraction distinct from flat facts

**Evidence:** `workers/css-extract/lib/css-schema-extract.mjs:51-76`, `Extract/FieldExtractionPlan.cs:15`
(`RowMode => !string.IsNullOrWhiteSpace(BaseSelector)`). When a `knowledge_schema` class includes a
`base_selector`, the worker switches from single-object flat-field extraction to **row mode**: it selects
all DOM nodes matching `base_selector`, then re-runs each field's selector **scoped inside each base node**
(`base.querySelectorAll(selector)`, not `doc.querySelectorAll`), returning `{ rows: [...], row_count: N }`
instead of a flat field map. Rows where every field is empty/null are dropped. However,
`KnowledgeExtractService.BuildFacts` (`Services/KnowledgeExtractService.cs:183-203`) only ever reads
`data.TryGetProperty(field.Key, ...)` against a flat object — it has **no code path that reads `rows`/
`row_count`** from a row-mode response. This means: if a resolved playbook's `knowledge_schema` for a
given page class sets `base_selector`, the MCP tool's `facts[]` output will come back **empty** (none of
the flat field keys exist at the top level of a `{rows, row_count}` payload) even though the worker did
real, correct extraction work — a genuine dead/unreachable capability from the `occam_extract_knowledge`
tool's perspective specifically (row mode is real, tested-looking worker code, but the host-side mapper
was never updated to consume it).

## CAP-601 — `occam_extract_knowledge` failure taxonomy is a distinct, narrower set

**Evidence:** consolidated from `KnowledgeExtractService.cs`, `KnowledgeSchemaPlanner.cs`,
`CssExtractWorker.cs`, `TranscodeAgentDecisions.cs:63-83`.

| Code | Source | Agent hint (from `TranscodeAgentDecisions`) |
|---|---|---|
| `invalid_arguments` | missing `url` / bad `backend_policy` / empty field spec | — |
| `invalid_url` → normalized | `FetchPreflight.Prepare` (reused SSRF/shape guard) | — |
| `private_url_blocked` | `FetchPreflight.Prepare` | — |
| `playbook_not_found` | `PlaybookSeedResolver.ResolveExtended` returned no playbook | `continue` — implies no schema exists, try `occam_transcode` |
| `knowledge_schema_missing` / `page_class_unmatched` / `knowledge_schema_empty` | `KnowledgeSchemaPlanner.TryMatch` | `stop` — "use `occam_transcode` for markdown instead of extract" (`Tool: occam_transcode`) |
| `workers_unavailable` | `workerPaths.IsConfigured` false (host-level) or `IsCssExtractConfigured` false (service-level) | — |
| `timeout` | `CssExtractWorker` (45s clamp) | — |
| `http_401`/`http_403`/`http_429` | initial HTTP fetch inside `css-extract.mjs`, also the browser-fallback trigger set | — |
| `extraction_failed` | generic worker/parse failure; `content_extraction_failed` from the worker is remapped to this (`KnowledgeExtractService.cs:118-122`) | — |
| `browser_unavailable` / `browser_failed` | `fetchHtmlViaBrowser` catch block, mapped verbatim as the CSS worker's `failure` string, then passed through `FailureCodeStrings.Normalize` on the host side (falls through to `extraction_failed`-shaped handling unless explicitly recognized) | — |
| `no_json[:...]` / `bad_json` | `NodeWorkerOutputCapture` parse failure (shared plumbing with other workers) | — |

Notably **absent** from this tool's reachable codes: `captcha_or_challenge`, `requires_login`,
`thin_extract`, `response_too_large`, `dns_error`/`tls_error`/`network_error` (the raw HTTP fetch inside
`css-extract.mjs` does not appear to classify these separately — a generic `catch` maps unrecognized
errors to `extraction_failed`/`timeout` only, per the worker's outer `try/catch`, lines 94-109).

## CAP-602 — Partial facts on failure

**Evidence:** `Services/KnowledgeExtractService.cs:124-131`, `Tools/OccamExtractKnowledgeTool.cs:101`
(`PartialFacts`). Even on a failed extraction (e.g. worker returned partial/malformed data before
erroring), `BuildFacts(plan, extract.Data)` is still run against whatever `Data` the worker did return, and
attached to the failure response as `partialFacts` — letting a caller see which fields DID resolve even
when the overall call is `ok:false`. This mirrors the general Occam pattern of "failure ≠ nothing was
learned" seen elsewhere (e.g. router recovery logs), reused here at a per-field granularity.

---

## Capability graph edges

```
TOOL|USES|CAP-590
TOOL|USES|CAP-591
CAP-590|CONSUMES|session
CAP-590|ROUTES_TO|css-extract.mjs
PARAM:url|ENABLES|CAP-590
PARAM:backend_policy|ENABLES|CAP-593
PARAM:session_profile|ENABLES|CAP-594
CAP-591|FALLS_BACK_TO|not_applicable
CAP-592|CONSUMES|proxy
CAP-593|ROUTES_TO|browser-session.mjs
CAP-593|FALLS_BACK_TO|throwaway_chromium_context
CAP-594|PRODUCES|hidden_gap
CAP-595|PRODUCES|dead_field
CAP-596|PRODUCES|fake_receipt
CAP-597|ROUTES_TO|css-extract.mjs
CAP-598|CONSUMES|attacker_controlled_html
CAP-599|CONSUMES|playbook_field_spec
CAP-600|PRODUCES|unreachable_row_mode
CAP-601|PRODUCES|failure_taxonomy
CAP-602|PRODUCES|partial_facts
CAP-590|USES|CAP-050
CAP-590|USES|CAP-068
CAP-590|USES|CAP-069
CAP-590|USES|CAP-070
CAP-590|USES|CAP-100
CAP-592|USES|CAP-102
CAP-592|USES|CAP-157
CAP-593|USES|CAP-236
CAP-596|USES|CAP-287
CAP-596|USES|CAP-090
```

---

## Cross-cutting categories checked

| Category | Status |
|---|---|
| Proxy | Used — `EgressProxyConfig.ApplyTo(psi)` (CssExtractWorker.cs:64) + worker's own `egressFetch` (CAP-592). |
| Session/cookies/headers | Used for the HTTP leg only; **silently absent** on the browser-fallback leg (CAP-594) — genuine gap. |
| HTTP | Yes — `egressFetch` in `css-extract.mjs`, 45s fixed timeout, no retry. |
| Browser | Yes, but only as a narrow 401/403/429-triggered fallback with no pool/daemon/recipes (CAP-593). |
| Managed providers | Not used — `ManagedExtractBackend` is never referenced from this tool's code path. |
| Retry | Not used — one HTTP attempt, at most one browser-fallback attempt; no same-backend retry. |
| Cache | Not used — no `TranscodeResponseCache`/`cache_ttl_s`-equivalent for this tool. |
| Diff | Not used — no `if_none_match`/`diff_against` equivalent. |
| Blocks/tables/chunks | Not used — this tool's "structured data" model is `facts[]`, unrelated to `json_blocks`/`json_tables`/`semantic_chunking`. |
| Budget | Not used — no `max_tokens`/`ResponseBudgetPlanner` involvement; facts are small enough that no budget system was built for them. |
| Receipts | Present in name only, not cryptographically real (CAP-596); `OCCAM_RECEIPTS` has no effect here. |
| Merkle / capsules | Not used. |
| Playbooks | Central — `PlaybookSeedResolver` + `knowledge_schema` + `genome.page_classes` are the entire routing mechanism (CAP-590). |
| Datasets / claims | Not used from this tool. |
| Trust tags (`tag_trust`) | Not used — no `json_blocks`-style block model exists here to tag. |
| Screenshots | Not used. |
| Translate | Not used. |
| llms.txt | Not used. |
| Feeds | Not used. |
| Profile (`OCCAM_PROFILE`) | Exposed in `reader`/`researcher`/`auditor`/`full` — the only always-included non-transcode/probe/digest/map/search/capabilities tool in the narrowest `reader` set (`Transport/OccamToolProfile.cs:25`). |
| Env vars | Only indirectly, via reused subsystems: `OCCAM_HOME`/worker-path resolution (`workerPaths.IsCssExtractConfigured`), `OCCAM_SESSIONS_ROOT` (session_profile), `OCCAM_HTTP_PROXY`/`OCCAM_HTTPS_PROXY`/`OCCAM_NO_PROXY` (egress proxy), `OCCAM_ALLOW_PRIVATE_URLS` (preflight). No `occam_extract_knowledge`-specific env var was found. |

---

## Hidden / non-obvious capabilities (a user would never discover from the short tool description)

The tool description says: *"Extract typed structured fields from a page (e.g. title, price, author) as
facts[], driven by the site's playbook knowledge_schema... requires a resolvable schema for the host."*
A user reading only that would never learn:

1. This tool runs a **completely separate extraction pipeline** from `occam_transcode`/`occam_digest` —
 none of the transcode-side safety/quality nets (challenge detection, thin-extract, robots politeness,
 domain tiers, budget planning) apply here at all (CAP-591).
2. `session_profile` **only protects the first HTTP attempt**; if that attempt gets 401/403/429'd and the
 tool escalates to its browser fallback, the browser session is anonymous — the very case where a session
 would matter most (CAP-594).
3. The browser fallback is a **bare, unpooled, un-recipe'd Chromium launch** — no consent-dismiss, no
 shadow-DOM flattening, no challenge detection, no shared concurrency gate with the rest of the browser
 subsystem (CAP-593).
4. The response's `confidence`/`receipt.confidence` fields are **dead** — always 0.0, always omitted from
 the actual JSON on success (CAP-595).
5. `receipt` on this tool is **not** a real cryptographic `ReceiptEnvelope` like every sibling tool's —
 it's two numbers, one of which is dead (CAP-596).
6. Playbook authors can write `attr:"nuxt"` fields that `eval()` page-supplied JavaScript-shaped state
 inside the worker process — a code-execution-adjacent extraction mode with no sandboxing (CAP-598), plus
 `attr:"regex"`/`attr:"const"`/`divide` modes (CAP-599), none of which are mentioned in the tool schema.
7. A `knowledge_schema` class using `base_selector` (row/list mode) will silently return **empty
 `facts[]`** through this MCP tool even though the worker extracted real rows — the host-side mapper never
 reads `rows`/`row_count` (CAP-600).

---

## Uncertainties

- Whether `content_extraction_failed` (remapped to `extraction_failed` at `KnowledgeExtractService.cs:
 119-122`) is ever actually emitted by `css-extract.mjs` under that exact string — no occurrence of that
 literal string was found in `css-extract.mjs`/`css-schema-extract.mjs`; the remap may be defensive code
 for a code path in `FailureCodeStrings.Normalize` not fully traced in this pass.
- Whether `PlaybookGenomeMerger.ParseRoot`'s merge (used to build `playbookRoot` before
 `KnowledgeSchemaPlanner.TryMatch`) can combine a `knowledge_schema` from one tier (e.g. seed) with
 `genome.page_classes` from another (e.g. community) — the merge itself is Wave-1/PB4a territory and was
 not re-derived line-by-line here; assumed consistent with `occam_playbook_resolve`'s documented tiering
 based on the shared resolver call.
- Exact behavior when `attr: "regex"`'s `{id}` placeholder has no `/item/(\d+)/`-shaped URL to match
 against (code returns `""` for the id and substitutes an empty string into the pattern) — read
 structurally, not exercised against a live fixture in this pass.

---

## Completeness

**COMPLETE** for the tool's own code path (schema → service → schema-match → worker → response mapping),
the CSS-extract worker internals, and the specific browser-fallback-bypasses-pool claim (CAP-236 origin)
and fake-receipt claim (CAP-287 origin) requested by the assignment. Not re-derived from scratch: the
shared `FetchPreflight`/`PrivacyClassifier`/`PlaybookSeedResolver` subsystems, which are Wave-1-owned and
only cited here by reference.
