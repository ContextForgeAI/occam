# CONFIG-NEGATIVE-SPACE (Wave 4 Phase 4F)

Independent, code-first enumeration of **every configuration input read from shipped code** (env vars, CLI switches, JSON keys, constants-as-gates), THEN compared to the Wave-1 `ENVIRONMENT-VARIABLES.md` catalog. Central orchestrator sweep; per-scope depth folded in from blind agents.

## Method

Repo-wide grep of `Environment.GetEnvironmentVariable`, `OccamEnvironment.Get*`, `*Var =` consts (C#) and `process.env.*` (workers). Result cross-referenced against `ENVIRONMENT-VARIABLES.md`.

## Headline: config catalog is largely COMPLETE

The Wave-1 env catalog already documents the overwhelming majority of code-read env vars, including internal-plumbing ones (`OCCAM_FEATURES`, `OCCAM_BROWSER_POOL_SLOT_ID`, `OCCAM_HTTP_OVERSIZE_MODE`) and legacy `WT_*` fallbacks. This is a **convergence signal** for the config surface — no major unexplained feature gate found centrally.

## Code-read config universe (C# host, from central sweep)

Gates / switches (feature-affecting):
- Opt-in MCP: `OCCAM_BATCH_MCP`, `OCCAM_WATCH_MCP`, `OCCAM_CONSENSUS_MCP`, `OCCAM_ATLAS_MCP` (`OccamMcpServerRegistration.cs`)
- Profile: `OCCAM_PROFILE` (`OccamToolProfile.cs`)
- Trust: `OCCAM_RECEIPTS` (`ReceiptsPolicy.cs` **and** duplicated in `ConsensusService.cs:117` — two independent parses), `OCCAM_TIME_ANCHOR`+`OCCAM_TSA_URL`+`OCCAM_TSA_TIMEOUT_MS`, `OCCAM_KEYS_ROOT`
- Providers/egress: `OCCAM_MANAGED_{PROVIDER,API_KEY,BASE_URL,DOMAINS,TIMEOUT_MS}`, `OCCAM_SEARCH_{PROVIDER,API_KEY,URL,TIMEOUT_MS}`, `OCCAM_TRANSLATE_{URL,API_KEY,TIMEOUT_MS}`, `OCCAM_{HTTP,HTTPS,NO}_PROXY`, `OCCAM_PROXY_LIST`/`_FILE`, `OCCAM_ROBOTS_TIMEOUT_MS`, `OCCAM_RESPECT_ROBOTS`, `OCCAM_HOST_THROTTLE_MS`
- Routing/genome: `OCCAM_DOMAIN_TIERS_PATH`, `OCCAM_SITE_GENOME_FETCH`, `OCCAM_PLAYBOOKS_LOCAL_ROOT`/`WT_PLAYBOOKS_PATH`
- Transport/remote: `OCCAM_TLS_{CERT_PATH,CERT_PASSWORD}`, `OCCAM_JWT_{ISSUER,AUDIENCE,METADATA_URI,JWKS_URI}`, `OCCAM_REMOTE_MAX_SESSIONS`, `OCCAM_MCP_MAX_MESSAGE_BYTES`
- Batch: `OCCAM_BATCH_{PORT,MAX_URLS,PARALLEL,DB_PATH}`; Watch: `OCCAM_WATCH_DB_PATH`; Cache: `OCCAM_CACHE_DIR`
- Browser/worker: `OCCAM_BROWSER_{CHANNEL,EXECUTABLE_PATH,PROFILE,DAEMON,POOL_SIZE,POOL_BASE_PORT,DAEMON_PORT,DAEMON_IDLE_TTL_MS,MAX_PARALLEL,TIMEOUT_MS,AUTOINSTALL,NODE_MAX_OLD_SPACE_MB,GOTO_TIMEOUT_MS}`, `OCCAM_CHROME_PATH`, `OCCAM_TIER_B`, `OCCAM_NODE_{BIN,MAX_OLD_SPACE_MB}`, `OCCAM_HTTP_DAEMON{,_PORT,_IDLE_TTL_MS,_SCRIPT,_PREWARM}`, `PLAYWRIGHT_BROWSERS_PATH`/`OCCAM_PLAYWRIGHT_BROWSERS_PATH`
- Digest: `OCCAM_DIGEST_PARALLEL`, `OCCAM_DIGEST_MAX_PARALLEL`; Chunk: `OCCAM_CHUNK_SIZE`; PDF: `OCCAM_MAX_PDF_BYTES`; body: `OCCAM_MAX_RESPONSE_BYTES`, `OCCAM_HTTP_OVERSIZE_MODE`
- Client: `OCCAM_CLIENT_CONTEXT_TOKENS`, `OCCAM_CLIENT_MODEL_ID`
- Identity/telemetry: `OCCAM_RUNTIME_ID`, `OCCAM_SESSION_ID`, `OCCAM_PARENT_{PID,LABEL}`, `OCCAM_OWNER_LABEL`, `OCCAM_HOME`, `OCCAM_LOG`/`WT_OCCAM_LOG`, `OCCAM_BANNER`/`WT_OCCAM_BANNER`, `WT_TOKEN_USD_PER_M`
- Privacy: `OCCAM_ALLOW_PRIVATE_URLS`
- Worker-only (`WT_*`): `WT_COOKIE_INJECT`, `WT_BROWSER_EXTRACT_VARIANT`, `WT_VIRTUAL_SCROLL`, `OCCAM_FEATURES`, `OCCAM_REQUEST_HEADERS_FILE`

## Candidate config gaps (to confirm with blind agents)

| Signal | Finding | Class |
|--------|---------|-------|
| `OCCAM_RECEIPTS` parsed twice | `ReceiptsPolicy.cs:12` AND `ConsensusService.cs:117` — two independent implementations of the same switch (drift risk) | MISSING_CONFIG (multi-switch-one-feature) — feeds EF |
| `OccamEnvironment.GetInt` clamp/parse-warn | On out-of-range/non-int, emits ONE `[occam.config]` stderr line and uses default/clamp — an **honest-config diagnostic capability** not captured as a CAP | MISSING_CAPABILITY (small) |
| `OCCAM_MANAGED_*` / `OCCAM_SEARCH_*` / `OCCAM_TRANSLATE_*` | Each is a full third-party-egress feature gated purely by presence of URL+key; confirm each documented as capability not just env row | verify COVERED |
| `OCCAM_BROWSER_MAX_PARALLEL` read at 3 sites | `BrowserPoolSettings`, `BrowserConcurrencyLimiter`, `BrowserConcurrencyGate` (one gate is Wave-1 dead) — one knob, 3 readers | COVERED_PARTIALLY |
| `OCCAM_DOMAIN_TIERS_PATH` | external file can extend routing tiers → silently changes backend selection | verify COVERED as routing capability |

## Status
Central sweep found **no major unexplained feature gate**. Deep per-scope config confirmations pending blind agents W4-A/B/D/E/F/G.
