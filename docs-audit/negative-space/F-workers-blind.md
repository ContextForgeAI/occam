# W4-F — Node.js workers (blind negative-space)

**Owner:** W4-F  
**SoT:** shipped Node under `workers/{http-extract,browser-extract,css-extract,shared}/**` (read before any prior `docs-audit/*` compare)  
**Date:** 2026-07-26  
**Constraint:** discovery only — no product edits

---

## 1. Blind inventory

Externally meaningful behaviors from code in scope. Numbered for gap cross-ref.

### 1.1 Entry surfaces / daemons

| # | Behavior | Evidence |
|---|----------|----------|
| B01 | HTTP one-shot: argv URL + optional `--html-file` / `--final-url` / `--headers-file`; stdout one JSON line | `http-extract/extract.mjs:9-28` |
| B02 | Silent-exit-13 guard on HTTP one-shot: unsettled TLA → emit `{ok:false,failure:"timeout"}` on `process.on("exit")` | `shared/lib/worker-exit-guard.mjs:27-48`; `extract.mjs:7,28` |
| B03 | HTTP daemon: bind `127.0.0.1`, port `OCCAM_HTTP_DAEMON_PORT`\|39218; `/health`, `/extract`, `/recycle`; serial extract queue | `http-daemon.mjs:6,48-55,57-110` |
| B04 | HTTP daemon `RECYCLE_AFTER_RUNS=10` counter increments then **resets without recycling process/state** — `/recycle` only clears counter | `http-daemon.mjs:8-46,64-66` |
| B05 | Browser one-shot: flags `--consent-aggressive`, `--lean-assets`, `--cookie-inject`, `--extract-variant`, `--browser-plan-file`, `--headers-file`, `--storage-state-file` | `browser-extract.mjs:10-30,58-68` |
| B06 | Browser daemon: bind `127.0.0.1`, port `OCCAM_BROWSER_DAEMON_PORT`\|39217; `/health` (+`slot_id`), `/extract`, `/recycle`, `/skeleton` | `browser-daemon.mjs:7,36-134,149` |
| B07 | Browser daemon `EADDRINUSE` → stderr + **exit 0** (stand down) | `browser-daemon.mjs:140-144` |
| B08 | Daemon `/extract` accepts inline `playbook_overlay_json` + `playbook_overlay_strict` (ALS via pool) | `browser-daemon.mjs:68-91`; `browser-pool.mjs:80-90` |
| B09 | CSS extract CLI: `<url> <fields.json>` + `--html-file` / `--final-url` / `--headers-file` / `--browser-fallback` | `css-extract/css-extract.mjs:6-17` |
| B10 | CSS extract: no daemon; fresh process per host spawn (host-side) | worker entry only; no `css-daemon.mjs` in tree |

### 1.2 HTTP extract pipeline

| # | Behavior | Evidence |
|---|----------|----------|
| B11 | Dual-stack DNS resolve + private-IP reject + **pinned** undici dispatcher (always pin; `OCCAM_ALLOW_PRIVATE_URLS=1` only relaxes reject) | `private-ip.mjs:128-224`; `http-extract-run.mjs:147-175` |
| B12 | Meta-refresh follow ≤3 hops with re-pin SSRF + cross-origin Cookie/Authorization strip | `http-extract-run.mjs:232-278`; `meta-refresh.mjs`; `request-headers.mjs:52-68` |
| B13 | Body cap default **8 MiB** (`OCCAM_MAX_RESPONSE_BYTES` clamp 64KiB–16MiB); modes `fail`\|`partial` (`OCCAM_HTTP_OVERSIZE_MODE`) | `response-body-cap.mjs:1-46,93-106` |
| B14 | Partial oversize → `failure:"response_truncated"` with markdown payload still present | `http-extract-run.mjs:400-417` |
| B15 | PDF short-circuit (`unpdf`): content-type or `.pdf` URL; cap `OCCAM_MAX_PDF_BYTES` default 16MiB; empty text → `pdf_no_text_layer` | `pdf-extract.mjs`; `http-extract-run.mjs:195-197,425-433,512` |
| B16 | Plain-text pass-through backend `plain_text` | `http-extract-run.mjs:211-228` |
| B17 | Opt-in feed codec when `OCCAM_FEATURES`/`features` includes `json_feed` | `http-extract-run.mjs:281-301` |
| B18 | `json_blocks` / `json_tables` feature flags → DOM collectors | `http-extract-run.mjs:304-306` |
| B19 | Automatic `genericMarkdownPrune` + playbook seed overlay (strict/soft argv or ALS) | `http-extract-run.mjs:642`; `playbook-seed.mjs:19-47` |
| B20 | Egress: `OCCAM_HTTP_PROXY` / `HTTPS_PROXY` / `NO_PROXY` → Undici `ProxyAgent`; invalid proxy → `EgressProxyError` | `egress-proxy.mjs:19-217` |
| B21 | Default UA/Accept from `profiles/occam-fetch-defaults.json` under `OCCAM_HOME`, else hardcoded Chrome/120 UA | `default-fetch-headers.mjs:5-47` |
| B22 | Access evidence booleans only (no form values / cookies in payload) | `access-evidence.mjs:10-53` |
| B23 | Plugins: only hardcoded `semantic_chunking` → dynamic import `shared/plugins/chunking.mjs` (not arbitrary path) | `plugins-runner.mjs:14-45`; `chunking.mjs:68-75` |
| B24 | Early HTTP error cancels body; `pinnedDispatcher.destroy()` (not `close`) to avoid silent exit 13 | `http-extract-run.mjs:177-181,373-386` |

### 1.3 Browser extract / session

| # | Behavior | Evidence |
|---|----------|----------|
| B25 | Always: `headless:true`, `STEALTH_ARGS` (`AutomationControlled`), `STEALTH_INIT_SCRIPT` masks `navigator.webdriver`, **`bypassCSP: true`** | `browser-launch-options.mjs:27-41,46-64`; `browser-session.mjs:139-152` |
| B26 | Always abort `image`/`font`/`media`; lean-A or recipe host also abort stylesheets (+ tracker scripts when stylesheet-block on) | `browser-session.mjs:38-39,154-172`; `browser-extract-run.mjs:24-25` |
| B27 | Pre-goto DNS SSRF + per-navigation `resolveAndValidateHost` in route; final URL literal `isUrlAllowed` (hostname-only) | `browser-session.mjs:173-184,469-488,25-35,644-651` |
| B28 | Auto Chromium provision on launch miss when `OCCAM_BROWSER_AUTOINSTALL≠0` and not system browser | `browser-session.mjs:119-136`; `browser-provision.mjs:14-16` |
| B29 | Channel/path: `OCCAM_BROWSER_CHANNEL`, `OCCAM_BROWSER_EXECUTABLE_PATH`, `OCCAM_CHROME_PATH` | `browser-launch-options.mjs:52-64` |
| B30 | Playwright proxy from same OCCAM_* proxy env (invalid → **null**, no throw — unlike HTTP sync path) | `egress-proxy.mjs:132-161` vs `106-128` |
| B31 | Session cookies from headers file via `addCookies` (domain-scoped); Authorization **blocked** from `extraHTTPHeaders` | `session-headers.mjs`; `request-headers.mjs:71-99` |
| B32 | Recipe cookie inject opt-in `WT_COOKIE_INJECT=1|true|yes` — **no shipped recipe defines `cookies[]` today** | `cookie-inject.mjs:3-18`; recipes `*.mjs` (no `cookies` keys) |
| B33 | Consent: auto-dismiss (site-agnostic + recipe extras); if first pass fails → **forced second pass `aggressive:true`**; CSS-hide overlays | `browser-session.mjs:572-586,594-596`; `consent.mjs:49-107` |
| B34 | Challenge wall fail-fast → `captcha_or_challenge` (phrase + near-empty body) | `browser-session.mjs:76-105,544-569` |
| B35 | Default extract variant **`css-hide`**; variants baseline/reextract/strip-consent/strip-chrome | `browser-session.mjs:63-70`; `browser-extract-run.mjs:28-30` |
| B36 | Virtual scroll default **on** (`mode:auto`); disable `WT_VIRTUAL_SCROLL=0`; replace mode **mutates DOM** (`#wt-virtual-scroll-merge`) | `virtual-scroll.mjs:29-64,291-315`; `browser-session.mjs:392-407,592` |
| B37 | Playbook browser plan: `js_before_wait` → `page.evaluate(string)`; `wait_for.js` → `page.waitForFunction(string)`; steps scroll/click/wait/type | `interaction-steps.mjs:6-63` |
| B38 | Shadow DOM open-root flatten (max 128 hosts) before `page.content()` | `shadow-dom-flatten.mjs:47-79`; `browser-session.mjs:602` |
| B39 | Short extract (&lt;800 chars) → automatic `networkidle` settle + second extract | `browser-session.mjs:613-629` |
| B40 | Browser HTML char cap 900_000 → `extraction_failed` / `html_too_large:` | `browser-session.mjs:41-60,606-625` |
| B41 | Recipe `contentPrefix` **prepends synthetic markdown** if page text lacks prefix | `browser-session.mjs:636-641`; `nuxt.com.mjs:19`; `docs.docker.com.mjs:37` |
| B42 | Recipe registry: allowlist 6 hosts; match `host===h \|\| host.endsWith('.'+h)`; lazy `import` module | `recipes/registry.mjs:7-105` |
| B43 | Per-recipe knobs: waits, selectors, strip, consentAggressive, extractVariant, virtualScroll, contentPrefix (not ad-hoc `if(host===)` in shared extract) | six `recipes/*.mjs` |
| B44 | Pool: one warm context; recycle after 10 runs / 400MB / headers|storage change / failure / timeout; daemon timeout → typed `timeout` + recycle | `browser-pool.mjs:9-10,106-151,162+` |
| B45 | Feature `screenshot` → JPEG base64 on success result | `browser-session.mjs:659-701` |
| B46 | Dom-skeleton capture path (heal): stealth + bypassCSP + consent + shadow flatten | `dom-skeleton-capture.mjs`; pool `/skeleton` |

### 1.4 CSS extract / schema

| # | Behavior | Evidence |
|---|----------|----------|
| B47 | HTTP fetch via `egressFetch` **without** `pinnedDispatcherForUrl` / `resolveAndValidateHost` | `css-extract.mjs:39-46` (contrast http-extract-run B11) |
| B48 | Body read `response.text()` — **no** `OCCAM_MAX_RESPONSE_BYTES` / oversize mode | `css-extract.mjs:78` |
| B49 | `--browser-fallback` on 401/403/429 → throwaway `createBrowserSession` + bare `goto` (no consent/recipe/challenge pipeline) | `css-extract.mjs:49-65,111-127` |
| B50 | `attr:"nuxt"` → regex `__NUXT__` assignment → **`(0, eval)(match[1])`** then path walk | `css-schema-extract.mjs:131-163` |
| B51 | `attr:"regex"` → `new RegExp(playbook pattern)` on raw HTML (`{id}` from `/item/(\d+)/`) | `css-schema-extract.mjs:124-128` |
| B52 | `attr:"const"` returns selector string as value (no page read) | `css-schema-extract.mjs:92-94` |
| B53 | Row mode `base_selector` → `{rows,row_count}` (host mapper may ignore — out of worker scope) | `css-schema-extract.mjs:23-75` |

### 1.5 Shared / publish / iframes / misc

| # | Behavior | Evidence |
|---|----------|----------|
| B54 | Iframes: collect innerHTML or markdown link to `src` — **does not fetch** iframe documents | `process-iframes.mjs:10-41` |
| B55 | Playbook publish sanitize: forbidden keys/headers/notes, denylist selectors, caps, PR package (no auto-upload) | `playbook-publish-sanitize.mjs:10-370` |
| B56 | Playbook seed tiers: local / `OCCAM_PLAYBOOKS_LOCAL_ROOT` / `WT_PLAYBOOKS_PATH` / community / seeds under `OCCAM_HOME` | `playbook-seed.mjs:59-89` |
| B57 | Proxy credentials redacted in `redactProxyUrl` | `egress-proxy.mjs:167-183` |
| B58 | Selftests for egress/pdf/private-ip/response-cap/… are runnable Node scripts (doctor-invoked for some) | `*.selftest.mjs` under `shared/lib` + http access-evidence |
| B59 | **No** `PLAYWRIGHT_*` env reads in workers (Playwright package defaults only) | repo grep under `workers/` |
| B60 | Tier-B goto cap: `OCCAM_TIER_B=1` + `OCCAM_BROWSER_GOTO_TIMEOUT_MS` (default 20s floor min with recipe) | `browser-session.mjs:410-418` |

---

## 2. Gap classification

Compared after inventory against: `CAPABILITY-INVENTORY.md`, `capabilities.json`, `CAPABILITY-GRAPH.md`, `ARTIFACT-MAP.md`, `CODE-DERIVED-WORKFLOWS.md`, `NONCORE-SURFACE-MAP.md`, `ENGINEERING-FINDINGS.md` (EF-013), `subsystems/browser-workers.md`, `subsystems/network-fetch-proxy.md`, `tools/occam_extract_knowledge.md`, `ENVIRONMENT-VARIABLES.md`.

| ID | Gap label | Finding | Code evidence | Model touch |
|----|-----------|---------|---------------|-------------|
| G01 | COVERED_EXACTLY | EF-013 / CAP-598 `attr=nuxt` `(0,eval)` still present | `css-schema-extract.mjs:139` | EF-013 OPEN; CAP-598 |
| G02 | COVERED_EXACTLY | CAP-599 regex/const/divide modes | `css-schema-extract.mjs:84-128` | CAP-599 |
| G03 | COVERED_EXACTLY | Recipe registry allowlist + host match (approved pattern, not forbidden scatter `if host===` in shared extract) | `registry.mjs:47-65`; CAP-222 | CAP-222 |
| G04 | COVERED_EXACTLY | Consent dismiss + CSS-hide; virtual scroll; silent-exit guard; PDF; body cap; stealth; pool bleed EF-002 | consent / virtual-scroll / worker-exit-guard / pdf / response-body-cap / CAP-180/247/237/102 | multiple CAPs |
| G05 | COVERED_PARTIALLY | CAP-220 documents playbook `js_before_wait` / `wait_for.js` as “arbitrary JS eval” but ledger has **no** SECURITY EF sibling to EF-013 for **page-context** playbook-controlled `page.evaluate` / `waitForFunction` | `interaction-steps.mjs:13-24` | CAP-220 only |
| G06 | COVERED_PARTIALLY | CAP-178 consent — omits **always** second pass with `aggressive:true` when first click fails | `browser-session.mjs:572-575` | CAP-178 / browser-workers consent loop |
| G07 | COVERED_PARTIALLY | CAP-177/213 cookie inject — mechanism real; **shipped recipes have zero `cookies`**, so operator-visible inject surface is empty unless custom recipe | `cookie-inject.mjs` + recipes | CAP-177/213 overstate ship |
| G08 | COVERED_PARTIALLY | CAP-247 exit guard — maps stall to **`timeout`**, not a distinct `worker_stalled` code (honesty gap) | `worker-exit-guard.mjs:39-46` | CAP-247 |
| G09 | COVERED_PARTIALLY | SSRF story claims worker HTTP + browser + Core HttpClient triad; **css-extract HTTP leg lacks pin/resolve** (Core `FetchPreflight` literal-blocks private hosts only) | `css-extract.mjs:39-46` vs `http-extract-run.mjs:147-155`; `KnowledgeExtractService.cs:28-35` | CAP-150–156, CAP-592/100 edge incomplete |
| G10 | COVERED_WRONG | Network subsystem summary “three independent… guards covering worker HTTP, worker browser, and Core” reads as exhaustive worker coverage — **css-extract is a fourth fetch surface without pin** | `network-fetch-proxy.md` SSRF summary; `css-extract.mjs:39` | COVERED_WRONG framing |
| G11 | MISSING_SECURITY_SEMANTIC | css-extract unbounded `response.text()` — no body cap / no `response_too_large` | `css-extract.mjs:78` | no CAP |
| G12 | MISSING_SECURITY_SEMANTIC | Always-on `bypassCSP: true` on every browser context (incl. skeleton / css browser-fallback) | `browser-session.mjs:143`; `dom-skeleton-capture.mjs:39` | not in CAP-180 stealth notes |
| G13 | MISSING_ARTIFACT | Recipe `contentPrefix` injects **non-page** prose into `markdown` (trust/provenance) | `browser-session.mjs:636-641` | CAP-222 lists field; no artifact/trust edge |
| G14 | MISSING_EDGE | Virtual-scroll replace re-injects synthesized DOM section into extract pipeline | `virtual-scroll.mjs:291-310` | CAP-216/217 modes covered; mutation trust edge thin |
| G15 | MISSING_EDGE / DEAD_CODE_MISTAKEN_AS_PRODUCT | HTTP daemon `RECYCLE_AFTER_RUNS` looks like pool recycle; **no recycle action** | `http-daemon.mjs:8-46` | CAP-201 documents `/recycle` host-side; worker counter dead |
| G16 | MISSING_FAILURE_SEMANTIC | Browser daemon port conflict → exit 0 (success) while peer owns slot | `browser-daemon.mjs:140-144` | CAP-204 mentions host serialize; silent stand-down underplayed |
| G17 | MISSING_CONFIG | Playwright proxy invalid URL → silent null (HTTP path throws `invalid_proxy_url`) | `egress-proxy.mjs:139-142` vs `109-112` | env catalog lists proxies; asymmetry missing |
| G18 | MISSING_SECURITY_SEMANTIC | plugins-runner is **allowlisted** (only `semantic_chunking`) — model focuses on chunk quality (CAP-075) not “no arbitrary plugin load” positive security fact | `plugins-runner.mjs:29-42` | CAP-075 |
| G19 | MISSING_RUNTIME_SURFACE | No worker reads of `PLAYWRIGHT_*` (operators may assume Playwright env knobs apply) | grep empty | ENV catalog §15 doesn’t warn |
| G20 | COVERED_EXACTLY | Short-response networkidle re-extract | `browser-session.mjs:613-629`; browser-workers.md | CAP note present |
| G21 | COVERED_EXACTLY | Iframe markdown blocks without fetch | `process-iframes.mjs` | lightly covered as P10-C5 elsewhere |
| G22 | PRODUCT_MISTAKEN_AS_INTERNAL | Doctor-invoked `egress-proxy` / `private-ip` / `pdf-extract` selftests are **operator-reachable behavior**, not pure CI | `*.selftest.mjs`; doctor subsystem CAP-949+ | mostly COVERED in doctor.md; keep PRODUCT |

---

## 3. New CAP / edge / artifact / workflow candidates

**Do not mint final CAP numbers** (orchestrator ≥1050).

| Candidate | Kind | Rationale |
|-----------|------|-----------|
| `CAP-NEW-F-1` | capability | css-extract fetch: egress-aware but **no DNS-pin SSRF** + **no body cap** |
| `CAP-NEW-F-2` | capability / security | Always-on `bypassCSP:true` browser contexts |
| `CAP-NEW-F-3` | artifact / trust | Recipe `contentPrefix` synthetic markdown provenance |
| Edge | `CAP-220 → SECURITY` | Playbook plan strings executed via Playwright `evaluate`/`waitForFunction` |
| Edge | `CAP-598 → still live` | Re-verify G01 (no remediations since Wave 2) |
| Edge | `CAP-151 ↛ css-extract` | Pin helper not used on css HTTP path |
| Artifact | `response_truncated` failure with partial markdown | Exists; ensure ARTIFACT-MAP / failure taxonomy link if thin |
| Workflow | css `--browser-fallback` throwaway Chromium | Covered CAP-236/593; keep edge “no session on fallback” |
| Workflow | none new for recipes | Registry is CAP-222; not AGENTS anti-pattern |

**NEW_WORKFLOWS:** none beyond refinements of Recipe D / browser render plan.

---

## 4. Automatic / silent behaviors (priority lens)

| Trigger | Visible? | Configurable? | Disableable? | Effect |
|---------|----------|---------------|--------------|--------|
| Stealth init + AutomationControlled | No (unless inspect launch) | Channel/path only | No dedicated off switch | Anti-bot baseline |
| `bypassCSP:true` | No | No | No | CSP ignored |
| Asset abort (media/font/image; +CSS on lean/recipe) | Indirect (missing images) | `--lean-assets` / recipe | lean false reduces CSS block | Perf / fidelity |
| Consent click + forced aggressive retry + CSS-hide | `consent_clicked` / overlays gone | recipe `consentAggressive`; no global off | Partial (CSS-hide tied to variant) | Page mutation |
| Virtual scroll (default on) | `virtual_scroll_*` fields | `WT_VIRTUAL_SCROLL=0`; recipe `virtualScroll` | Yes | Scroll + possible DOM inject |
| Short-body networkidle re-extract | Latency only | No | No | Extra wait ≤~8s+ |
| Auto Chromium provision | `browser_provisioned` | `OCCAM_BROWSER_AUTOINSTALL=0` | Yes | Disk/network ~175MB |
| Exit-13 → fake `timeout` JSON | Host sees timeout | No | No (guard always on one-shot) | Failure mislabel |
| `contentPrefix` prepend | In markdown | Recipe only | Remove recipe field | Trust |
| HTTP meta-refresh follow | Final URL | No | No (≤3) | Extra fetches |
| Daemon EADDRINUSE stand-down | stderr only | No | N/A | Exit 0 |

---

## 5. Failure / fallback audit

| Path | Behavior |
|------|----------|
| Private IP / DNS fail (HTTP) | `private_ip_blocked` / `dns_resolution_failed` |
| Oversize fail mode | `response_too_large` |
| Oversize partial | `response_truncated` + markdown |
| PDF no text | empty markdown + note `pdf_no_text_layer` |
| Challenge wall | `captcha_or_challenge` |
| Browser HTML too large | `extraction_failed` + `html_too_large:` |
| SSRF abort in Chromium | `ERR_BLOCKED_BY_CLIENT` → `private_url_blocked` |
| Daemon extract timeout | `timeout` / `daemon_enforced_timeout` + recycle |
| Worker stall | emitted `timeout` (B02) |
| Proxy bad (HTTP) | throw / `invalid_proxy_url` |
| Proxy bad (Playwright) | proxy omitted silently |
| css HTTP error without fallback | `http_N` |
| css browser fallback fail | `browser_unavailable` / `browser_failed` |
| Plugin load fail | stderr log; result unchanged |

---

## 6. Config reverse audit (worker-read)

| Var | Effect | Notes |
|-----|--------|-------|
| `OCCAM_HOME` | fetch defaults, playbook roots, publish out | |
| `OCCAM_HTTP_PROXY` / `HTTPS` / `NO_PROXY` | egress + Playwright proxy | |
| `OCCAM_ALLOW_PRIVATE_URLS` | SSRF relax | HTTP still pins |
| `OCCAM_MAX_RESPONSE_BYTES` | HTML body cap | **not** css-extract |
| `OCCAM_HTTP_OVERSIZE_MODE` | fail\|partial | host-plumbed |
| `OCCAM_MAX_PDF_BYTES` | PDF cap | |
| `OCCAM_FEATURES` | plugins + blocks/tables/feed/screenshot | CSV |
| `OCCAM_CHUNK_SIZE` | chunk plugin chars | |
| `OCCAM_REQUEST_HEADERS_FILE` | daemon default headers | |
| `OCCAM_HTTP_DAEMON_PORT` / `OCCAM_BROWSER_DAEMON_PORT` | listen | |
| `OCCAM_BROWSER_POOL_SLOT_ID` | health telemetry | host-injected |
| `OCCAM_BROWSER_*` / `OCCAM_CHROME_PATH` / `OCCAM_BROWSER_AUTOINSTALL` | launch/provision | |
| `OCCAM_TIER_B` / `OCCAM_BROWSER_GOTO_TIMEOUT_MS` | goto cap | |
| `OCCAM_PLAYBOOKS_LOCAL_ROOT` / `WT_PLAYBOOKS_PATH` | seed tiers | |
| `WT_COOKIE_INJECT` / `WT_BROWSER_EXTRACT_VARIANT` / `WT_VIRTUAL_SCROLL` | browser knobs | |
| `HTTP_PROXY`/`HTTPS_PROXY`/`NO_PROXY` | **written** by `syncStandardProxyEnv` | derived |
| `PLAYWRIGHT_*` | **unread** | |

---

## 7. Platform diffs

| Diff | Evidence |
|------|----------|
| Dynamic import `pathToFileURL` for Windows plugin load | `plugins-runner.mjs:33-35` |
| Daemons bind loopback only | both daemons `127.0.0.1` |
| Chromium provision `spawn` / `spawnSync` paths | `browser-provision.mjs`, `ensure-chromium-usable.mjs` |
| No worker-level `win32` feature gates beyond path URL | — |

---

## 8. Engineering finding candidates (EFC — not EF-NNN)

| ID | Class | Confidence | One-liner |
|----|-------|------------|-----------|
| `EFC-F-1` | SECURITY-CANDIDATE | PROVEN | Re-confirm EF-013: `readNuxtPath` still `(0,eval)` on page `__NUXT__` (`css-schema-extract.mjs:139`) |
| `EFC-F-2` | SECURITY-CANDIDATE | PROVEN | css-extract HTTP: no `pinnedDispatcherForUrl` + unbounded `response.text()` (`css-extract.mjs:39-78`) |
| `EFC-F-3` | SECURITY-CANDIDATE | PROVEN | Browser plan `js_before_wait` / `wait_for.js` execute playbook strings in page via Playwright (`interaction-steps.mjs:13-24`) — sibling surface to EF-013 (page vs Node) |
| `EFC-F-4` | SECURITY-CANDIDATE | PROVEN | Always `bypassCSP:true` (`browser-session.mjs:143`) |
| `EFC-F-5` | BUG-CANDIDATE | PROVEN | HTTP daemon `RECYCLE_AFTER_RUNS` counter never recycles (`http-daemon.mjs:36-45`) |
| `EFC-F-6` | OBSERVATION | PROVEN | Forced consent aggressive retry (`browser-session.mjs:573-575`) undocumented as always-on |
| `EFC-F-7` | DESIGN-QUESTION | PROVEN | `contentPrefix` synthesizes markdown not sourced from page (`browser-session.mjs:636-641`) |
| `EFC-F-8` | OBSERVATION | PROVEN | Playwright proxy validation fail-open (null) vs HTTP fail-closed (`egress-proxy.mjs:139-142`) |

---

## 9. Convergence

**CONVERGENCE_IN_SCOPE: YES** — major worker behaviors (extract pipelines, daemons, SSRF pin on HTTP/browser, consent/scroll/recipes, css eval, plugins allowlist, exit guard, PDF/cap) are enumerable from the tree; remaining gaps are mostly **parity holes** (css vs http SSRF/cap), **trust/silent** semantics, and **ledger understatement** of already-known CAP-220 JS execution — not large undiscovered subsystems.

---

## 10. Uncertainties

- Whether Core `FetchPreflight` + undici redirect policy fully close DNS-rebinding for css-extract without pin (likely weaker than HTTP worker; not live-probed this pass).
- Whether any host still passes `--cookie-inject` / recipe cookies in production playbooks outside this repo’s six golden recipes.
- Exact doctor which selftests are hard-fail vs advisory (out of worker-file scope beyond noting reachability).
- `attr:regex` ReDoS practicality under production playbook constraints (CAP-599 notes risk; not timed).

---

## Envelope (compact)

```
OWNER: W4-F
SCOPE_FILES_READ: ~55 notable (http/browser/css entries + session/consent/scroll/recipes/ssrf/egress/cap/pdf/plugins/sanitize/daemons/pool/interaction/shadow/iframes/headers)
BLIND_BEHAVIORS: 60
GAPS: covered_exact=8 partial=5 wrong=1 missing_cap=0 missing_edge=2 missing_artifact=1 missing_workflow=0 missing_config=1 missing_failure=1 missing_security=4 dead_as_product=1 product_as_internal=1
TOP_MISSED: css-extract no SSRF pin (css-extract.mjs:39); css unbounded body (css-extract.mjs:78); bypassCSP always (browser-session.mjs:143); playbook page.evaluate (interaction-steps.mjs:14); contentPrefix trust (browser-session.mjs:636); http-daemon fake recycle (http-daemon.mjs:36); consent forced aggressive (browser-session.mjs:573); Playwright proxy fail-open (egress-proxy.mjs:139)
NEW_CAP_CANDIDATES: CAP-NEW-F-1 (css fetch SSRF/cap gap); CAP-NEW-F-2 (bypassCSP); CAP-NEW-F-3 (contentPrefix artifact)
NEW_EDGES: CAP-220→SECURITY; CAP-151↛css-extract; CAP-598 still live
NEW_ARTIFACTS: contentPrefix synthetic markdown; response_truncated payload
NEW_WORKFLOWS: none (refinements only)
AUTOMATIC_SILENT: stealth+bypassCSP; consent+CSS-hide+forced aggressive; virtual-scroll; short-body re-extract; auto chromium; exit-guard timeout; contentPrefix
FAILURE_FALLBACK: exit-13→timeout; proxy PW null; css browser-fallback throwaway; daemon EADDRINUSE exit0
CONFIG_GAPS: OCCAM_MAX_RESPONSE_BYTES unused by css; PLAYWRIGHT_* unread; PW proxy asymmetry
PLATFORM_DIFFS: pathToFileURL plugins; loopback daemons
EFC: EFC-F-1 SECURITY EF-013 reconfirm; EFC-F-2 css SSRF/cap; EFC-F-3 playbook evaluate; EFC-F-4 bypassCSP; EFC-F-5 http recycle noop; EFC-F-6 consent aggressive; EFC-F-7 contentPrefix; EFC-F-8 proxy fail-open
CONVERGENCE_IN_SCOPE: YES — tree exhausted; gaps are parity/trust/ledger understatement
UNCERTAINTIES: css DNS-rebinding residual; production recipe cookies; doctor selftest severity; regex ReDoS timing
```
