# HANDBOOK-OUTLINE (Phase 5S)

**Agent:** P5-S
**SoT:** executable code → Wave-4 correction layer → canonical ledgers → subsystem/tool evidence.
**Docs (`docs/`, `README.md`, `llms.txt`) were not used as an input and are not an authority here.**
**Date:** 2026-07-26

**This file is a design for a handbook. It is not the handbook.** No chapter prose is written here.

**Purpose statement it must serve (product owner):** *"If I did not write Occam, how do I become someone who genuinely understands what it can do and how it works?"* The test of every chapter below is therefore **comprehension**, not coverage. A chapter that enumerates parameters and teaches nothing fails, even if every parameter is correct.

**Canonical model this outline is derived from:** 9 product systems · 39 registered family slugs (38 live + `canonical-knowledge-ir` retained as a shipped-dead evidence cluster) · 38 product capabilities · 674 CAPs (`canonical-capabilities.json`, `PRODUCT-TAXONOMY.md` §0). Reconciled moves applied: `quality-failure-semantics` → PS-1 (T-1), `digest-synthesis` → PS-7 (T-2).

---

## 0. Design rules, stated before the chapter list

Five rules constrain every decision below. They are the reason the structure differs from the proposed 25-chapter progression.

| # | Rule | Consequence for structure | Evidence |
|---|------|---------------------------|----------|
| D1 | **The honesty contract is prerequisite, not payoff.** `ok:false` means the content is UNKNOWN; an agent that ignores it converts the entire trust layer into decoration and nothing in the codebase detects it. | It becomes **Ch 2**, not a trust chapter at 12–13. | `TRUST-MODEL.md` §9.5; CAP-105; `PRODUCT-TAXONOMY.md` §6 step 1 |
| D2 | **Teach the ladder as one object.** The cascade is the single most-miswritten area in the whole corpus. Splitting HTTP / browser / managed into three chapters reproduces the myth, because the short-circuits and the dual-fail ranking only exist *between* the rungs. | Proposed ch 4 + 7 + 8 collapse into **Ch 5**. | C1 · EF-056 · GAP-001 · `OccamRouter.cs:134-182` · `FailureRanking.cs:10-21` |
| D3 | **Exposure class drives chapter weight, not CAP count.** PS-9 owns 159 CAPs and PS-1 owns 122; equal billing would produce an installer manual with a web reader attached. | Operator material is one chapter (Ch 19) plus a minimal setup chapter (Ch 3); `DOCUMENTATION-EXPOSURE-MATRIX` classes decide depth. | `PRODUCT-TAXONOMY.md` §1.3 · `DOCUMENTATION-EXPOSURE-MATRIX.md` §1 |
| D4 | **A name that overclaims does not get a chapter for its name.** A chapter is a promise of product weight. `consensus-crosscheck` is `DO_NOT_DOCUMENT_AS_FEATURE` for its claim; a chapter titled "Crosschecking" would grant the promise the code does not keep. | Proposed ch 17 becomes a section of **Ch 17 (opt-in surfaces)**. | `DOCUMENTATION-EXPOSURE-MATRIX.md` §2 · TRUST-MODEL §13 #9–10 · EF-031/032 |
| D5 | **The book must be falsifiable.** Every chapter carries a `CHECK` the reader can run; Ch 27 collects them into a protocol, including what it means when the observation disagrees with the text. | New final chapter; new per-chapter field. | Mission requirement 7; `DISCOVERABILITY-GATE.md` §3 (mechanical checks already designed) |

**Standing convention adopted here:** the thirteen "what Occam is NOT" statements in `PRODUCT-DEFINITION.md` §3 are **not** collected into a negative chapter. They are distributed as the per-chapter `MISCONCEPTION` field, so each is corrected at the moment the reader is most likely to form it. Only the six highest-frequency ones appear in Ch 1.

---

## 1. Chapter list at a glance

| # | Title | Part | Spine? | Class |
|---|-------|------|:------:|-------|
| 1 | What Occam is, and the six things it is not | A Orientation | | both |
| 2 | The honesty contract: `ok:false` means unknown | A | **●** | both |
| 3 | Standing up an install you can test this book against | A | | both |
| 4 | The request path — and why there is no single spine | A | **●** | both |
| 5 | Acquisition: the real ladder | B Reference path | **●** | both |
| 6 | When acquisition is hard: walls, sessions, egress | B | | both |
| 7 | Materialization: the token contract | B | **●** | both |
| 8 | Structured, differential and replayed output | B | | both |
| 9 | Spending less: discovery before acquisition | C Breadth | | both |
| 10 | Many sources in one call: digest | C | | both |
| 11 | Playbooks in band: resolution and the auto overlay | D Site-specific | | both |
| 12 | Authoring a playbook: heal → draft → lint → save | D | | both |
| 13 | Typed field extraction | D | | both |
| 14 | What a receipt proves — and what it does not | E Trust | **●** | both |
| 15 | Verifying: five modes, two surfaces, four asymmetries | E | | both |
| 16 | Evidence for claims and corpora | E | | both |
| 17 | Opt-in surfaces: watch, batch, crosscheck, atlas | F Deployment reality | | both (limits-first) |
| 18 | Exposure: 51 entrypoints, 15 default tools | F | **●** | both |
| 19 | Operating an install | F | | both |
| 20 | What Occam does without asking | F | | both |
| 21 | State, persistence and footprint | F | | both |
| 22 | Configuration and its negative space | F | | both |
| 23 | Security posture and threat model | F | | both |
| 24 | Composing tools: what chains, and what does not | G Synthesis | | both |
| 25 | Diagnosing a bad result | G | | both |
| 26 | Architecture internals | G | | handbook-only |
| 27 | Checking this book yourself | G | | handbook-only |

**CHAPTERS: 27.** Six spine chapters. Seven parts. Four appendices (§8).

---

## 2. The chapters

Field key — `OBJ` learning objective · `PRE` prerequisite chapters · `COVERS` product systems / family slugs · `SRC` source material · `MODEL` key mental model introduced · `EX` worked example (advances the recurring example of §5) · `MISC` common misconception corrected · `HONEST` what the chapter must admit · `DEPTH` pages / sections · `WHERE` handbook-only vs also public docs · `CHECK` the runnable falsifier.

---

### Ch 1 — What Occam is, and the six things it is not

| Field | Content |
|---|---|
| **OBJ** | State in one sentence what Occam does and does not do, and correctly reject six product categories it will be mistaken for. |
| **PRE** | none |
| **COVERS** | All nine systems at naming depth only. No family is taught here. |
| **SRC** | `PRODUCT-DEFINITION.md` §2 (adopt §2.1 verbatim as the chapter's thesis), §3.1–3.6 and §3.10, §5 boundary table; `PRODUCT-TAXONOMY.md` §2 hierarchy diagram |
| **MODEL** | **URL in → compiled, budgeted markdown out, or a typed refusal.** The object Occam returns is *not* the response body; it is a compiled reading of the page. |
| **EX** | Introduce Task R (§5). Show the two silent failure modes it must avoid: invented content, and an empty JS shell mistaken for the page. |
| **MISC** | "It is a fetcher." — A fetcher returns the response body. Occam returns compiled markdown, and the hash it later commits to is over that compiled form (`ReceiptCanonicalizer.cs:17-18`), not over the origin's bytes. |
| **HONEST** | Six rejections stated with their code proof, not as modesty: not a crawler (map caps at 64 links, `MapService.cs:10,76,180`); not a CAPTCHA bypass (no solver exists); not a cache or CDN (`TranscodeCacheEligibility.cs:13-16`); not a search engine (`occam_search` fails closed without `OCCAM_SEARCH_PROVIDER`); not a fact-checker (`ClaimCheckService.cs:102` hardcodes `not_evaluated`); not an LLM and it does not summarize. No token-reduction percentage anywhere in this chapter — the tokenizer is `heuristic-unicode-v1` with unmeasured error bounds. |
| **DEPTH** | 6–8 pages · 4 sections |
| **WHERE** | Both. The public front page is a compression of this chapter; the handbook version keeps the rejected-definition analysis. |
| **CHECK** | `occam_transcode` a page, then diff the returned `markdown` against `curl` of the same URL. The reader sees for themselves that these are different objects. |

---

### Ch 2 — The honesty contract: `ok:false` means unknown  ●SPINE

| Field | Content |
|---|---|
| **OBJ** | Read any Occam response correctly: decide from `ok`, `failure.code`, `quality.verdict`, `confidence`, `recovery[]` and `agentMeta.decisions` what is known, what is unknown, and what the correct next action is — without ever filling a gap from model memory. |
| **PRE** | Ch 1 |
| **COVERS** | PS-1 `quality-failure-semantics` (T-1); cross-cutting lens #7 (agent-facing response contract, T-5) which draws on PS-1 `acquisition-routing`, PS-2 `focus-selection`/`token-budget`, PS-3 `probe-diagnostics`, PS-7 `digest-synthesis`, PS-8 `mcp-exposure` |
| **SRC** | `TRUST-MODEL.md` §9.5; `FAILURE-BEHAVIOR-MAP.md`; `PRODUCT-TAXONOMY.md` §7.2 (the lens table is this chapter's skeleton); `PRODUCT-DEFINITION.md` §4.1; canonical cards `quality-failure-semantics.md`, `probe-diagnostics.md`; CAP-094/097/098/105/106/108, CAP-311, CAP-428-family, CAP-460 |
| **MODEL** | **A refusal is an answer.** `ok:false` is a typed statement that the content is UNKNOWN to the system. It is not "empty page", not "short page", and not permission to recall. |
| **EX** | Task R step 1: the API's marketing landing page returns `ok:false` / `thin_extract`. Contrast with the changelog page, which is genuinely three sentences and returns `ok:true` with `quality.verdict=short_quality`. Same size, opposite meaning, opposite correct action. |
| **MISC** | "`thin_extract` means the page was short." — It means the **extraction** was bad. A short good page is `ok:true` + `short_quality`, and healing or escalating it wastes a browser launch (CAP-097). |
| **HONEST** | The product makes honesty *possible* and cannot enforce it. If the agent substitutes recalled content, that content carries no hash, no receipt, no Merkle leaf — while the surrounding response may still be decorated with receipts belonging to URLs that *did* succeed. No mechanism detects this (TRUST-MODEL §9.5). Also: probe masks SSRF-policy refusals as `network_error` (GAP-003 / EF-042), so one failure code in the taxonomy is currently lying; say so here rather than in an appendix. |
| **DEPTH** | 10–12 pages · 6 sections (the contract · the code taxonomy · quality vs thin · `recovery[]` and `agentMeta.decisions` · what a signed failure proves · the one unenforceable rule) |
| **WHERE** | Both. This is the chapter public docs most need and the one most likely to be diluted; the handbook version keeps the "no mechanism detects this" paragraph. |
| **CHECK** | Call `occam_transcode` on a known 404 and on a known JS-only SPA. Assert: the 404 returns `http_404` with **no browser attempt in `recovery[]`**; the SPA shows an HTTP attempt followed by a browser attempt. |

---

### Ch 3 — Standing up an install you can test this book against

| Field | Content |
|---|---|
| **OBJ** | Get a working host, wired into one agent host, and run the book's first four `CHECK`s. Nothing more. |
| **PRE** | Ch 1 |
| **COVERS** | PS-9 `install-onboarding`, `host-connectors` (minimum path only); PS-8 `runtime-transports` (stdio only) |
| **SRC** | `ENTRYPOINT-MODEL.md` §7–§9; `USE-CASE-MODEL.md` UC-2 minimum entrypoints; FLOW-015, FLOW-016; `subsystems/install-onboard.md`, `subsystems/doctor.md`; canonical cards `install-onboarding.md`, `host-connectors.md` |
| **MODEL** | **Install is a prerequisite, not a subject.** The full operator surface is Ch 19; here you only need doctor → connect → one successful `occam_transcode`. |
| **EX** | Task R step 0: install, connect one host, call `occam_client_capabilities(context_tokens=…)` once, then `occam_transcode({url})` on the API docs index. |
| **MISC** | "`npx @ff-occam/mcp` is the quick path." — The npm package is unpublished and its packed launcher imports files outside its `files` set; it would be non-functional as packed (EF-034, `NEEDS_FIX_BEFORE_DOC` rank 5). Do not present npm install as available. |
| **HONEST** | Install is destructive replacement with no rollback (EF-028). Onboarding writes `~/.occam/onboard.json` **before** verification and that file is then merged into the environment of every later launcher invocation (EF-029, EF-050). Connect mutates third-party config files and its rollback is dead for restart-required hosts (EF-021) — back up before connecting. The canonical launcher is stdio-only and forwards no arguments (CAP-1001). |
| **DEPTH** | 6–8 pages · 5 sections |
| **WHERE** | Both, but with different endings: public docs end at "it works"; the handbook ends at "here is what it wrote to your machine, and Ch 21 explains why." |
| **CHECK** | After the first host start with `OCCAM_RECEIPTS=off`, list `~/.occam/keys/`. A signing key is there anyway (EF-044). This is the reader's first evidence that the book's honesty claims are load-bearing. |

---

### Ch 4 — The request path — and why there is no single spine  ●SPINE

| Field | Content |
|---|---|
| **OBJ** | Trace `occam_transcode(url)` end to end, and predict for any other tool whether it enters that path, partially enters it, or bypasses it entirely. |
| **PRE** | Ch 2, Ch 3 |
| **COVERS** | Structural view of PS-1 + PS-2 + the bypass spines of PS-3, PS-4, PS-5, PS-6 |
| **SRC** | `PRODUCT-ARCHITECTURE.md` §0 (the falsified hypothesis), §2 (14-step reference narrative), §3 (tool → spine table — reproduce it), §3 spine diagram |
| **MODEL** | **One reference narrative, several parallel spines.** `TranscodePipeline` is the transcode-family orchestration shell; `OccamRouter` owns escalation; probe, map, search, extract_knowledge, heal, resolve, lint, client_capabilities and atlas never enter either. |
| **EX** | Task R: annotate the first successful call with all 14 steps, then show that the `occam_probe` the reader ran in Ch 9 will skip steps 6–13 entirely. |
| **MISC** | "Everything goes through the pipeline, so everything gets budgets, post-processors and receipts." — False for nine of the twenty-one tool names. `occam_extract_knowledge` alone skips the router, post-processors, token budget and Receipt v1 (CAP-591, EF-006). |
| **HONEST** | The linear `client → discovery → acquisition → routing → materialization → trust → workflows` diagram is **false as a product-wide spine** and true only as a transcode-family narrative (`PRODUCT-ARCHITECTURE.md` §0). Say this in the chapter, because every reader arrives holding that diagram. Also disclose that the pipeline unconditionally pushes internal `json_blocks,json_tables` features whether or not the caller asked (`TranscodePipeline.cs:44-55`), and that the Canonical IR it plans is then discarded (EF-004) — mentioned here as a fact about the path, not as a capability. |
| **DEPTH** | 8–10 pages · 4 sections |
| **WHERE** | Both. Public docs need the reference narrative and the bypass table; the handbook keeps the falsification. |
| **CHECK** | Enable `OCCAM_LOG` and run `occam_transcode` then `occam_probe` against the same URL. Compare the stderr work each performs; the probe never spawns a worker. |

---

### Ch 5 — Acquisition: the real ladder  ●SPINE

| Field | Content |
|---|---|
| **OBJ** | Predict, for a given URL and `backend_policy`, exactly which backends will be attempted, in what order, where the ladder stops, and which attempt's failure will be surfaced. |
| **PRE** | Ch 2, Ch 4 |
| **COVERS** | PS-1 `acquisition-routing`, `http-acquisition`, `browser-acquisition`, `managed-acquisition` |
| **SRC** | `ACQUISITION-ROUTING-MODEL.md` (whole file; its hand-executable ladder is this chapter's centre), especially §"Decision model", §"Result-selection logic", §"Corrections to prior model" (10 numbered corrections); canonical cards `acquisition-routing.md`, `http-acquisition.md`, `browser-acquisition.md`, `managed-acquisition.md`; `OccamRouter.cs:134-182`, `FailureRanking.cs:10-21`, `DomainTierRegistry.cs:98-124` |
| **MODEL** | **A gated ladder with exits, not a cascade.** Escalation is conditional and termination is a first-class outcome. |
| **EX** | Task R step 2: the docs page succeeds on HTTP (ladder stops at rung 1); a sibling `/v2/` page 404s (**terminates**, no browser); the JS-rendered playground escalates to Chromium; a 403'd partner page fails both and surfaces the HTTP `http_403` because informativeness rank 100 beats the browser's `timeout` at 50. |
| **MISC** | "It always tries HTTP, then browser, then a managed provider, and picks whichever produced more text." — Wrong on four counts: 404/410 terminate; a public-reference URL that failed HTTP terminates; the dual-fail winner is chosen by `FailureRanking.Informativeness`, not by density; and a **managed failure never wins the surface** (EF-056, C1). |
| **HONEST** | `IsPublicReferencePage` skips the browser **silently** — the response looks like an ordinary HTTP failure with no "we chose not to escalate" signal. Managed is env-gated, is not a `backend_policy` value, and when `OCCAM_MANAGED_DOMAINS` is unset **every host is eligible**; the provider is a third party that sees the URL and returns content Occam will sign, and its HttpClient has no `OutboundHttpGuard` (EF-003). There is no automatic retry or backoff anywhere (CAP-188). Browser default timeout is **60 s** (`BrowserExtractTimeouts.cs:8,23`), not the 120 s repeated in older prose; HTTP is ~35 s. |
| **DEPTH** | 14–16 pages · 7 sections (policies · rung cards · the four exits · post-processors · dual-fail ranking · managed · reading `recovery[]`) |
| **WHERE** | Both. This is the chapter whose public version must be re-checked against `OccamRouter.cs` at every release; the gate rule `DISC-R10` exists for it. |
| **CHECK** | Three calls: (a) a 404 URL — assert no browser attempt in `recovery[]`; (b) a Wikipedia article with a forced HTTP failure — assert no browser attempt; (c) an SPA — assert a browser attempt with an `escalationReason`. |

---

### Ch 6 — When acquisition is hard: walls, sessions, egress

| Field | Content |
|---|---|
| **OBJ** | Diagnose which kind of wall a URL is behind and choose the one lever that can actually pass it — or conclude honestly that none can. |
| **PRE** | Ch 2, Ch 5 |
| **COVERS** | PS-1 `session-fetch`, `access-consent`, `network-safety`, `proxy-egress` (+ the operator levers of `managed-acquisition`) |
| **SRC** | `ACQUISITION-ROUTING-MODEL.md` §"Difficult acquisition playbook" (the obstacle × lever table is the chapter spine), Rung 1 auxiliaries and session tiers; `subsystems/session-lifecycle.md`; `subsystems/network-fetch-proxy.md`; canonical cards `session-fetch.md`, `access-consent.md`, `network-safety.md`, `proxy-egress.md`; FLOW-009, FLOW-017 |
| **MODEL** | **Occam names walls; it does not climb them.** Every wall maps to a typed code, and each code has at most one honest lever: cookies, a proxy, a forced browser, or an operator-configured third party. |
| **EX** | Task R step 3: the changelog is behind a login. `occam session import` / `export-state`, then `session_profile` on `occam_transcode` with `backend_policy=browser`. Then the same profile on `occam_probe` — and it silently reaches less. |
| **MISC** | "A session profile reproduces the same authenticated state in every tool." — Three tiers exist. Tier 1 (pipeline callers) gets headers **and** `storageState`; Tier 2 (probe, map) is HTTP-only; Tier 3 (heal, extract_knowledge) forwards headers and **drops `storageState` silently** (EF-017, CAP-594). |
| **HONEST** | No CAPTCHA solving, ever, and no fingerprint or identity rotation (CAP-180). Robots compliance is **off by default and fails open** on a robots fetch error (GAP-018) — Occam is not built to be a polite crawler. Proxy rotation does not reach the HTTP daemon, browser pool, css-extract or dom-skeleton spawns (CAP-165), and Core's own C# `HttpClient`s ignore `OCCAM_*` proxy entirely (CAP-166, EF-007). An empty configured proxy-list file **suppresses** the inline list rather than falling back (EF-057). Session import retains raw cookies in plaintext under `_imports/` by default (EF-054, `NEEDS_FIX_BEFORE_DOC` rank 7) — this chapter must state secure-deletion guidance in the same paragraph as the import command. |
| **DEPTH** | 12–14 pages · 6 sections |
| **WHERE** | Both. Public docs get the obstacle × lever table and the tier matrix; the handbook adds the EF-002/EF-040 context-bleed caveat (Ch 23 owns the depth). |
| **CHECK** | Create a session profile, then call `occam_transcode` and `occam_probe` with the same `session_profile`. Inspect which one carries `storageState` into the fetch. |

---

### Ch 7 — Materialization: the token contract  ●SPINE

| Field | Content |
|---|---|
| **OBJ** | Make an Occam response fit a context window on purpose, and read `compile.omitted` to know exactly what was removed. |
| **PRE** | Ch 4 (the compile half of the reference path) |
| **COVERS** | PS-2 `token-budget`, `focus-selection`; PS-8 `client-context` (the ambient input) |
| **SRC** | `PRODUCT-DEFINITION.md` §4.2 (mechanism table); `PRODUCT-TAXONOMY.md` PS-2 card; canonical cards `token-budget.md`, `focus-selection.md`, `client-context.md`; CAP-061/063/064/067/300/304/308/310/311; `ClientCapabilityStore.cs:15-17,81-85`; ART-023, ART-024 |
| **MODEL** | **Budgeting is not truncation.** The budget is a *whole-response* contract shared by markdown and every sidecar, and what it drops is reported. |
| **EX** | Task R step 4: the rate-limit page is 180 KB. Read it once with defaults (ambient budget = 20 % of the declared context window, clamped to [512, 16384] tokens), once with `max_tokens`, once with `fit_markdown` + `focus_query="rate limit"`. Compare `compile.omitted` across the three. |
| **MISC** | "Occam summarizes the page down to the budget." — There is no model and no generation anywhere in the host or the workers. Every reduction is selection or truncation of existing text; every word in the output came from the page. |
| **HONEST** | The tokenizer is `heuristic-unicode-v1` with **unmeasured error bounds** — no reduction percentage may be published, and any reduction figure must declare its baseline and its tier. `max_tokens` bounds content, not every serialized field (EF-055). `claim_check` and `dataset_export` apply **no** token budget at all, so they are outside every global budgeting claim (EF-016). Changing the ambient budget mid-session changes the compiled bytes and therefore the `contentHash` — a fact that only becomes dangerous in Ch 14, and must be planted here. `ResponseBudgetDiagnostics` is computed and never surfaced (CAP-303): do not promise observable diagnostics. |
| **DEPTH** | 12–14 pages · 6 sections |
| **WHERE** | Both. |
| **CHECK** | Same URL, two calls with different `max_tokens`. Assert the two `contentHash` values differ, and that `compile.omitted` in the smaller one names what went. |

---

### Ch 8 — Structured, differential and replayed output

| Field | Content |
|---|---|
| **OBJ** | Choose the right shape for a downstream consumer — blocks, tables, feed, chunks — and re-read a page cheaply with `if_none_match` / `diff_against` instead of paying full price. |
| **PRE** | Ch 7 |
| **COVERS** | PS-2 `structured-materialization`, `differential-materialization`, `response-cache` |
| **SRC** | `PRODUCT-TAXONOMY.md` PS-2 card; `ARTIFACT-ONTOLOGY.md` §1.2 (STRUCTURE family), ART-024, ART-035, ART-039; canonical cards `structured-materialization.md`, `differential-materialization.md`, `response-cache.md`; FLOW-010, FLOW-019, FLOW-020; CAP-074/080/082/085/089/307/318/319/320/321/322 |
| **MODEL** | **Sidecars are opt-in projections of the same compiled content, and they share its budget.** A delta is the same contract expressed as a difference. |
| **EX** | Task R step 5: `json_tables` to lift the limits table as records; hold the `contentHash`; a week later re-read with `if_none_match` and get an unchanged envelope for the price of an empty response. |
| **MISC** | "`semantic_chunking` chunks semantically." — It is a fixed-size line accumulator with heading breadcrumbs (CAP-320). Likewise `content_selectors` are heading anchors, not CSS (CAP-307), and the codec registry has no live selection surface — `MarkdownPassthroughCodec` is the only codec that ever runs (CAP-327/329). |
| **HONEST** | Blocks are collected internally **always**, and `diff_against` can force `blocks[]` into the response even with `json_blocks=false` (EF-010). The opt-in cache is not a cache feature to promote: its key omits `rank_blocks`, `tag_trust` and `emit_capsule` (EF-001) and omits the URL fragment, so a request for `page#b` can be served a stored `page#a` envelope **including its signed receipt** (EF-045, CRITICAL). TTL is evaluated only on read; there is no sweep (CAP-322). `translatedMarkdown` calls an external LibreTranslate endpoint, can block a request thread (EF-057), and **never enters the signed bytes** (ART-039). |
| **DEPTH** | 12–14 pages · 6 sections |
| **WHERE** | Both, but `response-cache` is `EXPERIMENTAL`/LOW in the exposure matrix: public docs mention it only with EF-001/EF-045 in the same paragraph; the handbook explains why it is not promoted. |
| **CHECK** | With `cache_ttl_s` set, fetch `page#a`, then fetch `page#b`. Inspect whether the second response is a replay of the first (EF-045 reproduction). |

---

### Ch 9 — Spending less: discovery before acquisition

| Field | Content |
|---|---|
| **OBJ** | Decide *whether* a URL is worth fetching, and find the right URLs, before paying for extraction. |
| **PRE** | Ch 2, Ch 5 |
| **COVERS** | PS-3 `probe-diagnostics`, `site-mapping`, `web-search` |
| **SRC** | `PRODUCT-TAXONOMY.md` PS-3 card and §1.1 (why discovery is a peer, not a mode); canonical cards `probe-diagnostics.md`, `site-mapping.md`, `web-search.md`; ART-011/012/013; FLOW-002, FLOW-003, FLOW-004; CMP-001, CMP-002, CMP-003 |
| **MODEL** | **Three signals that are not one scale.** `extractability` is a *prediction before* fetch; `confidence` / `quality` are *measurements after*; a playbook `verify.score` is a 0–100 *gate*. Never compare them. |
| **EX** | Task R step 6: `occam_probe` the docs index (cheap), read `agentHints`, then `occam_map({source:"sitemap"})` to enumerate the reference pages, then `occam_search` when the reader does not know the site at all. |
| **MISC** | "Probe tells me whether the page will extract." — Probe is structurally HTTP-only and **never escalates** (CAP-420); a browser-only page can be mis-predicted. Map never escalates either (CAP-510), so a JS-rendered navigation yields nothing. |
| **HONEST** | Nothing in PS-3 is signed, hashed or verifiable (ART-011/012/013). `occam_search` is registered as a core tool but **fails closed** without `OCCAM_SEARCH_PROVIDER`, and when configured it proxies a third party's index; `rerank` orders by *extractability*, not relevance, and can fire up to 20 live probes. `occam_map` is a link listing capped at 64 links with a bounded second-level expansion — never call it a crawl. Probe currently reports SSRF-policy blocks as `network_error` (GAP-003 / EF-042), so an agent may retry a private URL forever. `probe.autoRedirect` is registered and never selected (CAP-436) — omit it from the behavior tables. |
| **DEPTH** | 10–12 pages · 5 sections |
| **WHERE** | Both. |
| **CHECK** | `occam_search` with no provider configured: assert it fails closed rather than returning results. Then `occam_map` a large site and assert `links[]` never exceeds 64. |

---

### Ch 10 — Many sources in one call: digest

| Field | Content |
|---|---|
| **OBJ** | Read several URLs under one budget and one focus, and know what digest gives up compared with N transcodes. |
| **PRE** | Ch 7, Ch 9 |
| **COVERS** | PS-7 `digest-synthesis` (T-2 move: composition, not discovery) |
| **SRC** | `PRODUCT-TAXONOMY.md` §0 T-2 and PS-7 card; canonical card `digest-synthesis.md`; `COMPOSITION-MODEL.md` CMP-003; ART-010; FLOW-003; CAP-450…460 |
| **MODEL** | **Digest is the acquisition spine run N times under one budget**, plus a combine step and read-order hints — not a discovery tool and not a synthesizer. |
| **EX** | Task R step 7: five reference pages in one `occam_digest` with `focus_query="rate limit"` and `per_url_max_tokens`, versus five `occam_transcode` calls. Compare total tokens and what each per-item failure told you. |
| **MISC** | "Digest summarizes across sources." — It concatenates and orders bounded per-URL materializations. `suggestedReadOrder` is a hint, not a synthesis. |
| **HONEST** | Digest applies SSRF and session preflight to the **whole batch**, not per item (CAP-452), and truncates silently at its clamps (CAP-453). Digest items carry a **reduced** Receipt v1 — content hash only, no block leaves, no time anchor (CAP-457) — so a digest item is a weaker trust object than a transcode receipt. No playbook overlay and no transcode sidecars on digest items (FLOW-003 limit). |
| **DEPTH** | 8–10 pages · 4 sections |
| **WHERE** | Both. `PUBLIC_CORE` — the "several URLs → one digest, not N transcodes" rule belongs in the first task guide. |
| **CHECK** | Digest five URLs with one deliberately 404. Assert the response is `ok:true` overall with a typed per-item failure, and that the item receipt lacks block leaves. |

---

### Ch 11 — Playbooks in band: resolution and the auto overlay

| Field | Content |
|---|---|
| **OBJ** | Explain what changed in a transcode when a playbook applied, inspect what resolved, and decide when to turn the overlay off. |
| **PRE** | Ch 5, Ch 7 |
| **COVERS** | PS-5 `playbook-resolution` |
| **SRC** | `PRODUCT-TAXONOMY.md` PS-5 card; canonical card `playbook-resolution.md`; `PRODUCT-ARCHITECTURE.md` §2 step 6; `TranscodePipeline.cs:57-104`; CMP-004; FLOW-005/006; CAP-491, CAP-496 |
| **MODEL** | **A playbook is a soft overlay on the acquisition spine, resolved per call from four tiers with per-field precedence** — local → org (`WT_PLAYBOOKS_PATH`) → community → seeds, plus optional live genome. |
| **EX** | Task R step 8: `occam_playbook_resolve` the API host to see which tier wins and whether a `knowledge_schema` exists; then `occam_transcode(playbook_policy="auto")` and compare against `playbook_policy="off"`. |
| **MISC** | "I resolve a playbook and pass it to transcode." — There is no such parameter. Transcode **re-resolves** internally; resolve is for the agent's own planning (CMP-004). |
| **HONEST** | A resolved `preferredBackend` overrides the request policy **only** when that policy is `http_then_browser`. `page_class` / `knowledge_schema` match failures are computed and swallowed on the resolve path (CAP-496) — do not promise those failure codes. `claim_check`, `attest` and `dataset_export` force `playbook_policy=auto` internally with **no parameter to disable it** and no `playbookId` in the response (CAP-693, AUTOMATION #A5). Community playbooks are sha256-integrity-checked, **not authenticated** (G-E-03), and the marketplace CI can auto-merge an unvalidated playbook (EF-052, `NEEDS_FIX_BEFORE_DOC` rank 1) — this chapter may describe the community tier but must not describe it as a validated or authored-by-a-known-party supply chain. Live genome fetch has an empty-Content-Type bypass and reads the full body before truncating (EF-048). |
| **DEPTH** | 10–12 pages · 5 sections |
| **WHERE** | Both. |
| **CHECK** | Transcode a playbook-covered host twice, with `playbook_policy` `off` and `auto`. Diff the two markdowns and the receipt's `playbook{id,version}` field. |

---

### Ch 12 — Authoring a playbook: heal → draft → lint → save

| Field | Content |
|---|---|
| **OBJ** | Turn a page that extracts badly into a page that extracts well, once, as a portable signed JSON file. |
| **PRE** | Ch 2 (to know it is a genuine extraction failure), Ch 11 |
| **COVERS** | PS-5 `playbook-healing`, `playbook-validation`, `playbook-authoring` |
| **SRC** | Canonical cards `playbook-healing.md`, `playbook-validation.md`, `playbook-authoring.md`; `COMPOSITION-MODEL.md` CMP-012; FLOW-005; ART-015/016/018; `PlaybookSignature.cs:29-39,63-84`; EF-005, EF-015, EF-047 |
| **MODEL** | **The loop has a human-shaped hole in the middle.** Heal returns a DOM skeleton and candidates; **you** draft the playbook JSON; lint is advisory; save is the gate that matters. |
| **EX** | Task R step 9: the pricing page still returns `thin_extract` after browser escalation → `occam_playbook_heal` → draft selectors from the candidates → `occam_playbook_lint` → `occam_playbook_save(verify:true)` → re-run Ch 11's comparison. |
| **MISC** | "Heal produces the playbook and save stores it." — Heal emits skeleton + candidates, **not** `playbook_json`; there is no automated emitter (FLOW-005 limitation). And "lint passing means save will accept it" is false: lint uses a different parser from save and resolve (EF-015). |
| **HONEST** | `occam_playbook_save` **always signs**, ignoring `OCCAM_RECEIPTS` entirely (EF-005) — one of the two reasons that variable must never be called a master switch. The signature covers the **recipe body only**: `keyId`, `alg`, `signedAt` and the whole `verify{score, passesGate, noiseLeakage}` block sit inside the excluded top-level `provenance` object and are freely editable without invalidating `Verify` (TRUST-MODEL X1). Never call the quality score signed. `PlaybookCommunitySanitizer` is Core-dead — neither lint nor local save publish-sanitizes (EF-047, C3). Heal's `--consent-aggressive` worker flag is unreachable from MCP (CAP-553) — omit it. Playbook interaction plans reach `page.evaluate` / `waitForFunction`: **a playbook is code-like input, not a declarative selector list** (EF-046). |
| **DEPTH** | 12–14 pages · 6 sections |
| **WHERE** | Both. The handbook additionally carries the tamper case (EFC-P5-05-1: editing the unsigned `provenance.keyId` downgrades a detectable `invalid` to an innocuous `unknown_key`). |
| **CHECK** | Save a playbook, then edit `provenance.verify.score` in the file on disk and re-inspect. It still verifies — the score was never signed. |

---

### Ch 13 — Typed field extraction

| Field | Content |
|---|---|
| **OBJ** | Get `facts[]` in a caller-defined schema from a known site shape, and know precisely which guarantees you gave up to do it. |
| **PRE** | Ch 11 (hard dependency: PS-4 requires PS-5), Ch 2 |
| **COVERS** | PS-4 `schema-knowledge-extraction` |
| **SRC** | `PRODUCT-TAXONOMY.md` PS-4 card; canonical card `schema-knowledge-extraction.md`; `PRODUCT-ARCHITECTURE.md` §3 (bypass row); FLOW-006; CMP-004; ART-014, ART-036; CAP-590…601 |
| **MODEL** | **A separate spine with its own worker and its own, narrower failure taxonomy.** Recipe D is resolve → schema-match → CSS-extract. There is no schema-free mode. |
| **EX** | Task R step 10: extract `{limit, window, scope}` as typed fields instead of re-parsing markdown, using the schema saved in Ch 12. |
| **MISC** | "`occam_extract_knowledge` returns a Receipt, so its output is verifiable." — The field named `Receipt` is `{confidence, elapsedMs}` telemetry and is **not** Receipt v1 (CAP-287, EF-006). Nothing this tool returns is signed, hashed or verifiable, and `confidence` is always `0.0` (CAP-595). |
| **HONEST** | Bypassing the spine costs the token budget, the post-processors, receipts and pool reuse (per-call throwaway Playwright for the browser leg, CAP-593). `session_profile` is accepted but **silently does not reach the browser-fallback leg** (CAP-594). Row-mode `base_selector` is dead — host parsers never set it (CAP-600, EF-014, C4) — so do not document row mode. Two security items must appear in-chapter, not in an appendix: `readNuxtPath` runs `eval()` over page-controlled Nuxt state (EF-013) and css-extract lacks DNS pinning and a body cap (EF-043). Both are `NEEDS_FIX_BEFORE_DOC` rank 2: **this chapter may not present typed extraction as safe for untrusted URLs.** Worker timeout is a hardcoded 45 s (CAP-592). |
| **DEPTH** | 8–10 pages · 5 sections |
| **WHERE** | Both, with the safety limitation in the same section as the tool name. |
| **CHECK** | Call `occam_extract_knowledge` and `occam_transcode` on the same URL with `max_tokens` set. The extract call ignores the budget; its `Receipt.confidence` is `0.0`. |

---

### Ch 14 — What a receipt proves — and what it does not  ●SPINE

| Field | Content |
|---|---|
| **OBJ** | State the single sentence a receipt licenses you to say, and refuse the eight sentences it does not. |
| **PRE** | Ch 2, Ch 7 (the hash covers *compiled* bytes) |
| **COVERS** | PS-6 `receipts` |
| **SRC** | `TRUST-MODEL.md` §1–§6 (§2's twelve-concept vocabulary is the chapter's backbone; §6's fourteen-step chain of custody is its walkthrough), §13 forbidden claims; canonical card `receipts.md`; ART-006/007/008/009/024/034; `ReceiptSigner.cs:26-45,51-64,84-99`, `ReceiptCanonicalizer.cs:17-18`, `ReceiptVerifier.cs:19-21` |
| **MODEL** | **A receipt is a local integrity log entry, not provenance.** It proves: *the holder of this key asserted these exact bytes, and they are unaltered.* Nothing else. |
| **EX** | Task R step 11: keep the receipt for the sentence you are about to ship in a report. Read every field of it aloud and label each as "signed", "unsigned cargo", or "self-asserted". |
| **MISC** | "Signed by Occam means it came from the origin." — It means *an* Occam install's auto-minted local key signed it. No origin signature, TLS transcript or independent witness is ever captured; a host that fabricated the markdown produces a signature indistinguishable from an honest one. |
| **HONEST** | Twelve limits, all stated in-chapter: no PKI, registry, rotation, revocation or expiry — TOFU over a PEM the consumer must obtain out of band. `keyId` is a 64-bit truncated fingerprint with no identity attached. `ts` is the signer's own clock. The optional RFC3161 anchor covers only the signature's existence and its TSA certificate is **never chained to a trust root** (`ReceiptTimeAnchor.cs:34-35`); an anchor failure vanishes silently (D1). A capsule is a **signed core in an unsigned wrapper**; `verifyRecipe` is unvalidated advisory text. Merkle duplicate-last means `[A,B,C]` and `[A,B,C,C]` share a root, so leaf-count-derived values are not signed quantities (EFC-P5-05-3). A cached hit replays the stored envelope with its original `ts` — a receipt cannot prove freshness. `OCCAM_RECEIPTS=off` stops most emission but **not** the key mint (EF-044) and **not** playbook signing (EF-005). The `"paywall"` negative-receipt branch is unreachable (EF-008) — omit it. |
| **DEPTH** | 14–16 pages · 7 sections |
| **WHERE** | Both, and this chapter is the source of the public docs' forbidden-claims list (`DISCOVERABILITY-GATE` R5/R6 denylist). |
| **CHECK** | Run with `OCCAM_RECEIPTS=off`. Assert `contentHash` and `blockMerkleRoot` are still present, the key file still exists, and `occam_playbook_save` still signs. |

---

### Ch 15 — Verifying: five modes, two surfaces, four asymmetries

| Field | Content |
|---|---|
| **OBJ** | Verify a receipt correctly — including a receipt someone else produced — and recognise the verdicts that mean less than they sound. |
| **PRE** | Ch 14 |
| **COVERS** | PS-6 `verification` |
| **SRC** | `TRUST-MODEL.md` §7 (mode table), §8.2 (D1–D11), §12 X3/X6; canonical card `verification.md`; `subsystems/verify-cli.md`; ART-021; `OccamVerifyTool.cs:40`, `OccamCliVerbs.cs:221-224,290-293`; EF-011, EF-012, EF-018, EF-025 |
| **MODEL** | **Verification is arithmetic over bytes and keys.** No verdict in this codebase is about truth, and the MCP tool and the CLI are not the same surface. |
| **EX** | Task R step 12: hand the receipt and your public PEM to a colleague; they run `occam verify --mode receipt --pubkey pub.pem`. Then show the trap: the same receipt through MCP `occam_verify` **without** `public_key`, which silently uses the local host's key and reports `signature_invalid`. |
| **MISC** | "`live` mode proves whether the page changed." — The re-fetch drops session profile, playbook overlay, content selectors, token budget and backend pin; `drifted` usually means "my re-fetch lacked the original's context", and every re-fetch failure collapses to `refetch_failed` with no failure code (EF-012, CAP-653). |
| **HONEST** | Four asymmetries, named as such: `manifest` is **CLI-only** (EF-018) so a pure-MCP agent structurally cannot verify a dataset manifest; `live` and `prove` are MCP-only; the time anchor **gates the CLI verdict but not the MCP verdict**; `--pubkey` is mandatory on the CLI and optional on MCP (`OccamVerifyTool.cs:40`). Neither surface is reachable through the friendly `occam` wrapper (EF-025) — show the exact host-binary invocation. Unknown modes silently downgrade to `offline` and the response claims `"mode":"offline"` (EF-011). A wholly unsigned watch chain returns `history_verified` and CLI exit 0 (EFC-P5-05-2) — never write "verified history means signed history". The verdict vocabulary has no `wrong_key`, so "verified against the wrong key" and "tampered" are indistinguishable (EFC-P5-05-5): write "the signature did not validate under this key". |
| **DEPTH** | 12–14 pages · 6 sections |
| **WHERE** | Both. |
| **CHECK** | Build a watch history, strip every `Sig` field, rebuild `prevEntryHash`, and verify. Exit code 0 / `history_verified` reproduces EFC-P5-05-2. |

---

### Ch 16 — Evidence for claims and corpora

| Field | Content |
|---|---|
| **OBJ** | Attach checkable evidence to a quoted sentence and to a set of URLs, and describe that evidence in words the crypto supports. |
| **PRE** | Ch 8 (blocks exist), Ch 14, Ch 15 |
| **COVERS** | PS-6 `claims-attestation`, `dataset-provenance` |
| **SRC** | `TRUST-MODEL.md` §2 C7–C9, §4 (claim check / attestation), §9 Q3; canonical cards `claims-attestation.md`, `dataset-provenance.md`; `COMPOSITION-MODEL.md` CMP-006, CMP-007a/b, CMP-008; FLOW-007, FLOW-008; ART-019/020/022; `ClaimCheckService.cs:102`, `AttestService.cs:87-89` |
| **MODEL** | **Retrieval plus membership, never stance.** `occam_claim_check` returns the blocks that cleared a lexical BM25 floor plus a Merkle proof that each was in the signed extraction. |
| **EX** | Task R step 13: `occam_claim_check` the exact rate-limit sentence you plan to quote; take the `leaf` + `proof` into `occam_verify mode=citation`; then `occam_dataset_export` the five source URLs and verify the manifest **on the CLI**. |
| **MISC** | "`found:false` with `proven:true` means the page does not say it." — It means no extracted block cleared a lexical floor over an untruncated leaf set. Paraphrase, synonymy, non-English phrasing, text in images, content behind interaction and anything the extractor dropped are all outside its reach. It is a *retrieval*-complete negative, not a semantic one. |
| **HONEST** | `Verdict` is hardcoded `not_evaluated` — there is no stance evaluation at all. `occam_attest` is an unsigned tally from anchored regexes that recognise exactly two English claim shapes (`X is [a] Y`, `X uses Y`, CAP-721/722) over the top-3 blocks, with one Merkle proof attached for the top block only; every other phrasing returns `unknown`. Dataset export's top-level `ok` describes export completion, **not** row success (EF-018) — inspect rows. Neither `claim_check` nor `dataset_export` applies a token budget (EF-016), which is a precondition of `leafSetComplete`. `occam_attest` is `PUBLIC_ADVANCED`/MEDIUM at best: it may not be called cryptographic attestation anywhere. |
| **DEPTH** | 12–14 pages · 6 sections |
| **WHERE** | Both. |
| **CHECK** | Claim-check a sentence you know is on the page, then re-run with a paraphrase that carries no shared terms. The second returns `found:false` while the page plainly says it. |

---

### Ch 17 — Opt-in surfaces: watch, batch, crosscheck, atlas

| Field | Content |
|---|---|
| **OBJ** | Decide whether any of the four env-gated surfaces is worth enabling, knowing exactly what each does not provide. |
| **PRE** | Ch 10, Ch 14, Ch 18 (or read Ch 18 first if the reader is asking "why can't I see this tool") |
| **COVERS** | PS-7 `change-monitoring`, `batch-jobs`, `consensus-crosscheck`, `failure-atlas` |
| **SRC** | `PRODUCT-TAXONOMY.md` PS-7 card; `ENTRYPOINT-MODEL.md` §5, §10; canonical cards `change-monitoring.md`, `batch-jobs.md`, `consensus-crosscheck.md`, `failure-atlas.md`; `DOCUMENTATION-EXPOSURE-MATRIX.md` §1–§2; FLOW-012, FLOW-013, FLOW-014; ART-025/027/028 |
| **MODEL** | **Four surfaces the default deployment does not have, each with a named gate and a named ceiling.** They are opt-in because of what they lack, not because they are new. |
| **EX** | Task R step 14: `OCCAM_WATCH_MCP=1` and watch the rate-limit page for change; consider batch for the corpus and reject it; run crosscheck and read the verdict as an observation. |
| **MISC** | "Crosscheck proves the content is genuine." — All vantages leave one process, one egress IP and one proxy configuration (CAP-859). Agreement excludes exactly one cloaking axis (bot-vs-browser, anon-vs-authed) and is an **unsigned observation** that no shipped tool re-derives from the vantage receipts (EF-032). |
| **HONEST** | `occam_watch` has **no daemon** — the cadence is the agent's job — and **no un-watch**: `IWatchStore.Remove` has no product caller (EF-020); the URL set is uncapped while per-URL history is capped at 64; a corrupt store silently resets to empty (CAP-832); multi-process writes race (EF-019). Batch produces **no Receipt v1 at all** and retains full markdown forever with no delete API (EF-037), and is last-writer-wins across processes (EF-038). Crosscheck is exempt from `OCCAM_PROFILE` filtering and absent from server instructions, so it can be exposed without being advertised (EF-031, CAP-861). Atlas is per-session memory only, and enabling it **replaces** the host telemetry sink (CAP-875). Per D4 and `DOCUMENTATION-EXPOSURE-MATRIX` §2, crosscheck appears here as an experimental observation tool and **never** as a trust feature. |
| **DEPTH** | 10–12 pages · 5 sections (one per surface + a "should you?" decision table) |
| **WHERE** | Both, limits-first. Each surface's env gate must appear in the same paragraph as its tool name (`DISCOVERABILITY-GATE` R2). |
| **CHECK** | Enable `OCCAM_CONSENSUS_MCP=1` together with `OCCAM_PROFILE=reader`. Assert `occam_crosscheck` appears anyway — opt-ins are not profile-filtered (CAP-011). |

---

### Ch 18 — Exposure: 51 entrypoints, 15 default tools  ●SPINE

| Field | Content |
|---|---|
| **OBJ** | Explain why a given tool is or is not in a given deployment's `tools/list`, and choose a profile and transport deliberately. |
| **PRE** | Ch 3 |
| **COVERS** | PS-8 `mcp-exposure`, `runtime-transports`, `client-context` |
| **SRC** | `ENTRYPOINT-MODEL.md` (whole file; §2's counting method must be reproduced, not just its number); `PROFILE-TOOL-MATRIX.md`; `RUNTIME-MODES.md`; canonical cards `mcp-exposure.md`, `runtime-transports.md`, `client-context.md`; CAP-003…015, CAP-400…404, CAP-1000/1001/1002 |
| **MODEL** | **Product capability ≠ MCP tool count.** "15 tools" is the default stdio `tools/list` under `OCCAM_PROFILE=full` with the four opt-ins off — one exposure slice of 51 named entrypoints. |
| **EX** | Task R step 15: your colleague's host runs `OCCAM_PROFILE=reader`, so they can obtain your receipts and have no in-band way to verify them. Show the fix and the reasoning. |
| **MISC** | "Profiles are a security boundary." — Profiles change *exposure*, never handler semantics. A `reader` deployment still mints a key, still signs, still applies playbook overlays and may still use a managed provider. Opt-in tools are **not** profile-filtered at all (CAP-011). |
| **HONEST** | Profiles: `full` 15 · `reader` 7 · `researcher` 9 · `auditor` 12 · invalid → `full` with a stderr warning. `reader` exposes the receipt **producer** and hides the **verifier** (EFC-P5-05-4). The canonical launcher is stdio-only and never forwards args, so WS / Remote / BatchServer are unreachable through it (CAP-1001). Local WS has no session semaphore and each socket builds a DI container that reinstalls — and thereby kills — the shared browser pool (EF-041). BatchServer has **no auth** (loopback only). The banner can claim stdio while running WS or Remote (GAP-032). Server instructions can mention `occam_watch` without its gate (GAP-012). `model_id` and `suggestedProfile` are stored, echoed and never consumed (CAP-402/403). Do not repeat the Hermes smoke test's fixed 15-tool invariant as product semantics (EF-033). |
| **DEPTH** | 12–14 pages · 6 sections |
| **WHERE** | Both. The public version leads with "how do I see the tools I expect"; the handbook keeps the 51-entrypoint accounting and its counting method. |
| **CHECK** | Start the host four times, once per `OCCAM_PROFILE` value, and record `tools/list` each time. Then start with an invalid value and observe the fallback plus the stderr warning. |

---

### Ch 19 — Operating an install

| Field | Content |
|---|---|
| **OBJ** | Keep an install healthy, wired, credentialed and updated — and know what each operator verb does to the rest of the machine. |
| **PRE** | Ch 3, Ch 18 |
| **COVERS** | PS-9 `operator-cli`, `install-onboarding`, `host-connectors`, `packaging-distribution` |
| **SRC** | `CLI-SURFACE.md`; `CONNECT-PLATFORM.md`; `HOST-CAPABILITY-MATRIX.md`; `subsystems/install-onboard.md`, `subsystems/doctor.md`, `subsystems/packaging-distribution.md`; canonical cards `operator-cli.md`, `install-onboarding.md`, `host-connectors.md`, `packaging-distribution.md`; FLOW-015, FLOW-016, FLOW-018, FLOW-021; ART-029…033, ART-038 |
| **MODEL** | **The operator surface changes the machine, not just Occam.** It writes runtime assets, mutates up to fifteen third-party config files, authors credential-bearing profiles, and kills processes. |
| **EX** | Task R step 16: your colleague's install stops responding. `occam doctor` → `occam refresh` → reconnect — and the discovery that `refresh` killed a second, unrelated install on the same machine. |
| **MISC** | "`occam verify` and `occam keys export` are wrapper subcommands." — The wrapper's closed subcommand table has no `verify`, `keys` or `install-browser` entry and exits 1 with "unknown command" (EF-025). These are direct host-binary verbs; show the exact reachable invocation. |
| **HONEST** | `occam refresh` kills **every** `OccamMcp.Core[.exe]` on the machine with no scope flag (EF-049, CRITICAL). `launch-mcp-host` merges user-writable `~/.occam/onboard.json` env into every launch (EF-050). `version-surface` and `occam contract` collide by name and are not equivalent (EF-023); refresh still prints a stale "9 tools" (EF-022). Skill install `rmSync`s the destination and ships a stale version and tool count (EF-036). Level B install verifies a **sha256 manifest**, not a signature; the cosign bundle is consumed by no shipped install path (EF-053, "trust theater", `NEEDS_FIX_BEFORE_DOC` rank 4). The tarball may omit the `connect` script that its own help advertises (EF-035, rank 6). The Docker HEALTHCHECK invokes an unsupported `--version` and can hang in stdio (EF-051, rank 8) — no production-readiness or health claim. `occam keys export` against an **empty** key store mints a key and exports that (`OccamCliVerbs.cs:208-215`). Uninstalling the install tree leaves `~/.occam`, host configs, skills and the Playwright cache behind → Ch 21. |
| **DEPTH** | 14–16 pages · 7 sections |
| **WHERE** | Both. |
| **CHECK** | `occam keys export --keys-root <empty dir>`. A key appears that never signed anything. |

---

### Ch 20 — What Occam does without asking

| Field | Content |
|---|---|
| **OBJ** | Enumerate the decisions Occam makes on your behalf, rank them by surprise, and know which eleven you cannot switch off. |
| **PRE** | Ch 5, Ch 14, Ch 18 |
| **COVERS** | Cross-cutting automation lens over PS-1, PS-2, PS-5, PS-6, PS-8, PS-9 |
| **SRC** | `AUTOMATION-MODEL.md` (whole file; §2's 29-row table is the chapter, §3 the ordering, §5 the controllability tiers, §6 the disclosure duty); `AUTOMATIC-BEHAVIORS.md` |
| **MODEL** | **Seven classes of automatic decision** — routing, provisioning, content shaping, trust side effects, hygiene, network politeness, host mutation — each with a visibility and a controllability answer. |
| **EX** | Task R step 17: account for everything that happened during the very first call in Ch 3 that the reader did not request: a key was minted, an HTTP daemon prewarmed, a consent banner was dismissed, CSP was bypassed, virtual scroll ran, an IR was built and discarded. |
| **MISC** | "Nothing happens that I did not ask for." — Twenty-nine proven behaviors say otherwise, and eleven of them are not disableable. |
| **HONEST** | The ten that **must** be disclosed: key mint on every host start (EF-044) and always-sign on playbook save (EF-005), together proving `OCCAM_RECEIPTS` is not a master switch (C6); `bypassCSP:true` unconditionally plus playbook `page.evaluate` (EF-046); the opt-in cache storing full signed envelopes on disk; machine-wide process kill on refresh (EF-049); onboard env injection on every launch (EF-050); the WS/Remote pool kill (EF-041); third-party managed egress when configured; marketplace auto-merge (EF-052); the Nuxt `eval` footgun (EF-013). Safely undocumented at the product surface: daemon prewarm, feature injection, stderr cosmetics, the discarded IR (an engineering cost, not a capability — EF-004). |
| **DEPTH** | 10–12 pages · 5 sections |
| **WHERE** | Both. Public docs get the MUST-disclose subset with its controls; the handbook gets all 29 with the surprise ranking. |
| **CHECK** | Run one browser-backed transcode with `OCCAM_LOG` on and inspect the launch options and the page mutations. |

---

### Ch 21 — State, persistence and footprint

| Field | Content |
|---|---|
| **OBJ** | Say exactly what Occam wrote to the machine, what survives an upgrade or an uninstall, and where the secrets are. |
| **PRE** | Ch 3, Ch 20 |
| **COVERS** | Cross-cutting state lens; heaviest in PS-6 (key), PS-5, PS-7, PS-9, PS-2 |
| **SRC** | `STATE-MODEL.md` (whole file; §0's four-way verdict opens the chapter, §2's 29-item inventory is its reference, §3–§5 its payload, §7 its privacy summary); `ARTIFACT-ONTOLOGY.md` §4–§5 |
| **MODEL** | **"No file cache by design" means live extract is the default. It does not mean stateless.** |
| **EX** | Task R step 18: after everything the reader has run, walk `~/.occam/`, `{TEMP}/occam-*`, the Playwright cache and the host MCP config, and name every file. |
| **MISC** | "Deleting the install directory uninstalls Occam." — It leaves the entire `~/.occam` footprint, host MCP configs and their `.occam-bak` siblings, skill directories, the Playwright browser cache and temp cache leftovers. |
| **HONEST** | Credential-bearing state on disk: session profiles, Playwright `storageState`, and `_imports/` raw cookies retained **in plaintext by default** (EF-054). One unencrypted PKCS8 private key whose permission hardening is a **no-op on Windows** and whose POSIX `chmod` failure is swallowed (`ReceiptSigner.cs:84-99`). Unbounded growth with no cleanup path: batch `jobs.json` (full markdown forever, no delete API — EF-037), the watch URL set (EF-020), response-cache orphans (TTL deleted only on read). Concurrency: batch and watch stores are last-writer-wins across processes (EF-038, EF-019); the shared browser pool dies when a second WS session starts (EF-041). The failure atlas is per-session memory, **not** a process-wide leak — EF-024 is WITHDRAWN and must not be revived. |
| **DEPTH** | 10–12 pages · 5 sections |
| **WHERE** | Both. The uninstall footprint and the secrets inventory belong in public docs; the concurrency matrix is handbook depth. |
| **CHECK** | Snapshot the filesystem before Ch 3 and after Ch 20; diff. Every new path should be findable in `STATE-MODEL.md` §2. |

---

### Ch 22 — Configuration and its negative space

| Field | Content |
|---|---|
| **OBJ** | Configure a deployment from intent, and — more importantly — know which environment variables do not do what their names say. |
| **PRE** | Ch 5, Ch 14, Ch 18, Ch 20 |
| **COVERS** | Cross-cutting configuration lens (110 CAPs classified `CONFIG_BEHAVIOR` across every family) |
| **SRC** | `ENVIRONMENT-VARIABLES.md` (catalog); `CONFIG-NEGATIVE-SPACE.md` (independent re-derivation — the chapter's real content); `PLATFORM-DIFFERENCES.md`; C6 |
| **MODEL** | **Env vars are inputs to behavior, not behavior.** The interesting knowledge is the negative space: coverage holes, fail-open defaults and names that promise more than they gate. |
| **EX** | Task R step 19: for each surprise the reader hit in Ch 20, name the variable that would (or would not) have prevented it. |
| **MISC** | "`OCCAM_HTTP_PROXY` routes every network operation." — It reaches worker egress only. Core's own C# `HttpClient`s (probe, map, managed, search) ignore it entirely (CAP-166, EF-007), and rotation additionally misses the HTTP daemon, browser pool, css-extract and dom-skeleton spawns (CAP-165). |
| **HONEST** | `OCCAM_RECEIPTS` is parsed in two places (`ReceiptsPolicy` and a duplicated local parser in `ConsensusService`) and gates neither key minting nor playbook signing (C6). `OCCAM_MANAGED_DOMAINS` unset means **all hosts eligible**, not none. `OCCAM_RESPECT_ROBOTS` fails open on a robots fetch error (GAP-018). Playwright proxy resolution fails open to no proxy (GAP-030). An empty `OCCAM_PROXY_LIST_FILE` suppresses the inline list (EF-057). `OCCAM_CHUNK_SIZE` is characters, not tokens (CAP-337). `OCCAM_BATCH_DB_PATH` names a `.db` file that the store forces to `.json` (there is no SQLite). Platform deltas are mechanism-only (Job Object vs process group, cache paths, SIMD tier, path separators) and change no semantics — say so, so readers stop looking for behavioral differences. |
| **DEPTH** | 8–10 pages · 4 sections; the full catalog is Appendix B, not chapter body |
| **WHERE** | Both. The public configuration page is generated from the catalog; this chapter never restates it, it explains the holes. |
| **CHECK** | Set `OCCAM_HTTP_PROXY` to a dead address and run `occam_probe`. It succeeds — proving the Core client never used it. |

---

### Ch 23 — Security posture and threat model

| Field | Content |
|---|---|
| **OBJ** | State what Occam's trust layer defends against, what it explicitly does not, and which surfaces must not be pointed at untrusted input today. |
| **PRE** | Ch 6, Ch 13, Ch 14, Ch 20, Ch 21 |
| **COVERS** | Cross-cutting security lens; owned mechanisms in PS-1 `network-safety`, PS-6 `receipts`, PS-9 `install-onboarding`/`packaging-distribution` |
| **SRC** | `TRUST-MODEL.md` §5 (trust boundary diagram — reproduce it), §10.1 in-scope / §10.2 out-of-scope; `PRODUCT-VS-ENGINEERING.md` §3 (the eight-item `NEEDS_FIX_BEFORE_DOC` shortlist) and §5 (naming-honesty table); `STATE-MODEL.md` §7 |
| **MODEL** | **Everything above the signature boundary is asserted; everything below it is tamper-evident.** The host binary is fully trusted and the whole model rests on it. |
| **EX** | Task R step 20: red-team the workflow the reader has built. Who could make it lie? Origin cloaking, a compromised host, an operator-controlled key, a hostile playbook, a managed intermediary, a prompt injection in the page. |
| **MISC** | "`tag_trust` protects against prompt injection." — It is an off-by-default heuristic annotation that requires `json_blocks`, and the tag is carried **outside** the signature. Injected text is hashed, signed and Merkle-provable exactly like real content. |
| **HONEST** | In-scope and genuinely strong: post-hoc edit detection, quoted-block tampering, dataset row-set reordering, canonicalizer drift (hand-written fixed-order canonicalizer with a byte-for-byte golden vector in the gate), hostile input to every verifier, SSRF on the time-anchor call. Out of scope and named: a compromised host, an operator-controlled key, a lying origin, prompt injection, playbook and browser code execution (`bypassCSP:true` unconditional — EF-046), managed-provider intermediaries (EF-003), key distribution and identity, key compromise recovery, key at rest, multi-party attestation, the supply chain of the host itself, and browser-context session bleed (EF-002/EF-040). Three surfaces carry an explicit "do not point at untrusted URLs yet" banner: css-extract (no DNS pin, no body cap — EF-043) and the Nuxt `eval` path (EF-013), both rank 2; and pooled anonymous browser contexts as an isolation boundary (EF-002), rank 3. |
| **DEPTH** | 12–14 pages · 6 sections |
| **WHERE** | Both. The handbook keeps the full out-of-scope table and the blocker shortlist; public docs get the posture statement and the three banners. |
| **CHECK** | Point `occam_extract_knowledge` at a local HTTP server on a private IP and compare with `occam_transcode` against the same target. One is refused; the other is not (EF-043). |

---

### Ch 24 — Composing tools: what chains, and what does not

| Field | Content |
|---|---|
| **OBJ** | Build multi-tool workflows that actually pass data, and recognise the eight chains the product's names imply but the code does not complete. |
| **PRE** | Ch 10, Ch 13, Ch 16, Ch 17 |
| **COVERS** | Composition across all value systems |
| **SRC** | `COMPOSITION-MODEL.md` (whole file; §2's CMP-001…015 records, §3's rejected candidates, §4.1's join-key table and graph, §4.2 broken chains, §4.3 silent trust degradations, §4.5 anti-compositions); `CODE-DERIVED-WORKFLOWS.md` FLOW-001…022 |
| **MODEL** | **Five composition classes** — direct, shared-subsystem, artifact-handoff, operator-workflow, implicit — and only artifact-handoff means "the output of A is a valid input to B". |
| **EX** | Task R step 21: the reader tries to feed batch results into `dataset_export` and cannot; tries to feed heal output into save and cannot; tries to feed transcode markdown into `extract_knowledge` and cannot. Each failure teaches the join-key model. |
| **MISC** | "`claim_check` then `attest` is a pipeline." — `attest` internally re-runs claim-check and accepts claim text, not claim-check JSON (`AttestService.cs:67-69`). Calling both is a double live fetch with no composition benefit and a risk of divergent receipts. |
| **HONEST** | Eight rejected chains, each with the parameter contract that rejects it: transcode markdown → extract_knowledge; batch results → dataset_export; claim_check JSON → attest; `facts[]` → claim_check; heal response → save; MCP verify of a dataset manifest; crosscheck verdict → verify; extract-knowledge `Receipt` → verify. Nine compositions silently degrade trust — compiled-bytes hashing, cache replay, session tiers 2/3, double-fetch attest, unsigned crosscheck verdicts, the reader-profile produce-without-verify gap, receipts-off plus playbook save, unsigned watch entries, and an ambient budget change mid-session breaking hash continuity. |
| **DEPTH** | 10–12 pages · 5 sections |
| **WHERE** | Both. The anti-composition table is the single highest-value page in the book for an agent integrator and belongs in public docs verbatim. |
| **CHECK** | Attempt three rejected chains against a live host and record the exact error each returns. |

---

### Ch 25 — Diagnosing a bad result

| Field | Content |
|---|---|
| **OBJ** | Go from a disappointing response to a named cause and a next action, using only fields the response actually contains. |
| **PRE** | Ch 2, Ch 5, Ch 6, Ch 12, Ch 20 |
| **COVERS** | Practical reassembly of PS-1 `quality-failure-semantics` with PS-5 healing and PS-9 operator verbs |
| **SRC** | `FAILURE-BEHAVIOR-MAP.md` (both tables); `ACQUISITION-ROUTING-MODEL.md` §"Failure code map"; `AUTOMATION-MODEL.md` §4.1 (invisible behaviors, i.e. the causes with no signal); `subsystems/failure-atlas.md`; `subsystems/doctor.md` |
| **MODEL** | **A decision tree keyed on `failure.code` + `recovery[]` + `quality.verdict`,** with an explicit branch for "the response is fine and the cause is invisible". |
| **EX** | Task R step 22: the workflow that worked in Ch 16 now returns `thin_extract`. Walk the tree: did the ladder terminate early, did a post-processor downgrade it, did the playbook stop matching, did the browser pool die because a second session started? |
| **MISC** | "`workers_unavailable` means the network failed." — It means worker paths are missing or, under `http_then_browser`, that **either** backend is not ready. Run doctor and check `OCCAM_HOME`. |
| **HONEST** | The chapter must contain a section titled *causes with no signal*: the public-reference browser skip surfaces as an ordinary HTTP failure; a TSA failure vanishes silently; a corrupt watch store resets to empty; a batch persist IO failure is swallowed; a Playwright proxy resolution failure falls open to no proxy; `ThinExtractBrowserExhausted` is under-described in `agentMeta.decisions` (GAP-016); probe reports SSRF blocks as `network_error` (EF-042); an unknown host-binary CLI argument is silently ignored and stdio starts and blocks (GAP-035). Diagnosis is therefore partly a process of elimination, and the book must say so instead of implying every cause is reported. |
| **DEPTH** | 10–12 pages · 5 sections |
| **WHERE** | Both. |
| **CHECK** | Deliberately unset `OCCAM_HOME`, call `occam_transcode`, and match the response against the tree. |

---

### Ch 26 — Architecture internals

| Field | Content |
|---|---|
| **OBJ** | Read the source with an accurate map: layers, spines, process topology, DI lifetimes, and where state and side effects enter. |
| **PRE** | Ch 4, Ch 5, Ch 20, Ch 21 |
| **COVERS** | Structural view of all nine systems |
| **SRC** | `PRODUCT-ARCHITECTURE.md` §1 (L0–L8 layer model), §4 (process topology), §5 (side-effect entry points), §6 (concurrency and lifecycle), §8 (verified non-claims); `CODE-MAP.md`; `SHIPPED-CODE-MAP.md`; `SOURCE-COVERAGE-MATRIX.md` |
| **MODEL** | **Nine layers, six orchestration spines, one host process, several worker process families.** |
| **EX** | Task R step 23: for the single call in Ch 3, name every process that existed, every DI singleton that was resolved, and every file that was touched. |
| **MISC** | "Dead code does not ship." — The Core project uses the SDK default compile glob with no `<Compile Remove>`, so every type under `src/FFOccamMcp.Core/**` compiles into the AOT binary. **Shipped ≠ reachable ≠ documentable** (C8, `SHIPPED-CODE-MAP.md:7-13`). |
| **HONEST** | The chapter must carry the dead-but-shipped register (§6 of `PRODUCT-VS-ENGINEERING.md`) precisely so a contributor reading the tree does not mistake a registered-but-uninjected abstraction for an extension point: `IWorkerProcessSpawner`, `BrowserConcurrencyGate.Run`, `MaterializedProvenanceResolver`, the alternate codecs, `TableSemanticMaterializer`, `ResponseBudgetMode.Unchanged`/`DeltaOnly`, the canonical IR types. Worker timeouts, pool lifecycle and the `OCCAM_GATE` conditional-compilation boundary belong here too. |
| **DEPTH** | 12–14 pages · 6 sections |
| **WHERE** | **Handbook-only.** Public docs describe supported reachable behavior, never binary membership or internal type names. |
| **CHECK** | Grep the tree for one item in the dead register and confirm it has no caller outside tests and the gate. |

---

### Ch 27 — Checking this book yourself

| Field | Content |
|---|---|
| **OBJ** | Independently confirm or refute the book's load-bearing claims, and know what to do when the code and the book disagree. |
| **PRE** | all |
| **COVERS** | none — this is method, not capability |
| **SRC** | Every chapter's `CHECK`; `DISCOVERABILITY-GATE.md` §3 (mechanical checks already designed); `CANONICAL-AUDIT-INDEX.md` §"Known incompleteness" |
| **MODEL** | **A book about honesty must be falsifiable.** Precedence when they disagree: executable code wins, then the Wave-4 correction layer, then the canonical ledgers, then this book. |
| **EX** | Task R closes: re-run the whole thread from Ch 3 to Ch 24 as one scripted session and record every observation. |
| **MISC** | "If the book and the tool disagree, the tool is broken." — The book is downstream of the code. A disagreement is first a book bug; only re-reading the cited `path:line` decides. |
| **HONEST** | Three honest limits on falsifiability. **(a) Network-dependent checks decay** — any check against a live third-party site is not reproducible forever; each such check is tagged `NETWORK` and paired with a local alternative where one exists. **(b) Some claims were source-proven and never runtime-reproduced** in the audit that produced this book: EF-041 multi-session pool kill, EF-045 fragment cache collision, EF-051 Docker health, the EFC-P5-05-1/2/3 constructions, and the managed-backend receipt path. They are labelled `SOURCE-PROVEN` in the register, not asserted as observed. **(c) Tokenizer error bounds are unmeasured**, so no check in this book asserts a token count. |
| **DEPTH** | 8–10 pages · 4 sections (the register of ~30 checks · how to run them as one session · reading a disagreement · what is not checkable and why) |
| **WHERE** | **Handbook-only.** Public docs get individual verifiable snippets inside their own pages; the falsification protocol and the audit-precedence rule are a handbook concern. |
| **CHECK** | The chapter is its own check. Success criterion: a reader who has never seen the source can refute at least one sentence in this book without reading C#. |

---

## 3. Reading paths

Chapters are numbered for a single linear read. Every other audience takes a subset, and each subset is stated as an **ordered** list, not a set.

| Path | Order | Rationale |
|------|-------|-----------|
| **Shortest path to competence (6)** | **1 → 2 → 4 → 5 → 7 → 14** | The definition, the honesty contract, the plurality of spines, the real ladder, the token contract, and the limits of a receipt. These six make the reader safe with the product and immune to its four most dangerous over-readings. Add **18** as a seventh if they will deploy it for anyone else. |
| **Agent integrator** | 1 → 2 → 3 → 4 → 5 → 7 → 8 → 9 → 10 → 24 → 25 → 14 → 16 → *(6 if walls, 11+13 if site-specific, 18 when a tool is missing)* | Optimised for "what do I call, what comes back, what does it mean". Trust arrives after composition because the integrator's first failure mode is a wasted call, not a false proof. Ch 24 is placed unusually early — the anti-composition table prevents more wasted work than any other page. |
| **Operator** | 1 → 2 → 3 → 18 → 19 → 20 → 21 → 22 → 23 → 25 → 17 → *(5+6 to understand why fetches fail)* | Exposure before operation: an operator who does not understand profiles and gates cannot debug "the tool is missing". Automation, state and configuration are the operator's real subject matter, and they are three consecutive chapters for that reason. |
| **Auditor / verifier** | 1 → 2 → 7 → 14 → 15 → 16 → 21 → 23 → 18 → 27 | Ch 7 is non-negotiable here: an auditor who does not know the hash covers **compiled** bytes will mis-read two legitimate receipts for one page as a contradiction. Ch 18 late but mandatory (the `reader` produce-without-verify trap). Ch 27 is where an auditor's work actually starts. |
| **Contributor** | 26 → 4 → 5 → 7 → 14 → 20 → 24 → 2 → then the remainder in number order → 27 | Inverted deliberately: a contributor needs the map before the narrative. Ch 2 sits late because a contributor arrives already able to read a response but not yet knowing which of its fields is a contract. |
| **Someone deciding whether to adopt it** | 1 → 2 → 5 (skim) → 14 → 17 → 23 | Definition, contract, real routing behavior, the honest ceiling of the trust story, what is opt-in and why, and the threat model. Six chapters, no setup required. |

---

## 4. Authoring order (differs from reading order)

Reading order optimises comprehension; authoring order optimises **not having to rewrite**. The rule: write the chapters that fix vocabulary before any chapter that uses the vocabulary.

| Wave | Write | Why this position |
|------|-------|-------------------|
| **1 — vocabulary** | **Ch 2**, then **Ch 14** | Every other chapter's `HONEST` field derives from these two. "Failure", "unknown", "verified", "proof", "provenance", "signed" must be nailed to `TRUST-MODEL` §2 and §13 before a single other sentence uses them. Writing any trust word before Ch 14 exists guarantees a global rewrite. |
| **2 — the two most-miswritten mechanisms** | **Ch 5**, then **Ch 7** | The router is the corpus's single largest documented error (C1/EF-056); the token contract is the product's actual reason to exist. Both are cited by a dozen later chapters, so their tables must be canonical first. |
| **3 — the framing that resizes everything** | **Ch 18** | Until "15 tools ≠ the product" is written down, every later chapter silently over- or under-weights its subject. Writing this third prevents Ch 17 and Ch 19 from being drafted at the wrong scale. |
| **4 — the map** | **Ch 4**, then **Ch 26** | Ch 4 is the teaching subset of Ch 26; drafting them together keeps them from diverging, and drafting them after waves 1–3 keeps the reference narrative honest about what the ladder and the budget really do. |
| **5 — the body, in dependency order** | 6 → 8 → 9 → 10 → 11 → 12 → 13 → 15 → 16 → 17 → 19 → 20 → 21 → 22 → 23 → 24 → 25 | Each of these has all prerequisites already written. This wave can genuinely parallelise along the part boundaries (B, C, D, E, F) because waves 1–4 supply the shared vocabulary. |
| **6 — compression** | **Ch 3**, then **Ch 1** | Ch 3 must be written after Ch 19 so the minimum path is a deliberate subset of the real one, not a guess. Ch 1 is the compression of the entire book and is the last chapter to be *frozen* even though `PRODUCT-DEFINITION.md` §2 supplies its thesis on day one. |
| **7 — falsification** | **Ch 27** | It collects the other 26 `CHECK` fields; it cannot exist before them. |

**Spine chapters (must be right before anything else can be written): Ch 2, Ch 14, Ch 5, Ch 7, Ch 18, Ch 4** — waves 1–4. If only these six are ever written well, the book is still net-positive; if any one of them is wrong, every chapter downstream inherits the error.

---

## 5. The recurring worked example — Task R

**One task, twenty-three advances, one URL family.**

> **Task R.** *You must answer a colleague's question — "what rate limits does this API document?" — with a sentence you can quote, a proof that the sentence was on the page you read, and a way to notice when the page changes. You did not write Occam, you have no special access, and you must not guess.*

The site is a public API documentation site with the shape most likely to exercise the product honestly: a marketing landing page that renders client-side, a static reference section, a table of limits, a login-walled changelog, a `/v2/` path that 404s, and a pricing page that extracts badly.

| Ch | How the example advances |
|----|--------------------------|
| 1 | The naive attempts — recall from model memory, and a generic fetch that returns an empty shell — both produce confident wrong answers. |
| 2 | First real call. The landing page returns `ok:false` / `thin_extract`; the changelog returns `ok:true` / `short_quality`. Same length, opposite meaning. |
| 3 | Stand up the host; run the first call for real; find the key that appeared uninvited. |
| 4 | Annotate that call with all fourteen steps; observe that the probe in Ch 9 will skip nine of them. |
| 5 | The ladder in four flavours: static page stops at HTTP; `/v2/` 404 terminates; the playground escalates to Chromium; the partner page fails both and surfaces `http_403` by informativeness rank. |
| 6 | The changelog is login-walled. Session profile, browser policy — and the discovery that the same profile reaches less through `occam_probe`. |
| 7 | The reference page is 180 KB. Three reads: ambient budget, explicit `max_tokens`, `fit_markdown` + `focus_query="rate limit"`. Compare `compile.omitted`. |
| 8 | `json_tables` lifts the limits table as records; the `contentHash` is kept for next week's `if_none_match`. |
| 9 | Probe before fetching; map the docs sitemap; search when the site is unknown. |
| 10 | Five reference pages in one digest under one budget, with per-item failures. |
| 11 | `playbook_policy=auto` vs `off` on the same page; read which tier won. |
| 12 | The pricing page still fails → heal → draft → lint → save → re-compare. |
| 13 | `{limit, window, scope}` as typed facts using the schema from Ch 12. |
| 14 | Keep the receipt for the sentence about to be shipped; label every field signed / cargo / self-asserted. |
| 15 | The colleague verifies offline with the exported PEM; then the MCP default-key trap; then the `live` mode trap. |
| 16 | Claim-check the exact sentence, take the citation into `verify mode=citation`, export the five URLs as a dataset and verify the manifest **on the CLI**. |
| 17 | Watch the page for change; consider batch and reject it; run crosscheck and read the verdict as an observation, not a proof. |
| 18 | The colleague's host runs `reader` and cannot verify what it can produce. |
| 19 | Their install breaks; doctor, refresh — and refresh kills an unrelated install. |
| 20 | Account for everything the very first call did unasked. |
| 21 | Walk the filesystem and name every file the exercise created. |
| 22 | For each surprise, name the variable that would — or provably would not — have prevented it. |
| 23 | Red-team the finished workflow. |
| 24 | Three attempted chains that do not exist, and why the join keys say so. |
| 25 | The workflow regresses; diagnose it from the response alone. |
| 26 | Name every process, singleton and file behind the single call from Ch 3. |
| 27 | Re-run the whole thread as one scripted session and record every observation. |

**Why a single thread:** the alternative — a fresh example per chapter — lets each chapter look successful in isolation, which is exactly the failure mode the audit found in the previous documentation. One thread forces every chapter to inherit the previous chapter's inconvenient facts.

**Honesty constraint on the example:** the concrete host must be chosen at authoring time from a site that is (a) public, (b) stable enough to survive a release cycle, and (c) already represented in `corpora/l0-smoke.jsonl` so its behavior is under gate observation. Every step is tagged `NETWORK` or `LOCAL` in Ch 27's register, and no step's expected output is quoted as an exact string.

---

## 6. Concept dependency graph

Mental models, ordered by what must exist before what. `A → B` reads "B is not learnable, or is learnable wrongly, without A".

```
honesty contract (ok:false = UNKNOWN)                      [Ch 2]
   ├─► typed failure taxonomy ...................... [Ch 2 shallow, Ch 5 deep, Ch 25 applied]
   ├─► quality.verdict: thin_extract vs short_quality [Ch 2]
   └─► "a signed failure proves a wall, not content" . [Ch 2 → Ch 14]

spine plurality (one narrative, several spines)            [Ch 4]
   ├─► the acquisition ladder with exits ............ [Ch 5]
   │      ├─► escalation vs termination ............. [Ch 5]
   │      ├─► FailureRanking chooses the surface .... [Ch 5]
   │      └─► walls and their one lever each ........ [Ch 6]
   │             └─► session tiers 1/2/3 ............ [Ch 6 → Ch 13, Ch 15 live-mode]
   └─► "this tool bypasses the pipeline" ............ [Ch 9, Ch 13, Ch 26]

compiled markdown is the object                            [Ch 1 → Ch 7]
   └─► token budget as a whole-response contract .... [Ch 7]
          ├─► compile.omitted (budgeting ≠ truncation) [Ch 7]
          ├─► ambient client budget ................. [Ch 7 → Ch 18]
          ├─► contentHash covers COMPILED bytes ..... [Ch 7 → Ch 14]  ◄ load-bearing
          │      ├─► two budgets ⇒ two legitimate hashes [Ch 14]
          │      ├─► if_none_match / diff_against ... [Ch 8]
          │      └─► cache identity ................. [Ch 8 → Ch 21]
          └─► sidecars share the budget ............. [Ch 8]
                 └─► blocks exist ................... [Ch 8]
                        └─► Merkle leaves ........... [Ch 14]
                               └─► citations ........ [Ch 16]

extraction can be bad for one site                         [Ch 2, Ch 5]
   └─► playbook as a soft in-band overlay ........... [Ch 11]
          ├─► four-tier resolution, per-field ....... [Ch 11]
          ├─► authoring loop with a human hole ...... [Ch 12]
          └─► schema exists ⇒ typed extraction ...... [Ch 13]   ◄ hard dependency PS-4 → PS-5

signature = "this key asserted these bytes"                [Ch 14]
   ├─► no PKI / no identity / TOFU ................. [Ch 14]
   ├─► ts is the signer's clock .................... [Ch 14]
   ├─► capsule = signed core, unsigned wrapper ..... [Ch 14]
   ├─► verification is arithmetic .................. [Ch 15]
   │      └─► mode asymmetries MCP vs CLI ........... [Ch 15]
   └─► retrieval + membership, never stance ......... [Ch 16]

exposure model (51 entrypoints, 15 default tools)          [Ch 18]
   ├─► profiles change exposure, not semantics ...... [Ch 18]
   ├─► opt-ins are orthogonal to profiles ........... [Ch 17, Ch 18]
   └─► "why can't I see this tool" .................. [Ch 17, Ch 19, Ch 25]

automation (29 unrequested decisions)                      [Ch 20]
   ├─► state and footprint ......................... [Ch 21]
   │      └─► uninstall / privacy / retention ....... [Ch 21 → Ch 23]
   ├─► configuration negative space ................. [Ch 22]
   └─► threat model ................................. [Ch 23]

everything above                                           
   └─► composition and anti-composition ............. [Ch 24]
          └─► diagnosis by elimination .............. [Ch 25]
```

**Four orderings that are non-negotiable, with the failure that occurs if violated:**

| Must precede | If violated |
|--------------|-------------|
| `ok:false` contract **before** receipts | The reader learns to collect proofs before learning that a proof on a failure proves only the failure. Produces exactly the "decorated with receipts belonging to other URLs" failure of TRUST-MODEL §9.5. |
| Acquisition ladder **before** playbooks | A playbook's `preferredBackend` override only applies under `http_then_browser`; without the ladder the override looks unconditional. |
| Token budget / compiled-bytes **before** contentHash | The reader treats two different hashes for one page as evidence of tampering rather than of two budgets. |
| Materialization **before** cache and diff | `if_none_match` compares Occam's own hash of compiled markdown, not an HTTP ETag; taught earlier it reads as origin revalidation. |

---

## 7. Anti-chapters

Material that must **not** become a chapter, a section heading, or a positive bullet. Cross-referenced to `PRODUCT-VS-ENGINEERING.md` (PvE) and `DOCUMENTATION-EXPOSURE-MATRIX.md` (DEM).

### 7.1 Dead-but-shipped (never a capability)

| Would-be chapter | Reality | Reference |
|------------------|---------|-----------|
| "The canonical knowledge model" / "Choosing a codec" | The IR is built on every transcode and discarded; the codec registry has no live selection surface; `MarkdownPassthroughCodec` is the only codec that runs. | DEM §2; PvE §6 CAP-328/330/332/333; EF-004; C8 |
| "Provenance tracing" | `MaterializedProvenanceResolver` / `ProvenanceTrace` ship with zero callers. | PvE §6 CAP-286/331 |
| "Extending Occam with a custom worker spawner" | `IWorkerProcessSpawner` is registered and never injected; `BrowserConcurrencyGate.Run` is never called. These are not extension points. | PvE §6 CAP-248a/b; EF-009 |
| "Response budget modes" | `Unchanged` / `DeltaOnly` are test-only with no live selector. | PvE §6 CAP-324 |
| "Semantic table materialization" | `TableSemanticMaterializer` is a bench/test-only path. | PvE §6 CAP-334 |
| "Paywall detection" | The `paywall` negative-receipt branch has no producer. | PvE §6 CAP-264/279; EF-008 |
| "Row-mode extraction" | Host parsers never set `base_selector`. | PvE §6 CAP-600; EF-014; C4 |
| "Aggressive consent handling" | The heal worker's `--consent-aggressive` flag is unreachable from MCP. | PvE §6 CAP-553 |
| "How lint and save sanitize your playbook" | `PlaybookCommunitySanitizer` is Core-dead; nothing publish-sanitizes on the local path. | PvE §6; EF-047; C3 |
| "Automatic retry and backoff" | No automatic retry exists anywhere. | PvE §6 CAP-188 |

*Rule applied:* these may appear **only** in Ch 26's dead-register, whose explicit purpose is to stop a contributor mistaking them for extension points. They may not appear in Ch 1–25 at all.

### 7.2 Bug-derived behavior (never normalised as intent)

| Would-be chapter or claim | Why forbidden | Reference |
|---------------------------|---------------|-----------|
| "Cache normalization" (flags omitted from the key) | EF-001 is a live defect; documenting the omission as intentional identity would make callers rely on it. | PvE §2 EF-001 |
| "Fragment-scoped caching" | Fragment variants collide and can replay another fragment's signed response. | PvE §2 EF-045 (CRITICAL) |
| "Verified change history" | An entirely unsigned chain returns `history_verified` / exit 0. | EFC-P5-05-2; PvE §5 |
| "`unknown_key` identifies a foreign author" | Tampering manufactures that classification by editing one unsigned string. | EFC-P5-05-1; PvE §5 |
| "Machine-wide restart" as an operator feature | `occam refresh` killing every install by binary name is a defect, not a fleet-management verb. | PvE §2 EF-049 |
| "npm quick install" | Unpublished and DOA as packed. | PvE §3 rank 5; EF-034 |
| "Production-ready container" | HEALTHCHECK invokes an unsupported verb and can hang. | PvE §3 rank 8; EF-051 |
| "Signed supply chain" / "cosign-verified install" | No shipped install path consumes the bundle. | PvE §3 rank 4; EF-053 |
| "Validated community marketplace" | CI can auto-merge after a skipped gate. | PvE §3 rank 1; EF-052 |
| "Safe schema extraction for untrusted pages" | One path lacks DNS pin and body cap; another evaluates page-controlled text. | PvE §3 rank 2; EF-013/043 |
| "Session-isolated browser fetches" | Pooled anonymous contexts are not a security boundary. | PvE §3 rank 3; EF-002/040 |

### 7.3 Marketing framing (structurally wrong chapters)

| Would-be chapter | Why it is a category error |
|------------------|----------------------------|
| **"A tour of the 15 tools"** | The most tempting structure and the worst. It reproduces the exact error the audit was run to correct: 15 tools is one exposure slice of 51 named entrypoints, and it hides all of PS-7 and PS-9 (`ENTRYPOINT-MODEL.md` §0). A tool-by-tool reference is the API spec's job, not the handbook's. |
| **"Token savings benchmark"** | The tokenizer has unmeasured error bounds and a single global reduction headline is forbidden. Any figure must be per tier with a declared baseline — which the audit does not have. |
| **"Verifiable provenance"** | Forbidden claim #1. Provenance is precisely what is not established. |
| **"Bypassing walls"** | Forbidden. Occam detects walls and names them; operator-supplied sessions pass some of them. |
| **"Consensus and multi-party attestation"** | Forbidden claims #9–10; no remote node, signer or jury exists. Crosscheck lives in Ch 17 as an observation tool. |
| **"AI-powered extraction" / "intelligent summarization"** | No model, no weights, no generation anywhere in the host or the workers. |
| **A per-tool parameter chapter set** | Violates the single-source rule: parameter tables are generated from `[Description]` attributes into `MCP_API_SPEC.md` and `docs/tools-reference.md`. The handbook links and explains; it never restates a generated table. |

---

## 8. Handbook vs public docs boundary

| Content | Handbook | Public docs | Rule |
|---------|:--------:|:-----------:|------|
| Honesty contract, ladder truth, token contract, receipt limits, opt-in gates, operator workflows, anti-composition table | ● | ● | Shared spine. The public version is a compression, never a softening. |
| EF-level engineering findings and their documentation consequence | ● | limits only | Public docs may state the **limit** ("cache replay can cross URL fragments — avoid cache for fragment-sensitive reads") and never the finding ID or the defect narrative. |
| The 51-entrypoint accounting and its counting method | ● | the number only | Method is audit material; the number is a framing correction users need. |
| Why the previous documentation was wrong (`DOCUMENTATION-EXPOSURE-MATRIX` §3) | ● | ✗ | A product's docs should not litigate their predecessor. |
| The taxonomy derivation (9 systems, T-1…T-5, 674 CAPs) | ● | ✗ | Users need the model's *shape*, not its provenance. |
| Architecture internals, dead-but-shipped register, DI lifetimes, `OCCAM_GATE` boundary | ● | ✗ | `SHIPPED-CODE-MAP` C8: shipped ≠ reachable ≠ documentable. |
| Falsification protocol and audit-precedence rule (Ch 27) | ● | per-page snippets | Public pages carry individual verifiable snippets; the protocol is a handbook concern. |
| Generated per-parameter tables | ✗ | ● | Single source: code `[Description]` → `MCP_API_SPEC.md` + `docs/tools-reference.md`. The handbook links. |
| Per-OS copy-paste install quickstarts | minimal only | ● | Ch 3 is deliberately one path; the public docs own the matrix. |
| Full environment-variable catalog | Appendix B (reference) | ● | Ch 22 explains the negative space and defers the catalog. |
| Release notes and changelog | ✗ | ● | — |
| Marketing framing of any kind | ✗ | ✗ | Neither surface. §7.3 applies to both. |

**One asymmetry worth stating explicitly:** the handbook is allowed to be *pessimistic in tone* (it is written for someone who must trust the product without having built it), while public docs must be *accurate but usable*. The difference is emphasis and depth — never a different set of facts. A claim that cannot survive in public docs cannot survive in the handbook either.

---

## 9. Appendices (not chapters)

| # | Appendix | Content | Source |
|---|----------|---------|--------|
| A | Failure-code reference | Every code, its producer, its meaning, its honest next action, and the three codes that currently mislead (EF-042, CAP-653, GAP-016). | `FAILURE-BEHAVIOR-MAP.md`, `ACQUISITION-ROUTING-MODEL.md` |
| B | Environment-variable catalog | Full list with defaults; every entry cross-linked to the chapter that explains it; negative-space notes inline. | `ENVIRONMENT-VARIABLES.md`, `CONFIG-NEGATIVE-SPACE.md` |
| C | Artifact reference | The 39 artifacts by family, with signed / hashed / verifiable / persisted columns and the naming-honesty table. | `ARTIFACT-ONTOLOGY.md` |
| D | Forbidden-claims card | The twenty forbidden statements and their permitted replacements, as a one-page reviewer's checklist. | `TRUST-MODEL.md` §13; `PRODUCT-DEFINITION.md` §5 |

Appendices are **reference**, not teaching, and no chapter may be a thin wrapper over one.

---

## 10. Deviations from the proposed 25-chapter progression

Every change, with its reason. `P#` = the proposed chapter number.

| # | Change | From → to | Reason (evidence) |
|---|--------|-----------|-------------------|
| 1 | **DISSOLVE** | P2 "mental model" → per-chapter `MODEL` field + §6 dependency graph | A single mental-model chapter front-loads abstractions the reader cannot yet attach to anything. Each chapter now names exactly one model, and §6 orders them. |
| 2 | **ADD** | new Ch 2 "the honesty contract" | The proposal had no chapter for the response contract, and cross-cutting lens #7 (T-5) is the product's differentiating surface with no owning family. `TRUST-MODEL` §9.5 makes it prerequisite, not payoff. |
| 3 | **ADD + MOVE FORWARD** | slice of P20 "connecting hosts" → Ch 3 "an install you can test against" | The book must be falsifiable (mission req. 7) and nothing is checkable without a host. `USE-CASE-MODEL` ranks Operator #2 STRONG for the same reason. |
| 4 | **RESHAPE** | P3 "request flow" → Ch 4 "…and why there is no single spine" | `PRODUCT-ARCHITECTURE` §0 falsifies the linear spine product-wide; nine of twenty-one tool names bypass the pipeline. Teaching a universal flow guarantees a wrong model. |
| 5 | **MERGE (3→1)** | P4 acquisition + P7 browser + P8 managed → Ch 5 | D2. The short-circuits (404/410, public-reference) and `FailureRanking` exist *between* rungs; per-rung chapters cannot express them and reproduce the cascade myth. C1 / EF-056 / GAP-001. |
| 6 | **ADD + ABSORB** | P6 sessions (consumer half) → Ch 6 "when acquisition is hard" | Four PS-1 families — `session-fetch`, `access-consent`, `network-safety`, `proxy-egress` — had no chapter at all. `ACQUISITION-ROUTING-MODEL` §2 answers "what changes when it gets hard" as one subject. |
| 7 | **SPLIT (1→2)** | P5 materialization → Ch 7 token contract + Ch 8 structured/differential/cache | Exposure classes differ: `token-budget` and `focus-selection` are PUBLIC_CORE; sidecars, diff and cache are PUBLIC_ADVANCED / EXPERIMENTAL (DEM §1). One chapter would either bury the core or promote the cache. |
| 8 | **ADD** | new Ch 10 "digest" | `digest-synthesis` is a **core always-on** family (T-2) and was entirely absent from the proposal. "Several URLs → one digest, not N transcodes" is a first-guide rule. |
| 9 | **SPLIT (1→2)** | P10 playbooks → Ch 11 resolution + Ch 12 authoring | Two audiences with different prerequisites: every transcode caller meets the overlay; only an author runs heal→save. `USE-CASE-MODEL` adds "Playbook author" as a distinct mode (UC-4). |
| 10 | **MERGE (2→1)** | P14 claims + P15 datasets → Ch 16 | `dataset-provenance` is one family whose only set-level verification is CLI-only (EF-018); it teaches best beside claim citations, both being "evidence you attach to an assertion". |
| 11 | **DEMOTE** | P17 "crosschecking" → a section of Ch 17 | D4. `consensus-crosscheck` is `DO_NOT_DOCUMENT_AS_FEATURE` for its claim (DEM §2; TRUST §13 #9–10; EF-031/032). A chapter grants product weight the code does not support. |
| 12 | **MERGE (2→1)** | P16 monitoring + demoted P17 + batch + atlas → Ch 17 "opt-in surfaces" | All four are env-gated, invisible by default, and each has a named ceiling. Grouping keeps gate + limits in the same breath (`DISCOVERABILITY-GATE` R2). |
| 13 | **MERGE (3→1)** | P18 profiles + P19 CLI (exposure half) + P20 connecting hosts (exposure half) → Ch 18 "exposure" | `ENTRYPOINT-MODEL` §0: they are three fragments of one question — what can this deployment reach? |
| 14 | **RESHAPE** | P19 CLI + P20 connecting hosts (operator halves) → Ch 19 "operating an install" | PS-9 is 159 CAPs / 4 families / 16 EFs. It needs one honest operator chapter, not two thin ones split by accident of surface. |
| 15 | **ADD** | new Ch 20 "what Occam does without asking" | 29 proven automatic behaviors, 11 not disableable, 10 with a disclosure duty (`AUTOMATION-MODEL` §6). Absent from the proposal and the largest single source of operator surprise. |
| 16 | **ADD** | new Ch 21 "state, persistence and footprint" | `STATE-MODEL` §0: "no file cache by design" ≠ stateless. 29 state items, credential-bearing files, and an uninstall that leaves `~/.occam`, host configs and the Playwright cache behind. |
| 17 | **REFOCUS** | P21 configuration → Ch 22 "configuration and its negative space" | The catalog is Appendix B. The teachable content is the holes: EF-007/CAP-166, CAP-165, C6, GAP-018, GAP-030, EF-057. |
| 18 | **BIND** | P22 security → Ch 23, bound to `TRUST-MODEL` §10 + the `NEEDS_FIX_BEFORE_DOC` shortlist | A security chapter written from intuition would omit the three surfaces that currently may not be described as safe for untrusted input (EF-043/013, EF-002). |
| 19 | **RESHAPE** | P23 "advanced workflows" → Ch 24 "what chains, and what does not" | `COMPOSITION-MODEL`'s highest-value content is the 8 rejected chains, 10 broken half-wired chains and 12 anti-compositions — not more recipes. Recipes belong in public task guides. |
| 20 | **RENAME + REPOSITION** | P24 troubleshooting → Ch 25 "diagnosing a bad result" | Kept, but failure-code-driven and placed after automation, because a third of real causes are invisible in the response and can only be reached by elimination. |
| 21 | **KEEP + RESTRICT** | P25 architecture internals → Ch 26, handbook-only | Kept last, but explicitly not public: `SHIPPED-CODE-MAP` C8 means the binary contains types that are not product behavior. |
| 22 | **ADD** | new Ch 27 "checking this book yourself" | Mission req. 7. Also the only structural defence against the drift that produced this audit. |
| 23 | **KEEP (position confirmed)** | P9 discovery → Ch 9; P11 knowledge → Ch 13; P12 receipts → Ch 14; P13 verification → Ch 15 | Relative order survives contact with the model: discovery after the reference path (spending discipline needs something to spend on), knowledge after playbooks (hard dependency PS-4 → PS-5, CAP-590), receipts before verification. |
| 24 | **REDISTRIBUTE** | `PRODUCT-DEFINITION` §3's thirteen negations → per-chapter `MISCONCEPTION` field, six retained in Ch 1 | A negative chapter early is demotivating and forgettable; a misconception corrected at the moment of formation sticks. |
| 25 | **ADD (structural)** | new per-chapter `CHECK` field | Makes D5 enforceable per chapter rather than only in Ch 27. |
| 26 | **NET** | 25 → **27 chapters** | +7 added (2, 3, 10, 20, 21, 27, and the Ch 11/12 split), −5 removed by merging (3→1 acquisition, 3→1 exposure, 2→1 monitoring, 2→1 claims/datasets, 1 dissolved mental-model), +1 net from the materialization split. The growth is re-composition, not accretion: total page estimate is 290–350 pages, and no chapter exceeds 16. |

---

## 11. UNCERTAIN

| Item | Bound | What would resolve it |
|------|-------|----------------------|
| The concrete site for Task R | Not chosen here on purpose. Constraints stated in §5: public, stable, present in `corpora/l0-smoke.jsonl`. | An authoring-time selection reviewed against the smoke corpus. |
| Whether Ch 22 (configuration) should be a chapter or fold into Ch 20 + Appendix B | Kept as a chapter because the negative space is teachable and cross-cutting; a defensible alternative removes it and grows Ch 20 by ~4 pages. | An editorial decision after Ch 20 is drafted. |
| Whether Ch 6 should split into "walls" and "egress and safety" | Kept as one at 12–14 pages. If the wall taxonomy grows past that, split at the session/egress seam. | Draft length of Ch 6. |
| Whether the auditor path needs Ch 12 (playbook authoring) | Excluded. An auditor meets playbook signatures via Ch 14's `provenance`-exclusion note, which may be insufficient. | Review after Ch 12 and Ch 14 are drafted. |
| Whether a public "capability page per family" layer should exist between task guides and reference | Out of scope here — `DISCOVERABILITY-GATE` §0 lists `CAPABILITY` as a path type but does not require it. | A public-docs IA decision, separate from the handbook. |
| Page estimates | Rough; derived from family counts and evidence density, not from drafted text. | First three drafted chapters recalibrate the rest. |
