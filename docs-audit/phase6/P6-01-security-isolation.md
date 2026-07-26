# P6-01 — Security / Isolation

**Agent:** P6-01 · **Date:** 2026-07-26 · **Phase:** 6 (plan only — no product-code patches)  
**Inputs:** `PHASE5-REPORT.md`, `PRODUCT-VS-ENGINEERING.md`, `TRUST-MODEL.md`, `ENGINEERING-FINDINGS.md` (EF-002/013/040/041/043/046/054)  
**Method:** re-read CURRENT executable code; dispositions are recommendations for the orchestrator.

**Scope rule:** Do not rewrite the product model. Do not modify public docs. Propose PATCH + TEST plans only.

---

## Envelope (compact)

```
AGENT: P6-01
FILES_WRITTEN:
  - docs-audit/phase6/P6-01-security-isolation.md
KEY_RECOMMENDATIONS:
  - EF-043: give css-extract the same DNS-pin + body-cap path as http-extract before documenting network-safety parity.
  - EF-013: remove or sandbox `(0,eval)(__NUXT__)`; treat Nuxt attr as unsafe until a non-eval parser lands.
  - EF-002/040: recycle or clear BrowserContext cookie/storage between anonymous extracts (or per-host contexts); never claim isolation.
  - EF-054: default import to NOT retain `_imports/` plaintext; secure-delete option; fix “no secrets retained” claims.
  - EF-041: InstallShared must not StopAll on every per-session DI; make pool truly process-singleton or session-local without killing peers.
  - EF-046: keep bypassCSP as an honest limitation; treat playbook JS as trusted code-like input in the contract (document, do not market as safe selectors).
OWNER_DECISIONS:
  - OD-1: Per-navigation fresh BrowserContext vs clearCookies+storage between anonymous runs (latency vs isolation).
  - OD-2: Nuxt attr — REMOVE_SURFACE vs sandboxed parser vs DOCUMENT_LIMITATION with hard disable default.
  - OD-3: Session import — default `--no-keep-import` (breaking for operators who rely on retained raw files) vs keep default + force docs/warning.
  - OD-4: EF-041 — process-static pool without StopAll vs per-session private pools (ports/RAM).
PATCH_READY:
  - EF-043: YES (smallest: pin+cap in css-extract.mjs; mirror http-extract helpers)
  - EF-013: YES (disable nuxt attr / replace eval; schema + worker)
  - EF-002/040: YES (pool recycle/clear policy; optional per-host context)
  - EF-054: YES (default keepImport=false or require explicit --keep-import)
  - EF-041: YES (InstallShared idempotent / no StopAll on same-process re-register)
  - EF-046: NO code patch required for Docs Truth Gate if contract is honest; OPTIONAL harden later
UNCERTAIN:
  - EF-041 not runtime-reproduced (static PROVEN only; Phase 5 bounded uncertainty #1).
  - Cross-host cookie bleed magnitude under Playwright third-party / related-domain edges not measured live.
  - Whether any shipped knowledge_schema / seed playbook currently depends on attr=nuxt (grep seeds before REMOVE).
```

---

## Cross-cutting impact matrix

| Finding | Caller→caller? | Untrusted remote → local/net? | Secrets cross sessions? | Stored auth leak? | Browser more privileged than expected? | Required for product? |
|---------|----------------|-------------------------------|-------------------------|-------------------|----------------------------------------|------------------------|
| EF-043 | Indirect (DoS / SSRF via shared host) | **Yes** (private IP fetch, unbounded body) | No | No | N/A (HTTP worker) | No — parity gap |
| EF-013 | No (per-call) | **Yes** (eval of page JS in Node) | No | No | N/A (Node eval) | Convenience only |
| EF-002/040 | **Yes** (shared BrowserContext) | Indirect (leaked cookies alter later fetches) | **Yes** (cookie jar / storage) | In-memory until recycle | Yes (warm pool) | Warm reuse is; bleed is not |
| EF-054 | No | No | Disk secrets shared if FS shared | **Yes** (`_imports/` + profile Cookie) | N/A | Raw retain is optional |
| EF-041 | **Yes** (pool StopAll) | No | No | No | Availability blast | Singleton pool is; StopAll on DI is not |
| EF-046 | Indirect (playbook JS mutates DOM hashed) | Page JS runs with CSP bypass; playbook JS is trusted input | If playbook steals via evaluate | No (unless playbook writes) | **Yes** | bypassCSP largely yes; evaluate is authoring power |

---

## EF-043 — css-extract SSRF / body-cap parity

### FINDING ID
EF-043

### CURRENT BEHAVIOR (path:line)
- `workers/css-extract/css-extract.mjs:39-46` — `egressFetch(url, …)` with timeout only; **no** `resolveAndValidateHost` / `createPinnedDispatcher`.
- `workers/css-extract/css-extract.mjs:78` — `html = await response.text()` — **unbounded** body read.
- Contrast http-extract: `workers/http-extract/lib/http-extract-run.mjs:147-175,199` — DNS pin + private-IP reject + `readResponseBodyForExtract(…, maxResponseBytes)`.
- Contrast browser-extract: `workers/browser-extract/lib/browser-session.mjs:173-180,469-487` — navigation-time `resolveAndValidateHost` + route abort for private targets; HTML size gate `BROWSER_HTML_MAX_CHARS` (`:41,44-46`).
- Browser fallback inside css-extract (`css-extract.mjs:111-122`) uses `createBrowserSession()` (inherits browser SSRF route) but still no HTTP-path parity; `page.content()` not capped by `OCCAM_MAX_RESPONSE_BYTES`.

### WHY IT IS A PROBLEM
Network-safety is marketed/assumed as a host-wide property. css-extract is on the `occam_extract_knowledge` path and will fetch caller-supplied URLs without the SSRF/DNS-rebind pin and without the shared body cap (`OCCAM_MAX_RESPONSE_BYTES`, default 8 MiB in `response-body-cap.mjs:9`).

### USER / SECURITY / TRUST IMPACT
- **Security:** SSRF to link-local / RFC1918 / metadata endpoints from the worker process; DNS rebinding risk on unpinned fetch.
- **Availability:** unbounded `response.text()` → memory DoS of the Node worker / host.
- **Trust:** TRUST-MODEL §10.1 claims SSRF strength on some paths and explicitly contrasts EF-043; publishing “all workers enforce private-IP policy” would be a forbidden-class false guarantee.

### IMPACT QUESTIONS
| Question | Answer |
|----------|--------|
| CAN one caller affect another? | Yes, via shared-process memory/CPU DoS or SSRF side effects on the operator network. |
| CAN untrusted remote content trigger local/network access? | **Yes** — redirect/target host can be private; no pin. |
| CAN secrets cross session boundaries? | Not directly. |
| CAN stored auth material leak? | Not via this bug alone. |
| IS browser execution more privileged than users expect? | N/A for HTTP path; fallback browser is separate. |
| IS the behavior required for product functionality? | **No.** Parity with http-extract is the intended safety baseline. |

### RECOMMENDED CONTRACT (after fix)
> Every Occam acquisition worker that performs a direct HTTP GET of a user URL (http-extract, css-extract, and browser navigation guards) rejects private/link-local targets (unless `OCCAM_ALLOW_PRIVATE_URLS`) and bounds response body reads by `OCCAM_MAX_RESPONSE_BYTES`. Failure codes surface as `private_url_blocked` / `dns_resolution_failed` / `response_too_large` consistently.

### COMPATIBILITY IMPACT
- Private-URL extracts that currently succeed via css-extract will start failing (correct).
- Very large HTML that currently loads into css-extract will hit `response_too_large` (aligns with http-extract).

### PATCH PLAN (smallest)
1. In `workers/css-extract/css-extract.mjs`, before `egressFetch`:
   - import `resolveAndValidateHost`, `createPinnedDispatcher`, `shouldSkipPrivateIpCheck`, `SsrfBlockedError` from `shared/lib/private-ip.mjs`;
   - import `resolveMaxResponseBytes`, `readResponseBodyForExtract` (or equivalent) from `shared/lib/response-body-cap.mjs`;
   - pin + validate host; pass `dispatcher`; replace `response.text()` with capped reader;
   - map `SsrfBlockedError` / `ResponseTooLargeError` into typed `failure` JSON (match http-extract codes).
2. Optionally validate `response.url` after redirects (http-extract `validateFinalUrl`).
3. Do **not** invent new policy knobs; reuse existing env contracts.
4. No public docs in this phase; internal acceptance only.

### TEST PLAN
1. **Unit/selftest:** extend or add `workers/css-extract` selftest calling extract against `http://127.0.0.1/` / metadata-like host → expect `private_url_blocked` (with allow-private off).
2. **Body cap:** local fixture server returning `OCCAM_MAX_RESPONSE_BYTES+1` bytes → `response_too_large` (or truncated mode if http-extract mode is reused — match http semantics exactly).
3. **Parity:** same URL blocked by http-extract must be blocked by css-extract.
4. **Regression:** public URL extract_knowledge happy path still `ok:true`.
5. Gate hook: small L4/extract corpus case or worker selftest invoked from doctor/gate if already patterned.

### DISPOSITION
**FIX_BEFORE_PUBLIC_DOCS** (ranked NEEDS_FIX_BEFORE_DOC #2 with EF-013).

### Docs Truth Gate — GREEN when
- [ ] css-extract HTTP path uses pinned dispatcher + private-IP validation.
- [ ] css-extract body read honors `OCCAM_MAX_RESPONSE_BYTES`.
- [ ] Automated test proves private URL refusal and oversize refusal.
- [ ] Docs may state worker SSRF/body-cap **parity for http + css (+ browser nav guard)** without a css exception footnote.

---

## EF-013 — Nuxt `__NUXT__` eval in css-extract

### FINDING ID
EF-013

### CURRENT BEHAVIOR (path:line)
- `workers/css-extract/lib/css-schema-extract.mjs:86-87` — `attr === "nuxt"` → `readNuxtPath`.
- `workers/css-extract/lib/css-schema-extract.mjs:131-142` — regex capture of `window.__NUXT__ = …` then **`nuxt = (0, eval)(match[1])`** on page-controlled text.
- Reachable from row and field extract (`:59-60`, `:86-87`).
- Invoked whenever a knowledge schema / wire field uses `attr: "nuxt"`.

### WHY IT IS A PROBLEM
Page HTML is untrusted remote content. `eval` in the Node worker process grants that content the full privileges of the worker (FS, env, outbound network via Node APIs if the payload is crafted as JS, not merely a JSON-like object).

### USER / SECURITY / TRUST IMPACT
- **Security:** remote code execution in the extract worker = host compromise class for any caller who can point extract_knowledge at a malicious URL (or supply hostile HTML via `--html-file` in tests).
- **Trust:** TRUST-MODEL §10.2 lists playbook/browser/Nuxt eval as out-of-scope threats; documenting Nuxt extraction as “safe structured extract” would violate forbidden honesty rules.
- Not a crypto issue; it is an acquisition integrity / host integrity issue.

### IMPACT QUESTIONS
| Question | Answer |
|----------|--------|
| CAN one caller affect another? | Only via compromising the shared host process. |
| CAN untrusted remote content trigger local/network access? | **Yes** — eval in Node. |
| CAN secrets cross session boundaries? | If RCE reads `~/.occam` / env — yes after compromise. |
| CAN stored auth material leak? | Post-RCE, yes. |
| IS browser execution more privileged than users expect? | This is **Node** eval, often *more* privileged than page JS in Chromium. |
| IS the behavior required for product functionality? | **No** as currently implemented. Nuxt state can be parsed without `eval` or the attr can be removed/disabled. |

### RECOMMENDED CONTRACT (after fix)
> Schema field `attr: "nuxt"` either (a) does not exist, or (b) parses embedded state with a non-executing parser (JSON/AST) and never calls `eval`/`Function`/`vm` with page text. Hostile `__NUXT__` payloads cannot execute in the worker.

### COMPATIBILITY IMPACT
- Schemas using `attr: "nuxt"` break until migrated to CSS/regex/const or a safe parser.
- **OWNER_DECISION OD-2** required if any seed/community schema depends on it.

### PATCH PLAN (smallest)
1. Grep `profiles/playbooks`, corpora, and tests for `"nuxt"` / `attr: "nuxt"`.
2. **Smallest safe fix:** in `readNuxtPath`, refuse eval — return `null` and/or surface `failure: "unsafe_extractor"` / ignore field; or delete the `nuxt` branch and reject unknown attr at wire validation.
3. Optional follow-up (not required for GREEN): implement JSON-only extract after stripping to JSON subset, or `acorn` parse + static object walk with no code execution.
4. Do not broaden to a general JS expression language.

### TEST PLAN
1. HTML fixture: `__NUXT__={}; require('fs').writeFileSync('/tmp/pwn','x')` (or Windows equivalent) — assert worker does **not** create the file and does not throw into arbitrary code; field null or typed failure.
2. Benign fixture with JSON-like `__NUXT__={ data: { a: 1 } }` — document expected post-fix behavior (null vs parsed).
3. Schema with `attr: "nuxt"` regression in extract_knowledge unit/gate.

### DISPOSITION
**FIX_BEFORE_PUBLIC_DOCS** (with EF-043). Prefer **REMOVE_SURFACE** temporarily if no in-tree consumers; else FIX_NOW with disabled-by-default.

### Docs Truth Gate — GREEN when
- [ ] No `(0, eval)` / `Function(` / `vm.run` on page-controlled strings in css-extract.
- [ ] Exploit-style fixture test is green.
- [ ] Public docs either omit Nuxt attr or describe only the safe parser.

---

## EF-002 / EF-040 — anonymous BrowserContext bleed across hosts

### FINDING ID
EF-002 (parent) · EF-040 (refinement)

### CURRENT BEHAVIOR (path:line)
- `workers/browser-extract/lib/browser-pool.mjs:13-58` — one warm `#session` / BrowserContext per pool slot; recycle only when `headersFile` or `storageStateFile` **identity changes**, or on failure / 10 runs / 400 MB heap (`:9-10,187-198`).
- Anonymous→anonymous: both calls pass `headersFile: null` / no storageState → **no recycle** (`:31-37`).
- `workers/browser-extract/lib/browser-session.mjs:455-460` — each extract `addCookies` via recipe (`injectRecipeCookies`, gated by `WT_COOKIE_INJECT`) and `applySessionCookies`; **no `clearCookies` / storage reset** after the page closes.
- Page uses `context.newPage()` (`:438`) then closes the page; **cookie jar and origin storage remain on the shared context**.
- EF-040 refinement is accurate: session_profile / storageState transitions change headers/storage file paths → `ensureSession` recycles first; the residual vector is consecutive **anonymous** (and same-headersFile) extracts.

### WHY IT IS A PROBLEM
The warm pool is an optimization, not a security boundary. Site A’s `Set-Cookie`, consent cookies, CF clearance, or prior session cookies (if a previous call shared the same headers file identity) remain and can alter site A (same-host) or related-domain subsequent extracts. Extracted markdown is then signed as a normal success (TRUST-MODEL §10.2 session bleed).

### USER / SECURITY / TRUST IMPACT
- **Privacy / isolation:** caller-visible “anonymous” fetch may not be anonymous relative to prior pool use.
- **Multi-tenant WS/Remote:** different logical callers sharing one process pool can influence each other’s browser state (amplified by EF-041 thrash, but bleed exists even on stdio single-client).
- **Trust:** must not claim host/session isolation for browser acquisition.

### IMPACT QUESTIONS
| Question | Answer |
|----------|--------|
| CAN one caller affect another? | **Yes** — shared context in-process. |
| CAN untrusted remote content trigger local/network access? | Indirect (cookies change later auth/network behavior); not classic SSRF. |
| CAN secrets cross session boundaries? | **Yes** — cookies/tokens in the jar; also in-memory until recycle. |
| CAN stored auth material leak? | In-memory bleed; not the `_imports/` disk issue. |
| IS browser execution more privileged than users expect? | Warm reuse is expected for perf; **isolation is over-expected**. |
| IS the behavior required for product functionality? | Warm Chromium reuse: **yes**. Cross-extract state retention: **no**. |

### RECOMMENDED CONTRACT (after fix)
> A browser extract that does not explicitly attach `session_profile` / `storageState` starts from an empty cookie jar and empty origin storage relative to prior extracts, OR the product explicitly documents that the warm pool is **not** an isolation boundary and disables isolation claims. Preferred product promise: **anonymous means fresh ephemeral context (or cleared storage) per extract** (or per registrable domain).

### COMPATIBILITY IMPACT
- More frequent context creation → higher latency / less warm reuse (conflicts with EF-039 headers cold-path already).
- **OD-1:** choose clear-in-place vs newContext per anonymous call vs per-host map of contexts.

### PATCH PLAN (smallest)
1. **Minimal:** after each successful/failed `#doExtractOnce`, if no `storageStateFile` and no session cookie headers were requested, call `context.clearCookies()` and clear permissions/storage via Playwright APIs (or `recycle()` always for anonymous).
2. **Stronger (preferred for multi-tenant):** `browser.newContext()` per anonymous extract; keep browser process warm.
3. Always recycle when transitioning between distinct registrable domains if keeping one context.
4. Do not claim fix via run-count alone (10 runs is not a security control).

### TEST PLAN
1. Extract URL A that sets a distinctive cookie; extract URL A again without session → assert cookie **not** sent (CDP/network log or server echo fixture).
2. Session_profile extract then anonymous extract → assert recycle already happens (headersFile change); keep as regression.
3. Two different hosts anonymous → assert no cross-domain Cookie header leakage (fixture).
4. Optional multi-client: two WS sessions interleaved browser extracts (after EF-041 fix).

### DISPOSITION
**FIX_BEFORE_PUBLIC_DOCS** for any isolation claim (NEEDS_FIX_BEFORE_DOC #3).  
Until fixed: **DOCUMENT_LIMITATION** only if docs stay frozen and claims are withheld (Phase 6 plan: fix preferred).

### Docs Truth Gate — GREEN when
- [ ] Automated test proves anonymous extract N+1 does not carry cookies from extract N.
- [ ] No public sentence claims browser host/session isolation without qualifying the pool.
- [ ] `session-fetch` / `browser-acquisition` cards may describe isolation only after the test exists.

---

## EF-054 — session import retains plaintext cookies under `_imports/`

### FINDING ID
EF-054

### CURRENT BEHAVIOR (path:line)
- `scripts/occam-session.mjs:123-128` — `keepImport` defaults **true** unless `--no-keep-import`; copies source `cookies.txt` into `sessionsRoot/_imports/`.
- `scripts/occam-session.mjs:167` — profile `meta.source` points at `_imports/<basename>`.
- `scripts/occam-session.mjs:24` — `list` help text: “List profile ids and keys **(no secret values)**” — true for **stdout of list** (`cmdList` emits header **keys** only, `:83-85`), but misleading next to import retention: secrets remain on disk in (1) `_imports/*.txt` and (2) profile JSON `headers.Cookie`.
- `scripts/lib/occam-sessions-lib.mjs:105` — `init` always creates `_imports/`.
- Template README (`scripts/templates/occam-sessions-README.md`) documents dropping raw exports into `_imports/`.

### WHY IT IS A PROBLEM
Operators reasonably infer that import converts and discards raw secret material. Default behavior **duplicates** plaintext cookies into a durable tree under `~/.occam/sessions` (or `OCCAM_SESSIONS_ROOT`). Backup/sync/AV indexing then spreads auth material. `list`’s “no secret values” describes CLI output only, not storage hygiene.

### USER / SECURITY / TRUST IMPACT
- **Privacy:** plaintext session tokens at rest beyond the profile Cookie header (second copy).
- **Incident response:** harder to assert “import leaves no raw export.”
- **Trust/docs:** NEEDS_FIX_BEFORE_DOC #7 — withhold “import retains no secrets.”

### IMPACT QUESTIONS
| Question | Answer |
|----------|--------|
| CAN one caller affect another? | No (same operator FS). |
| CAN untrusted remote content trigger local/network access? | No. |
| CAN secrets cross session boundaries? | Only if profiles are shared across users/hosts via FS. |
| CAN stored auth material leak? | **Yes** — default `_imports/` retain + profile Cookie. |
| IS browser execution more privileged than users expect? | N/A. |
| IS the behavior required for product functionality? | **No.** Retention is convenience; `--no-keep-import` already exists. |

### RECOMMENDED CONTRACT (after fix)
> `occam-session import` writes a session profile. Raw import files are **not** retained unless the operator passes an explicit `--keep-import`. `list` never prints secret values. Documentation never claims secrets are absent from disk while Cookie headers or `_imports/` exist.

### COMPATIBILITY IMPACT
- **OD-3:** flipping default to not keep imports breaks operators who use `_imports/` as archive. Mitigate with changelog + explicit flag.
- Profile Cookie header retention is separate; fixing EF-054 does not encrypt profile secrets (out of scope unless OWNER expands).

### PATCH PLAN (smallest)
1. Invert default: `keepImport = args["keep-import"] === true` (require explicit keep); keep `--no-keep-import` as alias for clarity or deprecate.
2. On import without keep: do not copy; `meta.source` = basename only / `discarded_after_import`.
3. Optional: `import --shred-source` best-effort overwrite+delete of operator’s `--from` path (dangerous — make explicit opt-in only).
4. Help text: clarify list = “no secrets in list output; profiles on disk still contain Cookie headers.”

### TEST PLAN
1. Temp sessions root: import with default → assert `_imports/` has **no** new copy (after default flip).
2. Import with `--keep-import` → file exists.
3. `list` stdout does not contain cookie values (existing behavior).
4. Snapshot test of help strings.

### DISPOSITION
**FIX_BEFORE_PUBLIC_DOCS**.

### Docs Truth Gate — GREEN when
- [ ] Default import does not write `_imports/` copies.
- [ ] Test locks the default.
- [ ] Any mention of import states: raw retain is opt-in; profile JSON still holds Cookie material requiring FS protection.

---

## EF-041 — `BrowserPoolManager.InstallShared` StopAll on new WS/Remote DI

### FINDING ID
EF-041

### CURRENT BEHAVIOR (path:line)
- `src/FFOccamMcp.Core/Workers/BrowserPoolManager.cs:45-48` — `InstallShared` calls `_shared?.StopAll()` then replaces `_shared`.
- `src/FFOccamMcp.Core/Composition/OccamServiceCollectionExtensions.cs:39-46` — DI factory for `IBrowserPoolManager` always constructs a new manager and `InstallShared(manager)`.
- `src/FFOccamMcp.Core/Transport/WebSocketMcpTransport.cs:80-88` — **each** WebSocket session: `Host.CreateApplicationBuilder()` + `AddOccamMcpServer()` → new DI root → new `InstallShared`.
- `src/FFOccamMcp.Core/Transport/RemoteMcpTransport.cs:218-226` — same per-session DI pattern.
- Effect: session B connect/start kills process-wide browser daemon slots owned by the previous shared manager; in-flight extracts on session A fail/timeout; warm pool cold-starts again.

### WHY IT IS A PROBLEM
Per-session DI was chosen for isolation of *some* singletons (see withdrawn EF-024 atlas). The browser pool was incorrectly made process-global **and** reinstalled with StopAll, combining the worst of both: cross-session **availability coupling** without true isolation of browser state (EF-002 still shares whatever pool survives).

### USER / SECURITY / TRUST IMPACT
- **Availability / DoS (logical):** one new WS/Remote session disrupts another’s browser extracts.
- **Security:** availability class; also forces more cold starts (cost) and interacts badly with bleed (new contexts may still be shared after reinstall).
- **Trust:** must not document “concurrent remote sessions share a stable warm pool.”

### IMPACT QUESTIONS
| Question | Answer |
|----------|--------|
| CAN one caller affect another? | **Yes** — StopAll across sessions. |
| CAN untrusted remote content trigger local/network access? | No. |
| CAN secrets cross session boundaries? | Not via StopAll itself. |
| CAN stored auth material leak? | No. |
| IS browser execution more privileged than users expect? | N/A. |
| IS the behavior required for product functionality? | **No.** Accidental interaction of static `_shared` + per-session DI. |

### RECOMMENDED CONTRACT (after fix)
> Concurrent WebSocket/Remote MCP sessions on one host process either (a) share one process-lifetime browser pool **without** tearing it down on session start, or (b) use fully private pools per session without touching peers. Session connect/disconnect must not kill unrelated sessions’ browser work.

### COMPATIBILITY IMPACT
- Fixing StopAll improves availability (non-breaking for clients).
- **OD-4:** true per-session pools need unique ports (`OCCAM_BROWSER_POOL_BASE_PORT` + offset) and more RAM.

### PATCH PLAN (smallest)
1. **Smallest correct:** `InstallShared` becomes:
   - if `_shared` already non-null, **return existing** (ignore new manager) **or**
   - replace reference **without** `StopAll` and dispose the unused new manager;
   - only `StopAll` on process exit / explicit shutdown.
2. Ensure DI factory returns the same instance when sharing (`services.AddSingleton` already per-container — problem is cross-container static). Prefer: remove static `_shared` mutation from DI factory; have runners resolve `IBrowserPoolManager` from the current SP only; keep static solely for legacy `BrowserDaemonHost.Stop` if needed, set once.
3. Do not broaden to redesign Remote auth.

### TEST PLAN
1. **Gate/unit under OCCAM_GATE:** call `InstallShared` twice; assert first manager’s slots not stopped (expose test hook / counters).
2. **Manual/integration (repro Phase 5 uncertainty):** two WS clients; client A starts long browser extract; client B connects; A must complete without `workers_unavailable`/timeout caused by pool kill.
3. Stdio single-session regression: pool still starts and idle-TTL stops as today.

### DISPOSITION
**FIX_NOW** (availability bug; also DOCS_MUST_WARN until fixed). Not blocking css/Nuxt docs, but blocking honest “stable shared pool under WS” claims.

### Docs Truth Gate — GREEN when
- [ ] Second `InstallShared` / second session DI does not `StopAll` a live peer pool (automated assertion).
- [ ] Docs may describe concurrent WS browser use only after the multi-session test exists; until then withhold “stable warm pool across sessions.”

---

## EF-046 — `bypassCSP:true` + playbook `page.evaluate`

### FINDING ID
EF-046

### CURRENT BEHAVIOR (path:line)
- `workers/browser-extract/lib/browser-session.mjs:139-144` — every context created with **`bypassCSP: true`** (unconditional).
- Also `workers/browser-extract/dom-skeleton-capture.mjs:39` — same.
- `workers/browser-extract/lib/interaction-steps.mjs:13-14` — playbook `js_before_wait` → `page.evaluate(plan.js_before_wait)`.
- `:21-24` — `wait_for.js` → `page.waitForFunction(plan.wait_for.js, …)`.
- Built-in automation also evaluates page JS for consent/virtual-scroll/challenge probes (`browser-session.mjs`, `virtual-scroll.mjs`, etc.) — product automation, not only playbooks.

### WHY IT IS A PROBLEM
Users may think playbooks are “CSS selectors + declarative waits.” In reality they are **trusted browser-automation programs** running with CSP disabled, so page scripts and playbook scripts can reshape the DOM that later gets hashed and signed.

### USER / SECURITY / TRUST IMPACT
- **Security:** malicious or buggy playbook = arbitrary JS in the browser context (cookie theft to attacker URL, DOM rewrite). Community playbooks raise supply-chain risk (see EF-047/052 — out of scope here but compounding).
- **Trust:** signed content may reflect Occam-mutated DOM (TRUST-MODEL A7 / forbidden “proves the page said this”).
- **Expectation gap:** CSP bypass is silent and undisableable.

### IMPACT QUESTIONS
| Question | Answer |
|----------|--------|
| CAN one caller affect another? | Via EF-002 shared context if playbook sets storage; otherwise per-page. |
| CAN untrusted remote content trigger local/network access? | Page JS runs with CSP bypass (browser sandbox still applies); playbook JS is **operator-trusted**, not page-trusted. |
| CAN secrets cross session boundaries? | If playbook/page writes cookies into shared context — yes (EF-002). |
| CAN stored auth material leak? | Playbook could exfiltrate via `page.evaluate` + network if attacker controls playbook text. |
| IS browser execution more privileged than users expect? | **Yes.** |
| IS the behavior required for product functionality? | **bypassCSP:** largely **yes** for many SPAs/consent walls. **Playbook evaluate:** required for advanced heal/authoring, not for default transcode. |

### RECOMMENDED CONTRACT (after fix/docs honesty)
> Browser acquisition always runs with CSP bypass. Playbooks that include `js_before_wait` / `wait_for.js` / interaction evaluate are **trusted code**. Occam does not sandbox playbook JS. Default reading (`occam_transcode` without custom playbook plan) still uses first-party automation scripts (consent/scroll) under the same CSP bypass. Receipts hash post-automation markdown.

### COMPATIBILITY IMPACT
- Turning off bypassCSP would break many extracts — **do not** as a silent security fix.
- Restricting evaluate would break advanced playbooks — gate behind explicit schema field already present; sanitizer (EF-047) is separate.

### PATCH PLAN (smallest for Truth Gate)
- **Code optional:** none required if contract is honest.
- Optional harden (not PATCH_READY mandatory): refuse `js_before_wait` / `wait_for.js` unless `OCCAM_ALLOW_PLAYBOOK_JS=1`; or strip on community resolve (ties EF-047).
- Do **not** “fix” by documenting bypassCSP as a user feature.

### TEST PLAN
1. Contract/gate test: context options assert `bypassCSP === true` (documents reality).
2. Playbook with `js_before_wait: "window.__occam_pwn = 1"` → assert evaluate ran (feature test) AND lint/docs classify as trusted.
3. No test should claim CSP is enforced.

### DISPOSITION
**DOCUMENT_LIMITATION** (primary).  
Optional later: **FIX_BEFORE_PUBLIC_DOCS** only for any claim that playbooks are declarative/CSP-bound — those claims must be deleted, not coded around. Not NEEDS_FIX_BEFORE_DOC on its own if docs never claim selector-only safety.

### Docs Truth Gate — GREEN when
- [ ] No public claim that playbooks are CSP-constrained declarative selectors.
- [ ] Explicit sentence: CSP bypass always on; playbook JS is trusted input.
- [ ] Receipts/trust pages note DOM may be mutated before hash (align TRUST-MODEL).

---

## Acceptance criteria roll-up (Docs Truth Gate)

| ID | Disposition | PATCH_READY | GREEN criteria (summary) |
|----|-------------|-------------|---------------------------|
| EF-043 | FIX_BEFORE_PUBLIC_DOCS | YES | Pin + body cap + tests; parity claim allowed |
| EF-013 | FIX_BEFORE_PUBLIC_DOCS / REMOVE_SURFACE | YES | No eval on page text + exploit fixture |
| EF-002/040 | FIX_BEFORE_PUBLIC_DOCS | YES | Anonymous cookie non-retention test; no isolation claim otherwise |
| EF-054 | FIX_BEFORE_PUBLIC_DOCS | YES | Default no `_imports/` retain + test |
| EF-041 | FIX_NOW | YES | Second DI/session does not StopAll peer pool |
| EF-046 | DOCUMENT_LIMITATION | NO (optional harden) | Honest privileged-browser contract in docs when unfrozen |

**Suggested fix order for Phase 6 engineering:** EF-013 → EF-043 → EF-041 → EF-002/040 → EF-054 → (docs) EF-046.

---

## Explicit non-actions (this agent)

- No product-code patches applied.
- No public `docs/` / `MCP_API_SPEC.md` / `README.md` edits.
- No product-model rewrite; dispositions feed orchestrator triage only.
