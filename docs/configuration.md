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
| `OCCAM_NODE_BIN` | `PATH` / `{OCCAM_HOME}/bin/node` | Node executable |
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

---

## Browser and Playwright

| Variable | Default | Purpose |
|----------|---------|---------|
| `PLAYWRIGHT_BROWSERS_PATH` | Playwright cache | Standard browser cache path |
| `OCCAM_PLAYWRIGHT_BROWSERS_PATH` | — | Occam-specific override |
| `OCCAM_BROWSER_CHANNEL` | `chromium` | `chrome` \| `msedge` \| `chromium` |
| `OCCAM_BROWSER_AUTOINSTALL` | on | On first genuine browser need with no browser installed, occam provisions the user-level Chromium itself and reports `browser_provisioned`. Set `0` to instead return a typed `browser_required` failure to run manually. System libraries (root) are never auto-installed |
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
| `OCCAM_SEARCH_PROVIDER` | `searxng` \| `brave` \| `tavily` (unset → `search_unconfigured`) |
| `OCCAM_SEARCH_URL` | Required for SearXNG base URL |
| `OCCAM_SEARCH_API_KEY` | Required for Brave/Tavily |
| `OCCAM_SEARCH_TIMEOUT_MS` | Default `20000` (1k–120k) |

---

## Managed providers (optional escalation)

| Variable | Purpose |
|----------|---------|
| `OCCAM_MANAGED_PROVIDER` | `firecrawl` \| `jina` \| `spider` \| `scrapfly` |
| `OCCAM_MANAGED_API_KEY` | Provider API key |
| `OCCAM_MANAGED_BASE_URL` | Override provider base URL |
| `OCCAM_MANAGED_DOMAINS` | Comma-separated domain allowlist |
| `OCCAM_MANAGED_TIMEOUT_MS` | Default `60000` (1k–180k) |

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
| `OCCAM_DIGEST_PARALLEL` | on | `0` forces sequential |
| `OCCAM_DIGEST_MAX_PARALLEL` | — | Cap 1–8 parallel URL extracts |

---

## Opt-in MCP tools

| Variable | Registers |
|----------|-----------|
| `OCCAM_BATCH_MCP=1` | `occam_batch_submit`, `occam_batch_status`, `occam_batch_results` |
| `OCCAM_WATCH_MCP=1` | `occam_watch` |
| `OCCAM_CONSENSUS_MCP=1` | `occam_crosscheck` |
| `OCCAM_ATLAS_MCP=1` | `occam_failure_atlas` + per-host telemetry |

---

## Tool surface profile (`OCCAM_PROFILE`)

Narrows which **core** tools appear in `tools/list` (and in server instructions). Default **`full`** keeps all fifteen. Opt-in tools above are independent — still require their own flags.

| Value | Core tools exposed |
|-------|--------------------|
| `full` (default) | All fifteen |
| `reader` | `occam_client_capabilities`, `occam_transcode`, `occam_probe`, `occam_digest`, `occam_map`, `occam_search`, `occam_extract_knowledge` |
| `researcher` | reader + `occam_claim_check`, `occam_verify` |
| `auditor` | researcher + `occam_attest`, `occam_dataset_export`, `occam_playbook_lint` |

Invalid values fall back to `full` with a one-line `[occam.config]` warning on stderr. Use `researcher` (or `reader`) for agent hosts where a large tool set causes heal/save drift.

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

## Remote MCP (TLS + JWT)

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
  "OCCAM_HOME": "C:\\path\\to\\FFOccamMCP",
  "OCCAM_BANNER": "0"
}
```
