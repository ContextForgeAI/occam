# AUTOMATIC-BEHAVIORS (Wave 4 Phase 4H)

Behaviors users/agents do **not** explicitly request. Sources: A–H.

| # | TRIGGER | BEHAVIOR | VISIBLE? | CONFIGURABLE? | DISABLEABLE? | ARTIFACT | TRUST | PERF | EVIDENCE |
|---|---------|----------|----------|---------------|--------------|----------|-------|------|----------|
| 1 | MCP host start (stdio/WS/Remote) | `ReceiptSigner.LoadOrCreate` mints/loads ECDSA key | no | path via `OCCAM_KEYS_ROOT` | no (always) | `signing-key.pem` | key exists even if receipts off | disk once | `OccamServiceCollectionExtensions.cs:23` |
| 2 | MCP host start + default | HTTP daemon prewarm | banner/log | `OCCAM_HTTP_DAEMON_PREWARM` | `"0"` | warm daemon | — | cold-start↓ | `OccamMcpServerRegistration.cs:46` |
| 3 | New WS/Remote session DI | `InstallShared` → `StopAll` prior pool | latency only | no | no | killed browsers | — | **spike** | `BrowserPoolManager.cs:45-48` |
| 4 | Every successful extract path | post-processors (challenge/login/thin/EQM) | failure codes / quality | — | — | may downgrade ok | trust | — | PostProcessors/* |
| 5 | `http_then_browser` + public-ref HTTP fail | skip browser silently (policy) | same failure as HTTP | domain tiers / `OCCAM_DOMAIN_TIERS_PATH` | via tiers file | — | — | saves browser | `OccamRouter.cs:149-152` |
| 6 | Features scope / tool defaults | inject `json_blocks`/`json_tables` features into workers | response shape | tool params | omit params | plugins run | — | CPU | OccamFeaturesScope / runners |
| 7 | Browser extract | stealth + **bypassCSP:true** | no | no | **no** | page under CSP bypass | **weakens page CSP** | — | `browser-session.mjs:143` |
| 8 | Browser extract | consent dismiss + CSS-hide + forced aggressive retry | no | extract variant / recipe | partial (`WT_*`) | mutated DOM | may hide real UI | +latency | consent.mjs; browser-session |
| 9 | Browser extract default | virtual-scroll simulation | no | `WT_VIRTUAL_SCROLL` | `"0"` | more content | — | +latency | virtual-scroll.mjs |
| 10 | No Chromium | auto-provision user Chromium | `browser_provisioned` | `OCCAM_BROWSER_AUTOINSTALL` | `"0"` | browser bits | — | first-call cost | browser-provision.mjs |
| 11 | Recipe match | cookie inject / host recipe prune | recipe path | recipe registry | no recipe | cookies | privacy | — | recipes/*; cookie-inject |
| 12 | Playbook interaction plan | `page.evaluate` / waitForFunction | heal/extract path | playbook content | no plan | DOM actions | **code-exec surface** | — | interaction-steps.mjs:14 |
| 13 | `OCCAM_SITE_GENOME_FETCH` / tool flag | live `/.well-known` genome fetch | resolve fields | env + param | off default | genome overlay | network trust | — | WellKnownGenomeFetcher |
| 14 | playbook_save | always sign + write verify score into provenance | `SignedKeyId` | **not** via `OCCAM_RECEIPTS` | cannot disable sign | signed PB | trust | — | PlaybookSaveService:86 |
| 15 | Ambient client capabilities | sizes max_tokens → MaterializationKey/cache identity | after `client_capabilities` | env bootstrap | omit call | cache key shift | — | — | ClientCapabilityStore |
| 16 | Cache eligible success | write full post-sign envelope to disk | transparent | `OCCAM_CACHE_DIR` / eligibility | eligibility rules | OccamCacheEntry | replays receipt | I/O | TranscodeResponseCache |
| 17 | URL fragment present | implicit focus without `focus_query` | section rank | — | strip fragment | focus intent | may collide cache | — | FocusIntent |
| 18 | Canonical materialization | build then **discard** IR (Merkle leaves etc.) | none | — | — | none retained | — | **CPU waste** | TranscodeToCanonical; EF-004 extend |
| 19 | Search results | extractability scorer + optional probe fan-out | scores | provider env | no provider | ranked hits | honesty gap on paywall | N probes | SearchExtractabilityScorer |
| 20 | Robots/throttle env | polite delay / allow | rarely | `OCCAM_RESPECT_ROBOTS`, throttle ms | default off | — | fail-open | delay | RobotsThrottleService |
| 21 | Proxy list configured | round-robin rotation + one-shot spawn side effect | via egress | list/file | unset | — | empty-file swallows inline | — | ProxyRotation* |
| 22 | CSS Nuxt match | `(0,eval)(__NUXT__)` | extracted fields | schema attr | no Nuxt attr | facts | **page-controlled eval** | — | css-schema-extract; EF-013 |
| 23 | OCCAM_LOG on | stderr USD savings estimate | stderr | `OCCAM_LOG`, `WT_TOKEN_USD_PER_M` | off default | — | — | — | OccamStderrAnsiSink |
| 24 | Banner default on | always prints stdio listening line | stderr | `OCCAM_BANNER` | `"0"` | — | wrong on WS/Remote | — | BannerModel |

| 25 | `occam refresh` / stop-occam | name-wide kill of every `OccamMcp.Core[.exe]` | process death | no scope flag | no | — | collateral | — | stop-occam-processes.mjs |
| 26 | every `launch-mcp-host` | merge `~/.occam/onboard.json` env into host | no | edit onboard.json | delete file | host env | config | — | launch-mcp-host.mjs:29 |
| 27 | skill install | `rmSync` dest then copy | no confirm | — | — | wiped skill dir | — | — | install-occam-skill |
| 28 | marketplace validate success/skip | auto-merge squash to main | PR merge | workflow | branch protection? | community PB on main | **supply chain** | — | playbook-marketplace.yml |
| 29 | Docker missing profiles/ | silent built-in seed/tier/defaults | no | bind-mount | — | — | behavior drift vs Level B | — | Dockerfile vs PlaybookPaths |

## Major silent subsystem?
No *new* silent subsystem beyond what Waves 1–3 already named (escalation, receipts, cache, browser pool, consent, genome, connect, install). Wave 4 finding: **understatement / danger of already-known automatics** (bypassCSP, InstallShared kill, fragment collision, css SSRF asymmetry, save-always-sign, name-wide kill, marketplace auto-merge).
