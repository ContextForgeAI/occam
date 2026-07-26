# NEGATIVE-SPACE-GAPS (Wave 4 Phase 4D)

**Status:** COMPLETE (all 8 partitions A–H landed + orchestrator verification of criticals).
**Method:** CODE DISCOVERY → independent inventory → model comparison → gap class. Docs untrusted.

## Severity rollup (final)

| Severity | Count | Notes |
|----------|-------|-------|
| CRITICAL_MODEL_GAP | 2 | Cascade prose wrong (CAP-052/104); InstallShared×per-session DI kills pool |
| MAJOR_PRODUCT_GAP | 12 | A–F eight + G name-wide kill; H marketplace auto-merge-without-gate; H Docker HEALTHCHECK hang; H cosign/bundle trust theater |
| MEDIUM_DETAIL_GAP | ~35 | see catalog + blind reports |
| MINOR_DETAIL | ~20 | |
| IMPLEMENTATION_ONLY | ~12 | dead LoginWallDetector, releaseBaseToApiUrl, eval-harness no-ops, etc. |

All MAJOR gaps below are **resolved for audit purposes** (modeled with GAP/EF/edge IDs). Product code not fixed (Wave 4 = discovery only).

## CRITICAL

### GAP-001 — CASCADE MODEL WRONG (COVERED_WRONG → CRITICAL_MODEL_GAP)
- **Owners:** [W4-B](0987b056-c673-41e7-98ed-2d9999f7f68b)
- **Evidence:** `OccamRouter.cs:145-182`. Terminal short-circuit = **404/410 only** + `IsPublicReferencePage` (no browser). `ChooseRawFallback` ranks by `FailureRanking.Informativeness`, **not** markdown density. Managed success can win; **managed failure never enters surface ranking** (only http vs browser). `http_only` is probe-advisory — does **not** skip browser in router.
- **Model impact:** CAP-052 / CAP-104 prose misstates the escalation ladder. Waves 1–3 product narrative "http→browser→managed last-rung" is incomplete/wrong on failure ranking and public-ref short-circuit.
- **Prefer:** correct CAP-052/104 + new edges; CAP-NEW-B-1/B-2 only if edges insufficient.

### GAP-002 — InstallShared StopAll on every session DI (MISSING_CAPABILITY / CRITICAL)
- **Owners:** [W4-A](8a59cb39-43a3-43c9-9d01-30f04276ee08)
- **Evidence:** `BrowserPoolManager.InstallShared` (`:45-48`) calls `_shared?.StopAll()` then replaces; invoked from `AddOccamCore` DI factory (`OccamServiceCollectionExtensions.cs:39-46`). WS/Remote rebuild DI per session → **new session kills the process-wide browser pool**.
- **Orchestrator verify:** PROVEN.
- **EFC:** → canonical EF-041 (BUG, HIGH, SECURITY-adjacent / availability).

## MAJOR

### GAP-003 — Probe masks SSRF as network_error (MISSING_SECURITY_SEMANTIC)
- **W4-B.** `OutboundUrlBlockedException` (`OutboundHttpGuard.cs:84`) has typed `failureCode` (`private_url_blocked`/`dns_error`), but `HttpProbeFetcher.cs:172-175` bare `catch` → `"network_error"`. Agent cannot distinguish SSRF block from real network failure.
- **EFC:** → EF-042 SECURITY.

### GAP-004 — css-extract SSRF/body-cap parity gap (MISSING_SECURITY_SEMANTIC)
- **W4-F.** `css-extract.mjs` uses `egressFetch` with **no** `private-ip` import; `response.text()` unbounded (`:78`). HTTP/browser workers have DNS-pin + `OCCAM_MAX_RESPONSE_BYTES`; css does not.
- **EF-013 reconfirm:** `(0,eval)(__NUXT__)` still live in `css-schema-extract.mjs:139`.
- **EFC:** → EF-043 SECURITY (parity) + EF-013 remains open.

### GAP-005 — playbook_save ignores OCCAM_RECEIPTS (COVERED_WRONG / MISSING_CONFIG)
- **W4-E.** `PlaybookSaveService.cs:86-91` always `PlaybookSignature.BuildSignedJson` — no `ReceiptsPolicy` check. EF-005 reconfirmed independently. DI always `ReceiptSigner.LoadOrCreate()` (`OccamServiceCollectionExtensions.cs:23`) even when receipts off → key mint side effect.
- **EFC:** EF-005 stays; new EF-044 key-mint-vs-receipts-off.

### GAP-006 — URL fragment omitted from cache + MaterializationKey (MISSING_EDGE / BUG)
- **W4-C.** Fragment drives `FocusIntent` but is stripped before `TranscodeCacheKey` / `MaterializationKey` → `#section-a` can poison `#section-b`.
- **Evidence:** `FocusIntent.cs`, `TranscodeCacheKey.cs:54-71`, `MaterializationKey.cs:21-55`, `OccamTranscodeTool.cs:117-130`.
- **EFC:** → EF-045 BUG.

### GAP-007 — Always-on bypassCSP + playbook page.evaluate (MISSING_SECURITY_SEMANTIC)
- **W4-F.** `browser-session.mjs:143` `bypassCSP: true` always. `interaction-steps.mjs:14` `page.evaluate` / `waitForFunction` from playbook plan (page-adjacent code execution surface).
- **EFC:** → EF-046 SECURITY.

### GAP-008 — PlaybookCommunitySanitizer Core-dead; save≠sanitize asymmetry (DEAD + MISSING_EDGE)
- **W4-E.** Lint CAP-758 cites Sanitizer wrongly; local MCP save does not run cookie/selector strip that JS publish sanitize does. Cookie headers / body selectors survivable on local save.
- **EFC:** → EF-047 DESIGN/SECURITY.

### GAP-009 — Genome fetch CT skip + ReadToEnd before truncate (MISSING_FAILURE / DoS)
- **W4-E.** `WellKnownGenomeFetcher.cs:67-81` — empty Content-Type skips `not_json`; reads full body then truncates 32KiB.
- **EFC:** → EF-048 BUG.

## MEDIUM (selected — full list in blind reports)

| ID | Class | Source | One-liner |
|----|-------|--------|-----------|
| GAP-010 | MISSING_CAPABILITY | W4-A | `OccamJsonPrintableEscapes` wire transform unmodeled |
| GAP-011 | COVERED_WRONG | W4-A | CAP-021 labeled stdio framing but Content-Length adapter is WS (`WebSocketMcpStreams.cs:70-83`) |
| GAP-012 | MISSING_EDGE | W4-A | Server instructions advertise `occam_watch` without `OCCAM_WATCH_MCP` gate |
| GAP-013 | MISSING_EDGE | W4-A | WS unlimited concurrent sessions (Remote has `OCCAM_REMOTE_MAX_SESSIONS`) |
| GAP-014 | MISSING_FAILURE | W4-B | Managed fail recovery-only (never surface winner) |
| GAP-015 | DEAD | W4-B | `LoginWallDetector` dead (RequiresLogin PP lives) |
| GAP-016 | MISSING_CAPABILITY | W4-D | `ThinExtractBrowserExhausted` stop not in CAP-106 |
| GAP-017 | MISSING_EDGE | W4-D | Scorer ignores `LikelyPaywall` |
| GAP-018 | MISSING_FAILURE | W4-D | Robots Allow omitted + fetch fail-open |
| GAP-019 | MISSING_CONFIG | W4-D | Empty `OCCAM_PROXY_LIST_FILE` suppresses inline `OCCAM_PROXY_LIST` |
| GAP-020 | MISSING_ARTIFACT | W4-D | `translatedMarkdown` + translate warning codes |
| GAP-021 | PERFORMANCE | W4-D | LibreTranslate sync `.GetResult()` |
| GAP-022 | MISSING_ARTIFACT | W4-C | File-backed `OccamCacheEntry` store not in ARTIFACT-MAP |
| GAP-023 | MISSING_ARTIFACT | W4-C | Temp CSS field-spec file host→worker |
| GAP-024 | MISSING_FAILURE | W4-C | Malformed field-spec can throw past `ArgumentException` catch |
| GAP-025 | COVERED_WRONG | W4-C | CAP-600 row-mode/`base_selector` unreachable from host parsers |
| GAP-026 | MISSING_EDGE | W4-C | `max_tokens` not a hard bound on serialized response |
| GAP-027 | MISSING_ARTIFACT | W4-E | `~/.occam/keys/signing-key.pem` missing from ARTIFACT-MAP |
| GAP-028 | MISSING_EDGE | W4-E | Attest aggregate + consensus verdict unsigned pattern |
| GAP-029 | MISSING_EDGE | W4-E | QualityGate heuristic sealed into signed provenance |
| GAP-030 | MISSING_SECURITY | W4-F | Playwright proxy fail-open (`egress-proxy.mjs:139`) |
| GAP-031 | MISSING_ARTIFACT | W4-F | `contentPrefix` synthetic markdown trust |
| GAP-032 | COVERED_WRONG | W4-A | Banner always claims "Listening via stdio..." |

## Prefer edges/corrections over new CAPs

Proposed real new CAP mint only when edges insufficient:
- CAP-NEW-A-1 InstallShared×DI → **prefer EF + edge**, maybe one CAP if product-visible
- CAP-NEW-A-2 printable-escapes → small CAP or compile-wire edge
- CAP-NEW-B-1/B-2 → **correct CAP-052** + edges
- CAP-NEW-C fragment collision → **EF**, edge on cache
- CAP-NEW-D-1..3 thin-exhausted / paywall / robots → edges on CAP-106 / search / robots
- CAP-NEW-E-1/E-2 → edges + EF
- CAP-NEW-F-1..3 → security EFs + edges on CAP-151/220/598

## MAJOR (G/H additions)

### GAP-033 — `stop-occam-processes` name-wide kill ignoring OCCAM_HOME (MISSING_SECURITY_SEMANTIC)
- **Owners:** [W4-G](40963d24-87f6-4441-8b49-92f3017a0d7d)
- **Evidence:** `scripts/lib/stop-occam-processes.mjs:77-92` (Win: `$_.Name -eq OccamMcp.Core.exe` without root filter); `:135-138` (POSIX: `mentionsHost` bypasses `rootNorm`). Used by `occam refresh`. Contradicts INV-10 "never process-name-wide" which only guards `stopOccamHostByPid`.
- **Orchestrator verify:** PROVEN.
- **EFC:** → EF-049 SECURITY.

### GAP-034 — `launch-mcp-host` injects `~/.occam/onboard.json` env every launch (MISSING_EDGE / AUTOMATIC)
- **W4-G.** `launch-mcp-host.mjs:29` + `onboard-config.mjs:17-29` `mergeOnboardEnv`. Uncontrolled host-env surface from a user-writable JSON file.
- **EFC:** → EF-050 DESIGN (prefer edge on CAP-960+ onboard).

### GAP-035 — Docker HEALTHCHECK `--version` starts stdio → perpetual unhealthy (MISSING_FAILURE_SEMANTIC)
- **Owners:** [W4-H](4acbf6e8-82e7-4dbb-a657-19b8916445a0)
- **Evidence:** `Dockerfile:76`; `OccamMcpCli.Parse` silently ignores unknown args → stdio blocks. Correct verb would be `version-surface`.
- **EFC:** → EF-051 BUG.

### GAP-036 — playbook-marketplace auto-merge without L4 validation (MISSING_SECURITY_SEMANTIC)
- **W4-H.** `l4-gate` succeeds on `skipped` when diff empty; trigger `**/*.json` vs single-level `community/*.json` mismatch; `auto-merge` on `validate.result==success`. Unvalidated community PB can squash-merge to main → feeds resolve community tier.
- **Uncertainty:** branch-protection outside repo may block in practice; **code path is open**.
- **EFC:** → EF-052 SECURITY (supply chain).

### GAP-037 — Community cosign step misconfigured + release `.bundle` unused by install (MISSING_SECURITY)
- **W4-H.** marketplace: env key but no `--key`, no `id-token:write`. Release `sign-release.yml` produces `.bundle` but `npx`/`get-ff-occam` verify sha256 vs **unsigned** manifest only.
- **EFC:** → EF-053 SECURITY (trust theater).

### GAP-038 — session import retains plaintext cookies.txt in `_imports/` (MISSING_SECURITY / PRIVACY)
- **W4-G.** `occam-session.mjs:123-128`. List claims "no secret values."
- **EFC:** → EF-054 PRIVACY.

## MEDIUM (G/H selected)

| ID | Class | Source | One-liner |
|----|-------|--------|-----------|
| GAP-039 | AUTOMATIC | W4-G/H | skill install `rmSync` destructive overwrite, no backup |
| GAP-040 | MISSING_EDGE | W4-G/H | skill `--platform all` writes AGENTS.md codex pointer without copying `.agents` |
| GAP-041 | MISSING_EDGE | W4-H | npm RID_MAP rejects advertised win-arm64/linux-arm64 |
| GAP-042 | DEAD | W4-G | `releaseBaseToApiUrl` test-only dead |
| GAP-043 | COVERED_PARTIAL | W4-H | Docker missing `profiles/` → silent built-in fallbacks |
| GAP-044 | PRODUCT_AS_INTERNAL | W4-H | agent-sdk is real product code but npm-unreachable (uphold S3-12) |

## Orchestrator verification log
| Claim | Result |
|-------|--------|
| InstallShared StopAll | PROVEN `BrowserPoolManager.cs:45-48` + DI call site |
| Cascade 404/410 + public-ref | PROVEN `OccamRouter.cs:145-182` |
| ChooseRawFallback = FailureRanking | PROVEN `:206-213` |
| Managed fail excluded from surface | PROVEN `:182` |
| playbook_save always signs | PROVEN `PlaybookSaveService.cs:86-91` |
| Probe bare catch → network_error | PROVEN `HttpProbeFetcher.cs:172-175` |
| css-extract no private-ip | PROVEN (no import; `egressFetch` only) |
| stop-occam name-wide (Win Name-eq; POSIX mentionsHost) | PROVEN `stop-occam-processes.mjs:77-92,135-138` |
| mergeOnboardEnv on every launch | PROVEN `launch-mcp-host.mjs:29` |
