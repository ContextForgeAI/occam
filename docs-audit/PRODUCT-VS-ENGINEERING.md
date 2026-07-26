# PRODUCT-VS-ENGINEERING — documentation boundary

**Status:** Phase 5Q synthesis. **Rows below are the Phase-5 view (Status=OPEN); current statuses are owned by `ENGINEERING-FINDINGS.md`.** This file classifies documentation impact.

> **Phase 6 + 6.5 reconciliation (2026-07-26).** Several `NEEDS_FIX_BEFORE_DOC` items are now resolved in code — see `ENGINEERING-FINDINGS.md`, `DOCS-TRUTH-GATE.md`, `OWNER-DECISIONS.md`, `HONESTY-SCHEMA-MAP.md`:
> - **FIXED:** EF-013 (Nuxt eval disabled), EF-043 (CSS SSRF/body-cap), EF-054 (session import), EF-051 (Docker health), EF-035 (Level B scripts), EF-045 (fragment cache), EF-059/EF-061/EF-062 (trust), **EF-058 (playbook signature v2, OD-4)**, EFC-P5-05-1 (Inspect verify-before-classify + `unsupported_version`/`wrong_key`), EFC-P5-05-2 (`history_verified` split).
> - **MITIGATED (unit-covered; live BLOCKED):** EF-002 (anonymous context clear-state).
> - **OWNER/EXTERNAL (not code-fixable this phase):** EF-034 npm non-GA (OD-3), EF-053 cosign honesty-only (OD-2), EF-052 marketplace external-verify (OD-1).
> - **Naming frozen (wire preserved):** extract `receipt`=telemetry (OD-5), `claim_check.proven`=retrieval-complete negative (OD-6), `attest`=heuristic support (OD-7), `crosscheck`=multi-source comparison, never "consensus proof" (OD-8).

Canonical findings remain owned by `ENGINEERING-FINDINGS.md`; this file classifies their documentation impact.

## 1. Boundary rules

1. A shipped implementation defect is not a product capability. Describe intended and reliably
   observable semantics; link the defect internally and either warn, narrow the claim, or withhold the
   affected documentation.
2. `OPEN` does not mean “undocumented.” Most open findings can be documented honestly with a bounded
   warning. `NEEDS_FIX_BEFORE_DOC` is reserved for a short set where publishing the surrounding
   capability would itself create danger or a false availability/trust claim.
3. “Ships” means present in a distribution. “Reachable” means selectable from a supported entrypoint.
   “Documentable” means stable, intended, and safe to present as product behavior. These are independent
   properties (`SHIPPED-CODE-MAP.md:7-13`; C8).
4. Names do not expand proof. Trust terms must use the exact guarantees in `TRUST-MODEL.md`; “verified,”
   “receipt,” “claim check,” and “attest” require explicit qualification.
5. Severity below is documentation-risk severity, not a replacement for engineering triage.

Classification meanings:

- `AFFECTS_PUBLIC_SEMANTICS`: changes visible behavior, availability, response meaning, or supported workflow.
- `SECURITY_RELEVANT`: affects confidentiality, integrity, availability, policy enforcement, or supply chain.
- `PERFORMANCE_RELEVANT`: affects latency, memory, CPU, concurrency, or unbounded growth.
- `DOCS_MUST_WARN`: surrounding capability may be documented only with an explicit limitation.
- `DO_NOT_DOCUMENT_BUG_AS_FEATURE`: observed bug must not be normalized as intended behavior.
- `NEEDS_FIX_BEFORE_DOC`: withhold the specifically affected capability/claim until fixed.
- `INTERNAL_ONLY`: do not surface as a user-facing capability; engineering material may record it.

## 2. Canonical EF boundary matrix

All statuses are `OPEN` except canonical `EF-024`, which remains `WITHDRAWN`. “Block?” means whether
documentation of the surrounding capability must be withheld altogether; `LIMITED` means only the
named claim or broken distribution surface is blocked.

| ID | One-line summary | Classification(s) | Capability family slug(s) | What a naive writer would say | What documentation must say instead (or omit) | Block? | Severity | Status |
|---|---|---|---|---|---|---|---|---|
| EF-001 | Cache identity omits block-ranking, trust-tagging, and capsule flags. | `AFFECTS_PUBLIC_SEMANTICS`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `response-cache` | “A cache hit is equivalent to running the same request.” | Cache replay is opt-in and can replay stale optional annotations when these flags change; never describe the omission as cache normalization. | No | HIGH | OPEN |
| EF-002 | A pooled BrowserContext can bleed anonymous state across hosts. | `SECURITY_RELEVANT`, `DOCS_MUST_WARN`, `NEEDS_FIX_BEFORE_DOC`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `browser-acquisition`, `session-fetch` | “Browser fetches are host/session isolated.” | Do not claim isolation. Withhold shared-context isolation guidance; state that pooled anonymous contexts are not a security boundary. | LIMITED | CRITICAL | OPEN |
| EF-003 | Managed-provider HttpClient lacks the host outbound guard. | `SECURITY_RELEVANT`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `managed-acquisition`, `network-safety` | “All outbound fetch paths enforce the same SSRF policy.” | Managed providers are operator-configured third parties that receive user URLs and do not use the local `OutboundHttpGuard`; avoid parity claims. | No | CRITICAL | OPEN |
| EF-004 | Canonical Knowledge IR is built on every transcode and discarded. | `PERFORMANCE_RELEVANT`, `INTERNAL_ONLY`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `canonical-knowledge-ir`, `structured-materialization` | “Every transcode produces a reusable canonical knowledge model.” | Say nothing publicly: the IR is an internal, discarded computation, not an output capability. | No | MEDIUM | OPEN |
| EF-005 | Playbook save signs even when `OCCAM_RECEIPTS=off`. | `AFFECTS_PUBLIC_SEMANTICS`, `SECURITY_RELEVANT`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `playbook-authoring`, `receipts` | “`OCCAM_RECEIPTS=off` disables all signing.” | It disables most receipt emission, not playbook-save signing; never call it a master signing switch. | No | HIGH | OPEN |
| EF-006 | Extract-knowledge “Receipt” is unsigned telemetry, not Receipt v1. | `AFFECTS_PUBLIC_SEMANTICS`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `schema-knowledge-extraction`, `receipts` | “`occam_extract_knowledge` returns a verifiable Receipt v1.” | Call the field extraction telemetry and explicitly say it is not a signed Receipt v1 artifact (CAP-287). | No | HIGH | OPEN |
| EF-007 | Core C# clients ignore `OCCAM_HTTP_PROXY`. | `AFFECTS_PUBLIC_SEMANTICS`, `SECURITY_RELEVANT`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `proxy-egress` | “The proxy variable routes every network operation.” | Scope proxy support to worker paths; enumerate Core-side exceptions rather than presenting a global egress guarantee. | No | HIGH | OPEN |
| EF-008 | The `paywall` negative-receipt branch is unreachable. | `AFFECTS_PUBLIC_SEMANTICS`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `receipts`, `quality-failure-semantics` | “Occam emits signed negative receipts for detected paywalls.” | Omit `paywall` as an emitted failure/negative-receipt case until a live producer exists. | No | MEDIUM | OPEN |
| EF-009 | Registered process-spawner and concurrency `Run` abstractions have no callers. | `INTERNAL_ONLY` | `browser-acquisition` | “The host exposes pluggable spawning and a browser concurrency gate.” | Say nothing; these are dead abstractions, not extension points or runtime controls. | No | LOW | OPEN |
| EF-010 | Blocks are always collected and diff can force `blocks[]`. | `AFFECTS_PUBLIC_SEMANTICS`, `PERFORMANCE_RELEVANT`, `DOCS_MUST_WARN` | `structured-materialization`, `differential-materialization` | “`json_blocks=false` means blocks are neither computed nor returned.” | Explain that block collection is internal/always-on and `diff_against` can force block output, increasing payload and work. | No | MEDIUM | OPEN |
| EF-011 | Unknown verify modes silently become offline verification. | `AFFECTS_PUBLIC_SEMANTICS`, `SECURITY_RELEVANT`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `verification` | “Unsupported modes are rejected.” | State the accepted modes exhaustively and warn that MCP currently falls back to `offline`; do not present permissiveness as compatibility. | No | HIGH | OPEN |
| EF-012 | Live verify drops original context and collapses re-fetch failures. | `AFFECTS_PUBLIC_SEMANTICS`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `verification` | “`live` proves whether the original page changed.” | It compares against a bare anonymous/default re-fetch; `refetch_failed` does not distinguish context loss, wall, timeout, or content change. | No | HIGH | OPEN |
| EF-013 | Nuxt extraction evaluates page-controlled JavaScript. | `SECURITY_RELEVANT`, `DOCS_MUST_WARN`, `NEEDS_FIX_BEFORE_DOC`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `schema-knowledge-extraction` | “The Nuxt attribute extractor safely parses embedded state.” | Withhold Nuxt/eval-based extraction as a supported safe mode until fixed; schema-driven extraction can execute page-controlled text. | LIMITED | CRITICAL | OPEN |
| EF-014 | Extract-knowledge confidence is always zero; row `base_selector` is dead. | `AFFECTS_PUBLIC_SEMANTICS`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `schema-knowledge-extraction` | “Confidence measures extraction quality and row mode maps repeated records.” | Treat confidence as non-informative and do not document host row-mode support; malformed expectations can yield empty `facts[]`. | No | HIGH | OPEN |
| EF-015 | Playbook lint parser differs from save/resolve parsers. | `AFFECTS_PUBLIC_SEMANTICS`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `playbook-validation` | “Passing lint guarantees save and resolve acceptance.” | Lint is advisory and not parser-equivalent to save/resolve; document each acceptance boundary separately. | No | HIGH | OPEN |
| EF-016 | Claim-check and dataset export apply no token budget. | `AFFECTS_PUBLIC_SEMANTICS`, `PERFORMANCE_RELEVANT`, `DOCS_MUST_WARN` | `claims-attestation`, `dataset-provenance`, `token-budget` | “Ambient/max-token budgeting applies uniformly to all reading tools.” | Exclude these tools from global budget claims; their completeness and payload behavior follow separate paths. | No | MEDIUM | OPEN |
| EF-017 | Several session-aware tools pass headers but drop browser storage state. | `AFFECTS_PUBLIC_SEMANTICS`, `SECURITY_RELEVANT`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `session-fetch` | “A session profile reproduces the same authenticated state in every tool.” | Give a per-tool matrix: probe/map/heal/extract often use headers only and do not carry full storage state. | No | HIGH | OPEN |
| EF-018 | Dataset export reports top-level success regardless of row failures; manifest verification is CLI-only. | `AFFECTS_PUBLIC_SEMANTICS`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `dataset-provenance`, `verification` | “`ok:true` means every row succeeded and agents can verify the manifest.” | `ok` describes export completion, not universal row success; inspect rows. Manifest verification exists only in the host CLI. | No | HIGH | OPEN |
| EF-019 | Watch JSON persistence is vulnerable to multi-process last-writer-wins. | `AFFECTS_PUBLIC_SEMANTICS`, `PERFORMANCE_RELEVANT`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `change-monitoring` | “Watch history is durable under concurrent hosts.” | File persistence is single-process safe only; multiple writers can lose state. | No | HIGH | OPEN |
| EF-020 | Watch registrations have no eviction/unwatch API. | `AFFECTS_PUBLIC_SEMANTICS`, `PERFORMANCE_RELEVANT`, `DOCS_MUST_WARN` | `change-monitoring` | “Watch state is lifecycle-managed automatically.” | Registrations persist and can grow without bound; removal requires operator file maintenance. | No | MEDIUM | OPEN |
| EF-021 | Connect rollback is dead for restart-required config-file hosts. | `AFFECTS_PUBLIC_SEMANTICS`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `host-connectors` | “A failed post-write verification always rolls configuration back.” | For restart-required file hosts, verification/rollback cannot provide that guarantee; back up configuration before connect. | No | HIGH | OPEN |
| EF-022 | Refresh script prints a stale fixed tool count. | `AFFECTS_PUBLIC_SEMANTICS`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `operator-cli` | “Refresh validates a nine-tool contract.” | Tool counts are registry/profile/opt-in dependent; omit the stale literal and do not infer capability from that message. | No | LOW | OPEN |
| EF-023 | `version-surface` and `occam contract` are non-equivalent. | `AFFECTS_PUBLIC_SEMANTICS`, `DOCS_MUST_WARN` | `operator-cli` | “The two commands are interchangeable aliases.” | Document them as distinct host and wrapper surfaces with different outputs and reachability. | No | MEDIUM | OPEN |
| EF-024 | Withdrawn claim of a process-wide FailureAtlasStore leak. | `INTERNAL_ONLY` | `failure-atlas` | “Remote sessions share one in-memory failure atlas.” | Say nothing about a leak; per-session DI was re-verified. Preserve this finding as withdrawn and never revive it. | No | NONE | WITHDRAWN |
| EF-025 | Friendly wrapper does not route host `install-browser`, `verify`, or `keys` verbs. | `AFFECTS_PUBLIC_SEMANTICS`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `operator-cli`, `verification` | “Run `occam verify`, `occam keys`, or `occam install-browser`.” | These are direct host-binary verbs, not wrapper subcommands; show the exact reachable invocation or omit the workflow. | No | HIGH | OPEN |
| EF-026 | Verification docs use a stale binary name. | `AFFECTS_PUBLIC_SEMANTICS`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `operator-cli`, `verification` | “Invoke `FFOccamMcp.Core`.” | Use the current shipped executable name/path derived from the distribution, never the stale name. | No | MEDIUM | OPEN |
| EF-027 | Merkle formula prose contains a literal NUL byte. | `AFFECTS_PUBLIC_SEMANTICS`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `verification`, `receipts` | “Copy this displayed formula as the canonical preimage.” | Regenerate normative formula text from code and represent the separator unambiguously as the byte `0x00`; do not copy the corrupted source. | No | MEDIUM | OPEN |
| EF-028 | Installer deletes the target before extraction and has no rollback. | `AFFECTS_PUBLIC_SEMANTICS`, `SECURITY_RELEVANT`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `install-onboarding` | “Upgrade is atomic and preserves the previous install on failure.” | Warn that install is destructive replacement without rollback; require an external backup for recovery. | No | HIGH | OPEN |
| EF-029 | Onboarding writes state/config before verification. | `AFFECTS_PUBLIC_SEMANTICS`, `SECURITY_RELEVANT`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `install-onboarding` | “Onboard verifies first and commits only a valid configuration.” | It can leave `onboard.json` or MCP config behind after verification fails; document cleanup/backup requirements. | No | HIGH | OPEN |
| EF-030 | Installed copy contains stale source URI and fixed tool count. | `AFFECTS_PUBLIC_SEMANTICS`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `install-onboarding` | “The installed welcome text is the canonical product contract.” | Treat it as stale output, not evidence; derive URLs and tool availability from current registry/distribution. | No | MEDIUM | OPEN |
| EF-031 | Crosscheck bypasses profiles and is absent from server instructions. | `AFFECTS_PUBLIC_SEMANTICS`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `consensus-crosscheck`, `mcp-exposure` | “Profiles fully determine all exposed tools and instructions enumerate them.” | Crosscheck is env-gated outside profile filtering and may be exposed without being advertised; exposure is more than profile × core list. | No | HIGH | OPEN |
| EF-032 | Consensus has no end-to-end gate or shipped verdict re-derivation. | `SECURITY_RELEVANT`, `DOCS_MUST_WARN` | `consensus-crosscheck`, `verification` | “A consensus verdict is cryptographically verifiable.” | It is an unsigned observation; only individual vantage receipts can be checked manually. | No | HIGH | OPEN |
| EF-033 | Hermes smoke assumes exactly 15 tools. | `AFFECTS_PUBLIC_SEMANTICS`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE`, `INTERNAL_ONLY` | `mcp-exposure`, `packaging-distribution` | “A 15-tool count proves every deployment is healthy.” | Do not expose this test invariant as product semantics; profiles and opt-ins legitimately change the count. | No | MEDIUM | OPEN |
| EF-034 | npm launcher imports files excluded from its package. | `AFFECTS_PUBLIC_SEMANTICS`, `NEEDS_FIX_BEFORE_DOC`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `packaging-distribution` | “Install and run `@ff-occam/mcp` from npm.” | Do not document npm installation as available; the package is unpublished and would be nonfunctional as packed. | YES | CRITICAL | OPEN |
| EF-035 | Level B tarball omits scripts for advertised connect/contract commands. | `AFFECTS_PUBLIC_SEMANTICS`, `NEEDS_FIX_BEFORE_DOC`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `packaging-distribution`, `operator-cli` | “The tarball supports every command shown by help.” | Withhold connect/contract workflows for Level B until the artifacts ship; document only commands actually present. | LIMITED | CRITICAL | OPEN |
| EF-036 | Shipped skill card has stale version and tool count. | `AFFECTS_PUBLIC_SEMANTICS`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `packaging-distribution`, `mcp-exposure` | “The skill card is an up-to-date capability contract.” | Treat it as stale distribution data; availability comes from runtime `tools/list`, not embedded counts. | No | MEDIUM | OPEN |
| EF-037 | Batch emits no Receipt v1 and retains results indefinitely. | `AFFECTS_PUBLIC_SEMANTICS`, `SECURITY_RELEVANT`, `PERFORMANCE_RELEVANT`, `DOCS_MUST_WARN` | `batch-jobs`, `receipts` | “Batch outputs are receipt-backed and automatically expire.” | Batch has no Receipt v1 and JSON results have no retention limit; operators own deletion and privacy policy. | No | HIGH | OPEN |
| EF-038 | Batch snapshot persistence is cross-process last-writer-wins. | `AFFECTS_PUBLIC_SEMANTICS`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `batch-jobs` | “The file-backed batch store supports multiple host processes.” | Scope it to one writer/process; concurrent stores can overwrite each other. | No | HIGH | OPEN |
| EF-039 | Header/session calls defeat browser-pool warm reuse. | `PERFORMANCE_RELEVANT`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `browser-acquisition`, `session-fetch` | “All pooled browser calls reuse a warm daemon equally.” | Headered/session calls may take a cold one-shot path and have materially different latency. | No | MEDIUM | OPEN |
| EF-040 | Remaining context bleed vector is anonymous-to-anonymous reuse. | `SECURITY_RELEVANT`, `DOCS_MUST_WARN` | `browser-acquisition`, `session-fetch` | “Only explicit session transitions affect isolation.” | Even anonymous pooled calls share context state; session transitions recycle, but anonymous reuse remains the relevant risk. | No | HIGH | OPEN |
| EF-041 | Each new WS/Remote session can stop the process-wide browser pool. | `AFFECTS_PUBLIC_SEMANTICS`, `SECURITY_RELEVANT`, `PERFORMANCE_RELEVANT`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `browser-acquisition`, `runtime-transports` | “Concurrent remote sessions share a stable warm pool.” | New session DI can kill/recreate the shared pool, causing availability and latency disruption; do not normalize this as lifecycle management. | No | CRITICAL | OPEN |
| EF-042 | Probe masks SSRF-policy refusals as `network_error`. | `AFFECTS_PUBLIC_SEMANTICS`, `SECURITY_RELEVANT`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `probe-diagnostics`, `network-safety` | “Probe failure codes reliably distinguish policy blocks from network failures.” | Probe currently conflates private-URL/DNS policy blocks with `network_error`; never infer reachability from that code alone. | No | HIGH | OPEN |
| EF-043 | CSS extraction lacks DNS pinning and response-size cap. | `SECURITY_RELEVANT`, `PERFORMANCE_RELEVANT`, `DOCS_MUST_WARN`, `NEEDS_FIX_BEFORE_DOC`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `schema-knowledge-extraction`, `network-safety` | “All extraction workers enforce identical SSRF and body-size limits.” | Withhold claims that CSS extraction is safe for untrusted URLs until parity is fixed; it lacks private-IP pinning and bounded body reads. | LIMITED | CRITICAL | OPEN |
| EF-044 | Host always mints/loads a signing key even with receipts disabled. | `AFFECTS_PUBLIC_SEMANTICS`, `SECURITY_RELEVANT`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `receipts` | “Turning receipts off prevents signing-key creation and all trust artifacts.” | Key creation is an unconditional host-start side effect; receipts-off is not a no-key/no-signing mode. | No | HIGH | OPEN |
| EF-045 | URL fragments affect focus but are omitted from cache keys. | `AFFECTS_PUBLIC_SEMANTICS`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `response-cache`, `focus-selection` | “Fragment-focused requests have independent cached materializations.” | Fragment variants can collide and replay another fragment's signed response; avoid cache for fragment-sensitive reads. | No | CRITICAL | OPEN |
| EF-046 | Browser always bypasses CSP and playbooks can execute page JS. | `SECURITY_RELEVANT`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `browser-acquisition`, `playbook-authoring` | “Playbooks are declarative selectors constrained by page CSP.” | They are executable browser automation inputs; CSP is bypassed and interaction steps can evaluate JavaScript. Treat playbooks as trusted code-like input. | No | CRITICAL | OPEN |
| EF-047 | Community sanitizer is dead in Core; local save does not publish-sanitize. | `SECURITY_RELEVANT`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE`, `INTERNAL_ONLY` | `playbook-validation`, `playbook-authoring` | “Lint/save automatically strips sensitive headers and unsafe selectors.” | Distinguish lint, local save, hygiene, and publish sanitization. The dead Core sanitizer is not a runtime safety capability. | No | CRITICAL | OPEN |
| EF-048 | Well-known genome fetch accepts empty content type and reads before truncating. | `SECURITY_RELEVANT`, `PERFORMANCE_RELEVANT`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `playbook-resolution`, `network-safety` | “Genome fetch rejects non-JSON and caps reads at 32 KiB.” | Empty Content-Type bypasses the early check and the full response is read before truncation; do not claim hard input bounds. | No | HIGH | OPEN |
| EF-049 | Refresh kills matching host processes machine-wide. | `AFFECTS_PUBLIC_SEMANTICS`, `SECURITY_RELEVANT`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `operator-cli` | “Refresh restarts only this installation.” | It can terminate other installs by binary name; require exclusive-machine use or manual scoped process control. | No | CRITICAL | OPEN |
| EF-050 | Launcher silently merges user-writable onboard environment on every start. | `AFFECTS_PUBLIC_SEMANTICS`, `SECURITY_RELEVANT`, `DOCS_MUST_WARN` | `install-onboarding`, `runtime-transports` | “Only the current shell/service configuration controls host environment.” | `~/.occam/onboard.json` is an automatic configuration source on every launcher invocation; document precedence and integrity implications. | No | HIGH | OPEN |
| EF-051 | Docker healthcheck invokes an unknown option and blocks in stdio. | `AFFECTS_PUBLIC_SEMANTICS`, `NEEDS_FIX_BEFORE_DOC`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `packaging-distribution` | “The image includes a functioning health check suitable for orchestration.” | Withhold health/production-readiness claims; the shipped healthcheck remains unhealthy by construction until fixed. | LIMITED | CRITICAL | OPEN |
| EF-052 | Marketplace can auto-merge playbooks after skipped validation. | `SECURITY_RELEVANT`, `NEEDS_FIX_BEFORE_DOC`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `packaging-distribution`, `playbook-resolution` | “Community playbooks merged to main have passed the L4 validation gate.” | Do not document marketplace validation or trusted auto-merge until workflow and branch enforcement are fixed and verified. | LIMITED | CRITICAL | OPEN |
| EF-053 | Cosign workflow is ineffective and no installer consumes release bundles. | `SECURITY_RELEVANT`, `NEEDS_FIX_BEFORE_DOC`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `packaging-distribution`, `receipts` | “Releases and community playbooks are cosign-verified during install.” | Withhold signed-supply-chain claims. Shipped install paths verify sha256 against an unsigned manifest and consume no cosign bundle. | LIMITED | CRITICAL | OPEN |
| EF-054 | Session import retains plaintext cookie source under `_imports/`. | `AFFECTS_PUBLIC_SEMANTICS`, `SECURITY_RELEVANT`, `DOCS_MUST_WARN`, `NEEDS_FIX_BEFORE_DOC`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `session-fetch`, `operator-cli` | “Import stores no secret values after conversion.” | Withhold that privacy claim; raw cookies remain on disk by default. Document secure deletion and filesystem protection if import is mentioned. | LIMITED | CRITICAL | OPEN |
| EF-055 | Malformed schema kinds can escape typed errors; `max_tokens` is not a serialized hard bound. | `AFFECTS_PUBLIC_SEMANTICS`, `PERFORMANCE_RELEVANT`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `schema-knowledge-extraction`, `token-budget` | “All malformed schemas return `invalid_arguments`, and the complete JSON response never exceeds `max_tokens`.” | Narrow both guarantees: some malformed field nodes can fail outside typed handling, and token budgeting bounds content, not every serialized field. | No | HIGH | OPEN |
| EF-056 | Earlier cascade prose described routing and fallback ranking incorrectly. | `AFFECTS_PUBLIC_SEMANTICS`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `acquisition-routing` | “Every failure cascades HTTP → browser → managed, and density chooses the best failure.” | Code truth: 404/410 and public-reference policy can stop early; `FailureRanking` chooses HTTP/browser fallback; managed failure never wins the surface. | No | CRITICAL | OPEN |
| EF-057 | Empty proxy-list file suppresses inline proxies; LibreTranslate synchronously blocks. | `AFFECTS_PUBLIC_SEMANTICS`, `PERFORMANCE_RELEVANT`, `DOCS_MUST_WARN`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `proxy-egress`, `structured-materialization` | “An empty file falls back to inline proxies; translation remains fully async.” | Empty configured file takes precedence and disables the inline list; translation can block a request thread. | No | HIGH | OPEN |

## 3. Ranked `NEEDS_FIX_BEFORE_DOC` shortlist

This list is deliberately short. The block applies to the named claim/surface, not to every adjacent
feature. Other findings remain documentable with explicit limitations.

| Rank | Finding | Documentation withheld until fixed | Why publication is actively misleading or dangerous |
|---:|---|---|---|
| 1 | EF-052 | “Validated/trusted marketplace auto-merge” and community supply-chain guarantees | Unvalidated playbooks can reach the community tier through an open workflow path. |
| 2 | EF-043 + EF-013 | CSS/Nuxt extraction as safe for untrusted pages | One path lacks DNS-pin/body cap; another executes page-controlled text. |
| 3 | EF-002 | Browser/session isolation as a security guarantee | Shared anonymous BrowserContext state can cross host boundaries. |
| 4 | EF-053 | Signed or cosign-verified release/install chain | The signer path is misconfigured and no shipped installer verifies the bundle. |
| 5 | EF-034 | npm installation/zero-config npm launcher | The package is unpublished and its packed launcher omits required imports. |
| 6 | EF-035 | Level B connect/contract workflows | Help advertises scripts absent from the tarball. |
| 7 | EF-054 | “Session import retains no secrets” | Plaintext cookie input is retained by default. |
| 8 | EF-051 | Docker health or production-readiness guarantee | The healthcheck invokes no real version verb and blocks in stdio. |

Candidate findings EFC-P5-05-1 and EFC-P5-05-2 additionally block the unqualified phrases “verified
playbook provenance” and “verified/signed watch history”; they remain candidates until the orchestrator
allocates canonical IDs.

## 4. Bug-shaped behavior users may already depend on

Fixes in this group can change responses, exposure, files, or workflows. Engineering and docs must
land together, with release-note treatment where compatibility is affected.

| Finding(s) | Existing dependency surface | Coordination rule |
|---|---|---|
| EF-001, EF-045 | Cache users may observe stable but wrong cross-flag/fragment replay. | Correct cache identity invalidates existing entries and can change output/latency; document cache migration/expiry. |
| EF-005, EF-044 | Operators may rely on playbooks still being signed and keys existing while receipts are off. | Define the desired switch semantics before changing either side effect; do not silently delete signing behavior. |
| EF-010 | Diff consumers may rely on forced `blocks[]` despite `json_blocks=false`. | Treat removal as response-shape change. |
| EF-011 | Clients with misspelled modes currently receive offline results. | A correct `invalid_arguments` response is a breaking error-path change; announce it. |
| EF-014 | Consumers may special-case `confidence=0.0` or empty row facts. | Correct values alter ranking and schemas; version examples/tests together. |
| EF-015 | Existing playbooks may pass one parser and fail another. | Parser convergence requires a compatibility corpus and explicit rejection changes. |
| EF-018 | Export clients may interpret top-level `ok` as job completion rather than row completeness. | If semantics change, add/rename fields rather than silently redefining `ok`. |
| EF-020 | Manual edits are currently the only unwatch mechanism. | Adding eviction/TTL changes persistence lifecycle; document defaults and migration. |
| EF-023, EF-025 | Scripts may directly invoke host verbs because wrapper routing is absent. | Adding aliases must preserve direct-host invocations and disambiguate outputs. |
| EF-031, EF-033 | Deployments may rely on current crosscheck/profile exposure and fixed-count smoke behavior. | Profile enforcement changes `tools/list`; synchronize runtime contract checks and docs. |
| EF-037 | Operators may rely on indefinite batch history. | Introducing retention must be opt-in/versioned or include a migration and deletion policy. |
| EF-042 | Agents may retry private URLs because they see `network_error`. | Typed policy failures change agent control flow; update recipes and failure docs with the fix. |
| EF-049 | Some operators may use refresh as an intentional machine-wide kill. | A scoped fix should provide an explicit all-installs operation rather than silently removing it. |
| EF-050 | Existing launchers may depend on persisted onboard env overrides. | Any removal/preference change needs a config migration and diagnostics. |
| EF-056 | Correct routing documentation may contradict integrations built from old prose. | Code is authoritative; document actual short-circuits now, then separately version any router change. |
| EF-057 | Empty proxy files may be used deliberately as an override-to-none. | Preserve an explicit disable mechanism if fallback semantics are fixed. |
| EFC-P5-05-2 | Unsigned histories currently receive a success verdict. | Correcting the verdict is intentionally breaking trust semantics; coordinate verifier, CLI exit codes, and docs. |

## 5. Naming-honesty findings

These are semantic naming hazards even when the underlying mechanism remains useful.

| Surface/name | Honest meaning | Forbidden expansion | Evidence |
|---|---|---|---|
| `history_verified` | Sequence and previous-hash links are consistent; signatures are checked only on entries that have one. | “The history is signed/authenticated.” A wholly unsigned chain can pass. | EFC-P5-05-2; `WatchHistory.cs:132-162`; `OccamVerifyTool.cs:92-106` |
| Extract-knowledge `Receipt` | Extraction telemetry attached to knowledge output. | “Receipt v1,” “signed receipt,” or “verifiable provenance.” | EF-006; CAP-287 |
| `occam_claim_check` | BM25 lexical retrieval plus Merkle membership proofs for returned extracted blocks. | “Checks truth,” “evaluates stance,” or “proves absence from the page.” | `TRUST-MODEL.md:138-144,351-361`; CAP-697 |
| `occam_attest` | Unsigned regex/rule-based entailment tally over retrieved blocks. | “Cryptographic attestation” or “signed report verification.” | `TRUST-MODEL.md:146-152`; CAP-721/722/724 |
| `verified` receipt | Supplied bytes verify under the supplied public key; optional content hash matches. | Identity, origin, truth, freshness, or trusted timestamp. | `ReceiptVerifier.cs:17-21,40-79`; `TRUST-MODEL.md:89-110` |
| `unknown_key` playbook | The unsigned claimed `keyId` differs from the local key, so signature verification was skipped. | “Valid foreign author.” Tampering can manufacture this classification. | EFC-P5-05-1; `PlaybookSignature.cs:126-134` |
| `proven` claim-check field | No relevant block was omitted by token truncation from the extractor-provided leaf set. | “The claim was proven true/false” or “the page was exhaustively checked.” | `TRUST-MODEL.md:138-144` |
| “Receipt switch” / `OCCAM_RECEIPTS` | Controls most receipt emission. | A global no-key/no-signature master switch. | EF-005, EF-044 |
| `consensus` / `crosscheck` | One host compares fingerprints from local extraction vantages. | Multi-party, multi-node, geographic, or cryptographically signed consensus. | EF-032; `TRUST-MODEL.md:154-161` |
| Signed playbook `verify.score` / `passesGate` | Unsigned claims stored in the excluded top-level `provenance` block. | Signed quality assurance or a score vouched for by the signature. | `PlaybookSignature.cs:29-39,63-84,143-161`; confirmed sub-finding |

## 6. Dead code that ships

The production Core project uses the SDK default compile glob with no `<Compile Remove>`, so all
`src/FFOccamMcp.Core/**/*.cs` types compile into the AOT binary (`SHIPPED-CODE-MAP.md:7-10,19`).
Therefore **shipped ≠ reachable ≠ documentable**. Public docs describe supported reachable behavior,
not binary membership, internal type names, test-only selectors, or computed-but-discarded values.

| Canonical item | Shipping/reachability verdict | Documentation rule |
|---|---|---|
| CAP-286 / CAP-331 `MaterializedProvenanceResolver` / `ProvenanceTrace` | Ships in Core; zero callers. | Do not document provenance tracing as a capability. |
| CAP-248a `IWorkerProcessSpawner` / `NodeWorkerProcessSpawner` | Ships and is registered; never injected. | Do not present a pluggable worker-spawner extension point. |
| CAP-248b `BrowserConcurrencyGate.Run<T>` | Ships; `Run` is never called. | Do not claim this method enforces concurrency. |
| CAP-324 `ResponseBudgetMode.Unchanged` / `DeltaOnly` | Ships; tests only, no live selector. | Do not list as supported response modes. |
| CAP-328 `CompactMarkdownCodec` / `JsonKnowledgeCodec` | Ships and registers; no MCP codec selector. | Do not document selectable codecs. |
| CAP-330 / CAP-333 canonical knowledge computation | Runs but is discarded by the live response codec. | Do not imply a returned canonical model; record EF-004 performance cost internally. |
| CAP-332 `Fact` / `Entity` / `Relationship` canonical types | Ship; never instantiated. | Do not expose as product schemas. |
| CAP-334 `TableSemanticMaterializer` | Ships; bench/test-only path. | Do not document semantic table materialization. |
| CAP-264 / CAP-279 `paywall` negative receipt | Branch ships; no producer emits it. | Do not list `paywall` as a live emitted negative-receipt case. |
| CAP-165 proxy rotation coverage | Rotation code ships but does not reach daemon/pool/CSS/dom-skeleton paths. | Document exact covered paths, not a global proxy-rotation feature. |
| CAP-166 Core proxy behavior | Core clients ship but ignore `OCCAM_HTTP_PROXY`. | Document worker-only scope (EF-007). |
| CAP-188 retry/backoff | No automatic network retry exists. | Do not infer retry capability from failure handling. |
| CAP-303 `ResponseBudgetDiagnostics` | Computed; absent from MCP response. | Do not document diagnostics as observable fields. |
| CAP-335 `SurfaceSpanAttacher` spans | Computed; not exposed. | Do not document span output. |
| CAP-287 extract-knowledge “Receipt” | Exposed field, but not Receipt v1. | Document as telemetry only (EF-006). |
| CAP-315 incomplete cache key | Live bug, not dead. | Never normalize omitted flags as intended cache identity (EF-001). |
| CAP-436 probe branch | Ships; unreachable. | Omit from probe behavior/failure tables. |
| CAP-552 heal `CreateHeadersScope` | Ships; unused helper. | Omit as a session/header capability. |
| CAP-553 heal `--consent-aggressive` | Worker behavior exists but MCP cannot select it. | Do not document as an MCP option. |
| CAP-496 resolve knowledge-schema failures | Computed/swallowed rather than surfaced. | Do not promise these failure codes to resolve callers. |
| CAP-600 row `base_selector` | Worker concept exists; host parsers never set it. | Do not document live row mode (EF-014; GAP-025). |
| `PlaybookCommunitySanitizer` (EF-047/GAP-008) | Core type ships; no live Core caller. | Never cite it as protection applied by lint, save, or resolve. |

## 7. Phase-5 trust candidates — independent verdicts

No canonical EF numbers are allocated here.

| Candidate | Verdict | Classification(s) | Family slug(s) | Verified evidence and documentation consequence |
|---|---|---|---|---|
| EFC-P5-05-1 | **CONFIRMED** | `SECURITY_RELEVANT`, `AFFECTS_PUBLIC_SEMANTICS`, `DOCS_MUST_WARN`, `NEEDS_FIX_BEFORE_DOC`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `playbook-validation`, `receipts` | `PlaybookSignature.Inspect` reads the unsigned claimed key at `PlaybookSignature.cs:109`, branches to `unknown_key` before verification at `:126-134`. Tampering the claim can downgrade a detectable invalid self-signature into an unverified “foreign” classification. Withhold any statement that `unknown_key` identifies a valid foreign author. |
| EFC-P5-05-2 | **CONFIRMED** | `SECURITY_RELEVANT`, `AFFECTS_PUBLIC_SEMANTICS`, `DOCS_MUST_WARN`, `NEEDS_FIX_BEFORE_DOC`, `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | `change-monitoring`, `verification` | Signature checking is conditional on `Sig != null` (`WatchHistory.cs:155-159`) and the method then returns true (`:162`). MCP maps that to `history_verified` while only separately reporting `signedCount` (`OccamVerifyTool.cs:92-106`); CLI returns exit 0 (`OccamCliVerbs.cs:403-408`). Withhold “verified/signed history” claims. |
| EFC-P5-05-3 | **CONFIRMED** | `SECURITY_RELEVANT`, `DOCS_MUST_WARN` | `receipts`, `verification` | Duplicate-last is implemented in root reconstruction and proofs (`MerkleTree.cs:55-64,87-100,124-145`). Thus `[A,B,C]` and `[A,B,C,C]` can share a root; leaf-count-derived values are unsigned. This is structural ambiguity, not a SHA-256 collision and not proof of a never-extracted block. |
| EFC-P5-05-4 | **CONFIRMED** | `AFFECTS_PUBLIC_SEMANTICS`, `DOCS_MUST_WARN` | `mcp-exposure`, `receipts`, `verification` | Reader exposes transcode but verifier is added only at researcher (`OccamToolProfile.cs:17-32,61-70`). Document profiles as producer-only vs producer+verifier surfaces; do not imply every receipt-producing profile can verify in band. |
| EFC-P5-05-5 | **CONFIRMED** | `AFFECTS_PUBLIC_SEMANTICS`, `SECURITY_RELEVANT`, `DOCS_MUST_WARN` | `verification` | Verdict constants contain no wrong-key result (`ReceiptVerifier.cs:6-15`); any validly parsed nonmatching key reaches `signature_invalid` (`:40-64`). The MCP default-to-local-key behavior compounds the ambiguity (CAP-288; `TRUST-MODEL.md:172-177`). Docs must say “signature did not validate under this key,” not “receipt was tampered with.” |

### Confirmed playbook-provenance sub-finding

**CONFIRMED — `SECURITY_RELEVANT`, `AFFECTS_PUBLIC_SEMANTICS`, `DOCS_MUST_WARN`,
`DO_NOT_DOCUMENT_BUG_AS_FEATURE`; families `playbook-authoring`, `playbook-validation`, `receipts`.**
`ContentHash` excludes the entire top-level `provenance` object
(`PlaybookSignature.cs:29-39`). `BuildSignedJson` then places `keyId`, `alg`, `signedAt`, and
`verify.score` / `passesGate` / `noiseLeakage` inside that excluded object (`:63-84`), while
verification recomputes the same provenance-excluding hash (`:143-161`). Those fields are freely
editable without invalidating the recipe-body signature. Public docs must never call the quality score,
gate result, key claim, or signing timestamp signed; only the recipe body hash is signed.

## 8. Classification counts

Counts include canonical EF-001…EF-057 only; multi-label rows count once in each applicable class.
EF-024 is included only under `INTERNAL_ONLY`. Candidate counts are reported separately.

| Classification | Canonical EF count |
|---|---:|
| `AFFECTS_PUBLIC_SEMANTICS` | 42 |
| `SECURITY_RELEVANT` | 24 |
| `PERFORMANCE_RELEVANT` | 12 |
| `DOCS_MUST_WARN` | 43 |
| `DO_NOT_DOCUMENT_BUG_AS_FEATURE` | 47 |
| `NEEDS_FIX_BEFORE_DOC` | 9 |
| `INTERNAL_ONLY` | 5 |

Candidate EFC counts: `AFFECTS_PUBLIC_SEMANTICS` 4; `SECURITY_RELEVANT` 4;
`PERFORMANCE_RELEVANT` 0; `DOCS_MUST_WARN` 5; `DO_NOT_DOCUMENT_BUG_AS_FEATURE` 2;
`NEEDS_FIX_BEFORE_DOC` 2; `INTERNAL_ONLY` 0.

## 9. Documentation release gate

Before publishing surrounding product documentation:

1. Resolve every ranked blocker or remove the blocked claim/surface.
2. Search for the naive claims in §2 and the forbidden trust expansions in §5.
3. Keep EF-024 withdrawn.
4. Verify every tool/profile/distribution claim against runtime code and shipped artifacts, not file
   presence or old prose.
5. Pair any fix listed in §4 with docs and release notes because it may be behavior-changing.
