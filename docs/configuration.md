# Configuration

**What you'll do:** look up every `OCCAM_*` environment variable the host reads.

Only variables found in `src/FFOccamMcp.Core/` are listed. CLI flags override env where noted in [Transports](transports.md).

---

## Required for production

| Variable | Purpose |
|----------|---------|
| `OCCAM_HOME` | Install root; resolves worker scripts. Without it: `workers_unavailable` |

---

## Paths and workers

| Variable | Default | Purpose |
|----------|---------|---------|
| `OCCAM_HTTP_EXTRACT_SCRIPT` | auto | Override HTTP worker entry (pair with browser override) |
| `OCCAM_BROWSER_EXTRACT_SCRIPT` | auto | Override browser worker entry |
| `OCCAM_NODE_BIN` | stamped by launcher from install record / `process.execPath`; else `{OCCAM_HOME}/bin/node` / well-known paths / `PATH` | Absolute Node used to spawn all workers. Advanced override only — GUI MCP host configs should **not** set this. Install writes `{OCCAM_HOME}/runtime/node-bin`; `scripts/launch-mcp-host.mjs` and `occam-wrapper.sh` stamp `OCCAM_NODE_BIN` for Core. |
| `OCCAM_DOM_SKELETON_SCRIPT` | auto | Override DOM skeleton script for heal |
| `OCCAM_FORCE_DOTNET_RUN` | off | Launcher uses `dotnet run` instead of AOT binary |

---

## Session profiles

| Variable | Default | Purpose |
|----------|---------|---------|
| `OCCAM_SESSIONS_ROOT` | `~/.occam/sessions/` | Directory for `<id>.json` session profiles |
| `OCCAM_ALLOW_PRIVATE_URLS` | off | `1` allows localhost/private URLs (local dev only) |
| `OCCAM_REQUEST_HEADERS_FILE` | none | JSON file of extra HTTP headers for workers |

Export profiles: `node scripts/occam-session.mjs export-state --profile <id>`.

After a browser extract, Occam may replay the same URL once over HTTP with
first-party cookies harvested from that navigation. That harvest is in-call
only (temp headers file, deleted after the retry). It is not a session
profile, is not persisted, and cookie values are never written to receipts
or the MCP response.

---

## Browser and Playwright

| Variable | Default | Purpose |
|----------|---------|---------|
| `PLAYWRIGHT_BROWSERS_PATH` | Playwright cache | Standard browser cache path |
| `OCCAM_PLAYWRIGHT_BROWSERS_PATH` | — | Occam-specific override |
| `OCCAM_BROWSER_CHANNEL` | auto | `chrome` \| `msedge` \| `chromium` \| unset. Unset (default): prefer an installed system Chrome/Edge when found, else bundled Playwright Chromium. Set `chromium` to force the bundled browser. |
| `OCCAM_BROWSER_PREFER_SYSTEM` | on | When `OCCAM_BROWSER_CHANNEL` is unset, prefer system Chrome/Edge if present. Set `0` to always use bundled Chromium unless a channel/path is explicit. |
| `OCCAM_BROWSER_AUTOINSTALL` | on | On first genuine browser need with no browser installed, occam provisions the user-level Chromium itself and reports `browser_provisioned`. Set `0` to instead return a typed `browser_required` failure to run manually. System libraries (root) are never auto-installed. Auto-provision is skipped when a system browser is configured or auto-detected. |
| `OCCAM_BROWSER_EXECUTABLE_PATH` | — | Absolute browser binary |
| `OCCAM_CHROME_PATH` | — | Alias for executable path |
| `OCCAM_BROWSER_PROFILE` | `shared` | `shared`/`daemon`/`lean` = daemon pool; `isolated`/`parallel` = one-shot |
| `OCCAM_BROWSER_DAEMON` | on | `0` forces isolated one-shot extracts |
| `OCCAM_BROWSER_POOL_SIZE` | `1` | Daemon slots (1–8) |
| `OCCAM_BROWSER_POOL_BASE_PORT` | `39217` | Base port for pool slot 0 |
| `OCCAM_BROWSER_DAEMON_PORT` | `39217` | Legacy port when pool size = 1 |
| `OCCAM_BROWSER_MAX_PARALLEL` | `2` | Max concurrent browser extracts (1–16) |
| `OCCAM_BROWSER_TIMEOUT_MS` | `60000` | Per-extract timeout (15k–180k) |
| `OCCAM_BROWSER_DAEMON_SCRIPT` | auto | Override `browser-daemon.mjs` |
| `OCCAM_BROWSER_DAEMON_IDLE_TTL_MS` | `120000` | Idle shutdown; `0` = always warm |
| `OCCAM_BROWSER_NODE_MAX_OLD_SPACE_MB` | — | Node heap cap for browser worker |
| `OCCAM_NODE_MAX_OLD_SPACE_MB` | — | Node heap cap for HTTP worker |

---

## HTTP daemon

| Variable | Default | Purpose |
|----------|---------|---------|
| `OCCAM_HTTP_DAEMON` | on | `0` = one-shot HTTP worker per request |
| `OCCAM_HTTP_DAEMON_PORT` | `39218` | Daemon listen port |
| `OCCAM_HTTP_DAEMON_SCRIPT` | auto | Override `http-daemon.mjs` |
| `OCCAM_HTTP_DAEMON_IDLE_TTL_MS` | `120000` | Idle shutdown |
| `OCCAM_HTTP_DAEMON_PREWARM` | on | Background warm at host startup |

---

## Response size and politeness

| Variable | Default | Purpose |
|----------|---------|---------|
| `OCCAM_MAX_RESPONSE_BYTES` | `8388608` (8 MiB) | HTTP body cap (64 KiB–16 MiB) |
| `OCCAM_HTTP_OVERSIZE_MODE` | `fail` | `fail` or `partial` (truncated markdown) |
| `OCCAM_MAX_PDF_BYTES` | `16777216` (16 MiB) | PDF body cap |
| `OCCAM_RESPECT_ROBOTS` | off | `1` enforces robots.txt disallow |
| `OCCAM_HOST_THROTTLE_MS` | `0` | Per-host minimum interval between fetches |
| `OCCAM_ROBOTS_TIMEOUT_MS` | `10000` | robots.txt fetch timeout |

---

## Transcode cache (opt-in)

| Variable | Default | Purpose |
|----------|---------|---------|
| `OCCAM_CACHE_DIR` | `{TEMP}/occam-cache` | In-memory/disk cache dir for `cache_ttl_s` hits |

---

## Receipts

| Variable | Default | Purpose |
|----------|---------|---------|
| `OCCAM_RECEIPTS` | on | `off`/`0`/`false` disables signing |
| `OCCAM_KEYS_ROOT` | `~/.occam/keys/` | ECDSA P-256 key directory |
| `OCCAM_TIME_ANCHOR` | off | `1` requests RFC3161 token from TSA |
| `OCCAM_TSA_URL` | — | TSA endpoint (required when time anchor on) |
| `OCCAM_TSA_TIMEOUT_MS` | `3000` | TSA round-trip timeout (500–15000) |

---

## Capability tiers (experimental)

The exam engine (`occam exam selftest` / `exam tasks`) grades an agent on four checkable tasks and
maps the score to a tool surface. The engine is implemented and tested; **the host does not
administer the exam itself** — MCP has no general server-initiated task mechanism, so today a tier
reaches a surface only by an operator setting `OCCAM_PROFILE`. See
[ADR-0016](https://github.com/ContextForgeAI/occam/blob/main/docs/adr/0016-capability-exam.md) for
what is and is not wired up.

| Score | Tier | Profile | Tools |
|-------|------|---------|-------|
| 0–1 | `weak` | `minimal` | 1 |
| 2–3 | `medium` (default) | `basic` | 3 |
| 4 | `strong` | `full` | 16 |

A client that never sat the exam is treated as `medium`: starving a capable agent is a silent
failure, while over-trusting a weak one is visible and recoverable.

---

## Proof-of-read canary (experimental)

Tunables for the `occam canary` verbs and the probe endpoints. Defaults are the normative protocol
values from [PROBE_PROTOCOL.md](https://github.com/ContextForgeAI/occam/blob/main/PROBE_PROTOCOL.md)
§8 — change them only with that document open, because several interact.

| Variable | Default | Range | Purpose |
|----------|---------|-------|---------|
| `OCCAM_CANARY_BUCKET_SECONDS` | `300` | 30–3600 | Time-bucket width; the sentinel changes every bucket |
| `OCCAM_CANARY_FRESH_TOLERANCE` | `1` | 0–8 | Buckets on either side still counted `READ_VERIFIED` (absorbs clock skew) |
| `OCCAM_CANARY_STALE_HORIZON` | `24` | 1–512 | Oldest bucket still recognised as `READ_STALE` |
| `OCCAM_CANARY_SENTINEL_BYTES` | `32` | 16–32 | Tag length; 16 gives a 22-character sentinel, 32 gives 43 |
| `OCCAM_CANARY_ISSUE_LOG_HOURS` | `24` | 1–168 | Issuance retention — drives `REPLAY_SUSPECT` detection |
| `OCCAM_CANARY_ISSUE_LOG_CAPACITY` | `8192` | 256–1048576 | Hard cap on retained issuance records |
| `OCCAM_CANARY_RATE_LIMIT` | `30` | 1–10000 | Requests per window, per session and client |
| `OCCAM_CANARY_RATE_WINDOW_SECONDS` | `60` | 1–3600 | Rate-limit window width |

Notes that matter:

- The secret is **not** configurable. It is generated per process from the platform CSPRNG, never
  persisted and never logged. Restarting the host invalidates every outstanding sentinel by design.
- Setting `OCCAM_CANARY_STALE_HORIZON` at or below `OCCAM_CANARY_FRESH_TOLERANCE` is **rejected at
  startup**: it makes `READ_STALE` unreachable, so the verifier would silently lose a verdict.
- Shortening `OCCAM_CANARY_ISSUE_LOG_HOURS` below `STALE_HORIZON × BUCKET_SECONDS` makes genuine
  older reads report as `REPLAY_SUSPECT`, because their issuance record has been evicted.
- Out-of-range values are clamped with a note on stderr rather than accepted silently.

---

## Playbooks

| Variable | Purpose |
|----------|---------|
| `OCCAM_PLAYBOOKS_LOCAL_ROOT` | Local learn tier directory |
| `WT_PLAYBOOKS_PATH` | User/org playbook tier |
| `OCCAM_SITE_GENOME_FETCH` | `1` enables well-known site playbook fetch on resolve |

---

## Search (`occam_search`)

| Variable | Purpose |
|----------|---------|
| `OCCAM_SEARCH_PROVIDER` | Unset → keyless **`duckduckgo`** (HTML SERP, `provider` disclosed). `off` \| `none` → `search_unconfigured`. Explicit: `duckduckgo` \| `searxng` \| `brave` \| `tavily` \| `donsetch`. **Ignored when `OCCAM_SEARCH_PROVIDERS` is set.** |
| `OCCAM_SEARCH_PROVIDERS` | Optional CSV fan-out list (e.g. `duckduckgo,brave,searxng`). **Wins over** `OCCAM_SEARCH_PROVIDER`. Parallel poll of every configured healthy backend; merge by URL consensus. Response `provider` is `fanout` + `providersUsed[]`. Entries missing key/URL are skipped. |
| `OCCAM_SEARCH_URL` | Required for SearXNG base URL |
| `OCCAM_SEARCH_API_KEY` | Required for Brave/Tavily |
| `OCCAM_SEARCH_TIMEOUT_MS` | HttpClient ceiling. Default `20000` (1k–120k) |
| `OCCAM_SEARCH_PROVIDER_TIMEOUT_MS` | Per-provider cancel in fan-out. Default `3000` (1k–30k). Slow arms are dropped; others continue. |
| `OCCAM_SEARCH_FANOUT_TIMEOUT_MS` | Overall fan-out cancel. Default `provider+500` clamped (1k–35k). |
| `OCCAM_SEARCH_DEGRADE_MINUTES` | After `429` / CAPTCHA (`202`) / timeout, skip that provider for N minutes. Default `5` (1–120). |
| `OCCAM_SEARCH_RATE_MAX` | Per-provider fixed-window permit count. Default `30` (1–10000). |
| `OCCAM_SEARCH_RATE_WINDOW_S` | Rate-limit window seconds. Default `60` (1–3600). |
| `OCCAM_DONSETCH_PATH` | Optional absolute path to a local `donsetch` binary (`OCCAM_SEARCH_PROVIDER=donsetch`). Otherwise `donsetch` must be on `PATH`. Never bundled (AGPL). |

Occam does not index the web. The DuckDuckGo default is disclosed discovery for
first-run Research; operators who want a dedicated backend set SearXNG/Brave/Tavily
(or `off` for air-gap). DuckDuckGo may soft-block automated egress with an anomaly
challenge — Occam returns `search_http_202` and does not solve CAPTCHAs. Fan-out
skips degraded/rate-limited providers and logs switches on stderr as
`[occam.search] event=… provider=… reason=… untilUtc=…`.

---

## PDF OCR (optional, scanned PDFs)

Off by default. After `pdf_no_text_layer`, the HTTP worker may call a local helper:

| Variable | Purpose |
|----------|---------|
| `OCCAM_PDF_OCR` | `1` / `true` enables the OCR attempt |
| `OCCAM_PDF_OCR_BIN` | Executable (or `node`) that accepts the PDF path as the last argv and prints text to stdout |
| `OCCAM_PDF_OCR_ARGS` | Optional extra argv before the PDF path (e.g. path to a `.mjs` helper) |
| `OCCAM_PDF_OCR_TIMEOUT_MS` | Default `60000` (1k–300k) |

Honest notes on failure: `pdf_ocr_unconfigured`, `pdf_ocr_timeout`, `pdf_ocr_failed`, `pdf_ocr_empty`. Occam does not ship an OCR engine.
Evaluation and implementation-budget decision:
[PDF/OCR capability eval](examples/capability-eval/pdf-ocr/).

---

## Translation

| Variable | Default | Purpose |
|----------|---------|---------|
| `OCCAM_TRANSLATE_URL` | — | LibreTranslate base URL |
| `OCCAM_TRANSLATE_API_KEY` | — | Optional API key |
| `OCCAM_TRANSLATE_TIMEOUT_MS` | `20000` | Translation timeout |

---

## Egress proxy

| Variable | Purpose |
|----------|---------|
| `OCCAM_HTTP_PROXY` | HTTP forward proxy |
| `OCCAM_HTTPS_PROXY` | HTTPS proxy (falls back to HTTP proxy) |
| `OCCAM_NO_PROXY` | Comma-separated bypass list |
| `OCCAM_PROXY_LIST` | Rotating proxy pool (one-shot spawns) |
| `OCCAM_PROXY_LIST_FILE` | File-based proxy pool (wins over list) |

When a proxy pool is active, HTTP and browser daemons are disabled (rotation requires one-shot workers).

---

## Digest parallelism

| Variable | Default | Purpose |
|----------|---------|---------|
| `OCCAM_DIGEST_PARALLEL` | on | `0` forces fully sequential (all hosts) |
| `OCCAM_DIGEST_MAX_PARALLEL` | — | Cap 1–8 **hosts** in flight; URLs that share a host are always serial |

---

## Opt-in MCP tools

| Variable | Registers |
|----------|-----------|
| `OCCAM_BATCH_MCP=1` | `occam_batch_submit`, `occam_batch_status`, `occam_batch_results` |
| `OCCAM_WATCH_MCP=1` | `occam_watch` |
| `OCCAM_CONSENSUS_MCP=1` | `occam_crosscheck` |
| `OCCAM_ATLAS_MCP=1` | `occam_failure_atlas` + per-host telemetry |
| `OCCAM_BROWSER_ACTIONS_MCP=1` | `occam_browser_interact` (declarative click/type/scroll then materialize; never cached) |

---

## Tool surface profile (`OCCAM_PROFILE`)

Narrows which **core** tools appear in `tools/list` (and in server instructions). Default **`reader`**
keeps the day-to-day read surface (9 tools). Set `OCCAM_PROFILE=full` for all sixteen (including
playbook heal/save). Opt-in tools above are independent — still require their own flags.

| Value | Core tools exposed |
|-------|--------------------|
| `minimal` | `occam_transcode` only — one tool, so tool selection cannot go wrong |
| `basic` | `occam_transcode`, `occam_digest`, `occam_search` — read one page, read several, find pages |
| `reader` (default) | `occam_client_capabilities`, `occam_transcode`, `occam_probe`, `occam_digest`, `occam_map`, `occam_search`, `occam_extract_knowledge`, `occam_verify` |
| `researcher` | reader + `occam_claim_check` |
| `auditor` | researcher + `occam_attest`, `occam_dataset_export`, `occam_playbook_lint` |
| `full` | All fifteen (includes playbook resolve/heal/save) |

Surfaces are nested: widening a profile never removes a tool a narrower one had. Each profile also
gets its own `instructions` text, and no profile's instructions name a tool it does not expose —
advertising an absent tool is how an agent ends up calling something missing from its `tools/list`.

`minimal` and `basic` exist for the capability tiers above. `minimal` deliberately omits
`occam_client_capabilities`: declaring a context budget is useful, but on a one-tool surface it is
one more thing to get wrong, and `OCCAM_CLIENT_CONTEXT_TOKENS` does the same job from the operator
side.

Invalid values fall back to `reader` with a one-line `[occam.config]` warning on stderr.

---

## Client context budget (`OCCAM_CLIENT_CONTEXT_TOKENS`)

MCP does **not** tell servers the model's context window. Occam sizes extracts to the client in two ways:

| Mechanism | How |
|-----------|-----|
| Tool (preferred) | Call `occam_client_capabilities` once with `context_tokens` (the model knows its window) |
| Env (operator) | `OCCAM_CLIENT_CONTEXT_TOKENS` (1024–2000000); optional `OCCAM_CLIENT_MODEL_ID` label |

After configure, `occam_transcode` / `occam_digest` that **omit** `max_tokens` / `per_url_max_tokens` use ~**20%** of the context (clamped 512–16384). Explicit `max_tokens` always wins. Response includes `suggestedProfile` (`reader` / `researcher` / `full`) as advisory only — it does not change `OCCAM_PROFILE`.

### Batch

| Variable | Default | Purpose |
|----------|---------|---------|
| `OCCAM_BATCH_PORT` | `5051` | Batch HTTP server port |
| `OCCAM_BATCH_DB_PATH` | `~/.occam/jobs/jobs.db` | Job store |
| `OCCAM_BATCH_MAX_URLS` | `64` | Max URLs per submit |
| `OCCAM_BATCH_PARALLEL` | `4` | Concurrent transcodes (1–16) |

### Watch

| Variable | Default | Purpose |
|----------|---------|---------|
| `OCCAM_WATCH_DB_PATH` | `~/.occam/watch/watch.json` | Per-URL baseline store |

---

## Remote MCP (TLS + JWT) {#remote-mcp-tls-jwt}

| Variable | Default | Purpose |
|----------|---------|---------|
| `OCCAM_TLS_CERT_PATH` | — | TLS certificate (PFX or PEM) for `--remote` |
| `OCCAM_TLS_CERT_PASSWORD` | — | PFX password |
| `OCCAM_JWT_ISSUER` | `occam-mcp` | Expected JWT `iss` |
| `OCCAM_JWT_AUDIENCE` | `occam-mcp` | Expected JWT `aud` |
| `OCCAM_JWT_METADATA_URI` | issuer discovery | HTTPS OpenID Connect metadata document; its `jwks_uri` supplies rotating signing keys |
| `OCCAM_REMOTE_MAX_SESSIONS` | `4` | Concurrent authenticated WSS sessions (`1`–`32`) |
| `OCCAM_MCP_MAX_MESSAGE_BYTES` | `4194304` | Maximum local/remote WebSocket MCP text message (`65536`–`16777216`) |

Remote startup requires either an HTTPS `OCCAM_JWT_ISSUER` suitable for OpenID Connect discovery or
an explicit `OCCAM_JWT_METADATA_URI`. The deprecated `OCCAM_JWT_JWKS_URI` alias is still read as a
metadata-document URI; do not point it directly at raw JWKS JSON. Access tokens belong only in the
WebSocket handshake's `Authorization: Bearer` header. URI query tokens are rejected.

---

## Logging (stderr only)

| Variable | Fallback | Default | Purpose |
|----------|----------|---------|---------|
| `OCCAM_BANNER` | `WT_OCCAM_BANNER` | on | `0` hides startup banner |
| `OCCAM_LOG` | `WT_OCCAM_LOG` | off | `1` enables transcode profiler |

---

## Domain tiers (probe)

| Variable | Purpose |
|----------|---------|
| `OCCAM_DOMAIN_TIERS_PATH` | Extra domain tier JSON files |

---

## Install scripts (not read by host)

| Variable | Default / purpose |
|----------|-------------------|
| `OCCAM_REPO_URL` | Git remote — default public identity `https://github.com/ContextForgeAI/occam.git` |
| `OCCAM_REF` / `OCCAM_BRANCH` | Pin tag or branch |
| `OCCAM_INSTALL_DIR` | Install target directory |
| `OCCAM_RELEASE_URL` | Release tarball URL |
| `OCCAM_RELEASE_MANIFEST_URL` | Manifest with `sha256` |
| `OCCAM_RELEASE_BASE` | Release download base — default in `get-ff-occam.sh`: `https://github.com/ContextForgeAI/occam/releases/download/v<version>` |
| `OCCAM_RELEASE_BASE_URL` | Same for `@ff-occam/mcp` — `https://github.com/ContextForgeAI/occam/releases/download` (version appended by wrapper) |
| `OCCAM_GET_URL` | Raw `get-ff-occam.sh` URL — default `https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh` |
| `OCCAM_RELEASE_ALLOW_HTTP` | `1` — allow HTTP release URLs (non-default; HTTPS GitHub is preferred) |
| `OCCAM_RELEASES_API_URL` | Override for update-check — default `https://api.github.com/repos/ContextForgeAI/occam/releases/latest` (stable-only; set `OCCAM_LATEST_VERSION` during prerelease) |
| `OCCAM_HOST` | Onboard preset (`cursor`, `hermes`) |
| `OCCAM_CONFIG` | Onboard config path (`~/.occam/onboard.json`) |

---

## Example Cursor `env` block

```json
"env": {
  "OCCAM_HOME": "C:\\path\\to\\ff-occam",
  "OCCAM_BANNER": "0"
}
```
