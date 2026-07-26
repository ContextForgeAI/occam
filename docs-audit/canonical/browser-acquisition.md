# Browser acquisition

**Slug:** `browser-acquisition` · **Product system:** PS-1 Acquisition · **CAPs:** 32 · **Public relevance:** HIGH

**Member CAPs:** CAP-053, CAP-076, CAP-099, CAP-203–CAP-209, CAP-206b, CAP-219–CAP-224, CAP-228–CAP-232, CAP-234, CAP-236, CAP-237, CAP-243–CAP-247, CAP-248a, CAP-248b  
**Product capability:** CAP-203  
**Engineering findings:** EF-002, EF-041 (also EF-040/039/046 related; see sessions & automation)

## What it is

Playwright Chromium **rendered-page extract**: navigate, settle, optional consent/stealth/recipes/interaction plan, snapshot → markdown. Second cascade rung and sole path under `backend_policy=browser`. Includes daemon/pool lifecycle, auto-provision, and related Node worker mechanics.

**Name honesty:** does **not** solve CAPTCHAs, rotate fingerprints, or guarantee anti-bot bypass (CAP-209 scoped “Lean-A”).

## Why it exists

Defeat SPA shells and JS-rendered docs that HTTP thin-extracts; apply storageState sessions; run playbook interaction plans.

## User-visible entrypoints

| Surface | Notes | Evidence |
|---------|-------|----------|
| MCP tools using TranscodePipeline + browser policy/cascade | `BrowserExtractBackend` | `BrowserExtractBackend.cs:6-35` |
| `capture_screenshot=true` | Browser-only JPEG sidecar | CAP-076; `OccamTranscodeTool.cs:56` |
| `occam install-browser` CLI | User-level Chromium install | CAP-207; `OccamCliVerbs.cs:44-166` |
| Workers | `browser-extract.mjs`, `browser-daemon.mjs` | family entrypoints |
| `css-extract.mjs` (CAP-236) | Knowledge spine — related Node browser use, **not** TranscodeRouter | `PRODUCT-ARCHITECTURE.md` |
| CAP-237 PDF | Listed in membership but **HTTP-only** — see Known limitations | browser-workers CAP-237 |

## Core behavior

### Backend (CAP-203)

- Timeout: `OCCAM_BROWSER_TIMEOUT_MS` default **60000**, clamp **15–180s**; raised to **240s floor** when cold auto-provision predicted (`BrowserExtractTimeouts.cs:8-25`; CAP-099).
- Note: older prose “~120s” is **wrong** vs code default 60s (`ACQUISITION-ROUTING-MODEL.md` UNCERTAIN/doc scrub).

### Pool / daemon (CAP-204, CAP-230–232)

| Knob | Default | Role |
|------|---------|------|
| `OCCAM_BROWSER_POOL_SIZE` | 1–8 range | Slots |
| `OCCAM_BROWSER_POOL_BASE_PORT` | 39217 | Base; single-slot honors `OCCAM_BROWSER_DAEMON_PORT` |
| `OCCAM_BROWSER_MAX_PARALLEL` | 2 | Concurrency (`WT_BROWSER_MAX_PARALLEL` fallback) |
| Idle TTL | 120s | Auto-stop (CAP-232) |
| `OCCAM_BROWSER_DAEMON=0` or profile `isolated\|parallel\|throughput` | — | Force one-shot (CAP-205) |

Round-robin + serialized spawn (CAP-231). Concurrency limiter (CAP-230).

### One-shot (CAP-205) / rotation (CAP-234)

Proxy rotation forces SkipDaemon / one-shot for primary extract tools.

### Readiness / auto-provision (CAP-053, CAP-099, CAP-206, CAP-206b)

- Tool may downgrade to HTTP if browser missing and auto-provision off (`OccamTranscodeTool.cs:135-145`).
- Auto-install: `OCCAM_BROWSER_AUTOINSTALL` ≠ `"0"` (default on); `npx playwright install chromium`; host grace 240s.
- Provision-gate probe assumes `true` on probe failure (do not silent-downgrade) — CAP-206b.

### System browser (CAP-208)

`OCCAM_BROWSER_EXECUTABLE_PATH` / `OCCAM_CHROME_PATH` / `OCCAM_BROWSER_CHANNEL=chrome|msedge|…`. System browser disables auto-install path.

## Advanced behavior

| CAP | Behavior |
|-----|----------|
| CAP-209 | Lean-A stealth: hide `navigator.webdriver`; **not** full anti-detect |
| CAP-210–212 | Challenge fail-fast; consent dismiss; CSS-hide overlays (also `access-consent`) |
| CAP-219 | Open shadow DOM flatten (cap 128); closed out of scope |
| CAP-220 | Playbook interaction plan (`js_before_wait`, `wait_for`, steps) |
| CAP-222 | Per-host recipes registry (mdn, nuxt, k8s, …) |
| CAP-223 | Extract variants: baseline/reextract/css-hide/strip-* |
| CAP-224 | Soft/strict playbook overlay in worker |
| CAP-221 / 076 | Screenshot opt-in (GATED) |
| CAP-225 | Browser HTML max **900_000** chars |
| CAP-226 | Per-navigation SSRF in browser |
| CAP-227 / 249 | Pool session recycle; **shared context** across hosts |
| CAP-228–229 | Lifecycle recycle after 10 runs / on crash; process-tree kill |
| CAP-243 | Per-domain opt-in (GATED) |
| CAP-245–247 | Node bin, Playwright cache path, exit-13 → typed timeout |
| CAP-248a/b | `IWorkerProcessSpawner` / `BrowserConcurrencyGate` — **SHIPPED_DEAD** |

Virtual scroll (related, recipes): `WT_VIRTUAL_SCROLL=0` disables — AUTOMATION #9.

## Automatic / silent behavior

| # / behavior | Impact | Evidence |
|--------------|--------|----------|
| InstallShared → StopAll | New WS/Remote DI kills process-wide pool | **EF-041**; AUTOMATION #3 |
| Stealth + **bypassCSP:true always** | Page CSP weakened | AUTOMATION #7; **EF-046** |
| Consent dismiss + CSS-hide | May hide real UI | AUTOMATION #8 |
| Auto chromium provision | First-call latency | AUTOMATION #10 |
| Short-response settle retry | &lt;800 chars → networkidle re-extract | browser-session.mjs |
| Public-ref / 404 short-circuit | Browser never reached | router; EF-056 |

## Parameters

| Name | Default | Effect |
|------|---------|--------|
| `backend_policy` | `http_then_browser` | `browser` forces this family only |
| `capture_screenshot` | `false` | JPEG base64 if browser used |
| `session_profile` | unset | storageState + headers (Tier 1) |
| Playbook interaction / overlay | via resolve | CAP-220/224 |

No per-call browser timeout MCP param — env only (CAP-187).

## Configuration

See Core behavior table plus:

| Knob | Default | Effect |
|------|---------|--------|
| `OCCAM_BROWSER_TIMEOUT_MS` | 60000 | Per-extract budget |
| `OCCAM_BROWSER_AUTOINSTALL` | on | `0` disables |
| `OCCAM_BROWSER_PROFILE` | shared | isolated/parallel/throughput → one-shot |
| `OCCAM_PLAYWRIGHT_BROWSERS_PATH` / `PLAYWRIGHT_BROWSERS_PATH` | OS cache | Chromium location |
| `WT_BROWSER_EXTRACT_VARIANT` | — | Variant override |
| `WT_COOKIE_INJECT` | off | Recipe cookies (CAP-213 / access-consent) |
| `WT_VIRTUAL_SCROLL` | on | `0` disables |

## Backends

Playwright Chromium (channel/system/user install). Does not implement managed providers.

## Sessions / state

- Tier 1 TranscodePipeline: headers + storageState.
- Pool: **one warm BrowserContext per slot**, reused across hosts until recycle (CAP-249 / **EF-002**).
- Session→anon transitions recycle first; bleed refined as anon→anon shared context (**EF-040**).
- Per-call GUID headers temp file defeats warm reuse (**EF-039** / CAP-881).

## Network behavior

- Every navigation SSRF-checked (CAP-153/226).
- Proxy via Playwright launch options (static); rotation → one-shot.
- Proxy resolve fail-open → null proxy (GAP-030).
- Daemon queue wait can extend wall time (up to ~900s noted in acquisition model).

## Artifacts produced

Markdown; optional screenshot; worker structured features; typed failures (`timeout`, `captcha_or_challenge`, `thin_extract`, `playwright_missing`, `extraction_failed`, …). `browserProvisioned` may surface when auto-install ran.

## Trust / provenance properties

Rendered DOM ≠ authenticated user intent without session. Stealth/bypassCSP are engineering aids, **not** proof of clean acquisition. Cookie bleed across hosts weakens session isolation claims (EF-002).

## Failure / fallback behavior

| Condition | Behavior |
|-----------|----------|
| Pool miss / disabled / rotation | One-shot |
| Binary missing + autoinstall | Provision then retry; else typed miss |
| Challenge near-empty | Fast `captcha_or_challenge` |
| Cascade dual-fail | May be surface winner via FailureRanking |
| InstallShared mid-flight | Pool kill latency spike (EF-041) |

## Platform differences

| Topic | Difference | Evidence |
|-------|------------|----------|
| Playwright cache dirs | Win `%LOCALAPPDATA%\ms-playwright` vs macOS/Linux caches | CAP-246; `PLATFORM-DIFFERENCES.md` |
| Process kill | Win Job Object vs POSIX process group | CAP-229 |
| storageState path prefix check | Win ordinal-ignore-case vs Unix ordinal | `SessionProfileHeaders.cs:230-232` |

## Composition with other capabilities

- Escalated from HTTP by `acquisition-routing` (except terminal/public-ref).
- Consent/challenge shared with `access-consent`.
- Sessions with `session-fetch`; proxy with `proxy-egress`.
- css-extract (CAP-236) is PS-4 spine — do not document as transcode browser.

## Known limitations

- No CAPTCHA solving; no fingerprint rotation.
- Closed shadow roots out of scope.
- CAP-237 PDF in membership is **misplaced relative to runtime** — PDF is HTTP-only (flag in envelope).
- CAP-248a/b dead types still ship (C8 whole-glob compile).
- Default timeout 60s ≠ legacy “120s” docs.

## Engineering findings

| ID | Finding |
|----|---------|
| **EF-002** | Pool context reuse / cookie bleed until recycle |
| **EF-041** | InstallShared kills process-wide pool on new WS/Remote session |
| EF-039 | GUID headers temp file defeats pool reuse |
| EF-040 | Refines bleed: anon→anon shared context |
| EF-046 | Always `bypassCSP:true` |

## Code evidence

- `Backends/BrowserExtractBackend.cs`, `BrowserExtractTimeouts.cs:8-36`
- `Workers/BrowserPoolManager.cs`, `BrowserExtractRunner.cs`, `BrowserConcurrencyLimiter.cs`
- `workers/browser-extract/**` (session, consent, recipes, provision, pool)
- `docs-audit/subsystems/browser-workers.md`
- `AUTOMATION-MODEL.md` rows 3,7,8,10

## Public-doc relevance

**HIGH.** Explain when browser runs, what it cannot defeat, pool/autoinstall costs, and session_profile for login walls.

## Handbook relevance

“Hard page” path after HTTP thin/challenge; operator chapter for install-browser and pool env.
