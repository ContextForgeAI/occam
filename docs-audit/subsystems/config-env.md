# Subsystem Capability Report — Configuration / Environment / Feature Flags

**Wave 1 — Subagent S24. CAP ID range: CAP-350 – CAP-399 (this file only; no other subagent may
reuse this range).**

**Source of truth:** shipped executable code, enumerated independently of docs (see
`docs-audit/ENVIRONMENT-VARIABLES.md` for the full variable-by-variable table this report is
built on). `docs/configuration.md` was consulted only as a post-hoc secondary cross-check.

**Scope:** the config/env subsystem as a *capability surface* — how `OccamEnvironment` and its ~70
call sites turn process environment into host behavior: path resolution, feature flags, profiles,
daemon/pool sizing, provider selection, and the small number of scripts that stamp or forward env
before the host process even starts.

---

## Central helper

`Configuration/OccamEnvironment.cs` is the one shared primitive almost every capability below goes
through:

- `Get(primary, fallback?)` — plain string read, primary wins, empty/whitespace treated as unset.
- `GetExistingFile(primary, fallback?)` — like `Get`, but returns null unless `File.Exists`.
- `GetInt(primary, default, min, max, fallback?)` — parses, clamps, and **emits a stderr
  `[occam.config]` diagnostic line** both when the value fails to parse and when it gets clamped —
  so a silently-ignored or silently-clamped operator setting is never actually silent.
- `GetFlag(primary, default, fallback?)` — accepts `"1"` or case-insensitive `"true"`.

A sizeable minority of call sites (mostly older ones — `WorkerPaths`, `EgressProxyConfig`,
`BrowserExecutionProfile`, `HttpDaemonHost`, `ManagedExtractBackend`, `SearchService`,
`RemoteMcpAuthOptions.FromEnvironment`) still call `Environment.GetEnvironmentVariable` directly
rather than through `OccamEnvironment`, so they get **no** clamp-diagnostic and no fallback-name
support unless they hand-roll it. This is a real inconsistency, not a documentation gap — see
CAP-399 and the Gaps section.

---

## Capabilities

### CAP-350 — `OCCAM_HOME` root discovery and worker-path resolution
`WorkerPaths.Resolve()` / `ResolveOccamHome()` (`Workers/WorkerPaths.cs`). Three-tier resolution:
explicit `OCCAM_HOME` (validated by presence of `workers/http-extract/extract.mjs` or the core
`.csproj`) → walk up from `AppContext.BaseDirectory` → CWD. Feeds `HttpExtractScript`,
`BrowserExtractScript`, `CssExtractScript`, `DomSkeletonScript` paths. Failure mode: silent
`WorkerPaths` with all-null scripts → every extract call surfaces `workers_unavailable`. Also
consumed by `HostIdentity.CaptureSelf` (diagnostic `home` field), `OccamExtractKnowledgeTool`,
`OccamTranscodeTool` (schema-directory lookups), and three worker-side `.mjs` modules
(`default-fetch-headers.mjs`, `playbook-seed.mjs`, `playbook-publish-sanitize.mjs`) that each
independently re-derive `OCCAM_HOME` from their own `process.env` rather than receiving it as a
passed argument — i.e. this single env var is read from **eight separate code locations** across
two languages with no single source of truth object shared between host and workers.

### CAP-351 — Worker script path overrides
`OCCAM_HTTP_EXTRACT_SCRIPT` + `OCCAM_BROWSER_EXTRACT_SCRIPT` (paired — both-or-neither honored,
`WorkerPaths.cs:14`), `OCCAM_DOM_SKELETON_SCRIPT` (`DomSkeletonWorker.cs:165`),
`OCCAM_BROWSER_DAEMON_SCRIPT` (`BrowserPoolManager.cs:400`), `OCCAM_HTTP_DAEMON_SCRIPT`
(`HttpDaemonHost.cs:182`). Escape hatch for dev/test to point at alternate worker entry files
without touching `OCCAM_HOME`. No CSS-extract-script override exists (asymmetry — `CssExtractScript`
is always `{OCCAM_HOME}/workers/css-extract/css-extract.mjs`, non-overridable).

### CAP-352 — Node runtime + heap sizing
`OCCAM_NODE_BIN` (`Workers/NodeRuntime.cs`) resolves the `node` binary: explicit var → validated
file → `{OCCAM_HOME}/bin/node` → bare `"node"` (PATH search). `OCCAM_NODE_MAX_OLD_SPACE_MB` /
`OCCAM_BROWSER_NODE_MAX_OLD_SPACE_MB` (`Workers/NodeLaunchArguments.cs`, clamp 64–1024 MB, default
512 both) set `--max-old-space-size` per worker type — raised from an original 128 MB default
specifically so `OCCAM_MAX_RESPONSE_BYTES` at 8 MiB doesn't OOM-crash the V8 heap on heavy-HTML
pages (see inline Q-012 comment — this is a load-bearing historical fix, not an arbitrary default).

### CAP-353 — Host process identity stamping
`HostIdentity.CaptureSelf` (`Lifecycle/HostIdentity.cs`) assembles a diagnostic identity record
from `OCCAM_RUNTIME_ID`, `OCCAM_SESSION_ID` (both random-generated if unset), `OCCAM_PARENT_PID`,
`OCCAM_PARENT_LABEL`, `OCCAM_OWNER_LABEL`, plus `OCCAM_HOME`. `scripts/launch-mcp-host.mjs`
proactively **stamps** `OCCAM_RUNTIME_ID`/`OCCAM_SESSION_ID` (generating a UUID if the parent
didn't set one) and always sets `OCCAM_PARENT_PID=<own pid>` + a default
`OCCAM_PARENT_LABEL="launch-mcp-host"` before spawning the AOT binary — so under the standard
launch path these are never actually "unset" from the host's point of view, only from a bare
`dotnet run` invocation.

### CAP-354 — Launcher binary-selection + dev fallback (`OCCAM_FORCE_DOTNET_RUN`)
`scripts/launch-mcp-host.mjs` prefers the AOT-published binary (via
`scripts/lib/resolve-host-binary.mjs`, itself env-free — it only takes `root`/`rid` args). Only
when no binary is found does it check `OCCAM_FORCE_DOTNET_RUN=1` to permit a `dotnet run` dev
fallback (gated additionally on `.NET SDK >= 10` via `scripts/lib/host-install-gate.mjs`); with the
flag unset and no binary, it exits 1 with an install-blocker message that can be re-pointed via
`OCCAM_GET_URL` (display text only — this URL is not fetched, only printed).

### CAP-355 — Onboard config → launcher env merge (`OCCAM_CONFIG`)
`scripts/lib/operator/onboard-config.mjs::mergeOnboardEnv` reads `~/.occam/onboard.json` (or
`OCCAM_CONFIG` override path, `onboard-schema.mjs:16`) and merges its `env` object underneath the
launcher's own `process.env` (explicit host env always wins over the file). This is the mechanism
by which a one-time `occam-onboard.mjs` wizard run can persist e.g. a chosen `OCCAM_BROWSER_CHANNEL`
or `OCCAM_PROFILE` without the operator having to re-export it in every shell. Schema-versioned
(`schema_version: "1.0"`), fails soft (parse/schema errors just mean `{}` env + a stderr warning,
never a launch failure).

### CAP-356 — Session profile root + header/cookie loading
`OCCAM_SESSIONS_ROOT` (`Session/SessionProfileHeaders.cs:122`, default `~/.occam/sessions/`) is the
directory `session_profile` tool params resolve `<id>.json` against — cookies, headers, and an
optional storage-state path per profile. Same var independently re-read by
`scripts/lib/occam-sessions-lib.mjs` for the `occam-session.mjs` CLI (export-state / list / etc.),
so CLI and host must agree on the same env to operate on the same profile set — there is no IPC
between them, only the shared filesystem convention.

### CAP-357 — Extra request headers file
`OCCAM_REQUEST_HEADERS_FILE` (`Session/RequestHeadersMerger.cs:37`, also
`Workers/DomSkeletonWorker.cs:26`) — a JSON file of extra headers merged under session-profile
headers (session wins on key clash). Notably this is the **one** cross-process config value that
the host does *not* explicitly re-inject into a spawned worker's `ProcessStartInfo.Environment` —
`workers/browser-extract/browser-daemon.mjs:64,107` and `workers/http-extract/http-daemon.mjs:81`
read it straight from their own inherited OS environment, so it only reaches a worker/daemon if the
operator's original shell/host-launch environment already had it set (default `ProcessStartInfo`
env inheritance), not because the host actively forwards it per call.

### CAP-358 — Private/localhost URL blocking (SSRF policy)
`OCCAM_ALLOW_PRIVATE_URLS` (`Routing/PrivacyClassifier.cs:7`, mirrored in worker-side
`workers/shared/lib/private-ip.mjs:93,101`) is the single kill-switch for the SSRF guard: default
blocks RFC1918/loopback/link-local targets; `1` allows them (documented local-dev-only). Backing
enforcement lives in `Routing/OutboundHttpGuard.ConnectAsync`, wired as the `SocketsHttpHandler`
`ConnectCallback` on every in-process HTTP client that touches a user-supplied URL (probe redirect
tracking, genome fetch, robots.txt, TSA POST) — the env var is the single toggle, but the guard
itself resolves + pins DNS on every connect regardless, including redirect targets, so this is a
defense-in-depth control, not just an env check.

### CAP-359 — Playwright browser-cache path resolution
`PLAYWRIGHT_BROWSERS_PATH` (standard Playwright var, read to check for an existing Chromium
install) with an Occam-specific override `OCCAM_PLAYWRIGHT_BROWSERS_PATH`
(`Workers/PlaywrightEnvironment.cs`). OS-aware fallback search (`%LOCALAPPDATA%\ms-playwright` on
Windows via `LOCALAPPDATA`; `~/.cache/ms-playwright` or `~/Library/Caches/ms-playwright` via `HOME`
elsewhere). `FeatureDiscoveryService.IsBrowserAvailable()` consumes the same resolution to decide
whether the browser backend can even be attempted before spawning anything.

### CAP-360 — Browser channel / executable selection
`OCCAM_BROWSER_CHANNEL` (`chrome`/`msedge`/`chromium`, default `chromium`),
`OCCAM_BROWSER_EXECUTABLE_PATH`, `OCCAM_CHROME_PATH` (alias, checked second). Read identically on
both sides of the process boundary: `Cli/OccamCliVerbs.cs` (the `occam install-browser` CLI verb,
to decide "already present, no download needed") and
`workers/browser-extract/lib/browser-launch-options.mjs` (actual Playwright `launch()` call). A
configured channel other than `chromium`, or an explicit executable path, is treated as "using a
system browser" and skips Chromium auto-provisioning entirely.

### CAP-361 — Browser autoinstall / provisioning gate
`OCCAM_BROWSER_AUTOINSTALL` (`workers/browser-extract/lib/browser-provision.mjs:16`, default on,
`"0"` disables). Governs whether a first-ever browser-backend call with no Chromium present
silently downloads a **user-level** (never root/system) Chromium and reports
`browser_provisioned`, versus returning a typed `browser_required` failure for the operator to
resolve manually (`occam install-browser`, CAP-360's CLI verb).

### CAP-362 — Browser execution mode (shared daemon vs isolated)
`OCCAM_BROWSER_PROFILE` (`shared`/`daemon`/`lean` aliases → shared; `isolated`/`parallel`/
`throughput` aliases → isolated) and the blunter `OCCAM_BROWSER_DAEMON=0` override (isolated
regardless of profile) — `Workers/BrowserExecutionProfile.cs`. Two competing knobs for the same
binary decision by design: `OCCAM_BROWSER_PROFILE` is the user-facing preset name, `_DAEMON=0` is
the low-level kill switch also checked directly by `BrowserPoolSettings.IsEnabled`.

### CAP-363 — Browser daemon pool sizing and port layout
`OCCAM_BROWSER_POOL_SIZE` (1–8 slots), `OCCAM_BROWSER_POOL_BASE_PORT` (default 39217, slot *N* =
base+N), `OCCAM_BROWSER_DAEMON_PORT` (legacy single-slot override, only honored when pool size is
exactly 1), `OCCAM_BROWSER_DAEMON_IDLE_TTL_MS` (0–3.6M ms, 0 = never idle-shutdown) — all in
`Workers/BrowserPoolSettings.cs`. The pool-slot index is round-tripped to the daemon child process
via an **internally host-written** env var, `OCCAM_BROWSER_POOL_SLOT_ID`
(`BrowserPoolManager.cs:311` writes it into `psi.Environment`, `browser-daemon.mjs:39` reads it
back for its own `/health` response) — not an operator input.

### CAP-364 — Browser concurrency gate
`OCCAM_BROWSER_MAX_PARALLEL` (clamp 1–16, default 2, legacy fallback name
`WT_BROWSER_MAX_PARALLEL`) is read from **three independent call sites** with identical clamp
parameters (`BrowserPoolSettings.MaxParallelVar`, `BrowserConcurrencyLimiter.ResolveMaxParallel`,
`BrowserConcurrencyGate.MaxParallel`) rather than one shared cached value — functionally consistent
today because all three hard-code the same default/min/max, but a latent drift risk if one call
site's clamp range is ever edited without the other two. Also feeds `DigestParallelism`'s browser
branch and `BrowserExtractTimeouts.ResolveDaemonWaitTimeoutMs` (daemon queue-wait = per-extract
timeout × concurrency slots, capped at 900,000 ms).

### CAP-365 — Browser extract timeouts
`OCCAM_BROWSER_TIMEOUT_MS` (`Workers/BrowserExtractTimeouts.cs`, clamp 15,000–180,000, default
60,000) is the per-page ceiling; a hardcoded (non-env) 240,000 ms "provision grace" is added on top
when a cold Chromium auto-install is expected for that call, and the daemon-queue-wait derivation
(CAP-364) multiplies this by the concurrency-gate value. Env only controls the base; the grace
period and the multiplier are code constants.

### CAP-366 — Tier-B goto-timeout cap (worker-only, no host-side counterpart)
`OCCAM_TIER_B=1` (`workers/browser-extract/lib/browser-session.mjs:412`) switches on a stricter
per-page `goto()` ceiling, further capped by `OCCAM_BROWSER_GOTO_TIMEOUT_MS` (default 20,000 ms
when Tier-B is active; otherwise governed by recipe/consent-aggressive base values, 45–60 s). This
pair exists **only** in worker code — no C# host file reads either name — so it can currently only
be set as a raw process env var (or via the `OCCAM_CONFIG` onboard-merge path), never surfaced
through a host-side CLI flag or tool param.

### CAP-367 — HTTP daemon lifecycle
`OCCAM_HTTP_DAEMON` (on/off), `OCCAM_HTTP_DAEMON_PORT` (default 39218), `OCCAM_HTTP_DAEMON_IDLE_TTL_MS`
(default 120,000), `OCCAM_HTTP_DAEMON_PREWARM` (on by default — background-warms the daemon at MCP
server startup, best-effort/non-blocking, via `OccamMcpServerRegistration.AddOccamMcpServer`) — all
`Workers/HttpDaemonHost.cs` + one flag read directly in the registration path. Mirrors the browser
daemon's on/off/port/TTL shape (CAP-363) but has no pool-size concept — the HTTP daemon is always
single-instance.

### CAP-368 — Response body size cap + oversize mode
`OCCAM_MAX_RESPONSE_BYTES` (`workers/shared/lib/response-body-cap.mjs`, clamp 64 KiB–16 MiB,
default 8 MiB — raised from an original 1 MiB default that was hard-failing on common heavy-HTML
pages, per the Q-012 inline comment). `OCCAM_HTTP_OVERSIZE_MODE` (`fail`/`partial`) is the one
worker-read var in this whole audit that the **host writes rather than the operator sets** —
`Workers/HttpExtractRunner.cs:92` copies the per-call `on_oversize` tool param into the child
process's env at spawn time. Setting it globally as a real operator env var would still work (it's
a normal `process.env` read on the worker side) but the documented/intended control surface is the
tool param, not the env var.

### CAP-369 — PDF body cap
`OCCAM_MAX_PDF_BYTES` (`workers/http-extract/lib/http-extract-run.mjs:428`, clamp 64 KiB–128 MiB,
default 16 MiB) — a separate, independently-clamped ceiling from `OCCAM_MAX_RESPONSE_BYTES`
because PDFs routinely exceed the HTML cap; worker-only, no host-side read.

### CAP-370 — Robots.txt politeness + per-host throttle
`OCCAM_RESPECT_ROBOTS` (off by default — Occam is user-directed, not a crawler, so this is
opt-in) + `OCCAM_HOST_THROTTLE_MS` (0–600,000, default 0) — `Services/RobotsThrottleService.cs`.
Both env-gated at the very top of `CheckAndThrottle`: when *both* are at their off/zero defaults the
method is a documented no-op that never even fetches `robots.txt` — i.e. default behavior is
provably unchanged by this feature's mere existence. `OCCAM_ROBOTS_TIMEOUT_MS` (1,000–60,000,
default 10,000) bounds the robots.txt fetch itself, wired through the same SSRF-guarded
`SocketsHttpHandler` as user-URL fetches (CAP-358).

### CAP-371 — Transcode response cache directory
`OCCAM_CACHE_DIR` (`Caching/TranscodeResponseCache.cs:39`, default `{TEMP}/occam-cache`) — one
JSON file per cache key, opt-in via the `cache_ttl_s` tool param (the env var only controls
*where*, not *whether*, caching happens; there is no env-level cache on/off switch — the tool param
is the sole activation trigger). Entirely best-effort: any IO failure degrades to a cache miss.

### CAP-372 — Receipt v1 signing master switch + key storage
`OCCAM_RECEIPTS` (`off`/`0`/`false` disables; anything else, including unset, means on) — read
identically in `Receipts/ReceiptsPolicy.cs` and independently again in `Consensus/ConsensusService.cs`
(two copies of the same three-way string comparison rather than one shared call — a minor
duplication, not a behavioral risk since both use the exact same disable-string set).
`OCCAM_KEYS_ROOT` (default `~/.occam/keys/`) is where the ECDSA P-256 keypair is generated/loaded
once per host lifetime (`ReceiptSigner.LoadOrCreate`, singleton-registered in
`OccamServiceCollectionExtensions`) — this directory holds private key material and is the one
clear **secret-adjacent path** in the whole config surface that isn't a credential string itself.

### CAP-373 — RFC3161 time anchor (opt-in receipt enhancement)
`OCCAM_TIME_ANCHOR=1` + `OCCAM_TSA_URL` (both required — `TimeAnchorService.IsEnabled()`) plus
`OCCAM_TSA_TIMEOUT_MS` (500–15,000, default 3,000). Fail-open by design: off, misconfigured,
network error, malformed response, or a private-host TSA URL (guarded via the same
`PrivacyClassifier`, CAP-358) all just mean the receipt ships without a time anchor rather than the
extraction call itself failing — an explicit product decision documented in the class's own
XML-doc ("a time anchor is a bonus, never a gate").

### CAP-374 — Playbook tier paths
`OCCAM_PLAYBOOKS_LOCAL_ROOT` (default `~/.occam/playbooks/local/` — the "learn" tier written by
`occam_playbook_save`) and `WT_PLAYBOOKS_PATH` (no default — user/org tier, legacy `WT_` name
never renamed to an `OCCAM_` equivalent) are each read from **both** the C# host
(`Playbooks/PlaybookPaths.cs`) and the Node worker side (`workers/shared/lib/playbook-seed.mjs`)
independently — same duplication pattern as `OCCAM_HOME` (CAP-350): two languages, two readers, no
shared resolved-value handoff.

### CAP-375 — Well-known site genome auto-fetch
`OCCAM_SITE_GENOME_FETCH` (`Playbooks/PlaybookResolveOptions.cs:16`) is an env-level *default* for
the `fetch_site_genome` tool param on `occam_playbook_resolve` — `ShouldFetchSiteGenome()` returns
true if either the explicit tool param or the env flag says so (OR, not override) — so an operator
can flip this on globally without every caller passing the param each time.

### CAP-376 — Domain tier registry extra config
`OCCAM_DOMAIN_TIERS_PATH` (`Routing/DomainTierRegistry.cs:315`) — an OS-path-separator-delimited
list of *additional* domain-tier JSON files layered on top of the always-loaded built-in
`profiles/tiers/domain-tier.v1.json`. Additive, not a replacement — there is no way to disable the
built-in tier file via env.

### CAP-377 — Search provider selection
`OCCAM_SEARCH_PROVIDER` (`searxng`/`brave`/`tavily`) is the master switch — unset means
`occam_search` returns typed `search_unconfigured` rather than attempting anything
(`Services/SearchService.cs`). `OCCAM_SEARCH_URL` is required only for `searxng`
(`ISearchProvider.RequiresBaseUrl`); `OCCAM_SEARCH_API_KEY` required only for `brave`/`tavily`
(`RequiresApiKey`) — the requirement is a **per-provider interface property**, not a hardcoded
if/else, so adding a fourth provider only means implementing `ISearchProvider` correctly, not
touching the resolution logic. `OCCAM_SEARCH_TIMEOUT_MS` (1,000–120,000, default 20,000) is
provider-agnostic.

### CAP-378 — Managed extract provider escalation
Same shape as CAP-377 one layer down the extract cascade: `OCCAM_MANAGED_PROVIDER`
(`firecrawl`/`jina`/`spider`/`scrapfly`) selects, `OCCAM_MANAGED_API_KEY` required per-provider via
`IManagedProvider.RequiresApiKey`, `OCCAM_MANAGED_BASE_URL` overrides the provider default,
`OCCAM_MANAGED_TIMEOUT_MS` (1,000–180,000, default 60,000). `OCCAM_MANAGED_DOMAINS` is the one
capability here without a CAP-377 analog: a comma-separated allowlist that must match-or-subdomain
the request host, checked in `ManagedExtractBackend.IsHostOptedIn` — when unset, any host is
eligible (the provider var itself is treated as the opt-in); when set, it's a strict allowlist, not
a denylist.

### CAP-379 — Translation service
`OCCAM_TRANSLATE_URL` (LibreTranslate base, master switch — unset means `IsConfigured=false`),
`OCCAM_TRANSLATE_API_KEY` (optional), `OCCAM_TRANSLATE_TIMEOUT_MS` (1,000–120,000, default 20,000).
Explicitly documented as non-fatal on failure — caller keeps the original markdown and surfaces a
warning code (`translate_endpoint_unconfigured`, `translate_http_<status>`) rather than failing the
whole extract; "translation is a convenience codec, not the extraction contract" per the class
XML-doc.

### CAP-380 — Static egress proxy
`OCCAM_HTTP_PROXY` / `OCCAM_HTTPS_PROXY` (falls back to HTTP proxy) / `OCCAM_NO_PROXY`
(`Workers/EgressProxyConfig.cs`, mirrored in `workers/shared/lib/egress-proxy.mjs`). Explicitly
scoped to **worker spawns only** — the class's own doc comment states "Core never performs proxied
HTTP" — i.e. the host's own in-process HTTP clients (probe, genome fetch, search, managed,
translate, robots, TSA) never route through this proxy; only child Node processes do, via
`EgressProxyConfig.ApplyTo(ProcessStartInfo)` copying the three vars into the spawn env.

### CAP-381 — Rotating proxy pool
`OCCAM_PROXY_LIST` (inline, comma/semicolon/newline-delimited) / `OCCAM_PROXY_LIST_FILE` (file
wins over inline; supports both plain URL-per-line and a proxy-scraper CSV export format with
`ip`/`port`/`protocols` columns, auto-detected by header sniffing —
`Services/ProxyListParser.cs`). Documented interaction (per `docs/configuration.md`, confirmed
consistent with code structure though the disable-logic itself lives in the round-robin rotation
service rather than this audit's file list): an active rotation pool requires one-shot workers, so
it takes precedence over the daemon-pool capabilities (CAP-363/CAP-367) when configured.

### CAP-382 — Digest parallelism control
`OCCAM_DIGEST_PARALLEL=0` forces fully sequential `occam_digest` processing;
`OCCAM_DIGEST_MAX_PARALLEL` (clamp 1–`DigestService.MaxUrlsCap`) sets an explicit cap that, when
absent, defaults to policy-aware values (4 for pure-HTTP digests, the browser-concurrency-gate
value — CAP-364 — for browser/mixed) rather than one flat number
(`Services/DigestParallelism.cs`).

### CAP-383 — Opt-in MCP tool-group flags
`OCCAM_BATCH_MCP`, `OCCAM_WATCH_MCP`, `OCCAM_CONSENSUS_MCP`, `OCCAM_ATLAS_MCP` — four independent
flags checked in `OccamMcpServerRegistration.AddOccamMcpServer`, each gating both DI registration
(job store/service/hosted-service for batch; store/service for watch; service for consensus; a
telemetry-sink **swap** for atlas) and MCP tool exposure. All default off — the fifteen-tool "core"
surface (CAP-384) is unaffected by any of these being off; turning one on is strictly additive to
`tools/list`.

### CAP-384 — Tool surface profile (`OCCAM_PROFILE`)
`Transport/OccamToolProfile.cs` — `full` (default, all fifteen core tools per
`OccamMcpServerRegistration.OccamToolNames`) / `reader` (7 read-only tools) / `researcher` (reader +
claim-check + verify) / `auditor` (researcher + attest + dataset-export + playbook-lint). Invalid
values fall back to `full` with a stderr `[occam.config]` warning rather than failing startup. This
profile is orthogonal to CAP-383's opt-in flags — a `reader` profile with `OCCAM_BATCH_MCP=1` still
gets the batch tools, since `OccamToolProfile` only ever narrows the *core* list.

### CAP-385 — Client capability env bootstrap
`OCCAM_CLIENT_CONTEXT_TOKENS` (clamp `MinContextTokens`–`MaxContextTokens`) + optional
`OCCAM_CLIENT_MODEL_ID` label (`Client/ClientCapabilityStore.cs`) — the operator-level fallback path
for something the docs correctly frame as normally tool-driven: the intended flow is one
`occam_client_capabilities(context_tokens=…)` call per session, with the env vars existing for
hosts that can't or won't make that call. `ComputeOutputBudget` derives a clamped ~20%-of-context
default for `max_tokens`/`per_url_max_tokens` when a tool call omits them explicitly.

### CAP-386 — Batch server settings
`OCCAM_BATCH_PORT` (1–65535, default 5051), `OCCAM_BATCH_MAX_URLS` (1–256, default 64),
`OCCAM_BATCH_PARALLEL` (1–16, default 4), `OCCAM_BATCH_DB_PATH` (default
`~/.occam/jobs/jobs.db`) — `Batch/BatchSettings.cs`. Only meaningful when `OCCAM_BATCH_MCP=1`
(CAP-383); reading these when batch is off is harmless dead config, not an error.

### CAP-387 — Watch store path
`OCCAM_WATCH_DB_PATH` (default `~/.occam/watch/watch.json`, `Watch/WatchStore.cs`) — single-file
JSON last-seen store, whole-file rewrite per upsert under one process-wide lock (documented as
intentional given the watch set is expected to stay small). Only meaningful when
`OCCAM_WATCH_MCP=1` (CAP-383).

### CAP-388 — Remote MCP TLS configuration
`OCCAM_TLS_CERT_PATH` + `OCCAM_TLS_CERT_PASSWORD` (`Transport/RemoteMcpAuthOptions.FromEnvironment`,
also independently re-read as CLI-flag fallbacks in `Transport/OccamMcpCli.cs:184-185` — CLI flag
wins when both are present). `HasCert` gates on `File.Exists`, so a configured-but-missing cert path
is treated the same as unconfigured rather than a hard error at that check site.

### CAP-389 — Remote MCP JWT configuration
`OCCAM_JWT_ISSUER` (default `occam-mcp`), `OCCAM_JWT_AUDIENCE` (default `occam-mcp`),
`OCCAM_JWT_METADATA_URI` (explicit OIDC metadata doc, takes precedence), `OCCAM_JWT_JWKS_URI`
(deprecated alias, only consulted when metadata-uri is unset) — same dual host-CLI-flag-fallback
pattern as CAP-388. Startup requires either an HTTPS issuer suitable for OIDC discovery or an
explicit metadata URI — this validation constraint lives in code (per the class + CLI comments) but
is enforced at remote-transport startup, not at this config-read layer itself.

### CAP-390 — Remote transport limits
`OCCAM_REMOTE_MAX_SESSIONS` (1–32, default 4, `RemoteMcpAuthOptions.MaxSessionsVariable`) and
`OCCAM_MCP_MAX_MESSAGE_BYTES` (64 KiB–16 MiB, default 4 MiB,
`WebSocketMcpStreams.McpWebSocketLimits.MaxMessageBytesVariable`) are the two hard ceilings on the
optional WebSocket MCP transport — concurrent authenticated session count and per-message size —
both routed through `OccamEnvironment.GetInt` (so both get the clamp-diagnostic behavior described
under "Central helper").

### CAP-391 — Startup banner + diagnostic logging toggles
`OCCAM_BANNER` (on by default, `WT_OCCAM_BANNER` legacy fallback) and `OCCAM_LOG` (off by default,
`WT_OCCAM_LOG` legacy fallback) — `Telemetry/OccamLogger.cs`, both lazily resolved once and cached
behind a lock (`_bannerChecked`/`_logChecked`), i.e. changing the env var mid-process-lifetime after
first access has no effect — these are read-once-at-first-use, not read-per-call.

### CAP-392 — Telemetry cost-display rate
`WT_TOKEN_USD_PER_M` (`Telemetry/OccamStderrAnsiSink.cs:285`) — purely cosmetic: a USD-per-million-
token rate used only to compute an estimated dollar figure printed in the stderr profiler line when
`OCCAM_LOG=1` (CAP-391). No effect on any extraction, routing, or tool-response behavior — the only
capability in this whole file that is display-only end to end.

### CAP-393 — Internal per-call env plumbing (`OCCAM_FEATURES`, `OCCAM_HTTP_OVERSIZE_MODE`, `OCCAM_BROWSER_POOL_SLOT_ID`)
Grouping these three together deliberately because they share one architectural pattern the other
~65 variables don't: **the host writes them, not the operator.** `OCCAM_FEATURES`
(`Workers/HttpExtractRunner.cs:97`, `Workers/BrowserExtractRunner.cs:136`) carries the per-call
`json_blocks`/`json_tables` tool-param selection into the worker's plugin runner
(`workers/shared/lib/plugins-runner.mjs`). `OCCAM_HTTP_OVERSIZE_MODE` carries the per-call
`on_oversize` tool param (CAP-368). `OCCAM_BROWSER_POOL_SLOT_ID` carries the daemon's own pool-slot
index (CAP-363). All three *would* also work as ordinary operator-set env vars, since the worker
side just does a plain `process.env` read with no provenance check — but the intended and
documented control surface for the first two is the tool parameter, and the third is purely
internal bookkeeping never meant to be operator-visible at all.

### CAP-394 — Cookie injection / browser-extract-variant / virtual-scroll opt-ins
`WT_COOKIE_INJECT` (`workers/browser-extract/lib/cookie-inject.mjs`, self-set to `"1"` from the
`cookieInject` API option in both `browser-extract.mjs:29` and `browser-extract-run.mjs:21` — so in
normal operation this is also host/caller-driven rather than operator-env-driven, though nothing
stops an operator from exporting it directly too), `WT_BROWSER_EXTRACT_VARIANT` (fallback default
for the extract-variant recipe/option, `baseline`/`css-hide`/`strip-chrome`/`strip-consent`),
`WT_VIRTUAL_SCROLL` (`"1"` default-on, disables infinite-scroll simulation when set to anything
else). All three retain the legacy `WT_` prefix rather than `OCCAM_` — no equivalent `OCCAM_`-named
alias exists for any of them, unlike CAP-391's banner/log pair which has both names live.

### CAP-395 — Chunking plugin default size
`OCCAM_CHUNK_SIZE` (`workers/shared/plugins/chunking.mjs:11`, default 2000 chars) — the one knob in
the `json_blocks` semantic-chunking plugin path that has no per-call tool-param override wired
through from the host at all (contrast CAP-393's `OCCAM_FEATURES`, which *does* have a tool-param
path) — this default can currently only be changed via raw process env, not a documented tool
parameter.

---

## Gaps, inconsistencies, and open questions (for downstream synthesis — not fixed by this audit)

1. **Two config-read idioms coexist.** `OccamEnvironment.Get/GetInt/GetFlag` (with clamp-diagnostics
   and fallback-name support) vs. bare `Environment.GetEnvironmentVariable` (no diagnostics, no
   fallback). Roughly 30 call sites still use the bare form. Not a correctness bug today, but any
   future "why didn't my env var take effect" support case will get a stderr hint only for the
   `OccamEnvironment`-routed half.
2. **`OCCAM_HOME` and the two playbook-path vars are each read independently by both the C# host and
   Node workers**, with no single resolved value passed across the process boundary as an argument.
   They can't drift *today* because both sides apply the same literal default, but there is no
   structural guard against that changing.
3. **Three separate call sites hard-code the same `OCCAM_BROWSER_MAX_PARALLEL` clamp range**
   (CAP-364) instead of one shared constant/helper.
4. **`OCCAM_RECEIPTS`'s disable-string check is duplicated** verbatim in `ReceiptsPolicy.cs` and
   `ConsensusService.cs` rather than the latter calling the former.
5. **Legacy `WT_`-prefixed names never got an `OCCAM_` alias** for `WT_PLAYBOOKS_PATH`,
   `WT_COOKIE_INJECT`, `WT_BROWSER_EXTRACT_VARIANT`, `WT_VIRTUAL_SCROLL`, `WT_TOKEN_USD_PER_M` —
   inconsistent with `OCCAM_BANNER`/`OCCAM_LOG` (which do have both names) and
   `OCCAM_BROWSER_MAX_PARALLEL` (which has a `WT_` fallback the other direction). No functional bug,
   but it's an unclear naming convention for anyone scanning for "all Occam env vars start with
   `OCCAM_`".
6. **`OCCAM_TIER_B` / `OCCAM_BROWSER_GOTO_TIMEOUT_MS` have zero host-side presence** — worker-only,
   unreachable from any C# code path, CLI flag, or MCP tool param. Whether this is intentional
   (a worker-internal tuning knob) or an incomplete integration is not determinable from code alone.
7. **`OCCAM_CHUNK_SIZE` has no tool-param path**, unlike its sibling `OCCAM_FEATURES` — inconsistent
   control-surface pattern within the same `json_blocks` feature.
8. This subagent found **no evidence of any env var controlling secrets rotation, encryption at
   rest, or redaction beyond what's already noted** (`ReceiptSigner`'s POSIX 0600 best-effort
   permission hardening on `OCCAM_KEYS_ROOT`, Windows explicitly skipped as NTFS-ACL-inherited).

---

## Cross-check note (env-catalog.selftest.mjs)

`scripts/lib/operator/env-catalog.selftest.mjs` is a docs-drift guard, not an env-var source of
truth: it regex-scans `src/`, `workers/`, `scripts/`, `packages/` for `OCCAM_`/`WT_` tokens and
asserts every name documented in `docs/configuration.md` also appears somewhere in that scan (dead-doc
detection), plus one specific negative assertion (`OCCAM_LEGACY_ROOT` must never be documented). It
does **not** assert the reverse (that every scanned name is documented), so it cannot itself
certify completeness of either this file or `docs/configuration.md`. No contradiction between this
audit's code-derived list and what the selftest's pattern set would find; nothing here disagrees
with it. No UNRESOLVED items.
