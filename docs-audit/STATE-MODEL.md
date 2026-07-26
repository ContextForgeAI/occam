# STATE-MODEL (Phase 5K)

**Agent:** P5-07  
**SoT:** executable code. Wave-4 correction layer overrides older prose (C2 atlas WITHDRAWN; C6 receipts incompleteness; ART-034…037).  
**Docs (`docs/`, README) are untrusted.**  
**Date:** 2026-07-26

---

## 0. Verdict on “no file cache by design”

| Claim slice | Verdict | What is true |
|-------------|---------|--------------|
| **Default extract path does not reuse prior page content** | **TRUE** | `cache_ttl_s` omitted or `≤0` → `TranscodeCacheEligibility.IsCacheable` false; no read/write (`TranscodeCacheEligibility.cs:13-16`; `OccamTranscodeTool.cs:63,119`). |
| **Occam keeps no durable on-disk state** | **FALSE** | Large footprint under `~/.occam/` (keys, sessions, playbooks, watch, batch) plus opt-in cache, Playwright browser cache, host MCP configs, skill dirs. |
| **No extract-result cache exists** | **FALSE when opted in** | `FileTranscodeResponseCache` writes full post-sign envelopes under `OCCAM_CACHE_DIR` or `{TEMP}/occam-cache` (ART-035, FLOW-019). |
| **No secrets on disk** | **FALSE** | Session cookies/headers, `_imports/` plaintext (EF-054), unencrypted `signing-key.pem` (ART-034 / EF-044). |

**Honest product statement:** “No file cache by design” means **live extract is the default and content reuse requires an explicit `cache_ttl_s`**. It does **not** mean Occam is stateless, secret-free, or footprint-free.

---

## 1. Classification legend

| Class | Meaning |
|-------|---------|
| `EPHEMERAL` | Per-call / short-lived files; intended delete on dispose |
| `PROCESS` | Lives in host/worker process memory (or process-scoped files that die with process) |
| `SESSION` | Scoped to one MCP transport session / DI container lifetime |
| `PERSISTENT` | Survives process restart; Occam-owned under user data or cache dirs |
| `PORTABLE ARTIFACT` | Caller- or release-owned bytes that leave the host (responses, tarballs, manifests) |
| `HOST CONFIGURATION` | Mutates IDE/CLI/agent host configs or operator launch config outside Occam’s install tree |

---

## 2. State inventory

Evidence IDs: ART / EF / CAP / FLOW / GAP as cited. Paths are defaults; env overrides listed under USER CONTROL.

| ID | State | CLASS | LOCATION | LIFETIME | OWNER | PERSISTENCE | SECURITY | PORTABILITY | CLEANUP | USER CONTROL | Evidence |
|----|-------|-------|----------|----------|-------|-------------|----------|-------------|---------|--------------|----------|
| ST-01 | Session profile JSON | PERSISTENT | `~/.occam/sessions/<id>.json` (`OCCAM_SESSIONS_ROOT`) | Until operator deletes | Operator / `occam session` | Disk | **HIGH** (cookies, Auth headers) | Machine-local | Manual only | `OCCAM_SESSIONS_ROOT`; omit `session_profile` | ART-026; `SessionProfileHeaders.cs:115-129`; CAP-884 |
| ST-02 | Playwright storageState | PERSISTENT | `~/.occam/sessions/states/<name>.json` | Until deleted | Operator export-state | Disk | **HIGH** (browser cookies/localStorage) | Machine-local | Manual | Same root; Tier-1 tools only use it | CAP-880/885; session-lifecycle |
| ST-03 | Import raw cookies retain | PERSISTENT | `…/sessions/_imports/<file>` | Permanent by default | `occam session import` | Disk plaintext | **HIGH** (EF-054) | Machine-local | Manual; `--no-keep-import` skips copy | `--no-keep-import` | ART-037; EF-054; GAP-038; `occam-sessions-lib.mjs:105,161` |
| ST-04 | Request headers temp | EPHEMERAL | `{TEMP}/occam-headers-{guid}.json` | Per `FetchHeadersScope` | Host | Temp disk | **HIGH** while alive | None | `Dispose` + background retry | None (always when headers merged) | `FetchHeadersScope.cs:36-58`; CAP-881 |
| ST-05 | CSS field-spec temp | EPHEMERAL | `{TEMP}/occam-fields-{guid}.json` | Per css-extract call | Host | Temp disk | MED (schema/selectors) | None | Best-effort `File.Delete` | N/A | ART-036; `CssExtractWorker.cs:23,130` |
| ST-06 | Transcode response cache | PERSISTENT | `OCCAM_CACHE_DIR` or `{TEMP}/occam-cache/<key>.json` | Until TTL expiry **on read** or manual delete | Host (opt-in write) | Disk full envelope | **HIGH** (page markdown + receipt) | Relocatable via env | Delete on TTL miss only — **no sweep of unexpired/orphan** | `cache_ttl_s`; eligibility rules; `OCCAM_CACHE_DIR` | ART-035; FLOW-019; EF-001/045; `TranscodeResponseCache.cs:37-135` |
| ST-07 | Local playbooks | PERSISTENT | `~/.occam/playbooks/local/` (`OCCAM_PLAYBOOKS_LOCAL_ROOT`) | Until overwrite/delete | `occam_playbook_save` | Disk signed JSON | MED (site recipes; may encode intent) | Copyable files | Overwrite by id; no GC | `OCCAM_PLAYBOOKS_LOCAL_ROOT` | ART-015; `PlaybookPaths.cs:10-23`; EF-005 |
| ST-08 | User/org playbooks | PERSISTENT | `WT_PLAYBOOKS_PATH` (if set) | Operator-owned | Operator | Disk | MED–HIGH | Operator path | Operator | `WT_PLAYBOOKS_PATH` | `PlaybookPaths.cs:27-35` |
| ST-09 | Community + seed playbooks | PORTABLE ARTIFACT / install | `{OCCAM_HOME}/profiles/playbooks/{community,seeds}/` | Install lifetime | Install / marketplace CI | Disk in install tree | MED (unsigned community manifest integrity) | Ships with install | Reinstall / marketplace | Bind-mount / Level B tree | `PlaybookSeedResolver.cs:334-338`; FLOW-022; EF-052 |
| ST-10 | Genome fetch cache | PROCESS | In-memory `ConcurrentDictionary` per host | 1h TTL or process death | `WellKnownGenomeFetcher` | RAM only | LOW–MED (remote JSON) | None | TTL expiry; process exit | Disable fetch: leave param/env off (`OCCAM_SITE_GENOME_FETCH`) | E-trust §1.7; `WellKnownGenomeFetcher.cs:19-45,161` |
| ST-11 | Playbook seed list cache | PROCESS | In-memory resolver cache | Until save bust / process exit | `PlaybookSeedResolver` | RAM | LOW | None | `ClearCacheForTests` on save | N/A | `PlaybookSeedResolver.cs:37-38,181-188`; `PlaybookSaveService.cs:94` |
| ST-12 | Domain tier registry cache | PROCESS | Static in-memory | Process | `DomainTierRegistry` | RAM | LOW | None | Process exit | `OCCAM_DOMAIN_TIERS_PATH` | `DomainTierRegistry.cs:17,274-326` |
| ST-13 | Watch store + history | PERSISTENT | `~/.occam/watch/watch.json` (`OCCAM_WATCH_DB_PATH`) | Opt-in tool; URLs uncapped | `occam_watch` | Disk | **HIGH** (URLs + hashes + signed chain) | Relocatable | History capped **64/URL**; **no MCP un-watch** (`Remove` unused — EF-020); `reset` restarts one URL chain | `OCCAM_WATCH_MCP`; `OCCAM_WATCH_DB_PATH`; `reset` | ART-025/028; EF-020; `WatchStore.cs:30-72`; `WatchService.cs:76-78` |
| ST-14 | Receipt / capsule / time-anchor in responses | PORTABLE ARTIFACT | MCP/CLI response (caller may store) | Caller-owned | Tools gated by `OCCAM_RECEIPTS`* | Ephemeral unless caller persists | MED–HIGH (contentHash; capsule may embed markdown) | Yes | Caller | `OCCAM_RECEIPTS`; `OCCAM_TIME_ANCHOR`+`OCCAM_TSA_URL` | ART-007…009,006; *save bypasses (EF-005) |
| ST-15 | Host signing key | PERSISTENT | `~/.occam/keys/signing-key.pem` (`OCCAM_KEYS_ROOT`) | Forever unless deleted | DI `LoadOrCreate` on every host start | Disk PKCS8 **unencrypted** | **CRITICAL** | Machine identity | Manual delete (re-mints next start) | Path via `OCCAM_KEYS_ROOT`; **cannot disable mint** (EF-044) | ART-034; EF-044; `ReceiptSigner.cs:26-41`; `OccamServiceCollectionExtensions.cs:23` |
| ST-16 | Dataset rows + manifest | PORTABLE ARTIFACT | Response / CLI verify input | Caller-owned | `occam_dataset_export` | Not auto-persisted by host | MED–HIGH | Yes | Caller | `OCCAM_RECEIPTS` for sigs | ART-022 |
| ST-17 | Batch job store | PERSISTENT | `~/.occam/jobs/jobs.json` (env `OCCAM_BATCH_DB_PATH` default `.db` → store forces `.json`) | Opt-in; **no eviction** | Batch MCP / `--batch-server` | Disk: full markdown retained | **HIGH** (EF-037) | Relocatable | **None** (no delete API) | `OCCAM_BATCH_MCP` / batch-server; `OCCAM_BATCH_DB_PATH` | ART-027; EF-037/038; `JsonFileBatchJobStore.cs:28-29`; `BatchSettings.cs:25-37` |
| ST-18 | Failure atlas | SESSION | In-memory DI singleton per transport session | Session / process (stdio = process) | `FailureAtlasStore` when `OCCAM_ATLAS_MCP` | **Not durable** | LOW (hostnames + failure codes) | None | Process/session end; host cap 500 | `OCCAM_ATLAS_MCP` | EF-024 **WITHDRAWN**; `FailureAtlasStore.cs:5-17`; C2 |
| ST-19 | Client ambient budget | PROCESS | `ClientCapabilityStore` singleton | Process (+ env bootstrap) | `occam_client_capabilities` / `OCCAM_CLIENT_CONTEXT_TOKENS` | RAM | LOW | None | Process exit | Omit tool; unset env | ART-023; `ClientCapabilityStore.cs:20-25` |
| ST-20 | Browser pool / daemon slots | PROCESS | Child `browser-daemon.mjs` processes; static `_shared` manager | Until `StopAll` / host exit | `BrowserPoolManager` | Process | MED (anonymous cookie jar CAP-882) | None | Idle timer / recycle / InstallShared kill | Pool env knobs | EF-041; `BrowserPoolManager.cs:45-48`; CAP-881/882 |
| ST-21 | Playwright browser bits | PERSISTENT | `ms-playwright` / `PLAYWRIGHT_BROWSERS_PATH` / `OCCAM_PLAYWRIGHT_BROWSERS_PATH` | Until deleted | doctor / provision / Playwright | Disk **outside** `.occam` | LOW | Relocatable | Manual / OS cache clean | Path envs; `OCCAM_BROWSER_AUTOINSTALL=0` | `playwright-cache.mjs`; `PlaywrightEnvironment.cs` |
| ST-22 | Onboard config | HOST CONFIGURATION | `~/.occam/onboard.json` (`OCCAM_CONFIG`) | Permanent | `occam onboard` | Disk | MED (env map may hold secrets) | Machine | Manual delete | `OCCAM_CONFIG`; delete file | ART-029; EF-029/050; `onboard-schema.mjs:21` |
| ST-23 | Connect last-run | HOST CONFIGURATION | `~/.occam/connect-last.json` | Permanent | `occam connect` / get-ff-occam | Disk | LOW | Machine | Manual | N/A | ART-030; `occam-connect.mjs:94` |
| ST-24 | Host MCP config + bak | HOST CONFIGURATION | Host paths (e.g. `.cursor/mcp.json`) + `*.occam-bak` | Permanent / until restore | Connect CONFIG_FILE adapters | Disk | MED (command/env) | Host-specific | Rollback often dead when `requiresRestart` (EF-021) | Connect policy / manual edit | ART-031; EF-021; `config-engine.mjs:310-316` |
| ST-25 | Skill install trees | HOST CONFIGURATION | `~/.cursor/skills/occam`, Claude/Hermes/… | Until reinstall | skill install | Disk | LOW | Per host | `rmSync` on reinstall (destructive) | Platform flags | AUTOMATIC #27; EF-036 |
| ST-26 | Install tree / Level B | PORTABLE ARTIFACT | `OCCAM_HOME` / release extract dir | Until uninstall | Installer | Disk | LOW | Yes | Installer `rm -rf` before extract (EF-028 — no rollback) | Install dir | ART-032; EF-028 |
| ST-27 | Cosign release `.bundle` | PORTABLE ARTIFACT | GH Release asset | Release lifetime | CI `sign-release` | Remote | LOW (unused by install) | Yes | N/A | Manual verify only | ART-038; EF-053 |
| ST-28 | Stderr logs / banner | EPHEMERAL | Host stderr (not Occam log files) | Stream | Host / operator capture | None by Occam | LOW (may echo URLs) | N/A | Operator log rotation | `OCCAM_LOG`, `OCCAM_BANNER=0` | AUTOMATIC #23–24 |
| ST-29 | Process identity / kill targets | PROCESS | OS process table (`OccamMcp.Core[.exe]`) | While running | refresh / stop-occam | N/A | Availability (collateral kill) | N/A | Kill ends process | **No scope flag** (EF-049) | FLOW-021; EF-049 |

\*Receipts master switch incomplete: playbook_save always signs (EF-005); key always minted (EF-044).

---

## 3. Footprint outside Occam’s own directory

“Own directory” = install/`OCCAM_HOME` tree. Everything below is written **elsewhere** by Occam or its operator tools.

| Outside path (typical) | Writer | Contents |
|------------------------|--------|----------|
| `~/.occam/**` | Host DI, tools, onboard, session CLI, watch, batch | Keys, sessions, playbooks, watch, jobs, onboard, connect-last |
| `{TEMP}/occam-cache/**` | Response cache (default dir) | Full success envelopes |
| `{TEMP}/occam-headers-*.json` | FetchHeadersScope | Merged request headers |
| `{TEMP}/occam-fields-*.json` | CssExtractWorker | Field-spec JSON |
| OS Playwright cache (`%LOCALAPPDATA%/ms-playwright`, `~/Library/Caches/ms-playwright`, `~/.cache/ms-playwright`) | doctor / provision / Playwright | Chromium binaries |
| Host MCP config files (Cursor/Claude/… paths from connect registry) | `occam connect` | Server launch entries |
| `*.occam-bak` beside those configs | Connect backup | Pre-mutation snapshot |
| Agent skill dirs under home / project | skill install | Skill card copy; may edit `AGENTS.md` |
| Optional `OCCAM_CACHE_DIR` / `OCCAM_KEYS_ROOT` / `OCCAM_*_DB_PATH` / `WT_PLAYBOOKS_PATH` | Relocated stores | Same sensitivity as defaults |
| Third-party managed provider APIs | Managed backend (when configured) | URL/content egress — not a local file, but off-machine state |

**Uninstall implication:** Removing `OCCAM_HOME` alone **does not** remove `~/.occam/`, Playwright cache, host MCP configs, skill dirs, or temp cache leftovers.

---

## 4. What survives restart / upgrade / uninstall

| Event | Survives | Lost |
|-------|----------|------|
| **Host process restart** | All `PERSISTENT` / `HOST CONFIGURATION` / Playwright cache; signing key; sessions; playbooks; watch; batch; onboard | PROCESS/SESSION RAM (atlas, genome cache, seed cache, client budget unless env bootstrap, browser pool — **recreated**; key remounts from disk) |
| **Upgrade (replace install tree)** | `~/.occam/**`, host configs, Playwright cache, relocated env paths | Install-tree seeds/community unless preserved; Docker without bind-mount falls back to built-ins (AUTOMATIC #29 / GAP-043) |
| **Uninstall (`rm` install dir only)** | **Entire `~/.occam` footprint**, host MCP configs + bak, skills, Playwright cache, temp cache | Binary/workers under install dir |
| **`occam refresh` / stop-occam** | Disk state | Running hosts **machine-wide by binary name** (EF-049) — collateral across installs |

---

## 5. State with no cleanup path (unbounded or permanent)

| State | Growth / retention | Gap |
|-------|-------------------|-----|
| Batch `jobs.json` | Full markdown forever; no delete API | EF-037 |
| Watch URL set | Per-URL history capped at 64; **URL count uncapped**; `IWatchStore.Remove` has **no product caller** | EF-020 |
| Response cache | TTL delete **only on read**; orphans/unexpired pile under temp or `OCCAM_CACHE_DIR` | GAP-022 edge |
| `_imports/` | Permanent plaintext by default | EF-054 |
| `signing-key.pem` | Permanent; recreated if deleted | EF-044 |
| Local playbooks | Permanent until overwrite | — |
| Host MCP bak / connect-last / onboard | Permanent | EF-021 rollback holes |
| Anonymous browser context cookies | Bounded by 10 runs / 400 MB recycle — not disk, but cross-call bleed | CAP-882 |

---

## 6. Multi-process / multi-session concurrency

| Shared resource | Sharing rule | What breaks |
|-----------------|--------------|-------------|
| Browser pool `_shared` | Process-wide static | New WS/Remote DI → `InstallShared` → `StopAll` prior pool (**EF-041**, GAP-002) |
| Anonymous `BrowserContext` | Shared across anonymous calls | Cookie bleed CAP-882 |
| Headered/session calls | Per-call GUID headers path | Forces recycle; kills warm reuse (CAP-881) |
| Batch `jobs.json` | Per-process lock; load-once model | Two processes → last-writer-wins (**EF-038**) |
| Watch `watch.json` | In-process lock; non-atomic write | Multi-process race (E-trust) |
| `signing-key.pem` | Shared file | First writer wins; multi-host same key identity |
| Failure atlas | **Per DI session** (stdio = one; WS/Remote = new each session) | **Not** process-wide leak — EF-024 WITHDRAWN (C2) |
| Response cache dir | Shared files by key | Fragment collision **EF-045**; annotation key gaps **EF-001** |
| `onboard.json` env | Merged into every `launch-mcp-host` | Cross-tool env pollution (**EF-050**) |
| Process kill by name | Machine-wide | Collateral kill of other installs (**EF-049**) |

---

## 7. Privacy summary

| Sensitivity | State items | May contain |
|-------------|-------------|-------------|
| **Credentials / session secrets** | ST-01…04, ST-03 `_imports`, ST-22 env map | Cookies, Authorization, Cookie jars, storageState |
| **Private key material** | ST-15 | ECDSA PKCS8 PEM |
| **Page content** | ST-06 cache, ST-17 batch markdown, ST-14 capsules/receipts if caller-stored | Full extracts |
| **Private / sensitive URLs** | ST-13 watch, ST-17 batch, logs | Operator URLs |
| **Third-party egress** | Managed provider (config) | URL + page to Firecrawl/Jina/… when enabled |
| **Relatively safe aggregates** | ST-18 atlas (codes/hosts), ST-12 tiers | No body text |

---

## 8. Taxonomy note (PS-1…9)

State is a **cross-cutting lens**, not a product system. Heaviest writers: **PS-6 Trust** (key), **PS-5 Playbooks**, **PS-7 Monitoring** (watch/batch), **PS-9 Operator** (onboard/connect/session/skills), **PS-2 Materialization** (opt-in cache). Hypothesis accepted: do not invent a tenth “State” system — document it as this lens.

---

## 9. Corrections to prior model

1. “No file cache by design” ≠ “stateless product” (Wave-4 STATEMENT_10).
2. Batch store is **JSON** `jobs.json`, not live SQLite (despite `OCCAM_BATCH_DB_PATH` / `.db` naming) — `JsonFileBatchJobStore.cs:5-11,28-29`.
3. Atlas is **SESSION/process memory**, not durable multi-tenant leak — EF-024 WITHDRAWN.
4. Signing key + always-sign save are first-class state/trust facts (ART-034, EF-005/044), not optional receipt cosmetics.
5. Outside-directory footprint (§3) is required for uninstall/privacy honesty.

---

## 10. Uncertainty

| Item | Status | Resolve by |
|------|--------|------------|
| Whether any operator script ever deletes TTL-expired cache without a read | Source: delete only in `TryGet` expiry path | Optional sweep grep (likely none) |
| Exact host adapter path matrix for every connect target | SUPPORTING in CONNECT-PLATFORM | Not re-enumerated here |
| Runtime EF-041 dual-WS repro | Source-proven only (Wave-4) | Optional live repro |
