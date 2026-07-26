# W4-A — Host / runtime / transport / config / telemetry (blind negative-space)

**Owner:** W4-A  
**SoT:** shipped C# under assigned glob (read before any prior `docs-audit/*` compare)  
**Date:** 2026-07-26  
**Constraint:** discovery only — no product edits

---

## 1. Blind inventory

Externally meaningful behaviors discovered from code in scope (users / agents / operators / security / privacy / network / config / persistence / artifacts / trust / performance / failure / routing). Numbered for gap cross-ref.

### 1.1 Process entry (`Program.cs`)

| # | Behavior | Evidence |
|---|----------|----------|
| B01 | UTF-8 console + VT switch on Windows before any mode | `Program.cs:6-8` |
| B02 | Offline CLI verbs short-circuit **before** MCP CLI parse / transport (no workers) | `Program.cs:12-15` → `OccamCliVerbs.TryRun` |
| B03 | Invalid transport CLI → stderr `invalid_arguments: {FailureKind}` exit 1 | `Program.cs:24-28` |
| B04 | Ctrl+C cancels shared CTS (cancel=true); `OperationCanceledException` → exit 0 | `Program.cs:30-35,44-47,62-64` |
| B05 | Mode dispatch: BatchServer \| WebSocket \| Remote \| Stdio default | `Program.cs:37-55` |
| B06 | `finally` always `StopAsync` even after cancel | `Program.cs:66-69` |

### 1.2 Transport CLI (`OccamMcpCli`)

| # | Behavior | Evidence |
|---|----------|----------|
| B07 | Help: `-h/--help/-help=/help=/?` → usage on stderr, exit 0 | `OccamMcpCli.cs:36-38`; `Program.cs:18-21` |
| B08 | Modes: `--mcp-server` (WS:5050), `--remote` (8443), `--batch-server` (5051), default stdio | `OccamMcpCli.cs:56-74` |
| B09 | `--port` required int; WS/Batch/Remote reject port∉[1,65535] | `OccamMcpCli.cs:76-86,162-167` |
| B10 | **Bind policy asymmetry:** WS + BatchServer **reject** any `--bind` ≠ `127.0.0.1`; Remote accepts any parseable IP (incl. `0.0.0.0`) | `OccamMcpCli.cs:168-176` |
| B11 | Remote requires TLS cert path (CLI or `OCCAM_TLS_CERT_PATH`); missing → `remote_requires_tls_cert`; missing file → `tls_cert_not_found` | `OccamMcpCli.cs:184-199` |
| B12 | JWT metadata must be HTTPS if set (`invalid_jwt_metadata_uri`); if metadata omitted, **issuer must be HTTPS URI** else `remote_requires_jwt_metadata` | `OccamMcpCli.cs:200-207,267-269` |
| B13 | Defaults: `OCCAM_JWT_ISSUER`/`AUDIENCE` → `"occam-mcp"` (non-URI) — so **metadata (or HTTPS issuer) is effectively mandatory** for remote start | `OccamMcpCli.cs:186-187` |
| B14 | CLI auth args override env; `--jwt-jwks-uri` **aliased** to same slot as `--jwt-metadata-uri` | `OccamMcpCli.cs:149-158,188-190` |
| B15 | `WriteUsage` documents keys/verify/install-browser + transport flags; **omits** `version-surface` and `lifecycle` | `OccamMcpCli.cs:224-265` vs `OccamCliVerbs.cs:47-54` |
| B16 | Usage env blurb: `OCCAM_HOME`, `OCCAM_BANNER`, `OCCAM_LOG` + remote env list (incl. `OCCAM_REMOTE_MAX_SESSIONS`) | `OccamMcpCli.cs:263-264` |

### 1.3 Stdio transport

| # | Behavior | Evidence |
|---|----------|----------|
| B17 | Host builder: `Logging=None`, `AddOccamMcpServer().WithStdioServerTransport()`, `RunAsync` | `StdioMcpTransport.cs:39-45` |
| B18 | Bounded outbound channel (32, Wait) + `ReadRequestsAsync`/`SendResponseAsync` exist but **are not wired** into `StartAsync` (SDK owns stdio I/O) | `StdioMcpTransport.cs:14-37,39-45` |

### 1.4 Local WebSocket transport

| # | Behavior | Evidence |
|---|----------|----------|
| B19 | Slim Kestrel `http://{bind}:{port}/`; no JWT/TLS; non-WS → 426 | `WebSocketMcpTransport.cs:45-58` |
| B20 | **No session semaphore** — every upgrade runs `RunSingleSessionAsync` concurrently | `WebSocketMcpTransport.cs:53-62` (contrast Remote B28) |
| B21 | Per accepted socket: **new** `Host.CreateApplicationBuilder` + `AddOccamMcpServer().WithStreamServerTransport` | `WebSocketMcpTransport.cs:80-88` |
| B22 | Outer `StartAsync` also calls `AddOccamMcpServer()` once; tool chain never served from that container | `WebSocketMcpTransport.cs:48` |
| B23 | Class comment claims “single client”; **code does not enforce** | `WebSocketMcpTransport.cs:15-17` vs B20 |
| B24 | `IsListeningOnLocalhost(port)` probe via TcpListener start/fail | `WebSocketMcpTransport.cs:91-104` |

### 1.5 Remote WSS + JWT

| # | Behavior | Evidence |
|---|----------|----------|
| B25 | Kestrel Listen(IP, port) + `UseHttps(LoadCertificate)` | `RemoteMcpTransport.cs:55-61` |
| B26 | JWT Bearer: `MapInboundClaims=false`, `RequireHttpsMetadata=true`, MetadataAddress **or** Authority=issuer, validate iss/aud/lifetime/signing, RequireSignedTokens, ClockSkew=30s | `RemoteMcpTransport.cs:177-201` |
| B27 | Query `token` / `access_token` → 400 `query_token_forbidden` (Bearer header only) | `RemoteMcpTransport.cs:83-92,215-216` |
| B28 | Unauthenticated → 401 + WWW-Authenticate; non-WS → 400; capacity → 503 `remote_capacity_exceeded` + Retry-After | `RemoteMcpTransport.cs:95-126` |
| B29 | Session slots: `OCCAM_REMOTE_MAX_SESSIONS` via `OccamEnvironment.GetInt` default **4**, clamp 1–32 | `RemoteMcpAuthOptions.cs:42-43`; `RemoteMcpTransport.cs:32-33,116` |
| B30 | Unauthenticated `GET /health` → `{ok, mode:remote, transport:wss}` | `RemoteMcpTransport.cs:142-143` |
| B31 | Per authenticated WS: same per-session `AddOccamMcpServer` host as local WS | `RemoteMcpTransport.cs:218-226` |
| B32 | Outer app **also** `AddOccamMcpServer()` (auth middleware host); tools served only from per-session hosts | `RemoteMcpTransport.cs:73` |
| B33 | Cert load: `.pfx`/`.p12` via Pkcs12 (+ optional password); else `X509Certificate2.CreateFromPemFile` (**password ignored for PEM**) | `RemoteMcpTransport.cs:160-174` |
| B34 | Auth failure → stderr `auth_failed: {ExceptionType}` only (no body detail) | `RemoteMcpTransport.cs:202-209` |
| B35 | `RemoteMcpAuthOptions.FromEnvironment()` exists; live path uses CLI-inlined env reads | `RemoteMcpAuthOptions.cs:23-38` vs `OccamMcpCli.cs:183-210` |

### 1.6 WS stream framing / size caps

| # | Behavior | Evidence |
|---|----------|----------|
| B36 | Inbound: assemble WS text frames; reject binary; cap `OCCAM_MCP_MAX_MESSAGE_BYTES` default **4MiB**, clamp 64KiB–16MiB | `WebSocketMcpStreams.cs:86-126` |
| B37 | Each inbound JSON message is **re-framed** with `Content-Length` for MCP SDK stream transport | `WebSocketMcpStreams.cs:70-83`; `McpContentLengthFraming.cs:7-14` |
| B38 | Outbound: accumulate bytes, `TryExtractMessage` Content-Length bodies → WS text sends | `WebSocketMcpStreams.cs:150-157` |
| B39 | `TryExtractMessage`: header must start with `Content-Length:`; bad/incomplete → false (accumulator can grow until a valid frame appears) | `McpContentLengthFraming.cs:17-58` |

### 1.7 MCP registration / profile / instructions / opt-ins

| # | Behavior | Evidence |
|---|----------|----------|
| B40 | Canonical 15-name catalog `OccamToolNames`; runtime exposure via `OCCAM_PROFILE` | `OccamMcpServerRegistration.cs:15-32`; `OccamToolProfile.cs` |
| B41 | Profiles: `full` (default) / `reader` (7) / `researcher` (+claim_check,verify) / `auditor` (+attest,dataset_export,playbook_lint); invalid → stderr + full | `OccamToolProfile.cs:17-58,62-71` |
| B42 | **Opt-ins are NOT profile-filtered:** `OCCAM_BATCH_MCP`, `OCCAM_WATCH_MCP`, `OCCAM_CONSENSUS_MCP`, `OCCAM_ATLAS_MCP` | `OccamMcpServerRegistration.cs:120-157` |
| B43 | Profile-scoped `ServerInstructions` on initialize (`OccamServerInstructions.TextFor`) — trust rule, client budget, tool pick, opt-ins; full mentions `occam_watch` | `OccamServerInstructions.cs`; registration `:55-61` |
| B44 | Digest tool: transient + hand-built `McpServerTool` with **urls oneOf** array\|string schema patch | `OccamMcpServerRegistration.cs:85-96,162-214` |
| B45 | CallTool filter: MEAI binding failures → typed `invalid_arguments` JSON result (IsError unset) + stderr log | `OccamMcpServerRegistration.cs:64-75`; `McpArgumentBindingGuard.cs` |
| B46 | Automatic HTTP daemon prewarm (`Task.Run`) when daemon enabled and `OCCAM_HTTP_DAEMON_PREWARM` default true; failures swallowed | `OccamMcpServerRegistration.cs:40-53` |
| B47 | Startup banner via `OccamLogger.TryWriteStartupBanner` at every `AddOccamMcpServer` (once per process via static latch) | `OccamMcpServerRegistration.cs:37-38`; `OccamLogger.cs:25-42` |

### 1.8 DI composition (`AddOccamCore`)

| # | Behavior | Evidence |
|---|----------|----------|
| B48 | Always registers full core graph: WorkerPaths, **ReceiptSigner.LoadOrCreate()** (creates `~/.occam/keys` or `OCCAM_KEYS_ROOT`), ClientCapabilityStore, TimeAnchorService, HTTP+Browser backends, post-processors, BrowserPoolManager (**InstallShared**), SSRF-guarded HttpClients, managed providers, search, robots, daemon client, probe/transcode/digest/map, claim/attest/dataset, playbooks, translation, **FileTranscodeResponseCache**, codecs (extensions **Disabled**), MaterializationPlanner | `OccamServiceCollectionExtensions.cs:18-151` |
| B49 | `BrowserPoolManager` factory calls `InstallShared(manager)` → **process-static** `_shared`; prior shared `StopAll()` | `OccamServiceCollectionExtensions.cs:39-46`; `BrowserPoolManager.cs:45-48` |
| B50 | Ad-hoc builders `BuildOccamCore` / `BuildTranslationService` / `BuildManagedBackend` / `BuildSearchService` each spin a **new** ServiceProvider | `OccamServiceCollectionExtensions.cs:154-189` |
| B51 | Timeouts via OccamEnvironment: `OCCAM_TSA_TIMEOUT_MS` (3s), `OCCAM_TRANSLATE_TIMEOUT_MS` (20s), `OCCAM_MANAGED_TIMEOUT_MS` (60s), `OCCAM_SEARCH_TIMEOUT_MS` (20s), `OCCAM_ROBOTS_TIMEOUT_MS` (10s) | `OccamServiceCollectionExtensions.cs:73-101` |
| B52 | Atlas opt-in **adds second** `IOccamTelemetrySink` registration (last-wins) wrapping `new OccamLoggerTelemetrySink()` + `FailureAtlasStore` singleton | `OccamMcpServerRegistration.cs:149-156` vs core `:37` |

### 1.9 Failure atlas lifetime (independent re-verify)

| # | Behavior | Evidence |
|---|----------|----------|
| B53 | Store: in-memory, MaxHosts=500, www-strip, silent drop when full / bad URL; classifier closure codes captcha/login/401/403/404/410 | `FailureAtlasStore.cs` |
| B54 | **Stdio:** one `AddOccamMcpServer` → one DI singleton for process lifetime | `StdioMcpTransport.cs:41-45` |
| B55 | **WS/Remote:** each connection builds a **new** host + `AddOccamMcpServer` → **new** `FailureAtlasStore` per session; reconnect clears atlas | `WebSocketMcpTransport.cs:80-88`; `RemoteMcpTransport.cs:218-226` |
| B56 | Outer Kestrel `AddOccamMcpServer` may construct an **unused** atlas singleton (tools never run there) | Remote `:73`; WS `:48` |

**Verdict (B54–B56):** Wave 3 EF-024 WITHDRAWN is **correct** — atlas is **not** a process-wide multi-tenant store under `--remote`. Cross-tenant leak claim in `subsystems/failure-atlas.md` EF-019 is **stale/wrong**.

### 1.10 Telemetry / banner / cost display

| # | Behavior | Evidence |
|---|----------|----------|
| B57 | Banner default **on**; disable `OCCAM_BANNER=0` / `WT_OCCAM_BANNER=0` | `OccamLogger.cs:181` |
| B58 | Transcode/pool/stage profiler default **off**; `OCCAM_LOG=1` / `WT_OCCAM_LOG=1` | `OccamLogger.cs:201` |
| B59 | Success report shows shredder + **USD savings** using `WT_TOKEN_USD_PER_M` (default $0.15/MTok); cosmetic only | `OccamStderrAnsiSink.cs:115-123,283-289` |
| B60 | Banner content always `ListeningHint: "Listening via stdio..."` even when host is WS/Remote | `BannerModel.cs:43` |
| B61 | Banner Tools row = live profile tool count | `BannerModel.cs:36-39` |
| B62 | Stage breakdown lines on stderr when log enabled | `OccamStderrAnsiSink.cs:61-65` |

### 1.11 CLI verbs (`OccamCliVerbs`) — full dispatch

| # | Verb | Behavior | Evidence |
|---|------|----------|----------|
| B63 | `keys export` | LoadOrCreate signer; PEM pubkey stdout; optional `--keys-root` | `:38-40,208-214` |
| B64 | `verify` | modes receipt\|citation\|manifest\|history; exit 0/1/2; JSON stdout | `:41-42,217-409` |
| B65 | `install-browser` | skip if system browser path/channel; else `npx playwright install chromium` (Windows via `cmd /c`); JSON marker stdout | `:44-46,65-160` |
| B66 | `version-surface` | host/package version + assembly path; protocol/fingerprint null — **undocumented in WriteUsage** | `:47-49,173-205` |
| B67 | `lifecycle self\|diagnose` | HostIdentity INV-10; diagnose peers from `--peers` JSON only (no OS scan/kill) — **undocumented in WriteUsage** | `:50-52,482-547` |
| B68 | Unknown first arg → fall through to MCP host | `:53-54` |

### 1.12 Host identity / lifecycle types

| # | Behavior | Evidence |
|---|----------|----------|
| B69 | `OCCAM_RUNTIME_ID` / `OCCAM_SESSION_ID` or generate; `OCCAM_PARENT_PID`/`LABEL`, `OCCAM_OWNER_LABEL`, `OCCAM_HOME` | `HostIdentity.cs:14-32,266-311` |
| B70 | Overlap warnings: same occamHome+transport+ownerLabel across runtimeIds — diagnose only | `HostIdentity.cs:314-338` |
| B71 | Shutdown planning requires exact RuntimeId+Pid; rejects process-name kills | `HostIdentity.cs:223-256,154-178` |

### 1.13 Paths / config resolver / JSON / abstractions

| # | Behavior | Evidence |
|---|----------|----------|
| B72 | `OccamUserPaths.ResolveUserDataRoot()` → `{UserProfile}/.occam` or `CWD/.occam` if no profile | `OccamUserPaths.cs:8-17` |
| B73 | OccamUserPaths **does not create** dirs; consumers do. Related layout (out of file but rooted here): `sessions/`, `playbooks/local/` via OccamUserPaths; ReceiptSigner uses `{UserProfile}/.occam/keys` (not OccamUserPaths API); watch/jobs often hardcode `.occam/...` under home | Grep + `ReceiptSigner.cs:80-82` |
| B74 | `OccamEnvironment` API: `Get`, `GetExistingFile`, `GetInt` (stderr on bad/clamp), `GetFlag` (`1`/`true`) | `OccamEnvironment.cs` |
| B75 | **`GetExistingFile` has zero call sites** in Core — dead API surface | repo grep |
| B76 | Scope OccamEnvironment reads: profile, banner/log, WS max message, remote max sessions, HTTP daemon prewarm, atlas/batch/watch/consensus flags, composition timeouts, `WT_TOKEN_USD_PER_M` | see §1.14 |
| B77 | `OccamJsonPrintableEscapes.Relax/Serialize` — post-serialize rewrite `\u003E`/`\u0027`/`\u0022` for MCP wire readability; keeps `\u003C`/`\u0026`; AOT-safe with source-gen `JsonTypeInfo` | `OccamJsonPrintableEscapes.cs` |
| B78 | Abstractions seams: `IExtractBackend`, `ITranscodePostProcessor`, `IOccamTelemetrySink` | `Abstractions/*` |

### 1.14 Env vars touched in this scope (complete for resolver + transport/cli/lifecycle/telemetry)

**Via `OccamEnvironment`:**  
`OCCAM_PROFILE`, `OCCAM_BANNER`(+`WT_OCCAM_BANNER`), `OCCAM_LOG`(+`WT_OCCAM_LOG`), `OCCAM_MCP_MAX_MESSAGE_BYTES`, `OCCAM_REMOTE_MAX_SESSIONS`, `OCCAM_HTTP_DAEMON_PREWARM`, `OCCAM_BATCH_MCP`, `OCCAM_WATCH_MCP`, `OCCAM_CONSENSUS_MCP`, `OCCAM_ATLAS_MCP`, `OCCAM_TSA_TIMEOUT_MS`, `OCCAM_TRANSLATE_TIMEOUT_MS`, `OCCAM_MANAGED_TIMEOUT_MS`, `OCCAM_SEARCH_TIMEOUT_MS`, `OCCAM_ROBOTS_TIMEOUT_MS`, `WT_TOKEN_USD_PER_M`.

**Direct `Environment.GetEnvironmentVariable` in scope:**  
`OCCAM_TLS_CERT_PATH`, `OCCAM_TLS_CERT_PASSWORD`, `OCCAM_JWT_ISSUER`, `OCCAM_JWT_AUDIENCE`, `OCCAM_JWT_METADATA_URI`, `OCCAM_JWT_JWKS_URI`, `OCCAM_BROWSER_EXECUTABLE_PATH`, `OCCAM_CHROME_PATH`, `OCCAM_BROWSER_CHANNEL`, `OCCAM_RUNTIME_ID`, `OCCAM_SESSION_ID`, `OCCAM_PARENT_PID`, `OCCAM_PARENT_LABEL`, `OCCAM_OWNER_LABEL`, `OCCAM_HOME`.

**Referenced but defined outside file (daemon gate for prewarm):** `OCCAM_HTTP_DAEMON` via `HttpDaemonHost.IsEnabled`.

### 1.15 Automatic / silent behaviors (lens)

| Trigger | Visible? | Configurable? | Disableable? | Effect |
|---------|----------|---------------|--------------|--------|
| `AddOccamMcpServer` | stderr banner | OCCAM_BANNER | yes=0 | Operator UX |
| Same | background HTTP daemon spawn | OCCAM_HTTP_DAEMON + PREWARM | yes | Perf / process tree |
| `AddOccamCore` resolve | may create signing key on disk | OCCAM_KEYS_ROOT | N/A (always LoadOrCreate) | Persistence / trust |
| `InstallShared` on each session DI | pool StopAll | — | — | **Cross-session browser pool kill** |
| OCCAM_LOG=1 | USD “saved” line | WT_TOKEN_USD_PER_M | OCCAM_LOG=0 | Cosmetic cost claim |
| Atlas opt-in | sink swap | OCCAM_ATLAS_MCP | leave unset | Aggregates + last-wins telemetry |

### 1.16 Platform diffs in scope

| Diff | Evidence |
|------|----------|
| Windows VT switch | `Program.cs:6` |
| `install-browser`: Windows `cmd.exe /c npx` vs Unix `npx` | `OccamCliVerbs.cs:134-147` |
| ReceiptSigner POSIX 0600 only (out of Cli but triggered by keys export / DI) | `ReceiptSigner.cs:84-98` |
| OccamUserPaths / keys use UserProfile (Windows vs Unix home) | `OccamUserPaths.cs`; `ReceiptSigner.cs:80-82` |

---

## 2. Gap classification

Compared **after** inventory against: `CAPABILITY-INVENTORY.md`, `capabilities.json`, `CAPABILITY-GRAPH.md`, `ARTIFACT-MAP.md`, `CODE-DERIVED-WORKFLOWS.md`, `NONCORE-SURFACE-MAP.md`, `RUNTIME-MODES.md`, `subsystems/runtime-mcp.md`, `subsystems/failure-atlas.md`, `subsystems/config-env.md`, `ENVIRONMENT-VARIABLES.md`, `CLI-SURFACE.md`, `ENGINEERING-FINDINGS.md` (EF-024).

| ID | Gap | Label | Code evidence | Notes |
|----|-----|-------|---------------|-------|
| G01 | Transport modes, bind asymmetry, JWT remote, query token ban, health, max sessions, profile, instructions, opt-in orthogonality, binding guard, prewarm, banner, WS size cap, CLI verbs catalog | COVERED_EXACTLY | CAP-003…028, CAP-1000…1002, CLI-SURFACE CAP-920+ | Solid Wave1/3 coverage |
| G02 | FailureAtlas **per-session DI** under WS/Remote (EF-024 WITHDRAWN / CAP-1000) | COVERED_EXACTLY | B54–B55; `ENGINEERING-FINDINGS.md` EF-024 | Independent re-verify **confirms** withdrawal |
| G03 | Stale S3-04 text still claims process-wide atlas multi-tenant leak (EF-019 in failure-atlas.md) | COVERED_WRONG | `failure-atlas.md:222-241` vs B55 | Model ledger fixed; **subsystem report not** |
| G04 | CAP-021 frames Content-Length as “stdio protocol”; live use is **WS↔SDK stream adapter**; stdio uses SDK transport; Stdio channel dead | COVERED_WRONG | B18, B37–B38; CAP-021 wording | Prefer edge correction over new CAP |
| G05 | CAP-1003 documents dead outer registration for **WS only**; Remote has same pattern | COVERED_PARTIALLY | B32; CAP-1003 | Extend CAP-1003 / new edge |
| G06 | Local WS “single client” vs unlimited concurrent accepts | COVERED_PARTIALLY | B20, B23 | Comment vs code |
| G07 | `--jwt-jwks-uri` / `OCCAM_JWT_JWKS_URI` fed to OIDC **MetadataAddress** (not raw JWKS) | COVERED_PARTIALLY | B14, B26; ENVIRONMENT-VARIABLES note | Name implies JWKS JSON |
| G08 | Opt-in atlas sink last-wins + new inner logger | COVERED_EXACTLY | CAP-875 | |
| G09 | USD cost display / WT_TOKEN_USD_PER_M | COVERED_EXACTLY | config-env CAP-391 area | |
| G10 | `version-surface` / `lifecycle` exist as product verbs | COVERED_EXACTLY | CAP-920…922 | |
| G11 | Help omits those verbs | MISSING_RUNTIME_SURFACE | B15 vs B66–B67 | Operator discoverability |
| G12 | **Process-global `BrowserPoolManager.InstallShared` StopAll on every new WS/Remote session DI** — breaks “isolation” for browser pool across concurrent sessions | MISSING_EDGE / MISSING_SECURITY_SEMANTIC | B49; `BrowserPoolManager.cs:45-48` | Top miss; not in CAP-1000 narrative |
| G13 | Banner always claims stdio listening under all transports | MISSING_EDGE | B60 | Misleading operator signal |
| G14 | `OccamJsonPrintableEscapes` MCP wire escape relaxation | MISSING_ARTIFACT | B77; **zero** docs-audit hits | Affects agent-visible markdown quotes |
| G15 | `OccamEnvironment.GetExistingFile` never used | DEAD_CODE_MISTAKEN_AS_PRODUCT | B75; config-env documents API as live primitive | |
| G16 | Stdio `IMcpTransport` channel / Read/Send unused after SDK | DEAD_CODE_MISTAKEN_AS_PRODUCT | B18 | |
| G17 | `~/.occam` layout as artifact map (keys/sessions/playbooks; plus watch/jobs siblings) | MISSING_ARTIFACT | B72–B73; ARTIFACT-MAP thin on host data root | |
| G18 | PEM cert + `--tls-password` ignored | MISSING_EDGE | B33 | Config surprise |
| G19 | Default issuer `occam-mcp` forces metadata/HTTPS-issuer gate | MISSING_CONFIG | B12–B13 | Surprising default |
| G20 | Full-profile instructions advertise `occam_watch` even when `OCCAM_WATCH_MCP` off | MISSING_EDGE | `OccamServerInstructions.cs:112` vs B42 | Discoverability vs honesty |
| G21 | Concurrent local WS sessions share process-static pool via InstallShared (related G12) | MISSING_FAILURE_SEMANTIC | B20+B49 | Session A pool killed when B connects |
| G22 | Outer+per-session double prewarm/DI cost on Remote | COVERED_PARTIALLY | CAP-1003 WS-focused; B32, B46 | |
| G23 | HostIdentity / lifecycle diagnose workflow | COVERED_EXACTLY | CAP-921/922; FLOW adjacent | |
| G24 | AOT source-gen JSON contexts on CLI verbs | COVERED_PARTIALLY | implied by receipts audit; not host-scoped | |

---

## 3. Proposed additions (orchestrator allocates IDs)

### NEW_CAP_CANDIDATES
- **CAP-NEW-A-1** — Process-global browser pool `InstallShared`/`StopAll` across per-session DI hosts (WS/Remote concurrency hazard). Prefer edge on CAP-1000 if orchestrator rejects new CAP.
- **CAP-NEW-A-2** — MCP wire printable-escape relaxation (`OccamJsonPrintableEscapes`) as returned-artifact transform.

### NEW_EDGES
- `TRANSPORT:websocket|CONCURRENT_SESSIONS|unlimited` (no semaphore)
- `TRANSPORT:websocket|USES|CAP-NEW-A-1` (InstallShared)
- `TRANSPORT:remote|USES|CAP-NEW-A-1`
- `TRANSPORT:remote|HAS|CAP-1003-like-outer-dead-registration`
- `CAP-021|CORRECT_TO|WS_stream_ContentLength_adapter_not_stdio_primary`
- `TOOL:occam_failure_atlas|LIFETIME|per_session_DI_on_WS_Remote` (affirm CAP-1000; contradict stale failure-atlas EF-019)
- `INSTRUCTIONS:full|ADVERTISES|occam_watch` `WITHOUT` requiring `OCCAM_WATCH_MCP`

### NEW_ARTIFACTS
- ART-NEW-A-1: `OccamJsonPrintableEscapes`-relaxed tool JSON strings on MCP wire
- ART-NEW-A-2: `~/.occam/` user-data root layout (keys/, sessions/, playbooks/local/, …)

### NEW_WORKFLOWS
- WF-NEW-A-1: Operator remote bring-up — TLS cert + HTTPS metadata (or HTTPS issuer) + Bearer header (never query) + `/health` probe
- WF-NEW-A-2: Undocumented binary verbs `version-surface` / `lifecycle self|diagnose` (help gap)

### EFC (propose; orchestrator → EF-041+)
- **EFC-A-1** | BUG-CANDIDATE / SECURITY | G12/G21 | Concurrent WS/Remote session start calls `InstallShared` → `StopAll` on prior pool — cross-session browser extract disruption. Confidence: PROVEN (static). Needs multi-session repro.
- **EFC-A-2** | OBSERVATION | G03 | `subsystems/failure-atlas.md` still asserts process-wide atlas leak after EF-024 WITHDRAWN. Confidence: PROVEN (doc vs code).
- **EFC-A-3** | OBSERVATION | G20 | Full instructions mention opt-in `occam_watch` when flag off. Confidence: PROVEN.
- **EFC-A-4** | OBSERVATION | G11 | `WriteUsage` omits shipped `version-surface` / `lifecycle`. Confidence: PROVEN.
- **EFC-A-5** | OBSERVATION | G04 | CAP-021 “stdio framing” label mismatches WS adapter usage. Confidence: PROVEN.

---

## 4. Cross-cutting lens summaries

### Automatic / silent (top)
1. Per-session DI rebuild + **InstallShared StopAll** (G12)  
2. HTTP daemon prewarm on every `AddOccamMcpServer` (outer + session)  
3. ReceiptSigner LoadOrCreate disk key on DI build  
4. Banner + optional USD “savings” on OCCAM_LOG  

### Failure / fallback (top)
1. Remote auth/capacity/query-token typed HTTP errors  
2. Binding guard → `invalid_arguments` tool result  
3. Invalid OCCAM_PROFILE / GetInt clamp → stderr + safe default  
4. WS oversize / binary → `InvalidDataException` (session tear)  
5. Atlas host cap → silent stop tracking  

### Config gaps (top)
1. Default JWT issuer `occam-mcp` non-discoverable without metadata  
2. JWKS URI name vs MetadataAddress semantics  
3. PEM ignores tls-password  
4. GetExistingFile dead  
5. Instructions advertise watch without env gate  

### Platform diffs (top)
1. install-browser Windows cmd vs Unix npx  
2. UserProfile-based `~/.occam`  
3. Console VT enable on Windows  

---

## 5. Convergence

**CONVERGENCE_IN_SCOPE: YES** for cataloged transport/profile/opt-in/CLI verb surfaces (Wave 1+3 already dense). **NO** for the InstallShared × per-session DI interaction and OccamJsonPrintableEscapes artifact — those are material unmodeled behaviors found only by adversarial host-scope pass.

**UNCERTAINTIES**
- Whether MCP SDK stdio path uses any Content-Length framing internally (outside this glob) — does not change B37 WS adapter fact.  
- Live multi-session InstallShared repro not executed (static PROVEN only).  
- Whether outer Remote DI’s unused FailureAtlasStore is ever resolved (likely never if nothing requests it from RequestServices).  

---

## 6. Files read (scope)

`Program.cs`; `Composition/OccamServiceCollectionExtensions.cs`; `Configuration/OccamEnvironment.cs`; `Lifecycle/HostIdentity.cs`; `Cli/OccamCliVerbs.cs`; `Portable/OccamUserPaths.cs`; `Json/OccamJsonPrintableEscapes.cs`; `Abstractions/*` (3); `Transport/*` (12); `Telemetry/*` (10); plus spot `BrowserPoolManager.InstallShared`, `ReceiptSigner.DefaultKeysRoot` for path/lifetime evidence. Compare pass: listed model files in §2 header.
