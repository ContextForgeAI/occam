# ENGINEERING-FINDINGS (ledger)

Seeded from Wave 1. Wave 2–3 agents append. **No product code changes in audit waves.**

**Orchestrator note (Wave 3):** agents reused `EF-019`/`EF-020`/`EF-021` across reports.  
IDs below are the **canonical** renumbering. Original agent-local IDs are noted in parentheses.

| ID | Class | Related CAPs | Summary | Confidence | Needs repro? | Security review? | Status |
|----|-------|--------------|---------|------------|--------------|------------------|--------|
| EF-001 | BUG-CANDIDATE | CAP-315 | `TranscodeCacheKey` omits `rank_blocks`/`tag_trust`/`emit_capsule` — cache hit may replay stale block annotations | PROVEN in code | Yes (cache hit with flag flip) | No | OPEN |
| EF-002 | SECURITY-CANDIDATE | CAP-249 | Browser pool reused one BrowserContext across hosts (cookie bleed). **Phase 6:** clear cookies/storage between anonymous extracts (`browser-pool.mjs`). Not a hard isolation guarantee. | PROVEN | Optional | Yes | **MITIGATED** (P6) |
| EF-003 | SECURITY-CANDIDATE | CAP-194, CAP-238 | **ORCH CONFIRMED:** `AddHttpClient("occam.managed")` sets timeout only — **no** `OutboundHttpGuard.ConnectCallback`. Managed calls go to operator-configured third-party APIs with user URL in body/path. | PROVEN | Optional | Yes | OPEN |
| EF-004 | PERFORMANCE-CANDIDATE | CAP-330, CAP-333 | Canonical Knowledge extraction runs every transcode then discarded by MarkdownPassthroughCodec | PROVEN | Optional bench | No | OPEN |
| EF-005 | BUG-CANDIDATE | CAP-280, CAP-571 | `occam_playbook_save` signs every successful save unconditionally — `OCCAM_RECEIPTS=off` has zero effect | PROVEN in code | No | No | OPEN |
| EF-006 | OBSERVATION | CAP-287 | `occam_extract_knowledge` "Receipt" is telemetry, not signed Receipt v1 | PROVEN | No | No | OPEN |
| EF-007 | OBSERVATION | CAP-166 | Core C# HttpClients never honor `OCCAM_HTTP_PROXY` (workers do) | PROVEN | No | No | OPEN |
| EF-008 | OBSERVATION | CAP-264 | `"paywall"` negative-receipt branch unreachable | PROVEN | No | No | OPEN |
| EF-009 | OBSERVATION | CAP-248a/b | Dead DI abstractions: NodeWorkerProcessSpawner unused; BrowserConcurrencyGate.Run unused | PROVEN | No | No | OPEN |
| EF-010 | DESIGN-QUESTION | CAP-078, CAP-083 | Always-on block collection; `diff_against` forces blocks[] even if json_blocks=false | PROVEN | No | Privacy (payload size) | OPEN |
| EF-011 | BUG-CANDIDATE | CAP-651 | `occam_verify` unrecognized `mode` silently falls back to offline (no `invalid_arguments`) | PROVEN (W2) | Yes | No | OPEN |
| EF-012 | DESIGN-QUESTION | CAP-652, CAP-653 | `occam_verify` live re-fetch uses bare options; drops session/playbook/budget; collapses failures to `refetch_failed` | PROVEN (W2) | Optional | No | OPEN |
| EF-013 | SECURITY-CANDIDATE | CAP-598 | `attr="nuxt"` previously ran `(0, eval)` on page `__NUXT__`. **Phase 6:** Nuxt attr fails closed (`nuxt_attr_disabled`); no eval. | PROVEN | Yes | Yes | **FIXED** (P6) |
| EF-014 | BUG-CANDIDATE | CAP-595, CAP-600 | extract_knowledge: `confidence` always 0.0; `base_selector` row-mode unused → empty facts[] | PROVEN (W2) | Yes | No | OPEN |
| EF-015 | BUG-CANDIDATE | CAP-756, CAP-759–762 | playbook_lint drifted from save/resolve parsers | PROVEN (W2) | Yes | No | OPEN |
| EF-016 | OBSERVATION | CAP-404, CAP-691, CAP-771 | Ambient/token budget asymmetry: claim_check & dataset_export apply **no** token budget | PROVEN (W2) | No | No | OPEN |
| EF-017 | OBSERVATION | CAP-423/424, CAP-527, CAP-543, CAP-594 | session_profile often headers-only; storageState dropped on probe/map/heal/extract | PROVEN (W2) | Optional | Privacy | OPEN |
| EF-018 | DESIGN-QUESTION | CAP-773, CAP-283 | dataset_export top-level `ok` always true; manifest verify only via CLI | PROVEN (W2) | No | No | OPEN |
| EF-019 | OBSERVATION/BUG-CANDIDATE | CAP-845, CAP-387, CAP-832 | WatchStore: in-process `lock` only — multi-process race on `watch.json` can silently wipe store (S3-02) | PROVEN (W3) | Yes (two processes) | No | OPEN |
| EF-020 | OBSERVATION | CAP-840, CAP-841 | No un-watch / eviction API — store growth unbounded except manual file edit (S3-02) | PROVEN (W3) | No | No | OPEN |
| EF-021 | BUG-CANDIDATE | CAP-985, CAP-992..998 | `occam connect` post-verify rollback dead for CONFIG_FILE hosts with `requiresRestart:true` (was agent EF-019 / S3-10) | PROVEN (W3) | Yes | No | OPEN |
| EF-022 | OBSERVATION | CAP-928, CAP-938 | `occam-refresh-host.mjs` hardcodes stale "9 occam_* tools" (was agent EF-019 / S3-07) | PROVEN (W3) | No | No | OPEN |
| EF-023 | DESIGN-QUESTION | CAP-929, CAP-939 | `version-surface` names two non-equivalent commands (host verb vs `occam contract`) (was agent EF-020 / S3-07) | PROVEN (W3) | No | No | OPEN |
| EF-024 | WITHDRAWN | CAP-871, CAP-875, CAP-1000 | **ORCH REJECT:** S3-04 claimed process-wide `FailureAtlasStore` under `--remote`. Spot-check: `RemoteMcpTransport.RunSingleSessionAsync` / `WebSocketMcpTransport` each call `Host.CreateApplicationBuilder()` + `AddOccamMcpServer()` **per WebSocket session** — in-memory atlas is per-session DI, resets on reconnect. Multi-tenant leak as stated is **not** proven. (was agent EF-019 / S3-04) | REJECTED by orch | n/a | n/a | WITHDRAWN |
| EF-025 | BUG-CANDIDATE | CAP-900, CAP-901, CAP-902 | Operator wrapper does not route `install-browser` / `verify` / `keys` to host binary (was agent EF-020 / S3-06) | PROVEN (W3) | Yes (`occam install-browser`) | No | OPEN |
| EF-026 | OBSERVATION | CAP-903 | Stale binary name `FFOccamMcp.Core` in several docs (was agent EF-021 / S3-06) | PROVEN (W3) | No | No | OPEN |
| EF-027 | OBSERVATION | CAP-904 | `receipt_verification.md` embeds literal `0x00` byte in Merkle formula prose (was agent EF-022 / S3-06) | PROVEN (W3) | No | No | OPEN |
| EF-028 | DESIGN-QUESTION | CAP-965 | Install: `rm -rf` INSTALL_DIR before extract — no rollback (was agent EF-021 / S3-09) | PROVEN (W3) | No | No | OPEN |
| EF-029 | BUG-CANDIDATE | CAP-967, CAP-968, CAP-969 | Onboard writes `onboard.json` / optional mcp.json **before** verify (was agent EF-022 / S3-09) | PROVEN (W3) | No | No | OPEN |
| EF-030 | OBSERVATION | CAP-978, CAP-979 | Stale install copy: old source-uri + hardcoded "14 tools" (was agent EF-023 / S3-09) | PROVEN (W3) | No | No | OPEN |
| EF-031 | OBSERVATION | CAP-860, CAP-861 | `occam_crosscheck` exempt from `OCCAM_PROFILE` + absent from server instructions (was agent EF-029 / S3-03) | PROVEN (W3) | No | No | OPEN |
| EF-032 | OBSERVATION | CAP-867, CAP-856 | Consensus gate is unit-only — no e2e tool/receipt re-derive (was agent EF-030 / S3-03) | PROVEN (W3) | Optional | No | OPEN |
| EF-033 | BUG-CANDIDATE | CAP-959, CAP-008, CAP-384 | `hermes-smoke` asserts `EXPECTED_TOOLS=15` with no profile/opt-in awareness (was agent EF-024 / S3-08) | PROVEN (W3) | Yes (`OCCAM_PROFILE=reader`) | No | OPEN |
| EF-034 | BUG-CANDIDATE | CAP-1033, CAP-1032 | npm bin imported outside files. **Phase 6:** vendored host-install-gate; pack dry-run OK. Unpublished (EA-034). | PROVEN (W3) | No | No | **FIXED** (P6 pack); publish INTENT open |
| EF-035 | BUG-CANDIDATE | CAP-1035, CAP-1026 | Level B omitted contract/connect scripts. **Phase 6:** check-public-mcp-contract in scriptFiles; occam-connect absent on main (skipped). | PROVEN (W3) | Yes | No | **PARTIAL** (P6) |
| EF-036 | OBSERVATION | CAP-1039 | Skill card version 0.9.1 + "14 tools" vs product 1.0.0-rc.2 / 15 tools (was agent EF-028 / S3-12) | PROVEN (W3) | No | No | OPEN |
| EF-037 | DESIGN-QUESTION | CAP-804, CAP-805, CAP-808 | Batch never produces Receipt v1; results persist indefinitely in JSON snapshot (was agent EF-029 / S3-01) | PROVEN (W3) | No | Privacy/retention | OPEN |
| EF-038 | BUG-CANDIDATE | CAP-807, CAP-810–812 | Batch job store load-once + whole-snapshot persist — cross-process last-writer-wins (was agent EF-030 / S3-01) | PROVEN (W3) | Yes (two processes) | No | OPEN |
| EF-039 | PERFORMANCE-CANDIDATE | CAP-881 | Per-call GUID headers temp-file defeats browser-pool warm reuse for any headered/session call (S3-05 proposed EF-019) | PROVEN (W3) | Optional | No | OPEN |
| EF-040 | OBSERVATION | CAP-882 | Refines EF-002: bleed vector is anonymous→anonymous shared context (session transitions recycle first) (S3-05) | PROVEN (W3) | Optional | Privacy | OPEN |

## Wave 4 (adversarial negative-space) — EF-041+

Wave 3 CLOSED. Agent-local IDs `EFC-A-*`…`EFC-H-*` mapped here. No renumber of EF-001…040.

| ID | Class | Related | Finding | Confidence | Repro? | Sec review? | Status |
|----|-------|---------|---------|------------|--------|-------------|--------|
| EF-041 | BUG-CANDIDATE | GAP-002 | InstallShared previously StopAll+replace. **Phase 6:** idempotent — keep first shared, no StopAll. | PROVEN | Yes | Availability | **FIXED** (P6) |
| EF-042 | SECURITY-CANDIDATE | GAP-003 | Probe masks `OutboundUrlBlockedException` (`private_url_blocked`/`dns_error`) as `network_error` via bare catch (`HttpProbeFetcher.cs:172-175`) (W4-B EFC-B-2) | PROVEN | Yes (private URL probe) | Yes | OPEN |
| EF-043 | SECURITY-CANDIDATE | GAP-004, CAP-151 | css-extract lacked DNS-pin + body cap. **Phase 6:** parity with http-extract helpers. | PROVEN | Optional | Yes | **FIXED** (P6) |
| EF-044 | DESIGN-QUESTION | GAP-005, EF-005 | DI always `ReceiptSigner.LoadOrCreate()` even when `OCCAM_RECEIPTS=off`; key mint is independent of receipts master switch (W4-E EFC-E-5). EF-005 (save ignores receipts) reconfirmed | PROVEN | No | Trust | OPEN |
| EF-045 | BUG-CANDIDATE | GAP-006 | Fragment omitted from cache/materialization keys. **Phase 6:** focus_fragment folded into both keys. | PROVEN | Yes | No | **FIXED** (P6) |
| EF-046 | SECURITY-CANDIDATE | GAP-007 | Browser context always `bypassCSP:true`; playbook interaction plan can `page.evaluate` / `waitForFunction` JS (W4-F EFC-F-3/F-4) | PROVEN | Optional | Yes | OPEN |
| EF-047 | DESIGN-QUESTION / SECURITY | GAP-008, CAP-758 | `PlaybookCommunitySanitizer` Core-dead; local MCP save does not apply publish-sanitize (cookie headers/selectors survivable); lint CAP wrongly cites Sanitizer (W4-E EFC-E-2/E-3) | PROVEN | No | Yes | OPEN |
| EF-048 | BUG-CANDIDATE | GAP-009 | `WellKnownGenomeFetcher`: empty Content-Type skips `not_json`; `ReadToEnd` before 32KiB truncate (DoS/latency) (W4-E EFC-E-4) | PROVEN | Optional | DoS | OPEN |
| EF-049 | SECURITY-CANDIDATE | GAP-033 | `stop-occam-processes` / `occam refresh` kill by host-binary **name** machine-wide, ignoring `OCCAM_HOME` (Win Name-eq; POSIX `mentionsHost` bypass). Contradicts INV-10 name-wide framing which only guards pid path (W4-G EFC-G-1) | PROVEN | Yes (2 installs) | Yes | OPEN |
| EF-050 | DESIGN-QUESTION | GAP-034 | `launch-mcp-host` always `mergeOnboardEnv` from `~/.occam/onboard.json` into host process env — uncontrolled config surface (W4-G) | PROVEN | No | Config integrity | OPEN |
| EF-051 | BUG-CANDIDATE | GAP-035, CAP-1029 | Docker HEALTHCHECK used --version. **Phase 6:** version-surface. | PROVEN | Yes | No | **FIXED** (P6) |
| EF-052 | SECURITY-CANDIDATE | GAP-036, CAP-1031 | Marketplace skipped L4 could auto-merge. **Phase 6:** in-repo requires l4_result==passed. Branch protection EXTERNAL (EA-052). | PROVEN | CI | Yes | **PARTIAL** (P6) |
| EF-053 | SECURITY-CANDIDATE | GAP-037 | Community cosign step misconfigured (env key, no `--key`, no `id-token:write`); release `.bundle` produced but no shipped install path verifies it — sha256 vs unsigned manifest only (W4-H EFC-H-2/H-6) | PROVEN | Optional | Yes (trust theater) | OPEN |
| EF-054 | OBSERVATION / PRIVACY | GAP-038 | Session import retained plaintext _imports/ by default. **Phase 6:** default no retain; --keep-import opt-in. | PROVEN | No | Privacy | **FIXED** (P6) |
| EF-055 | BUG-CANDIDATE | GAP-024, GAP-026 | Malformed knowledge_schema field node kinds can escape typed `invalid_arguments`; whole-response `max_tokens` is not a serialized hard bound (W4-C EFC-C-2/C-3) | PROVEN (by construction) | Optional | No | OPEN |
| EF-056 | OBSERVATION | GAP-001, CAP-052 | Cascade model wrong: only 404/410 + `IsPublicReferencePage` skip browser; `ChooseRawFallback` = FailureRanking not density; managed fail never wins surface (W4-B EFC-B-1). **Model correction**, not a code bug | PROVEN | No | No | OPEN (docs-model) |
| EF-057 | BUG-CANDIDATE | GAP-019, GAP-021 | Empty `OCCAM_PROXY_LIST_FILE` suppresses inline `OCCAM_PROXY_LIST`; LibreTranslate path sync-blocks via `.GetResult()` (W4-D EFC-D-1/D-2) | PROVEN | Optional | No | OPEN |

## Phase 5 (canonical model synthesis) — EF-058+

Wave 4 CLOSED. Phase 5 is model synthesis, not discovery, but the conservative trust re-read
(P5-05) and the boundary pass (P5-10) surfaced five findings that are code-proven and product-
relevant. Agent-local IDs `EFC-P5-05-1…5` and `EFC-P5-G2-1` map here. No renumber of EF-001…057.

| ID | Class | Related | Finding | Confidence | Repro? | Sec review? | Status |
|----|-------|---------|---------|------------|--------|-------------|--------|
| EF-058 | SECURITY-CANDIDATE | CAP-281, CAP-574 | Provenance unsigned; Inspect short-circuited on keyId. **Phase 6 interim:** Inspect verify-first + wrong_key. **Phase 6.5 (OD-4):** playbook signature **v2** now signs keyId/alg/contentHash/signedAt + verify snapshot with domain separation + version marker; v1 verification preserved; Inspect/Verify report `sigVersion`; unsupported_version distinct. Fixtures T1–T11 green. | PROVEN | Optional | Yes | **FIXED** (P6.5 v2) |
| EF-059 | BUG-CANDIDATE / HONESTY | CAP-284 | Unsigned chain returned history_verified. **Phase 6:** chainIntegrity + signatureStatus; history_verified only if all signed+verified. | PROVEN | Yes | Trust | **FIXED** (P6) |
| EF-060 | OBSERVATION | CAP-252 | Merkle duplicate-last-leaf promotion yields the CVE-2012-2459 structural ambiguity shape; leaf counts and leaf-count-derived values are unsigned quantities (was `EFC-P5-05-3`) | PROVEN (by construction) | Yes (crafted leaf set) | Yes | OPEN |
| EF-061 | DESIGN-QUESTION | CAP-008, CAP-959 | Reader hid occam_verify. **Phase 6:** reader includes verify. | PROVEN | Yes | No | **FIXED** (P6) |
| EF-062 | OBSERVATION | CAP-284, EF-058 | No wrong_key verdict. **Phase 6:** wrong_key / key_mismatch / history_wrong_key shipped. | PROVEN | No | Trust | **FIXED** (P6) |

**Not promoted:** P5-05's cross-canonicalizer preimage-collision concern (four hand-written
canonicalizers, no domain-separation tag) stays an UNCERTAIN in `TRUST-MODEL.md` — no concrete
collision was constructed, so it is not yet a finding. **Phase 6.5 update:** the playbook v2 preimage
now carries an explicit domain-separation prefix (`occam-playbook-sig-v2\n`), removing that surface for
the playbook path specifically; receipt/watch/dataset canonicalizers remain distinct-by-structure.

## CAP remints (Wave 3 orch)

| Original collision | Remint | Reason |
|--------------------|--------|--------|
| CAP-995 `roo` (shared with `cline`) | **CAP-1040** | HOST-CAPABILITY-MATRIX duplicate ID |
| CAP-999 `junie` (shared with `goose`) | **CAP-1041** | HOST-CAPABILITY-MATRIX duplicate ID |



## Phase 6 status note (2026-07-26)

Product hardening on branch `fix/phase6-product-hardening`. Status values **FIXED** / **MITIGATED** / **PARTIAL** above supersede OPEN for those rows. Historical Wave text in Summary is retained where still informative. See `PHASE6-REPORT.md`, `REGRESSION-CONTRACT.md`, `DOCS-TRUTH-GATE.md`.

## Phase 6.5 status note (2026-07-26)

Owner decisions OD-1..OD-8 recorded in `OWNER-DECISIONS.md`. **EF-058 → FIXED** via playbook signature **v2** (`PLAYBOOK-SIGNATURE-V2-CONTRACT.md`; `PlaybookSignature.cs`; fixtures in `ReceiptUnitTests`). Naming/honesty: extract `receipt`=telemetry (OD-5), `claim_check.proven`=retrieval-complete negative (OD-6), `attest`=heuristic support (OD-7), `crosscheck`=multi-source comparison, never "consensus proof" (OD-8) — wire preserved, meanings frozen in `HONESTY-SCHEMA-MAP.md`. External: EA-052 external-verify (marketplace-trust only), EA-053 cosign honesty-only, EA-034 npm non-GA. No remaining trust-honesty docs blockers.
