# HTTP acquisition

**Slug:** `http-acquisition` · **Product system:** PS-1 Acquisition · **CAPs:** 4 · **Public relevance:** HIGH

**Member CAPs:** CAP-059, CAP-200, CAP-201, CAP-202  
**Product capability:** CAP-200  
**Engineering findings:** None listed on family; PDF/timeouts interact with network-safety EF-043 (css path only — not this family).

## What it is

Local **Node HTTP extract** path: fetch HTML (or PDF text layer), run Readability/Turndown-style extraction, return markdown to the host. First rung under `http_then_browser`, sole path under `backend_policy=http`, and the only path used for `prefer_llms_txt` probes.

## Why it exists

Fast, no Chromium cost for static/docs pages. Default cheap attempt before browser escalation (`ACQUISITION-ROUTING-MODEL.md` Rung 1).

## User-visible entrypoints

| Surface | Notes | Evidence |
|---------|-------|----------|
| `occam_transcode` / digest / claim_check / attest / dataset / watch / crosscheck / batch | Via `HttpExtractBackend` when policy includes HTTP | `Backends/HttpExtractBackend.cs:6-28` |
| `prefer_llms_txt` | HTTP-only pipeline on `{origin}/llms.txt` | `OccamTranscodeTool.cs:147-164` |
| Worker scripts | `workers/http-extract/extract.mjs`, `http-daemon.mjs` | CAP-200/201 entrypoints |
| Browser / css-extract / probe | Not this family | — |

Transparent PDF (CAP-059): HTTP worker auto-detects PDF; **not** a separate MCP parameter.

## Core behavior

### Backend (CAP-200)

- Host timeout: **fixed 35s** (`HttpExtractBackend.DefaultHttpTimeoutMs = 35_000`, `HttpExtractBackend.cs:8`).
- Backend id / worker: `node_readability_turndown` → `workers/http-extract/extract.mjs`.
- Readiness = worker scripts present on disk (`workerPaths.IsConfigured`) — **not** a live network probe (`browser-workers.md` CAP-200).

### Daemon amortization (CAP-201)

- `HttpDaemonHost`: default port **39218** (`OCCAM_HTTP_DAEMON_PORT`), idle TTL **120s** (`OCCAM_HTTP_DAEMON_IDLE_TTL_MS`).
- Disable: `OCCAM_HTTP_DAEMON=0`.
- Endpoints: `/health`, `/extract`, `/recycle`.
- Daemon **skipped** when: proxy rotation configured, playbook overlay path set, or `PreferOneShot` (`HttpExtractRunner.cs:44-46`).

### One-shot fallback (CAP-202)

Fresh `node extract.mjs <url>` when daemon disabled/unhealthy/bypassed (`HttpExtractRunner.cs:67-164`).

### PDF (CAP-059 / CAP-237 membership note)

PDF text-layer extract on HTTP path only; binary cap default **16 MiB** (`OCCAM_MAX_PDF_BYTES`, clamp 64 KiB–128 MiB). Scanned PDFs → honest `pdf_no_text_layer` — **no OCR**. Not available via browser backend (`browser-workers.md` CAP-237).

## Advanced behavior

| Behavior | Evidence |
|----------|----------|
| Feed parse when `json_feed` features set | Worker short-circuit (structured family) |
| Meta-refresh redirect loop (≤3 hops) + per-hop SSRF | CAP-152/185; `network-fetch-proxy.md` |
| Cross-origin credential strip on redirect | CAP-172 |
| Body size cap | `OCCAM_MAX_RESPONSE_BYTES` default **8 MiB** (`response-body-cap.mjs`) |
| Ambient fetch headers / session Cookie header | Family `session-fetch` |
| Static/rotating proxy via `egressFetch` | Family `proxy-egress` |

## Automatic / silent behavior

| Behavior | Notes |
|----------|-------|
| HTTP daemon prewarm | AUTOMATION #2 — cold-start latency reduction |
| Auto PDF path | CAP-059 — no param |
| Thin/challenge treated as non-success by router | Escalates if cascade | `OccamRouter.cs:194-199` |

## Parameters

No HTTP-specific MCP params beyond shared `url`, `backend_policy`, `session_profile`, structured feature flags that become worker features. Timeouts are **not** per-call negotiable (CAP-187).

## Configuration

| Knob | Default | Effect |
|------|---------|--------|
| `OCCAM_HOME` | required for workers | Locates extract scripts |
| `OCCAM_HTTP_DAEMON` | on | `0` → always one-shot |
| `OCCAM_HTTP_DAEMON_PORT` | 39218 | Daemon listen |
| `OCCAM_HTTP_DAEMON_IDLE_TTL_MS` | 120000 | Idle kill |
| `OCCAM_MAX_RESPONSE_BYTES` | 8 MiB | Oversize → `response_too_large` / partial |
| `OCCAM_MAX_PDF_BYTES` | 16 MiB | PDF binary cap |
| `OCCAM_HTTP_PROXY` / `HTTPS_PROXY` / `NO_PROXY` | unset | Worker egress (not Core HttpClient) |
| `OCCAM_PROXY_LIST` / `_FILE` | unset | Forces one-shot (skip daemon) |
| `OCCAM_REQUEST_HEADERS_FILE` | unset | Ambient headers |
| `OCCAM_NODE_BIN` | PATH/`OCCAM_HOME/bin/node` | Node resolution |

## Backends

This family **is** the HTTP backend. Does not call browser or managed.

## Sessions / state

Headers via `FetchHeadersScope` temp file; **storageState is browser-only** — HTTP uses Cookie/header values only (`ACQUISITION-ROUTING-MODEL.md` Rung 1 auxiliaries). Daemon is process-scoped warm state (idle TTL).

## Network behavior

- Scheme/host preflight in Core before dispatch.
- Worker: DNS-pin SSRF (CAP-151), meta-refresh re-check (CAP-152).
- Timeouts fixed 35s host-side.
- **No** automatic retry/backoff on transient failure (CAP-188).

## Artifacts produced

Markdown (and optional structured worker sidecars when features on). PDF → markdown text layer. Failure codes: `timeout`, `http_*`, `dns_error`, `tls_error`, `network_error`, `private_url_blocked`/`private_ip_blocked`, `response_too_large`, `extraction_failed`, `pdf_no_text_layer`, …

## Trust / provenance properties

Local extract — no third-party content rewrite. Does not prove site authenticity. Thin shells can still be `ok` until EQM/router/PP reject.

## Failure / fallback behavior

| Condition | Behavior |
|-----------|----------|
| Daemon unhealthy | One-shot (CAP-202) |
| Oversize body | Typed oversize / partial per mode |
| 404/410 | Terminal for cascade (router) |
| Thin/challenge | Non-success → browser if cascade |
| Workers missing | `workers_unavailable` |

## Platform differences

Node path resolution and Playwright-unrelated. Process group kill for one-shot children is OS-specific (`WorkerProcessGroup` — family browser also). No HTTP-cascade semantic delta by OS.

## Composition with other capabilities

- Ordered by `acquisition-routing`.
- Guards: `network-safety`, `proxy-egress`, `session-fetch`.
- Post-success: post-processors → PS-2 materialization.
- PDF is HTTP-only — do not compose with browser for OCR.

## Known limitations

- Cannot defeat SPA shells / many anti-bot walls.
- storageState cookies without Cookie header do not apply on HTTP.
- 35s fixed — no per-call timeout param.
- Name “HTTP” includes PDF text extract — not HTML-only.

## Engineering findings

None family-owned. Related: **EF-007** (Core HttpClients ignore OCCAM proxy — probe/managed, not this worker). **EF-043** is css-extract parity, not http-extract.

## Code evidence

- `src/FFOccamMcp.Core/Backends/HttpExtractBackend.cs:6-28`
- `src/FFOccamMcp.Core/Workers/HttpDaemonHost.cs`, `HttpExtractRunner.cs:44-164`
- `workers/http-extract/extract.mjs`, `http-daemon.mjs`, `lib/http-extract-run.mjs` (PDF)
- `docs-audit/subsystems/browser-workers.md` §1
- `docs-audit/ACQUISITION-ROUTING-MODEL.md` Rung 1

## Public-doc relevance

**HIGH.** Default first fetch path; document 35s timeout, daemon, PDF honesty (no OCR), and that SPA needs browser policy/cascade.

## Handbook relevance

“Fast path” card: static pages, docs, PDF text. Point to browser family when HTTP returns thin/challenge.
