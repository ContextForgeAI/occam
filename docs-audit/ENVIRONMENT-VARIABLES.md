# Environment Variables — Exhaustive Code Audit (Wave 1, S24)

**Source of truth:** shipped executable code only — `src/FFOccamMcp.Core/**/*.cs`,
`workers/{http-extract,browser-extract,css-extract,shared}/**/*.mjs`, and the runtime-affecting
scripts explicitly in scope (`scripts/launch-mcp-host.mjs`, `scripts/occam-session.mjs` +
`scripts/lib/occam-sessions-lib.mjs`, `scripts/occam-connect.mjs`, plus the modules those three
scripts actually `import` at runtime: `scripts/lib/resolve-host-binary.mjs`,
`scripts/lib/host-install-gate.mjs`, `scripts/lib/operator/onboard-config.mjs`,
`scripts/lib/operator/onboard-schema.mjs`).

**Docs are UNTRUSTED and were NOT used to derive this list.** `docs/configuration.md` was read
only *after* the code enumeration below was complete, as the secondary cross-check the brief
allows. Every row below was found by grep for `Environment.GetEnvironmentVariable(`,
`OccamEnvironment.Get/GetFlag/GetInt/GetExistingFile(`, and `process.env.*` / `process.env[...]`
across the in-scope trees, followed by manual read of each call site.

`Documented?` column is **N/A-Wave1** everywhere per instructions (doc-comparison is out of
scope for this subagent) — except where the Wave-1 code enumeration and the (secondary,
post-hoc) `docs/configuration.md` pass visibly disagree; those are flagged `UNRESOLVED`.

Legend — **Scope**: `host` = read by `FFOccamMcp.Core` process directly · `worker` = read by a
spawned Node worker/daemon process · `script` = read only by an out-of-process launcher/installer
script · **Secret**: `Y` = credential/token, must never be logged or committed.

---

## 1. Paths, identity, and process wiring

| Variable | Code references | Type | Default | Effect | Scope | Secret | Example | Documented? |
|---|---|---|---|---|---|---|---|---|
| `OCCAM_HOME` | `Workers/WorkerPaths.cs:75`, `Lifecycle/HostIdentity.cs:272`, `Tools/OccamTranscodeTool.cs:110`, `Tools/OccamExtractKnowledgeTool.cs:36`, `workers/shared/lib/default-fetch-headers.mjs:14`, `workers/shared/lib/playbook-seed.mjs:60`, `workers/shared/lib/playbook-publish-sanitize.mjs:386`, `scripts/launch-mcp-host.mjs:23`, `scripts/occam-connect.mjs:20` | string (dir path) | auto-detected by walking up from `AppContext.BaseDirectory` / CWD | Install root — resolves worker `.mjs` scripts, `profiles/`, fetch-defaults JSON. Without it (and without a detectable repo root) → `workers_unavailable` | host + worker + script | N | `OCCAM_HOME=C:\ff-occam` | N/A-Wave1 |
| `OCCAM_HTTP_EXTRACT_SCRIPT` | `Workers/WorkerPaths.cs:12` | string (file path) | none (auto from `OCCAM_HOME`) | Override the HTTP extract worker entry script. Only takes effect when paired with `OCCAM_BROWSER_EXTRACT_SCRIPT` (both must be set) | host | N | `C:\custom\extract.mjs` | N/A-Wave1 |
| `OCCAM_BROWSER_EXTRACT_SCRIPT` | `Workers/WorkerPaths.cs:13` | string (file path) | none (auto) | Override the browser extract worker entry script (paired requirement, see above) | host | N | — | N/A-Wave1 |
| `OCCAM_DOM_SKELETON_SCRIPT` | `Workers/DomSkeletonWorker.cs:165` | string (file path) | auto (`{OCCAM_HOME}/workers/browser-extract/dom-skeleton-capture.mjs`) | Override the DOM-skeleton capture script used by `occam_playbook_heal` | host | N | — | N/A-Wave1 |
| `OCCAM_NODE_BIN` | `Workers/NodeRuntime.cs:8` | string (file path) | `PATH` search for `node`, else `{OCCAM_HOME}/bin/node` | Node executable used to spawn all workers/daemons | host | N | `/usr/local/bin/node` | N/A-Wave1 |
| `OCCAM_FORCE_DOTNET_RUN` | `scripts/launch-mcp-host.mjs:80` | flag (`"1"`) | off | Launcher uses `dotnet run --project … -c Release` instead of the AOT-published binary (dev-only path; requires .NET 10 SDK, else launcher exits 1) | script only | N | `1` | N/A-Wave1 |
| `OCCAM_RUNTIME_ID` | `Lifecycle/HostIdentity.cs:16` (`RuntimeId.FromEnvironmentOrNew`), `scripts/launch-mcp-host.mjs:27,32` | string | random `rt-<guid>` | Stable per-process runtime identity (INV-10) for identity diagnostics; launcher stamps a fresh one into the child's env if unset | host + script | N | `rt-abc123` | N/A-Wave1 |
| `OCCAM_SESSION_ID` | `Lifecycle/HostIdentity.cs:28` (`SessionId.FromEnvironmentOrNew`), `scripts/launch-mcp-host.mjs:28,33` | string | random `sess-<guid>` | Client/session correlation id for multi-profile coexistence | host + script | N | `sess-abc123` | N/A-Wave1 |
| `OCCAM_PARENT_PID` | `Lifecycle/HostIdentity.cs:266`, `scripts/launch-mcp-host.mjs:34` | int | `0` (none) | Diagnostic-only: records launcher's own PID as the host's parent | host + script | N | `12345` | N/A-Wave1 |
| `OCCAM_PARENT_LABEL` | `Lifecycle/HostIdentity.cs:295`, `scripts/launch-mcp-host.mjs:35` | string | none / `"launch-mcp-host"` from script | Diagnostic-only free-text label for the launching process | host + script | N | `cursor-ide` | N/A-Wave1 |
| `OCCAM_OWNER_LABEL` | `Lifecycle/HostIdentity.cs:296` | string | none | Ownership metadata (host-agnostic "who owns desired state") — diagnostic only | host | N | — | N/A-Wave1 |
| `OCCAM_CONFIG` | `scripts/lib/operator/onboard-schema.mjs:16` (`defaultOnboardPath`) | string (file path) | `~/.occam/onboard.json` | Path to the onboard config file whose `env` object is merged into the launched host's environment (`mergeOnboardEnv`, explicit host env wins over file) | script only (launcher chain) | N | `/etc/occam/onboard.json` | N/A-Wave1 |
| `OCCAM_GET_URL` | `scripts/lib/host-install-gate.mjs:120` | string (URL) | `https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh` | Overrides the install-script URL shown in the "AOT binary not found" doctor/launcher error message. Display-only, not fetched by this code path | script only (error-message text) | N | — | N/A-Wave1 |

## 2. Sessions, headers, privacy

| Variable | Code references | Type | Default | Effect | Scope | Secret | Example | Documented? |
|---|---|---|---|---|---|---|---|---|
| `OCCAM_SESSIONS_ROOT` | `Session/SessionProfileHeaders.cs:122`, `scripts/lib/occam-sessions-lib.mjs:10` | string (dir path) | `~/.occam/sessions/` | Directory holding `<id>.json` session profiles (cookies/headers/storage-state) referenced by `session_profile` params | host + script | N (dir itself is not secret; **files inside may contain session cookies — secret**) | `~/.occam/sessions` | N/A-Wave1 |
| `OCCAM_REQUEST_HEADERS_FILE` | `Session/RequestHeadersMerger.cs:37`, `Workers/DomSkeletonWorker.cs:26`, `workers/browser-extract/browser-daemon.mjs:64,107`, `workers/http-extract/http-daemon.mjs:81` | string (file path) | none | JSON file of extra HTTP headers merged into every worker request (session-profile headers win on clash). Propagates to daemons via normal OS env inheritance (host does not explicitly re-inject it into `psi.Environment`) | host + worker | Y (headers may include auth tokens) | `/secure/headers.json` | N/A-Wave1 |
| `OCCAM_ALLOW_PRIVATE_URLS` | `Routing/PrivacyClassifier.cs:7`, `workers/shared/lib/private-ip.mjs:93,101`, referenced in `Routing/OutboundHttpGuard.cs:14` doc comment | flag (`"1"`) | off (private/localhost URLs blocked) | `1` allows fetching localhost/RFC1918/link-local targets — local-dev-only escape hatch for the SSRF guard | host + worker | N | `1` | N/A-Wave1 |

## 3. Browser backend / Playwright

| Variable | Code references | Type | Default | Effect | Scope | Secret | Example | Documented? |
|---|---|---|---|---|---|---|---|---|
| `PLAYWRIGHT_BROWSERS_PATH` | `Workers/PlaywrightEnvironment.cs:19,42`, `Services/FeatureDiscoveryService.cs:22` | string (dir path) | Playwright's own default cache dir | Standard Playwright browser-binary cache location; host inherits/propagates it to worker `ProcessStartInfo` when it already contains a Chromium install | host + worker | N | — | N/A-Wave1 |
| `OCCAM_PLAYWRIGHT_BROWSERS_PATH` | `Workers/PlaywrightEnvironment.cs:47` | string (dir path) | none (falls back to OS-specific default: `%LOCALAPPDATA%\ms-playwright` on Windows, `~/.cache/ms-playwright` / `~/Library/Caches/ms-playwright` elsewhere) | Occam-specific override for where to look for/report the Chromium cache | host | N | — | N/A-Wave1 |
| `OCCAM_BROWSER_CHANNEL` | `Cli/OccamCliVerbs.cs:75`, `workers/browser-extract/lib/browser-launch-options.mjs:59,106` | string enum | `chromium` | Playwright browser channel: `chrome` \| `msedge` \| `chromium` | host (CLI) + worker | N | `chrome` | N/A-Wave1 |
| `OCCAM_BROWSER_EXECUTABLE_PATH` | `Cli/OccamCliVerbs.cs:69`, `workers/browser-extract/lib/browser-launch-options.mjs:53,100` | string (file path) | none | Absolute path to a system browser binary (skips Chromium download) | host (CLI) + worker | N | `/usr/bin/chromium` | N/A-Wave1 |
| `OCCAM_CHROME_PATH` | `Cli/OccamCliVerbs.cs:72`, `workers/browser-extract/lib/browser-launch-options.mjs:54,101` | string (file path) | none | Alias for `OCCAM_BROWSER_EXECUTABLE_PATH` (checked second) | host (CLI) + worker | N | `/usr/bin/google-chrome` | N/A-Wave1 |
| `OCCAM_BROWSER_AUTOINSTALL` | `workers/browser-extract/lib/browser-provision.mjs:16` | flag (`"0"` disables) | on | When no browser is found, worker auto-provisions a user-level Chromium (`browser_provisioned`); `0` instead returns typed `browser_required` failure | worker | N | `0` | N/A-Wave1 |
| `OCCAM_BROWSER_PROFILE` | `Workers/BrowserExecutionProfile.cs:18` | string enum | `shared` (aliases: `daemon`,`lean` = shared; `isolated`,`parallel`,`throughput` = isolated) | Selects browser execution mode: long-lived shared daemon vs. fresh isolated worker per extract | host | N | `isolated` | N/A-Wave1 |
| `OCCAM_BROWSER_DAEMON` | `Workers/BrowserExecutionProfile.cs:32`, `Workers/BrowserPoolSettings.cs:47` | flag (`"0"` disables) | on | `0` forces isolated one-shot browser extracts (overrides `OCCAM_BROWSER_PROFILE`'s shared default) | host | N | `0` | N/A-Wave1 |
| `OCCAM_BROWSER_POOL_SIZE` | `Workers/BrowserPoolSettings.cs:8,23` (`PoolSizeVar`) | int, clamp 1–8 | `1` | Number of daemon pool slots (parallel persistent browser contexts) | host | N | `2` | N/A-Wave1 |
| `OCCAM_BROWSER_POOL_BASE_PORT` | `Workers/BrowserPoolSettings.cs:9,24` (`BasePortVar`) | int, clamp 1024–65535 | `39217` | Base TCP port; slot *N* listens on `BasePort + N` | host | N | `39300` | N/A-Wave1 |
| `OCCAM_BROWSER_DAEMON_PORT` | `Workers/BrowserPoolSettings.cs:10,35` (`DaemonPortVar`), `workers/browser-extract/browser-daemon.mjs:7` | int | `39217` | Legacy single-slot port override — only honored when `OCCAM_BROWSER_POOL_SIZE == 1` | host + worker | N | `39217` | N/A-Wave1 |
| `OCCAM_BROWSER_DAEMON_IDLE_TTL_MS` | `Workers/BrowserPoolSettings.cs:11,25` (`IdleTtlVar`) | int ms, clamp 0–3,600,000 | `120000` | Idle shutdown timer for the browser daemon; `0` = always warm | host | N | `0` | N/A-Wave1 |
| `OCCAM_BROWSER_MAX_PARALLEL` | `Workers/BrowserPoolSettings.cs:12,26` (`MaxParallelVar`), `Workers/BrowserConcurrencyLimiter.cs:40`, `Workers/BrowserConcurrencyGate.cs:28` | int, clamp 1–16 (fallback `WT_BROWSER_MAX_PARALLEL`) | `2` | Max concurrent browser extracts (gates `occam_digest` parallelism and daemon queue-wait sizing) | host | N | `4` | N/A-Wave1 |
| `WT_BROWSER_MAX_PARALLEL` | same 3 call sites as above (fallback param) | int | — | Legacy-name fallback for `OCCAM_BROWSER_MAX_PARALLEL`, only read when the `OCCAM_` name is unset | host | N | — | N/A-Wave1 |
| `OCCAM_BROWSER_TIMEOUT_MS` | `Workers/BrowserExtractTimeouts.cs:23` | int ms, clamp 15,000–180,000 | `60000` | Per-page browser extract timeout; daemon queue-wait timeout = this × concurrency slots (capped 900,000 ms) | host | N | `90000` | N/A-Wave1 |
| `OCCAM_BROWSER_DAEMON_SCRIPT` | `Workers/BrowserPoolManager.cs:400` | string (file path) | auto (`{OCCAM_HOME}/workers/browser-extract/browser-daemon.mjs`) | Override the browser-daemon entry script | host | N | — | N/A-Wave1 |
| `OCCAM_BROWSER_NODE_MAX_OLD_SPACE_MB` | `Workers/NodeLaunchArguments.cs:19` | int MB, clamp 64–1024 | `512` | V8 `--max-old-space-size` for the **browser** worker/daemon process | host | N | `768` | N/A-Wave1 |
| `OCCAM_NODE_MAX_OLD_SPACE_MB` | `Workers/NodeLaunchArguments.cs:24` | int MB, clamp 64–1024 | `512` | V8 `--max-old-space-size` for the **HTTP** worker/daemon process | host | N | `768` | N/A-Wave1 |
| `OCCAM_BROWSER_POOL_SLOT_ID` | written by host `Workers/BrowserPoolManager.cs:311` (`psi.Environment["OCCAM_BROWSER_POOL_SLOT_ID"]`); read by `workers/browser-extract/browser-daemon.mjs:39` | int | none | **Internal plumbing** — host stamps the pool slot index into the daemon's own env so its `/health` endpoint can report `slot_id`. Not operator-facing | worker (host-injected) | N | — | N/A-Wave1 |
| `OCCAM_TIER_B` | `workers/browser-extract/lib/browser-session.mjs:412` | flag (`"1"`) | off | Enables a tightened goto-timeout ceiling (see `OCCAM_BROWSER_GOTO_TIMEOUT_MS`) for a stricter latency tier | worker | N | `1` | N/A-Wave1 |
| `OCCAM_BROWSER_GOTO_TIMEOUT_MS` | `workers/browser-extract/lib/browser-session.mjs:416` | int ms | `20000` (only consulted when `OCCAM_TIER_B=1`) | Caps the per-page `goto()` timeout when Tier-B mode is active | worker | N | `15000` | N/A-Wave1 |
| `OCCAM_FEATURES` | Written by host: `Workers/HttpExtractRunner.cs:97`, `Workers/BrowserExtractRunner.cs:136`; read by worker: `workers/browser-extract/lib/browser-session.mjs:428,660`, `workers/browser-extract/browser-extract.mjs:67`, `workers/shared/lib/plugins-runner.mjs:15`, `workers/http-extract/lib/http-extract-run.mjs:38` | CSV string | `""` | **Internal plumbing** — host serializes the per-call `json_blocks`/`json_tables` tool params into this env var when spawning the worker, so the worker's plugin runner knows which optional post-processing plugins to run. Not meant to be set manually by an operator (though it would work if set globally) | worker (host-injected; per-call) | N | `json_blocks,json_tables` | N/A-Wave1 |
| `WT_COOKIE_INJECT` | `workers/browser-extract/lib/cookie-inject.mjs:4`, set by `workers/browser-extract/browser-extract.mjs:29` and `workers/browser-extract/lib/browser-extract-run.mjs:21` (`options.cookieInject` → `process.env.WT_COOKIE_INJECT = "1"`) | flag (`"1"`/`"true"`/`"yes"`) | off | Opt-in pre-goto cookie injection from a matched recipe (privacy-reviewed per domain) | worker (self-set from CLI/API option, not typically operator env) | N | `1` | N/A-Wave1 |
| `WT_BROWSER_EXTRACT_VARIANT` | `workers/browser-extract/lib/browser-extract-run.mjs:29` | string enum | `css-hide` (via `parseExtractVariant` default) | Selects the browser extract DOM-reduction variant (`baseline`/`css-hide`/`strip-chrome`/`strip-consent`) when not supplied via recipe/option | worker | N | `baseline` | N/A-Wave1 |
| `WT_VIRTUAL_SCROLL` | `workers/browser-extract/lib/virtual-scroll.mjs:314` | flag (`"1"` on, anything else off after lowercasing) | `"1"` (on) | Enables/disables infinite-scroll simulation during browser extract | worker | N | `0` | N/A-Wave1 |

## 4. HTTP backend / daemon

| Variable | Code references | Type | Default | Effect | Scope | Secret | Example | Documented? |
|---|---|---|---|---|---|---|---|---|
| `OCCAM_HTTP_DAEMON` | `Workers/HttpDaemonHost.cs:20` | flag (`"0"` disables) | on | `0` forces a fresh one-shot HTTP worker process per request instead of the persistent daemon | host | N | `0` | N/A-Wave1 |
| `OCCAM_HTTP_DAEMON_PORT` | `Workers/HttpDaemonHost.cs:23`, `workers/http-extract/http-daemon.mjs:6` | int | `39218` | HTTP daemon listen port | host + worker | N | `39300` | N/A-Wave1 |
| `OCCAM_HTTP_DAEMON_IDLE_TTL_MS` | `Workers/HttpDaemonHost.cs:28` | int ms | `120000` | Idle shutdown timer for the HTTP daemon | host | N | `0` | N/A-Wave1 |
| `OCCAM_HTTP_DAEMON_SCRIPT` | `Workers/HttpDaemonHost.cs:182` | string (file path) | auto (`{OCCAM_HOME}/workers/http-extract/http-daemon.mjs`) | Override the HTTP daemon entry script | host | N | — | N/A-Wave1 |
| `OCCAM_HTTP_DAEMON_PREWARM` | `Transport/OccamMcpServerRegistration.cs:46` (`OccamEnvironment.GetFlag`) | flag (`"0"` disables) | on | Background-prewarms the HTTP daemon at MCP server startup so the first real `occam_transcode` is not the cold-start call | host | N | `0` | N/A-Wave1 |

## 5. Response size, PDF, politeness

| Variable | Code references | Type | Default | Effect | Scope | Secret | Example | Documented? |
|---|---|---|---|---|---|---|---|---|
| `OCCAM_MAX_RESPONSE_BYTES` | `workers/shared/lib/response-body-cap.mjs:10,29` | int bytes, clamp 65,536–16,777,216 | `8388608` (8 MiB) | HTTP body read cap; exceeding it raises `response_too_large` (or truncates, see next var) | worker | N | `4194304` | N/A-Wave1 |
| `OCCAM_HTTP_OVERSIZE_MODE` | Written by host: `Workers/HttpExtractRunner.cs:92` (`psi.Environment["OCCAM_HTTP_OVERSIZE_MODE"] = options.OversizeMode`); read by worker: `workers/shared/lib/response-body-cap.mjs:11,44` | string enum | `fail` | `fail` hard-fails oversize bodies; `partial` truncates to a safe HTML boundary and returns truncated markdown. **Host plumbs this in per-call from the `on_oversize` tool param** — not typically an operator-set global | host (writes) + worker (reads) | N | `partial` | N/A-Wave1 |
| `OCCAM_MAX_PDF_BYTES` | `workers/http-extract/lib/http-extract-run.mjs:428` | int bytes, clamp 65,536–134,217,728 | `16777216` (16 MiB) | PDF body cap (separate, larger ceiling than the general HTML cap) | worker | N | `33554432` | N/A-Wave1 |
| `OCCAM_RESPECT_ROBOTS` | `Services/RobotsThrottleService.cs:31` (`OccamEnvironment.GetFlag`) | flag | off | `true`/`1` enforces `robots.txt` `Disallow` rules (fetch returns `robots_disallowed`) | host | N | `1` | N/A-Wave1 |
| `OCCAM_HOST_THROTTLE_MS` | `Services/RobotsThrottleService.cs:32` (`OccamEnvironment.GetInt`) | int ms, clamp 0–600,000 | `0` | Minimum interval enforced between requests to the same host (politeness throttle) | host | N | `1000` | N/A-Wave1 |
| `OCCAM_ROBOTS_TIMEOUT_MS` | `Composition/OccamServiceCollectionExtensions.cs:96` (`OccamEnvironment.GetInt`) | int ms, clamp 1,000–60,000 | `10000` | HTTP client timeout for fetching `robots.txt` | host | N | — | N/A-Wave1 |

## 6. Cache, receipts, time anchor

| Variable | Code references | Type | Default | Effect | Scope | Secret | Example | Documented? |
|---|---|---|---|---|---|---|---|---|
| `OCCAM_CACHE_DIR` | `Caching/TranscodeResponseCache.cs:39` | string (dir path) | `{TEMP}/occam-cache` | Directory for the opt-in on-disk transcode response cache (`cache_ttl_s` hits); one JSON file per key | host | N | `/var/cache/occam` | N/A-Wave1 |
| `OCCAM_RECEIPTS` | `Receipts/ReceiptsPolicy.cs:12`, `Consensus/ConsensusService.cs:117` | flag (`off`/`0`/`false` disables) | on | Master switch for Receipt v1 signing across transcode/digest/claim-check/dataset/crosscheck paths | host | N | `off` | N/A-Wave1 |
| `OCCAM_KEYS_ROOT` | `Receipts/ReceiptSigner.cs:81` | string (dir path) | `~/.occam/keys/` | Directory for the ECDSA P-256 signing key pair (generated on first use) | host | Y (private key material lives here) | `/secure/occam/keys` | N/A-Wave1 |
| `OCCAM_TIME_ANCHOR` | `Receipts/TimeAnchorService.cs:18,23` (local `Flag()` helper, accepts `1`/`true`/`on`) | flag | off | Enables requesting an RFC3161 timestamp token from a TSA over each receipt's signature (SI-15) | host | N | `1` | N/A-Wave1 |
| `OCCAM_TSA_URL` | `Receipts/TimeAnchorService.cs:78` | string (URL) | none (required when `OCCAM_TIME_ANCHOR=1`) | Timestamp Authority endpoint; SSRF-guarded (private hosts rejected) | host | N | `https://freetsa.org/tsr` | N/A-Wave1 |
| `OCCAM_TSA_TIMEOUT_MS` | `Receipts/TimeAnchorService.cs:39-40`, `Composition/OccamServiceCollectionExtensions.cs:74` (both via `OccamEnvironment.GetInt`) | int ms, clamp 500–15,000 | `3000` | TSA round-trip timeout; fail-open (no anchor) on timeout | host | N | — | N/A-Wave1 |

## 7. Playbooks / genome

| Variable | Code references | Type | Default | Effect | Scope | Secret | Example | Documented? |
|---|---|---|---|---|---|---|---|---|
| `OCCAM_PLAYBOOKS_LOCAL_ROOT` | `Playbooks/PlaybookPaths.cs:17`, `workers/shared/lib/playbook-seed.mjs:72` | string (dir path) | `~/.occam/playbooks/local/` | Local "learn" tier directory for `occam_playbook_save`/heal-learn loop | host + worker | N | — | N/A-Wave1 |
| `WT_PLAYBOOKS_PATH` | `Playbooks/PlaybookPaths.cs:33`, `workers/shared/lib/playbook-seed.mjs:80` | string (dir path) | none | User/org playbook tier (checked ahead of community/seed tiers) | host + worker | N | — | N/A-Wave1 |
| `OCCAM_SITE_GENOME_FETCH` | `Playbooks/PlaybookResolveOptions.cs:16` | flag (`"1"`/`"true"`) | off | Enables fetching a site's published `.well-known` genome manifest during `occam_playbook_resolve` even when the `fetch_site_genome` tool param is not passed | host | N | `1` | N/A-Wave1 |
| `OCCAM_DOMAIN_TIERS_PATH` | `Routing/DomainTierRegistry.cs:315` | string (path list, OS path-separator delimited) | none (built-in `profiles/tiers/domain-tier.v1.json` always loaded first) | Extra domain-tier JSON config file(s) merged on top of the built-in tier map | host | N | `/etc/occam/tiers.json` | N/A-Wave1 |

## 8. Search / managed escalation / translation

| Variable | Code references | Type | Default | Effect | Scope | Secret | Example | Documented? |
|---|---|---|---|---|---|---|---|---|
| `OCCAM_SEARCH_PROVIDER` | `Services/SearchService.cs:50` | string enum | none (unset → `search_unconfigured`) | Selects `occam_search` backend: `searxng` \| `brave` \| `tavily` | host | N | `brave` | N/A-Wave1 |
| `OCCAM_SEARCH_URL` | `Services/SearchService.cs:41,63` | string (URL) | none | Required base URL for the `searxng` provider | host | N | `https://searx.example.com` | N/A-Wave1 |
| `OCCAM_SEARCH_API_KEY` | `Services/SearchService.cs:42,57` | string | none | Required API key for `brave`/`tavily` providers | host | Y | — | N/A-Wave1 |
| `OCCAM_SEARCH_TIMEOUT_MS` | `Composition/OccamServiceCollectionExtensions.cs:90` | int ms, clamp 1,000–120,000 | `20000` | HTTP client timeout for the search provider call | host | N | — | N/A-Wave1 |
| `OCCAM_MANAGED_PROVIDER` | `Backends/ManagedExtractBackend.cs:50` | string enum | none (disabled) | Names a managed-escalation extract provider: `firecrawl` \| `jina` \| `spider` \| `scrapfly` | host | N | `firecrawl` | N/A-Wave1 |
| `OCCAM_MANAGED_API_KEY` | `Backends/ManagedExtractBackend.cs:41,57` | string | none | Provider API key (required when the selected provider needs one) | host | Y | — | N/A-Wave1 |
| `OCCAM_MANAGED_BASE_URL` | `Backends/ManagedExtractBackend.cs:42` | string (URL) | provider default | Override the managed provider's base URL | host | N | — | N/A-Wave1 |
| `OCCAM_MANAGED_DOMAINS` | `Backends/ManagedExtractBackend.cs:72` | CSV string of domains | none (any host eligible) | Per-domain allowlist restricting which hosts may escalate to the managed provider | host | N | `example.com,news.example` | N/A-Wave1 |
| `OCCAM_MANAGED_TIMEOUT_MS` | `Composition/OccamServiceCollectionExtensions.cs:83` | int ms, clamp 1,000–180,000 | `60000` | HTTP client timeout for the managed provider call | host | N | — | N/A-Wave1 |
| `OCCAM_TRANSLATE_URL` | `Services/TranslationService.cs:30` | string (URL) | none (disabled) | Self-hosted/managed LibreTranslate endpoint base URL | host | N | `https://translate.example.com` | N/A-Wave1 |
| `OCCAM_TRANSLATE_API_KEY` | `Services/TranslationService.cs:59` | string | none | Optional LibreTranslate API key | host | Y | — | N/A-Wave1 |
| `OCCAM_TRANSLATE_TIMEOUT_MS` | `Composition/OccamServiceCollectionExtensions.cs:81` | int ms, clamp 1,000–120,000 | `20000` | HTTP client timeout for the translation call | host | N | — | N/A-Wave1 |

## 9. Egress proxy

| Variable | Code references | Type | Default | Effect | Scope | Secret | Example | Documented? |
|---|---|---|---|---|---|---|---|---|
| `OCCAM_HTTP_PROXY` | `Workers/EgressProxyConfig.cs:11,99`, `workers/shared/lib/egress-proxy.mjs:1,20` | string (proxy URL) | none | Static forward proxy for `http://` targets; host copies it into worker `ProcessStartInfo.Environment` on spawn | host (propagates) + worker (reads) | Y (may embed proxy credentials in userinfo) | `http://user:pass@10.0.0.5:3128` | N/A-Wave1 |
| `OCCAM_HTTPS_PROXY` | `Workers/EgressProxyConfig.cs:12,99`, `workers/shared/lib/egress-proxy.mjs:2,21` | string (proxy URL) | falls back to `OCCAM_HTTP_PROXY` | Static forward proxy for `https://` targets | host (propagates) + worker (reads) | Y | — | N/A-Wave1 |
| `OCCAM_NO_PROXY` | `Workers/EgressProxyConfig.cs:13,99`, `workers/shared/lib/egress-proxy.mjs:3,25` | CSV string | none | Bypass list (exact host / `.suffix` / `*.suffix` / `*`) for the proxy | host (propagates) + worker (reads) | N | `localhost,127.0.0.1` | N/A-Wave1 |
| `OCCAM_PROXY_LIST` | `Services/ProxyRotationSettings.cs:6` (`ProxyListVar`), consumed by `Services/ProxyListParser.cs:16` | CSV/multiline string of proxy URLs | none | Inline rotating proxy pool for one-shot worker spawns (rotation requires isolated workers — disables HTTP/browser daemons when active) | host | Y (may embed credentials) | `http://p1:8080,http://p2:8080` | N/A-Wave1 |
| `OCCAM_PROXY_LIST_FILE` | `Services/ProxyRotationSettings.cs:7` (`ProxyListFileVar`), consumed by `Services/ProxyListParser.cs:10` | string (file path) | none | File-based proxy pool (URL-per-line or scraper CSV export); wins over `OCCAM_PROXY_LIST` when both set | host | Y (file contents) | `/etc/occam/proxies.csv` | N/A-Wave1 |

## 10. Digest, batch, watch

| Variable | Code references | Type | Default | Effect | Scope | Secret | Example | Documented? |
|---|---|---|---|---|---|---|---|---|
| `OCCAM_DIGEST_PARALLEL` | `Services/DigestParallelism.cs:18` | flag (`"0"` disables) | on | `0` forces `occam_digest` to process URLs sequentially | host | N | `0` | N/A-Wave1 |
| `OCCAM_DIGEST_MAX_PARALLEL` | `Services/DigestParallelism.cs:23` | int, clamp 1–`DigestService.MaxUrlsCap` | policy-dependent (4 for HTTP, browser-concurrency-limit for browser/mixed) | Explicit cap on concurrent per-URL transcodes inside one `occam_digest` call | host | N | `8` | N/A-Wave1 |
| `OCCAM_BATCH_MCP` | `Transport/OccamMcpServerRegistration.cs:122` (`OccamEnvironment.GetFlag`) | flag | off | Registers `occam_batch_submit`/`occam_batch_status`/`occam_batch_results` + background job processor (opt-in async batch) | host | N | `1` | N/A-Wave1 |
| `OCCAM_BATCH_PORT` | `Batch/BatchSettings.cs:11` | int, clamp 1–65535 | `5051` | Experimental batch HTTP server port | host | N | — | N/A-Wave1 |
| `OCCAM_BATCH_MAX_URLS` | `Batch/BatchSettings.cs:16` | int, clamp 1–256 | `64` | Max URLs accepted per `occam_batch_submit` call | host | N | `128` | N/A-Wave1 |
| `OCCAM_BATCH_PARALLEL` | `Batch/BatchSettings.cs:21` | int, clamp 1–16 | `4` | Concurrent transcodes processed by the batch job processor | host | N | `8` | N/A-Wave1 |
| `OCCAM_BATCH_DB_PATH` | `Batch/BatchSettings.cs:29` | string (file path) | `~/.occam/jobs/jobs.db` | Batch job store location | host | N | — | N/A-Wave1 |
| `OCCAM_WATCH_MCP` | `Transport/OccamMcpServerRegistration.cs:134` | flag | off | Registers the opt-in `occam_watch` stateful page-change tool | host | N | `1` | N/A-Wave1 |
| `OCCAM_WATCH_DB_PATH` | `Watch/WatchStore.cs:32` | string (file path) | `~/.occam/watch/watch.json` | Last-seen JSON store backing `occam_watch` | host | N | — | N/A-Wave1 |
| `OCCAM_CONSENSUS_MCP` | `Transport/OccamMcpServerRegistration.cs:142` | flag | off | Registers the opt-in `occam_crosscheck` (SI-14 cloaking/consensus) tool | host | N | `1` | N/A-Wave1 |
| `OCCAM_ATLAS_MCP` | `Transport/OccamMcpServerRegistration.cs:149` | flag | off | Registers the opt-in `occam_failure_atlas` tool + swaps in a per-host telemetry sink | host | N | `1` | N/A-Wave1 |

## 11. Tool surface / client capability

| Variable | Code references | Type | Default | Effect | Scope | Secret | Example | Documented? |
|---|---|---|---|---|---|---|---|---|
| `OCCAM_PROFILE` | `Transport/OccamToolProfile.cs:44` | string enum | `full` | Narrows the exposed **core** tool surface: `full` \| `reader` \| `researcher` \| `auditor`. Invalid values fall back to `full` with a stderr warning | host | N | `reader` | N/A-Wave1 |
| `OCCAM_CLIENT_CONTEXT_TOKENS` | `Client/ClientCapabilityStore.cs:95` | int, clamp `MinContextTokens`–`MaxContextTokens` | none (unconfigured) | Operator-level fallback for the client's model context window (normally set once via the `occam_client_capabilities` tool instead); sizes default `max_tokens`/`per_url_max_tokens` to ~20% of context when omitted | host | N | `128000` | N/A-Wave1 |
| `OCCAM_CLIENT_MODEL_ID` | `Client/ClientCapabilityStore.cs:108` | string | none | Optional free-text model id label attached to the env-sourced capability snapshot | host | N | `gpt-5.6` | N/A-Wave1 |

## 12. Remote MCP transport (TLS + JWT)

| Variable | Code references | Type | Default | Effect | Scope | Secret | Example | Documented? |
|---|---|---|---|---|---|---|---|---|
| `OCCAM_TLS_CERT_PATH` | `Transport/RemoteMcpAuthOptions.cs:25`, `Transport/OccamMcpCli.cs:184` | string (file path) | none | PFX/PEM TLS certificate for `--remote` WebSocket transport (CLI flag can override) | host | Y (cert file may hold private key) | — | N/A-Wave1 |
| `OCCAM_TLS_CERT_PASSWORD` | `Transport/RemoteMcpAuthOptions.cs:26`, `Transport/OccamMcpCli.cs:185` | string | none | PFX password (null for PEM) | host | Y | — | N/A-Wave1 |
| `OCCAM_JWT_ISSUER` | `Transport/RemoteMcpAuthOptions.cs:27`, `Transport/OccamMcpCli.cs:186` | string | `occam-mcp` | Expected JWT `iss` claim for remote auth; must be an HTTPS URI for OpenID Connect discovery unless `OCCAM_JWT_METADATA_URI` is set | host | N | `https://auth.example.com` | N/A-Wave1 |
| `OCCAM_JWT_AUDIENCE` | `Transport/RemoteMcpAuthOptions.cs:28`, `Transport/OccamMcpCli.cs:187` | string | `occam-mcp` | Expected JWT `aud` claim | host | N | — | N/A-Wave1 |
| `OCCAM_JWT_METADATA_URI` | `Transport/RemoteMcpAuthOptions.cs:29`, `Transport/OccamMcpCli.cs:189` | string (URL) | none (derived from issuer discovery) | Explicit OpenID Connect metadata document URI (overrides issuer-based discovery) | host | N | — | N/A-Wave1 |
| `OCCAM_JWT_JWKS_URI` | `Transport/RemoteMcpAuthOptions.cs:30`, `Transport/OccamMcpCli.cs:190` | string (URL) | none | Deprecated alias, still read as a fallback metadata-document URI (not raw JWKS JSON) | host | N | — | N/A-Wave1 |
| `OCCAM_REMOTE_MAX_SESSIONS` | `Transport/RemoteMcpAuthOptions.cs:21,43` (`MaxSessionsVariable`, via `OccamEnvironment.GetInt`) | int, clamp 1–32 | `4` | Max concurrent authenticated WSS sessions for remote transport | host | N | `8` | N/A-Wave1 |
| `OCCAM_MCP_MAX_MESSAGE_BYTES` | `Transport/WebSocketMcpStreams.cs:119,122` (`McpWebSocketLimits.MaxMessageBytesVariable`, via `OccamEnvironment.GetInt`) | int bytes, clamp 65,536–16,777,216 | `4194304` (4 MiB) | Max size for a single local/remote WebSocket MCP JSON-RPC text message | host | N | — | N/A-Wave1 |

## 13. Logging / telemetry

| Variable | Code references | Type | Default | Effect | Scope | Secret | Example | Documented? |
|---|---|---|---|---|---|---|---|---|
| `OCCAM_BANNER` | `Telemetry/OccamLogger.cs:181` (`OccamEnvironment.GetFlag`, fallback `WT_OCCAM_BANNER`) | flag (`"0"`/`"false"` disables) | on | Suppresses the stderr startup banner | host | N | `0` | N/A-Wave1 |
| `WT_OCCAM_BANNER` | same call site (fallback param) | flag | — | Legacy-name fallback for `OCCAM_BANNER` | host | N | — | N/A-Wave1 |
| `OCCAM_LOG` | `Telemetry/OccamLogger.cs:201` (`OccamEnvironment.GetFlag`, fallback `WT_OCCAM_LOG`) | flag (`"1"`/`"true"` enables) | off | Enables the stderr transcode profiler / telemetry line output | host | N | `1` | N/A-Wave1 |
| `WT_OCCAM_LOG` | same call site (fallback param) | flag | — | Legacy-name fallback for `OCCAM_LOG` | host | N | — | N/A-Wave1 |
| `WT_TOKEN_USD_PER_M` | `Telemetry/OccamStderrAnsiSink.cs:285` (`OccamEnvironment.Get`) | decimal (USD per 1M tokens) | built-in `DefaultUsdPerMillionTokens` | Cost-per-token rate used only to render an estimated USD figure in the stderr telemetry line | host | N | `3.00` | N/A-Wave1 |

## 14. Plugins / chunking

| Variable | Code references | Type | Default | Effect | Scope | Secret | Example | Documented? |
|---|---|---|---|---|---|---|---|---|
| `OCCAM_CHUNK_SIZE` | `workers/shared/plugins/chunking.mjs:11` | int (chars) | `2000` | Default max chunk length for the semantic-chunking plugin (`json_blocks`) when no explicit `maxChunkLength` option is passed | worker | N | `4000` | N/A-Wave1 |

## 15. Non-`OCCAM_`/`WT_` standard vars read by this codebase

| Variable | Code references | Type | Default | Effect | Scope | Secret | Documented? |
|---|---|---|---|---|---|---|---|
| `LOCALAPPDATA` | `Workers/PlaywrightEnvironment.cs:56` | string (dir path) | OS-provided | Windows-only: base dir for the default `ms-playwright` cache probe | host | N | N/A-Wave1 |
| `HOME` | `Workers/PlaywrightEnvironment.cs:69` | string (dir path) | OS-provided | Non-Windows: base dir for the default `ms-playwright` cache probe | host | N | N/A-Wave1 |

---

## Cross-check against `docs/configuration.md` (secondary pass, post-enumeration)

Per the task brief, `docs/configuration.md` was read **after** the code pass above, purely as a
sanity cross-check. Code is authoritative; the table above was **not** edited to match docs. No
contradiction was found for any variable this subagent's code-search actually located — every
`OCCAM_*` name in `docs/configuration.md`'s host-facing sections (paths, browser, HTTP daemon,
response size, cache, receipts, playbooks, search, managed, translation, egress, digest, opt-in
tools, profile, client capability, batch, watch, remote MCP, logging, domain tiers) has a matching
row above with a matching default/clamp range. `docs/configuration.md`'s final "Install scripts
(not read by host)" section lists several additional `OCCAM_*` names
(`OCCAM_REPO_URL`, `OCCAM_REF`/`OCCAM_BRANCH`, `OCCAM_INSTALL_DIR`, `OCCAM_RELEASE_URL`,
`OCCAM_RELEASE_MANIFEST_URL`, `OCCAM_RELEASE_BASE`, `OCCAM_RELEASE_BASE_URL`,
`OCCAM_RELEASE_ALLOW_HTTP`, `OCCAM_RELEASES_API_URL`, `OCCAM_LATEST_VERSION`, `OCCAM_HOST`) that
live in general install/release/update-check scripts (`install.ps1`/`.sh`, `get-ff-occam.sh`,
`occam-onboard.mjs`, `update-check.mjs`, `packages/occam-mcp` wrapper) — **out of this
subagent's explicit scope** (host/workers/launch-mcp-host/occam-session/connect only) and
therefore intentionally not enumerated above. No `UNRESOLVED` contradictions found.

**Total in-scope variables catalogued: 74** distinct names (`OCCAM_*`: 66, `WT_*`: 6,
non-prefixed standard: 2), across 15 categories.
