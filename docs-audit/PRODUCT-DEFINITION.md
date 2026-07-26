# PRODUCT-DEFINITION (Phase 5D)

**Agent:** P5-02
**SoT:** executable code. Wave-4 corrections override older prose (C1–C9).
**Derived from:** `PRODUCT-TAXONOMY.md` (9 systems / 38 families / 38 product capabilities), `canonical-capabilities.json`, `PRODUCT-ARCHITECTURE.md`, `ENTRYPOINT-MODEL.md`, `TRUST-MODEL.md`, `ACQUISITION-ROUTING-MODEL.md`, `STATE-MODEL.md`, `AUTOMATION-MODEL.md`, `ARTIFACT-ONTOLOGY.md`.
**Public docs and README were not consulted. No marketing language is permitted in this file.**
**Date:** 2026-07-26

Every sentence below must be traceable to code or to a canonical audit ID. Where a word is stronger than the code supports, the file says so instead of using the word.

---

## 1. Candidate definitions, evaluated against the complete model

| # | Candidate | What it captures | What it misses | What it overstates |
|---|-----------|------------------|----------------|--------------------|
| 1 | **fetcher** | The default act: one URL in, page content out (`occam_transcode(url)`, only `url` required). | Almost everything downstream: the token budget that is the actual reason to use it (PS-2), the two-rung escalation ladder and post-processors (PS-1), discovery without fetching (PS-3), recipes (PS-5), receipts (PS-6), multi-fetch composition (PS-7), and the operator surface (PS-9). | Nothing — it understates. It is also quietly **wrong about the object**: a fetcher returns the response body; Occam returns *compiled markdown*, and the hash it commits to is over that compiled form after budgeting, fitting, focus and translation (`ReceiptCanonicalizer.cs:17-18`), not over the bytes the origin served. |
| 2 | **content acquisition system** | PS-1 accurately and in full: routing, HTTP/browser/managed backends, SSRF and private-URL policy, proxy and rotation, session-aware fetch, access-wall classification, typed failures. Also PS-3. | PS-2 entirely. Two callers fetching the same page with different `max_tokens` get different content *and different content hashes* — that is materialization, not acquisition. Also misses PS-5/6/7. | Nothing, but it stops at the halfway point of the reference path (`TranscodePipeline.cs:149` router → `:177` materialize). |
| 3 | **context acquisition layer** | The *purpose*: filling a model's context window with real page content. Directly supported by the ambient budget — declare a context size once and later reads are sized to **20 % of it**, clamped to [512, 16384] tokens (`ClientCapabilityStore.cs:15-17,81-85`). | PS-5, PS-6, PS-7, PS-9; and discovery, which deliberately avoids acquiring content at all. | Two words. "Context" implies Occam manages the agent's context — it does not; it sizes *its own* output and has no view of what else is in the window. "Layer" implies interposition; Occam is a tool the agent calls, not something in the path. |
| 4 | **materialization engine** | PS-2 precisely: `BudgetOwnership` two-layer split, `FitMarkdown` BM25 prune, `SectionRanker` focus, `OmittedManifest`, blocks/tables/feed/chunks, `if_none_match`/`diff_against`/`delta_only`, opt-in disk replay. | PS-1, where most of the code, most of the risk and all of the network behavior live. Also PS-3/5/6/7/9. | "Engine" oversells the compile stage: the codec registry has **no live selection surface** (CAP-327) and `MarkdownPassthroughCodec` is the only codec that ever runs in production (CAP-329). The genuinely engine-like part is the budget planner, not the codec pipeline. |
| 5 | **agent data plane** | That the caller is an agent, not a human, and that PS-7 composes many fetches into one answer. | That there is no control plane, no scheduler and no delivery machinery. `occam_watch` has **no daemon** — cadence is the agent's job (`WatchService`); batch is a JSON file with last-writer-wins across processes (EF-038) and no eviction (EF-037); there is no queue broker, no retry policy (CAP-188: no automatic retry/backoff), no backpressure. | Heavily. "Data plane" implies managed throughput, routing guarantees and durable state. Occam is request-scoped by default (live extract, cache opt-in) and its durable stores are unbounded flat files. |
| 6 | **evidence / provenance layer** | The ambition of PS-6 and its real math: ECDSA over canonical bytes, Merkle membership over extraction blocks, dataset manifest row-set binding, watch-chain links. | PS-1/2/3/5 — i.e. the part of the product that runs on every call, versus the trust layer that is partial and opt-in-shaped. | The single most dangerous word in the corpus. **Provenance is precisely what is not established**: one auto-minted self-signed key with no identity binding, no PKI, no registry, no rotation, no revocation (`ReceiptSigner.cs:26-45`); no origin signature, no TLS transcript; `ts` is the signer's own clock. `ReceiptVerifier.cs:19-21` states the limit in the source. TRUST-MODEL §13 lists "verified provenance" as forbidden claim #1. |
| 7 | **research runtime** | The workflow arc that the tools genuinely support end to end — search → probe → transcode/digest → claim_check → attest → verify — and the fact that it *is* a runtime: a long-lived host process with DI, worker pools, daemons and session state. | The operator/packaging half (PS-9) and the fact that no research is orchestrated: there is no plan, no loop, no agent inside Occam. Every step is a separate call the agent chooses to make. | "Research" implies synthesis and judgment. `occam_claim_check` performs **no stance evaluation** — `Verdict` is hardcoded `not_evaluated` (`ClaimCheckService.cs:102`); `occam_attest` is a regex classifier that recognises two English claim shapes (CAP-721/722) and its aggregate is unsigned. |
| 8 | **MCP server** | How virtually every user first meets it: a default stdio host advertising 15 tool names under `OCCAM_PROFILE=full`. | ~71 % of the product's named entrypoints. The audit counts **51** distinct user-reachable entrypoints, of which the core MCP tools are 15 (`ENTRYPOINT-MODEL.md` §2): offline CLI verbs, 13 operator subcommands, installers, connect, three alternate process modes, package bins and the Docker entrypoint are all outside it. Also misses that profiles subtract tools and four env flags add six more. | Nothing — it is a category error rather than an exaggeration. It names a protocol adapter (PS-8, one of two *enabling* systems) and calls it the product. |
| 9 | **agent context infrastructure** | The intent, and the reason PS-9 exists at all: install, doctor, connect-to-15-hosts, packaging — the product is meant to be *installed and wired in*, not imported. | Everything concrete. The phrase does not say what happens when you call it. | "Infrastructure" implies shared, multi-tenant, centrally operated. Occam is a single-user local process: a per-machine self-minted key (EF-044), a name-wide process kill with no scope flag (EF-049), non-atomic single-writer stores (EF-019/038), and a browser pool that a second session destroys (EF-041). |
| 10 | **verifiable web-content acquisition and materialization runtime for agents** | PS-1 + PS-2 + PS-6 + the runtime nature, in one phrase, and it correctly puts acquisition and materialization in the head position. | PS-3, PS-5, PS-7, PS-9 — defensible, since each is either a pre-step, an overlay, a composition of the head, or an enabler. | One word: **"verifiable"**. Unqualified it reads as third-party-verifiable provenance. What is verifiable is *tamper-evidence against a locally minted key that a consumer must obtain out of band*. Adopt only with the qualification carried inside the definition. |
| 11 | **honest web reader for agents** (model-suggested) | The actual differentiator: `ok:false` means UNKNOWN, failures are typed, and quality is reported rather than smoothed over. | Everything structural. | "Honest" is an adjective a product cannot assert about itself; it is a property of the response contract, which should be shown, not claimed. |
| 12 | **token-economical, typed-refusal web reader with an optional local integrity log** (model-suggested) | Nearly everything load-bearing, in defensible words: token economics (PS-2), typed refusal (PS-1 `quality-failure-semantics`), integrity-not-provenance (PS-6), optional-not-universal. | Discovery, playbooks and multi-fetch composition. | Nothing. It is accurate and unmarketable. Its value is as the internal sanity check on any shorter phrasing. |

**Selected head phrase:** #10, with "verifiable" bound to its meaning in the sentence itself, cross-checked against #12.

---

## 2. CANONICAL TECHNICAL DEFINITION

### 2.1 One sentence

> **Occam is a locally run host process that turns a URL into content an LLM agent can actually use: it acquires the page through a gated HTTP→browser→(optional third-party) ladder, compiles the result into a token-bounded, focusable, optionally structured representation, returns a typed `ok:false` meaning *the content is unknown* rather than a guess when acquisition fails, and can sign what it did produce so the exact bytes can later be checked for tampering against a key the recipient obtains out of band.**

Claim-by-claim backing: *locally run host process* — `Program.cs`, stdio default (CAP-003), no vendor backend. *Gated ladder* — `OccamRouter.cs:134-182`, managed env-gated (CAP-054). *Token-bounded / focusable / structured* — `Compile/BudgetOwnership`, CAP-061/063/064/077/079/080. *Typed `ok:false`* — CAP-105, `FAILURE-BEHAVIOR-MAP.md`. *Sign / check for tampering / out-of-band key* — `ReceiptSigner.cs:51-64`, `ReceiptVerifier.cs:19-21`, TRUST-MODEL §3.

### 2.2 One paragraph

> **Occam is a local .NET host that exposes web-content acquisition to LLM agents, primarily as an MCP server (15 core tools by default) but also through an offline CLI, an operator wrapper, host auto-connect, and alternate WebSocket/remote/batch process modes. Its reference path takes a URL, applies safety and session preflight, and runs a gated escalation ladder — an HTTP extraction worker first, a Playwright Chromium worker if the HTTP result is unusable, and an operator-configured third-party provider only if both fail — then classifies the outcome through a post-processor pipeline that can downgrade an apparent success to `captcha_or_challenge`, `requires_login` or `thin_extract`. Content that survives is compiled: budgeted against a caller or ambient token limit, optionally pruned to a focus query, optionally emitted as blocks, tables, feed items or chunks, and optionally returned as a delta against a hash the caller already holds. Around that core sit five more capability systems: cheap pre-fetch discovery (probe, sitemap/link mapping, provider-backed search), per-site extraction recipes that can be resolved, healed, linted and signed, typed schema extraction through a separate CSS worker, multi-fetch composition (digest, batch, watch, cross-vantage comparison, session failure aggregation), and a trust layer that signs receipts, commits to extraction blocks with a Merkle root, and verifies them offline. Two further systems make the rest usable rather than adding outcomes: runtime exposure (transports, tool profiles, env gates, ambient client budget) and the operator surface (install, doctor, connect, sessions, refresh, packaging). Nothing is fetched from a vendor service; the only network calls are to the target origin, to whatever provider the operator configures, and to optional timestamping or translation endpoints.**

### 2.3 Five bullets

- **It reads web pages for models, not for browsers.** One required argument (`url`); the output is compiled markdown plus opt-in structured sidecars, sized to a token budget — by default 20 % of the context window the client declared, clamped to [512, 16384] tokens (`ClientCapabilityStore.cs:15-17,81-85`).
- **It escalates, and it stops.** `http_then_browser` tries HTTP, escalates to Chromium when the result is thin, a short challenge or a non-terminal failure, and may try an operator-configured provider only after both fail. It **terminates** on 404/410 and on failed HTTP for public-reference URLs, and on dual failure it surfaces whichever local attempt was more informative — never the managed failure (`OccamRouter.cs:134-182`, `FailureRanking.cs:10-21`, EF-056).
- **Failure is a typed answer, not a blank.** `ok:false` means the content is UNKNOWN and must not be filled in from model memory; `thin_extract` means bad extraction while a genuinely short good page is `ok:true` with `quality.verdict=short_quality`. Responses carry the router's attempt log and the host's decisions so an agent can act instead of guess (CAP-098/105/106).
- **Live by default; state is opt-in but not absent.** Every call re-extracts — the disk cache requires `cache_ttl_s > 0` (`TranscodeCacheEligibility.cs:13-16`). But "no cache by design" is not "stateless": a signing key, session cookies, playbooks, watch history and batch jobs persist under `~/.occam/` (`STATE-MODEL.md` §2).
- **Signing gives tamper-evidence, not provenance.** A receipt proves the holder of one locally minted key asserted those exact bytes and they are unaltered; a Merkle proof shows a block was among the ones that signer committed to. It proves nothing about who that holder is, that the origin served the content, that a fetch happened, or when (`ReceiptVerifier.cs:19-21`, TRUST-MODEL §3–§4).

---

## 3. What Occam is NOT

Each row was checked against code before being asserted. The "what it does instead" column is the sentence that should replace the misconception in any future document.

### 3.1 Not a crawler or scraper-at-scale platform

**Verified:** there is no frontier, no queue broker, no scheduler and no distributed worker tier. `occam_map` is bounded to at most a **second-level hub expansion** and hard-capped at **64 links** (`MapService.cs:10,76,180`) — it is a link *listing*, not a crawl. Batch is a local job list over URLs the caller supplied, stored in a single `jobs.json` that is last-writer-wins across processes (EF-038) with no eviction (EF-037). `occam_watch` has **no daemon** — the agent decides when to poll. Robots compliance is **off by default and fails open** (`OCCAM_RESPECT_ROBOTS`, GAP-018), and per-host throttling is `0` unless set — which is another reason it must not be described as a crawler: it is not built to be a polite one.

**What it does instead:** bounded, caller-initiated fetches of specific URLs, optionally fanned out over a list the caller provides.

### 3.2 Not a CAPTCHA or anti-bot bypass service

**Verified:** no solver exists anywhere in the tree. A challenge page becomes the typed failure `captcha_or_challenge` (`ChallengePagePostProcessor`, order 100). The browser worker's stealth is explicitly scoped and is not full anti-detect (CAP-180); there is no fingerprint or identity rotation (`ACQUISITION-ROUTING-MODEL.md`). Consent-banner dismissal (CAP-211/212) handles cookie overlays, which are not an anti-bot control. Managed providers may succeed where the local backends fail, but that is the operator paying a third party, configured per install, not a capability of Occam.

**What it does instead:** detects the wall, names it, and hands the caller the levers that legitimately work — a `session_profile` with real cookies, a proxy, `backend_policy=browser`, or an operator-configured provider.

### 3.3 Not a cache or CDN

**Verified:** the default path never reads or writes a cache (`TranscodeCacheEligibility.cs:13-16`; `cache_ttl_s` omitted → ineligible). With TTL set, entries are local files under `OCCAM_CACHE_DIR` or `{TEMP}/occam-cache`, expiry is evaluated **only on read** with no sweep (CAP-322), and the key omits `rank_blocks`/`tag_trust`/`emit_capsule` (EF-001) and the URL fragment (EF-045). There is no shared cache, no edge, no invalidation protocol, and no origin revalidation — `if_none_match` compares against Occam's own content hash of the compiled markdown, not an HTTP ETag.

**What it does instead:** live extraction every call, plus an opt-in single-machine replay of a previously materialized response and caller-held content hashes for cheap "did this change?" checks.

### 3.4 Not a search engine

**Verified:** no index, no crawl corpus, no ranking of a web-scale collection. `occam_search` is off by default and fails closed without `OCCAM_SEARCH_PROVIDER`; when set, it calls a third-party provider and can reorder results by **extractability** — a prediction of how well Occam could read each hit, not a relevance judgment (CAP-620…631). The only ranking Occam performs itself is BM25 *within a single already-extracted page* (`FitMarkdown`, `ClaimBlockRanker`).

**What it does instead:** proxies a configured search provider and annotates the hits with how readable each one is likely to be.

### 3.5 Not a browser automation framework

**Verified:** no scripting API, no selectors-as-a-public-surface, no assertions, no recorder, no test runner. Playwright is an internal backend (`BrowserExtractBackend`, pool + daemon) reached only by `backend_policy`, and its page-level actions are fixed extraction behavior: consent dismissal, virtual-scroll simulation, stealth baseline, `bypassCSP:true` unconditionally (EF-046). Playbook interaction plans do reach `page.evaluate`/`waitForFunction` (`interaction-steps.mjs:14`), but they are bounded declarative steps inside a recipe, not a user-facing automation language — and the heal worker's `--consent-aggressive` flag is not even reachable from MCP (CAP-553).

**What it does instead:** uses a browser as a rendering fallback to obtain content, and exposes recipes — not scripts — for sites that need interaction.

### 3.6 Not a fact-checker or truth oracle

**Verified:** `occam_claim_check` returns `Verdict = not_evaluated`, hardcoded (`ClaimCheckService.cs:102`); it retrieves blocks that clear a BM25 lexical floor and attaches Merkle membership proofs. `found:false` with `proven:true` is a *retrieval*-complete negative over an untruncated leaf set — not a semantic one; paraphrase, images, non-English phrasing and unextracted regions are outside its reach. `occam_attest` classifies with anchored regexes that recognise two English claim shapes (CAP-721/722) over the top-3 blocks, and its aggregate response is **unsigned**. `occam_crosscheck` compares fingerprints from vantages that share one process, one egress IP and one proxy configuration (CAP-859) — agreement excludes one cloaking axis and says nothing about accuracy.

**What it does instead:** retrieves the passages lexically relevant to a claim and proves they were in the extraction it signed. Whether they are true, current or in context is left to the reader.

### 3.7 Not a PKI, notary or certificate authority

**Verified:** one ECDSA P-256 key, minted silently on first host start, unencrypted PKCS8, `chmod 600` on POSIX and **no hardening at all on Windows** (`ReceiptSigner.cs:26-45,84-99`). No registry, no key distribution, no rotation, no expiry, no revocation. `keyId` is a truncated self-descriptive fingerprint with no identity attached. `occam keys export` against an empty store **mints a key and exports that** (`OccamCliVerbs.cs:208-215`). The optional RFC3161 anchor covers only the signature's existence and its TSA certificate is never chained to a trust root (`ReceiptTimeAnchor.cs:34-35`). The release cosign bundle is consumed by no shipped install path (EF-053).

**What it does instead:** maintains a local, self-signed integrity log. For the same machine that is real tamper-evidence; for a third party it is trust-on-first-use over a PEM they must obtain through a channel Occam does not provide.

### 3.8 Not a hosted service

**Verified:** the product is a process on the user's machine — stdio by default (CAP-003), optionally local WebSocket or self-hosted remote WSS+JWT, optionally a loopback batch HTTP API with **no auth** (CAP-006). There are no accounts, no quotas, no vendor endpoint and no telemetry sent anywhere: logging and the cost estimate go to stderr (CAP-028, CAP-392). The only outbound calls are the target origin, an operator-configured managed provider, an operator-configured search provider, an optional LibreTranslate endpoint, an optional TSA, and optional site `/.well-known` genome fetches (off by default).

**What it does instead:** runs where the operator installs it, and sends data off-box only to endpoints the operator configured.

### 3.9 Not a general-purpose HTTP client

**Verified:** the tool surface has no `method`, no request body, no arbitrary per-call headers and no auth flow — `occam_transcode` takes `url` plus shaping/fetch opt-ins (`OccamTranscodeTool.cs:46-67`). Headers arrive only ambiently, from `OCCAM_REQUEST_HEADERS_FILE` or a `session_profile`. The request shape is guarded: private/RFC1918 targets are refused unless `OCCAM_ALLOW_PRIVATE_URLS=1`, workers pin DNS against rebinding, and the response body is capped (`OCCAM_MAX_RESPONSE_BYTES`, 8 MiB default). The response is extracted markdown, never the raw bytes — there is no way to obtain the original response body from any tool.

**What it does instead:** performs one guarded, extraction-shaped retrieval per call and returns a reading of the page, not the transport response.

### 3.10 Not an LLM, and it does not summarize

**Verified:** no model, no weights, no generation anywhere in the host or the workers. Every reduction is deterministic and lossy-by-selection, never by rewriting: `fit_markdown` is a BM25 paragraph **prune** (CAP-063/308), `focus_query` ranks existing sections (`SectionRanker`), `semantic_chunking` is a fixed-size line accumulator despite the name (CAP-320), token counting is the `heuristic-unicode-v1` estimator, and what was dropped is reported in `compile.omitted` (CAP-067/310). `translate_to` calls an external LibreTranslate service and its output never enters the signed bytes (ART-039).

**What it does instead:** selects and truncates existing text and tells the caller exactly what it removed. Every word in the output came from the page.

### 3.11 Three more the model implies

| Not… | Verified | Does instead |
|------|----------|--------------|
| **an archive or wayback** | Nothing stores page bodies for retrieval as history. Watch persists hashes and block hashes, not content (ART-028). The opt-in cache is TTL-scoped replay, not an archive. Batch retains markdown, but only as job results with no history and no query surface (EF-037). | Commits to *hashes* of past extractions so a later fetch can be compared to them. |
| **a RAG store or vector database** | No embeddings, no vector index, no persistence of chunks. Ranking is lexical BM25 within one response; `chunks[]` are returned to the caller and forgotten. | Emits citation-ready blocks with real CSS `source_selector` provenance and Merkle leaves, for the caller's own store. |
| **an agent or orchestrator** | There is no plan, loop or decision engine. Recipe A/B/D are documented *call sequences the agent performs*; the host never chains tools on its own. `WatchService` explicitly has no daemon; digest fan-out is a single call's internal parallelism, not orchestration. | Gives an agent tools with honest signals so the agent can orchestrate. |

---

## 4. What problem does it solve?

### 4.1 The failure it prevents

An agent asked about a web page has two silent failure modes without a tool like this: it invents the content from model memory, or it receives an empty shell (a JS-rendered page, a consent interstitial, a login wall) and treats the emptiness as the page. Both produce confident, wrong output, and neither leaves a trace.

Occam's answer is the **trust rule**: `ok:false` means the page content is **UNKNOWN**, and the correct response is to act on `failure.code` and `agentMeta.decisions`, never to fill in from memory. The taxonomy exists to make the unknown *typed* rather than silent — `captcha_or_challenge`, `requires_login`, `http_404`, `timeout`, `robots_disallowed`, `private_url_blocked` and the rest (`FAILURE-BEHAVIOR-MAP.md`). Two traps the design specifically addresses:

- `thin_extract` means **bad extraction**, not a short page. A genuinely short, complete page is `ok:true` with `quality.verdict=short_quality`, so an agent should not heal or escalate merely because the body is small (CAP-097).
- A **negative receipt** is a signed statement that a wall was hit. A signature on an `ok:false` response proves the failure was claimed — never that content was obtained (`OccamTranscodeModels.cs:427-428`).

This is the one failure the trust layer cannot survive: if the agent ignores `ok:false` and substitutes recalled content, that content never entered Occam, so it carries no hash, no receipt and no Merkle leaf — while the surrounding response may still be decorated with receipts belonging to URLs that *did* succeed. No mechanism in the codebase detects this (TRUST-MODEL §9.5). Honesty is made *possible* by the product and must be enforced by the caller.

### 4.2 The economics it fixes

Feeding web content to a model is a budget problem before it is a quality problem. A raw HTML page routinely costs 10–100× what its readable content costs, and the naive fix — truncation — silently removes the answer.

What Occam does about it, mechanically:

| Mechanism | Effect | Evidence |
|-----------|--------|----------|
| Extraction to markdown | Drops navigation, scripts, styling and boilerplate before the model ever sees them | `http-extract` (readability + turndown), CAP-200 |
| Ambient client budget | Declare the context window once; later reads default to **20 %** of it, clamped [512, 16384] tokens | `ClientCapabilityStore.cs:15-17,81-85`; CAP-304 |
| Two-layer budget split | `max_tokens` is a **whole-response** budget shared across markdown and every sidecar, so structured output cannot silently blow the window | `BudgetOwnership`, CAP-061/300 |
| `fit_markdown` + `focus_query` | BM25 paragraph pruning toward the caller's intent, with different thresholds focused vs unfocused | CAP-063/308, CAP-064 |
| `OmittedManifest` / `compile.omitted` | A machine-readable record of what was cut — the difference between budgeting and truncating | CAP-067/310 |
| `if_none_match` / `diff_against` / `delta_only` | A re-read costs delta-size tokens instead of full-page tokens; unchanged pages cost an empty envelope | CAP-074/082/089 |
| `occam_digest` with `per_url_max_tokens` | Five sources under one budget instead of five independent full reads | CAP-450/454 |
| Response body cap | 8 MiB default before extraction, so a pathological page fails typed (`response_too_large`) instead of consuming the run | `OCCAM_MAX_RESPONSE_BYTES` |

The honest framing for any future document: **report reduction against a declared baseline, per tier, or not at all.** A single global "reduces tokens by N %" headline is forbidden (`AGENTS.md` claims discipline), and token reduction is not evidence of quality — `compile.omitted` exists precisely because the two can diverge.

### 4.3 The one-line answer

> An agent needs to know what a page says *right now*, in a form that fits its context window, and it needs to be told when it does not know. Occam does the first two deterministically and the third by contract — and can commit to what it produced so the bytes can be checked later.

---

## 5. Boundary statements for future documentation

Binding phrasing rules derived from §3 and from TRUST-MODEL §13. Each left-hand phrase is unusable; each right-hand phrase is defensible.

| Do not write | Write |
|--------------|-------|
| "verified provenance" / "cryptographically verified source" | "tamper-evident against the signing host's key" |
| "proves the page said this" | "proves this host asserted this extraction result" |
| "bypasses cookie, login and CAPTCHA walls" | "detects access walls and reports them as typed failures; operator-supplied sessions can pass some of them" |
| "crawls the site" | "lists up to 64 links from the sitemap, robots and homepage, with a bounded second-level expansion" |
| "searches the web" | "queries the search provider the operator configured" |
| "checks whether the claim is true" | "retrieves the passages lexically relevant to the claim and proves they were in the signed extraction" |
| "caches pages for speed" | "re-extracts on every call unless the caller opts into a local TTL replay" |
| "summarizes the page" | "prunes and budgets the page, and reports what was omitted" |
| "signed by Occam" | "signed by this install's locally minted key" |
| "`OCCAM_RECEIPTS=off` disables signing" | "`OCCAM_RECEIPTS=off` stops receipt signing only; the key is still minted and `occam_playbook_save` still signs" |
| "15 tools" as the product | "15 core MCP tools is the default exposure of a product with 51 named entrypoints" |

---

## 6. UNCERTAIN

| Item | Bound | What would resolve it |
|------|-------|----------------------|
| Whether the head phrase should lead with "acquisition" or with "reading" | Editorial, not factual. §2 leads with acquisition because that is where the code and the risk are; a user-facing handbook may reasonably lead with reading. | A handbook-audience decision, out of scope here. |
| Token-reduction magnitude | Deliberately unquantified in this file. The tokenizer is `heuristic-unicode-v1` with **unmeasured error bounds** (`CANONICAL-AUDIT-INDEX.md` §Known incompleteness). | A measured per-tier baseline study. Until then no percentage may be published. |
| Whether "runtime" overstates the host for the stdio single-session case | Low risk: stdio is one long-lived process with DI, pools and daemons, which satisfies the word. | None needed; noted for reviewers. |
| Whether any managed-provider success is distinguishable in a receipt beyond the `backend` string | Source-read only; EF-003's path was not exercised. | A live managed-backend run with receipt inspection. |
