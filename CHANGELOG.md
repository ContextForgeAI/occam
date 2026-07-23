# Changelog

All notable changes to **FFOccamMCP** (L0 core) are documented here.

Format based on [Keep a Changelog](https://keepachangelog.com/). Versioning: SemVer; `1.0.0-rc.1` was the first release candidate after L0 closed; `1.0.0-rc.2` is the current RC.

## [Unreleased]

### Fixed

- **Release pipeline — Windows GitHub assets + signing** — `release-windows` now uploads
  `ff-occam-*-win-x64` tarball/manifest to the tag Release (same softprops path as linux/osx;
  waits on `release-linux` so the Release already exists). `sign-release` downloads assets from
  the GitHub Release (`gh release download`), signs only platform `ff-occam-*-{linux-x64,osx-arm64,win-x64}.tar.gz`
  (fail-closed on zero matches), and uploads `.bundle` signatures back to the same tag (`--clobber` on rerun).

## [1.0.0-rc.2] — 2026-07-23

Second **Occam Core 1.0** release candidate after the completed RC.2 stabilization cycle.

### Highlights

#### Runtime

- Truthful `oneOf` contract for `occam_digest.urls`
- Typed `invalid_arguments` for MCP binding failures
- Deterministic receipt validation
- Stable lifecycle handling

#### Validation

- Characterization re-baselined
- RAM stress restored
- Overnight stability PASS WITH WARNINGS resolved
- Public MCP contract stabilized

#### CI

- Nightly host staging
- Shared published-host resolver
- Transport gate stabilization

### Notes

- No public API regression
- No schema rollback
- No runtime behavior regressions

### Added

- **RC.2 integration and release-candidate packaging (PR-H)** — cumulative revalidation of PR-A…PR-G,
  bounded local soak (`scripts/rc2-soak.mjs`), win-x64 Native AOT manifest recording, and remote
  validation packages for macOS ARM64 and Linux x64/Hermes-neutral MCP clients under `docs/rc2/pr-h/`.
  No new product architecture; linux/osx AOT and remote hardware runs remain pending native hosts.

### Changed

- **RC.2 lifecycle identity diagnostics (PR-G)** — Core exposes identity-scoped host descriptors
  (`HostIdentity` / `HostIdentityDescriptor`), targeted shutdown planning, and read-only
  `lifecycle self|diagnose` CLI verbs. The launcher stamps runtime/session/parent identity env vars
  and forwards termination signals to the exact child only. No global singleton and no Hermes-invented
  external API.

- **RC.2 semantic result contract (PR-F)** — responses and recovery attempts now publish independent
  transport, usability, access, focus, completeness, and verdict dimensions. Legacy `ok`, `confidence`,
  `focusMatched`, and `found` keep their previous meanings as aliases; `claim_check` adds `retrieved`
  plus `verdict: not_evaluated`. Agent hints derive from the structured dimensions.

- **RC.2 projection-first response budget (PR-E)** — `max_tokens` allocation now starts from fields
  eligible for serialization, so hidden extraction blocks/tables no longer reduce the Markdown budget.
  Structural focus protects a minimum heading/body unit plus tightly coupled list/table/code evidence;
  constrained retained answers are tracked internally as partial, while a found target whose body cannot
  fit is explicitly incomplete for PR-F mapping. Allocation never silently expands the requested budget,
  and codecs remain presentation-only.

- **RC.2 structural focus and exact fragments (PR-D)** — constrained transcode focus now ranks a
  deterministic `SectionIndex` instead of flat Markdown windows. Exact URL fragments/anchors outrank
  heading coverage, nearby body evidence, and broader relevance; TOC/index sections receive a structural
  penalty and ties keep document order. Numeric and technical identifiers are retained. URL fragments
  are removed from the network request and used locally as intent; no embedding, LLM ranker, or
  site-specific rule is involved.

- **RC.2 unified access classification (PR-C)** — probe and transcode now derive login decisions from
  one evidence-based classifier. Authentication terminology and login-like URL paths are non-decisive;
  a hard `requires_login` needs direct evidence such as HTTP 401/authentication challenge, a redirect
  to a login route, or blocking identity UI without usable public content. Workers emit bounded,
  non-sensitive DOM signals, and diagnostics use stable evidence codes.

- **RC.2 digest MCP boundary (PR-B)** — `occam_digest.urls` now truthfully accepts a preferred native
  string array or a deprecated legacy string. All accepted forms terminate at one bounded
  normalization boundary; malformed, mixed, nested, empty, and oversized inputs return typed
  `invalid_arguments` instead of opaque binding failures.

- **Version-consistent GitHub releases** — `v*` release tags now drive NuGet/assembly/file/informational
  versions, bundle names, manifests, and `VERSION`; packaging executes the produced Native AOT host and
  rejects any mismatch. Any SemVer with a non-empty prerelease component publishes as a GitHub
  prerelease; only tags without a prerelease component are stable. SemVer build metadata (`+…`) is
  rejected before publish. Install docs distinguish checkout vs downloaded-asset paths.

- **Env catalog honesty** — removed undocumented/unimplemented `OCCAM_DEFINITIONAL_NEEDLES` and `OCCAM_OTEL_*` from [configuration.md](docs/configuration.md); `env-catalog.selftest` now also scans `packages/` (covers `OCCAM_RELEASE_BASE_URL` in `@ff-occam/mcp`).

- **Budget ownership cleanup (Occam 1.1 R6)** — single documented story for `max_tokens`: whole-response layer (`ResponseBudgetPlanner`) vs surface/semantic layer (`MaterializationPlanner`). Live path goes through `Compile.BudgetOwnership` (PrepareSurfaceBudget → Plan → TrimStructured). Layers intentionally not merged (parity). Docs: local `docs-internal/BUDGET-OWNERSHIP.md`; [occam_transcode](docs/tools/occam_transcode.md) param note. Gate: L1a `RunBudgetOwnership`.

- **Agent DX (Occam 1.1 R5)** — no new tools / no `occam_read`. MCP `instructions` and docs now state thin ≠ short (`quality.verdict=short_quality` is success; do not heal), prefer one `occam_digest` over N× `occam_transcode`, and keep the winning `occam_transcode` “default page reader” description. Updated: `OccamServerInstructions`, `occam_digest` Description, [choosing-a-tool.md](docs/choosing-a-tool.md), [recipes.md](docs/recipes.md), `llms.txt`. Gate: server-instructions + public-contract description invariants.

### Added

- **Canonical-driven Planner (Occam 1.1 R2)** — `MaterializationPlanner` retains Canonical claim→evidence→source→provenance under `ProvenancePolicy`: `default` budgets claim statements (MaxTokens/4) and focus-ranks when `FocusQuery` is set; `evidence-preserving` keeps the full closure. Codecs remain presentation-only. Live MCP still requests `ProvenancePolicy=default` (no new public param); Markdown surface bytes unchanged. Bench profiles: compact may prune Canonical; evidence-preserving differs on Canonical metrics. Gate: `RunPlannerBench` / `RunMaterializationPlanner`. Contract: local `docs-internal/PLANNER-BENCHMARK-FOUNDATION.md`.

- **PlannerBench expansion (Occam 1.1 R3)** — objective metrics on the post-R2 planner: `TokenReductionRatio`, block/claim/evidence retention ratios, `FocusClaimHitRatio`, `StabilityOk` (N≥3 identical Plans), `CompareToBaseline` vs `compat`, richer planner×codec report (tokens vs passthrough). Merge-blocking marker `L_PLANNER_BENCH_OK`. No live MCP change. Contract: local `docs-internal/PLANNER-BENCHMARK-FOUNDATION.md`.

- **Codec usefulness evaluation (Occam 1.1 R4)** — `CodecBench` multi-fixture matrix vs `markdown-passthrough`, `CompareToPassthrough`, `EvaluateDispositions` / written disposition. Verdicts: passthrough = live default; `compact-markdown` = keep experimental (no public MCP); `knowledge-json` = tooling/tests only (usually larger; Canonical ids/refs). Marker `L_CODEC_BENCH_OK`. No public `codec` param. Contract: local `docs-internal/CODEC-USEFULNESS-DISPOSITION.md`.

- **Extract Quality Gate (ADR-0004, Occam 1.1 R1)** — multi-signal EQM separates short quality documents from bad extraction. `thin_extract` no longer fires on length alone (e.g. example.com-class pages → `ok:true`, `quality.verdict=short_quality`). Success responses may include additive `quality: { score, noise, contentDensity, semanticRichness, lengthPrior, verdict }`; `confidence` maps from EQM score. Promo/consent/headings shells still fail `thin_extract`. Gate: `L_EXTRACT_QUALITY_OK`. Docs: [failure-codes.md](docs/failure-codes.md) (thin ≠ short).

- **Planner Benchmark Foundation (internal, 1.1 validation)** — `PlannerBench` runs the existing `MaterializationPlanner` over fixed offline fixtures with benchmark profiles (`compat` / `compact` / `focus` / `evidence-preserving`), collecting surface/structural/canonical/integrity/focus metrics and determinism checks. Optional planner × codec matrix reuses `CodecBench` (plan once per case, encode with passthrough / compact-markdown / knowledge-json). Not MCP-wired; live Markdown path unchanged. Contract: local `docs-internal/PLANNER-BENCHMARK-FOUNDATION.md`. Gate: `RunPlannerBench`.

- **Architecture migration 1.0 complete (internal)** — Canonical → Planner → View → Codec spine closed (master PR-A…PR-E). Live path remains `ExtractedKnowledgeBundle` → `MaterializationPlanner` → `MaterializedKnowledgeView` → default `markdown-passthrough`. Public MCP Markdown contract, receipt/`contentHash` semantics, and schema unchanged. Canonical-driven planning and public alternate codecs remain deferred (1.1+). Closeout: local `docs-internal/ARCHITECTURE_COMPLETE_1.0.md`. Gate: `RunRuntimeMaterializationMigration` + codec/extension seam asserts.

- **Experimental `knowledge-json` codec (internal)** — `JsonKnowledgeCodec` serializes an already-materialized view (surface + document IR + optional Canonical sidecars) via source-generated JSON (AOT-safe). Registered as `BuiltinExperimental`; selectable by explicit id in tests/bench only — not exposed on public `occam_transcode`. Extended `CodecBench` compares passthrough / compact-markdown / knowledge-json over one planned view. Gate: `RunJsonKnowledgeCodec` / `RunCodecBench`.

- **Runtime Canonical → Planner → Codec cutover (internal)** — live `occam_transcode` now materializes through `ExtractedKnowledgeBundle` → `MaterializationPlanner` → `MaterializedKnowledgeView` (`SourceSurface`) → `KnowledgeCodecSelector` / default Markdown codec. Semantic compile (selectors/fit/budget) is owned by the planner (delegating to existing compile helpers for byte parity). Workers always emit internal blocks/tables for IR; public sidecars still require `json_blocks` / `json_tables`. MCP schema and Markdown bytes unchanged. Gate: `RunRuntimeMaterializationMigration` parity + architecture asserts.

- **Canonical Legacy Adapter (internal)** — `Knowledge/Legacy/TranscodeToCanonical` maps a successful `TranscodeOutcome` to `Source` / `Evidence` / `ClaimCandidate` / `KnowledgeProvenance` (blocks never become Facts; Merkle leaf hashes match receipt leaves). Fail-closed on `ok:false`. Acquisition path via `TryAdaptAcquisition` (no compiled-outcome dependency). Gate: L0 infra unit asserts.

- **Materialized view carries canonical refs (internal, PR-C)** — `MaterializedKnowledgeView` optional `SourceRefs` / `EvidenceRefs` / `Claims` / `Provenance`; planner passes them through under document-IR budget (no claim pruning). Markdown codecs unchanged. Not MCP-wired.

- **Built-in Markdown codec compatibility baseline (internal, master PR-D)** — the live transcode path now sends already-compiled Markdown through the guaranteed `markdown-passthrough` registry default before block reconciliation and receipt hashing. Output remains byte-identical and deterministic; representative Markdown fixtures and receipt-hash equivalence are gated. No MCP schema or response change; alternate codecs remain unwired.

- **Canonical provenance resolver (internal, migration PR-D)** — `MaterializedProvenanceResolver` answers Claim → Evidence → Source → receipt leaf from a materialized view, with optional Merkle membership verify via the same `MerkleTree` primitives as `occam_verify`. Fail-closed; membership ≠ truth. Not MCP-wired.

- **Codec extension seam (internal, master PR-E)** — `KnowledgeCodecSelector` + trust tiers (`Builtin` / `BuiltinExperimental` / `OptInExtension`) + fail-closed codes (`unsupported_codec`, `unknown_capability_profile`, `codec_extension_not_allowed`, …). Third-party codecs register only via explicit `TryRegisterExtension` when opt-in is enabled; no assembly scan / marketplace. Markdown remains the sole default. Not exposed as a public MCP `codec` param yet.

### Fixed

- **Conditional re-reads are whole-response, not Markdown-only 304** — `unchanged:true` now omits `blocks` / `chunks` / `tables` / `feed` / `mediaRefs` / screenshots / translation (minimal envelope). Echoes `contentHash` + new `materializationKey` (`sha256:…` over URL + options + playbook identity). `delta_only` likewise omits heavy sidecars. Response budget modes allocate no markdown floor for unchanged/delta. `BlockReconciler` strips Markdown emphasis/links for survival matching, derives list subsets, and prioritizes focus-relevant blocks for structured budget (version-note-only leftovers dropped). Gates: `L_CONDITIONAL_ECONOMY_OK`, `L_BLOCK_SURVIVAL_OK`, `L10_WORKFLOW_FROZEN_OK`, `L11_WORKFLOW_SECURITY_OK`. Recipe: [Repeated read](docs/recipes.md#repeated-read-conditional--delta).

### Added

- **End-to-end agent workflow regression suite (Prompt 5)** — frozen chains A–J (capabilities→transcode, probe stop-rules, map→digest focus, search rerank→attest, claim→verify, repeated read/delta, RAG trust/salience/staleness, playbook heal/lint provenance, capsule handoff, dataset manifest) plus stop-rule and observability JSONL reports. Merge-blocking markers `L10_WORKFLOW_FROZEN_OK` / `L11_WORKFLOW_SECURITY_OK`; opt-in tunnel/live `L12_WORKFLOW_LIVE_OK` via `--workflow-live`. Matrix: `corpora/workflow-matrix.jsonl`. Report: `corpora/workflow-report.md`.

- **Entity-first focus discovery** — `occam_map` / digest `source_url` no longer treat all `focus_query` terms symmetrically. Query decomposition splits **primary anchors** (rare identifiers, CamelCase/snake_case, path-like, quoted) from **supporting** topical terms; ranking prefers exact path/title primary hits; missing-all-primaries and documentation version-root penalties apply. Hub expansion no longer treats leaf `/library/*.html` pages as hubs; HTML is scanned for primary-anchor hrefs past sequential extract caps. Focused digest discovery reuses map homepage hub expansion, merges sitemap candidates, then ranks before `max_links` cap. Unfocused digests (all `focusMatched:false`) emit `agentHints` `focus_not_found`. Gate: `L_DISCOVERY_FOCUS_OK` / `L_DISCOVERY_FOCUS_LIVE_OK` (`--discovery-focus-live`). Corpus: `corpora/discovery-focus.jsonl`.

- **Public MCP tunnel/schema parity** — live `tools/list` (stdio launch path used by the ChatGPT tunnel) is now the contract gate, not C# reflection alone. Asserts `occam_digest.urls` is optional (`source_url`-only valid), RC1 `occam_transcode` params (`rank_blocks`, `tag_trust`, `delta_only`, `emit_capsule`, …) are present, and `auto_recover` stays absent. Adds `version-surface` CLI verb + `scripts/check-public-mcp-contract.mjs` (schema fingerprint corpus `corpora/public-mcp-schema-fingerprint.txt`). TypeScript `@ff-occam/mcp` / agent-sdk types updated for `source_url` and RC1 transcode options. Gate: `L_PUBLIC_MCP_TOOLS_LIST_OK` / `PUBLIC_MCP_CONTRACT_OK`.

- **Discovery focus ranking precision** — `occam_map` / digest `source_url` discovery previously skipped BM25 reordering whenever the candidate pool was ≤ `max_links` (common on homepages), so `focus_query=asyncio` on Python Docs returned version/whatsnew links in DOM order. Ranking now always reorders on focus: path/title phrase boosts, neighbor context, BM25, soft-stem semantic overlap, version/changelog penalty. Homepage with no strong hit expands up to 3 hub pages (library/docs/index) and re-ranks. Digest auto-discovery applies the same ranker. Gate: `L_DISCOVERY_FOCUS_OK`. Corpus note: `corpora/discovery-focus.jsonl`.

- **Public MCP contract sync** — docs/schema/behavior alignment: digest success fields are `items`/`combined` (not `pages`/`combinedMarkdown`); `occam_digest.max_links` and `occam_playbook_heal.max_skeleton_nodes` now default to `8`/`600` in the MCP JSON Schema (were `null` while docs claimed numeric defaults); `diff_against` rejects JSON objects that previously slipped through as a single CSV token; `if_none_match` / `cache_ttl_s` descriptions match receipt-prefix and cache-eligibility rules. Gate: `L_PUBLIC_MCP_CONTRACT_OK` (`PublicMcpContractUnitTests`). Report: local `docs-internal/CONTRACT_REPORT.md`.

### Added

- **ADR-0004 (accepted, Occam 1.1 R1)** — extract quality gate: separate short quality documents from bad extraction; multi-signal EQM. See CHANGELOG Unreleased and `ExtractQualityEvaluator`.

## [1.0.0-rc.1] — 2026-07-20

First **Occam Core 1.0** release candidate. RC1 fixed-corpus regression green (`RC1_REGRESSION_OK` 12/12); unit gate `L0_GATE_OK`. Engineering report: local `docs-internal/RELEASE_REPORT.md` (not published).

### Highlights

- Version surface → `1.0.0-rc.1` (host + `@ff-occam/mcp` / `agent-sdk` / `skill`); always-on core tools **15**.
- RC1 harness: `corpora/rc1-regression.jsonl` + `--rc1-regression`.
- Whole-response `max_tokens` budget (`ResponseBudgetPlanner`).
- Digest `focusMatched` stem/synonym tiers; HN `records[]`; feed HTML-safe summaries.
- Known non-blocking: HN Readability semantic gap; PDF host may `http_403` (honest).

### Notes

The detailed Changed / Fixed / Added bullets below fold the former `[Unreleased]` post-0.9.0 track into this RC.

### Changed

- **`max_tokens` is a whole-response budget** — previously only markdown was capped while `blocks` / `tables` / `chunks` / `mediaRefs` / `feed` / screenshots could still inflate the MCP payload far past the caller's expectation. `ResponseBudgetPlanner` now shares one budget: receipt reserve → markdown ≥50% floor → greedy structured fill (blocks → tables → chunks → media → feed); screenshot drops first when over budget. `compile.tokensEstimated` = sum of retained buckets; `compile.budget` reports the allocation; `compile.omitted.structured` counts dropped sidecars. Gate: L1a `budget bench` + ASCII allocation diagram on stderr.

- **Attest trust model — retrieval ≠ support ≠ proof (`occam_attest`)** — fixes a critical honesty defect where BM25/lexical retrieval alone set `grounded=true` (e.g. claim “asyncio is a database engine” matching “database connection libraries”). Attestation is now three independent layers: (1) retrieval returns top-K blocks only; (2) a fail-closed semantic classifier returns `status` ∈ `supported` \| `contradicted` \| `related` \| `unsupported` \| `unknown`; (3) Merkle proof proves only that a cited block existed in the signed extract — never that the claim is true. `grounded` remains as a compat alias for `status=supported` only. Report adds named status counts plus `unsupportedTotal` (sum of non-supported). Receipt v1 / `occam_verify` unchanged. `occam_claim_check` stays retrieval (`found` ≠ support). Docs: [occam_attest](docs/tools/occam_attest.md), [MCP_API_SPEC.md](MCP_API_SPEC.md).

### Fixed

- **`focusMatched` false on clearly matching excerpts** — digest used an all-terms exact-substring rule (`hits >= terms.Count`) with no stem/synonym tolerance, so e.g. query `configuration syntax` vs excerpt “Configure … syntax” scored `false`. New `FocusMatcher.EvaluateForDigest`: phrase → ideal (all terms via exact/soft-stem/synonym) → partial (`max(2, ceil(2n/3))` hits when n≥3) → none; 2-term queries stay strict for hub honesty. Gate: L2 focus ideal/partial/none/synonym asserts.

- **Hacker News tables → semantic records** — physical `rows[]` still emit one array per `<tr>` (so one HN story stayed split across two rows and never became knowledge). `json_tables` now also emits optional `records[]`: each HN item is one object `{ rank, title, url, site, author, points, comments, age, schema:"hn_item", provenance }` spanning the title+subtext rows. Markdown/GFM path unchanged. Knowledge IR maps `records` → `SemanticRows`. Gate: `dom-tables.selftest` + tables contract round-trip.

- **Feed summaries no longer leak raw HTML** — `json_feed` items now expose `summaryHtml` (source markup when present), `summaryText` (tags stripped, entities decoded), and `summaryMarkdown` (clean markdown, no raw `<p>`/`<a>`). Compat field `summary` aliases `summaryText`. Page markdown for feeds uses `summaryMarkdown`. Also accepts JSON Feed 1/1.1 (`application/feed+json`). Lightweight string convert — no per-item Turndown/JSDOM. Gate: `feed-items.selftest` (RSS 2.0, Atom, JSON Feed).

- **`occam_digest` schema — `urls` no longer required when `source_url` is set** — docs said `source_url` ignores `urls`, but the MCP input schema marked `urls` as required (non-nullable C# param), so `source_url`-only calls were rejected by hosts before the tool ran. `urls` is now optional (`string? = null`); provide `urls` and/or `source_url` (at least one) or get typed `invalid_arguments`. When `source_url` is set, `urls` is ignored with no silent fallback if discovery is empty (`invalid_urls`). Gate: digest input-contract asserts in `L2DigestUnitTests`.

- **Markdown wire escapes (`\u003E` / `\u0022` / `\u0027`)** — System.Text.Json's default encoder rewrote `>`, `"`, and `'` as unicode escapes inside tool JSON. After MCP wraps that string as `content[].text`, agents saw literal `\u003E` in markdown (blockquotes, quotes) while structured `blocks[].text` looked fine after nested parse. Transcode/digest serialization now relaxes only those three printable escapes on the wire; `\u003C` (`<`) and `\u0026` (`&`) stay escaped (XSS floor preserved — not `UnsafeRelaxedJsonEscaping`). Helper: `OccamJsonPrintableEscapes`. Gate: `L_MD_ESCAPES_OK`.

### Added

- **Heading level on structured blocks** — `occam_transcode(json_blocks=true)` now emits `level` (1–6) on `heading` blocks, so a consumer can recover the `h1`…`h6` hierarchy instead of seeing every heading flattened to one level. Additive (present on headings only; other block types are unchanged); surfaced from the DOM block extractor through to the response. Docs: [MCP_API_SPEC.md](MCP_API_SPEC.md).

- **Delta-as-primary re-reads (`delta_only`)** — when an agent already holds a page's prior extract, `occam_transcode(diff_against=…, json_blocks=true, delta_only=true)` now returns only the block-level `diff` and an **empty** `markdown` (`deltaOnly:true`), so a re-read costs delta-size tokens instead of re-sending the whole page. The consumer reconstructs current content from its prior blocks + the delta (drop `removedHashes`, apply `addedBlocks` in `blockHashes` order) and verifies the result against the returned `contentHash` — which, under `delta_only`, hashes the *full* current markdown even though the body is empty. The delta codec and `contentHash` compose: the delta transports the change, the hash proves the reconstruction. Falls back to full markdown with a `delta_only_ignored_*` warning when there's no base or no blocks. Docs: [MCP_API_SPEC.md](MCP_API_SPEC.md).

- **Always-on `contentHash` — a cache / KV-prefix key on every read** — `occam_transcode` success responses now carry a bare-hex SHA-256 of the returned markdown, no receipts required. Two uses from one field: (1) store it and pass as `if_none_match` next call for a 304-style skip (previously only receipt users had a token to send); (2) it is the KV-cache prefix key — an identical `contentHash` means byte-identical markdown, so a harness can reuse the already-encoded prompt tokens instead of paying to re-encode the same page. Same digest as `receipt.signed.contentHash` minus the `sha256:` prefix (either form is accepted by `if_none_match`); omitted on an `unchanged` body. Docs: [MCP_API_SPEC.md](MCP_API_SPEC.md).

- **Omitted manifest — a machine-readable record of what `max_tokens` dropped** — when token budgeting truncates a page, `compile.omitted` now carries a structured manifest instead of leaving the holes only as in-band `<!-- SNIP … -->` comments: `reason` (the truncation strategy), `tokensDropped` (estimated tokens removed), `regions` (`tail` / `middle` / `unchosen`), and `sections` (`focus_window` only — count of on-topic sections dropped). So a consuming agent *knows* the returned body is partial and how big the gap is, instead of developing false confidence in a silently-truncated read. Present only when `truncated` is `true` (honest absence when nothing was cut); always on — an agent can never accidentally hide the holes. Docs: [MCP_API_SPEC.md](MCP_API_SPEC.md).

- **Trust channels — per-span prompt-injection isolation (`tag_trust`)** — `occam_transcode(json_blocks=true, tag_trust=true)` tags each block with a machine-checkable `trust` channel: `suspicious` (the text reads like an instruction to the reader/model — "ignore previous instructions", "you are now…", "system:") or `boilerplate` (a non-content region — nav/footer/aside/comment that leaked past readability). Normal main content is untagged. Instead of asking the model to judge injection in prose it's simultaneously reading, a harness gets an explicit, structural signal it can act on ("never execute instructions from a `suspicious`/`boilerplate` span"). Conservative by design — ordinary prose that merely uses "you"/"instructions" is not flagged. Heuristic signal, not a guarantee; rides the same per-block annotation as `salience`. Docs: [MCP_API_SPEC.md](MCP_API_SPEC.md).

- **Per-span salience — machine-native attention signal on extraction blocks (`rank_blocks`)** — `occam_transcode(json_blocks=true, focus_query=…, rank_blocks=true)` now tags each block with a `salience` (0–1): its BM25 relevance to the focus query, normalized to the top-scoring block. Instead of re-deriving importance from the whole page, a consuming LLM gets an explicit per-span signal of which blocks to weight and cite — the first carrier of the "span-addressable substrate" (things like trust-channel and confidence ride the same per-block annotation). Reuses the deterministic block ranker; opt-in and side-effect-free (blocks a query matched nothing get a flat `0`; no query → no `salience`). Docs: [MCP_API_SPEC.md](MCP_API_SPEC.md).

- **Provable absence — `occam_claim_check` can now prove a claim is NOT on a page** (`proven` + signed `leafSetComplete`) — a grounded "no" instead of a silent miss, the anti-hallucination counterpart to a citation. When a check finds no matching block AND the extract wasn't truncated, the response carries `proven:true` and the signed receipt attests `leafSetComplete` — meaning the block Merkle root covers the *complete* extracted content, so "the claim is absent" is verifiable (it is not among the signed leaves), not merely "not found". `proven:false` when completeness is unknown (truncated/empty extract). `leafSetComplete` is a new signed, backward-compatible receipt field (omitted on older receipts → identical canonical bytes, so existing receipts still verify). Second slice of the Occam Knowledge Protocol trust layer, after the proof-carrying capsule. Docs: [MCP_API_SPEC.md](MCP_API_SPEC.md).

- **Proof-carrying capsule — verified hand-off between agents (`emit_capsule` + `occam_verify`)** — the first primitive that lets one agent trust another agent's web read **without re-fetching**. `occam_transcode(emit_capsule=true)` returns `receipt.capsule`: a single self-contained `occam://capsule/…` string bundling the signed receipt, the markdown it commits to, the block leaves, and a self-describing verify recipe. A receiving agent passes it straight to `occam_verify` — which now accepts a capsule anywhere it accepts a receipt — and trusts the fact only on `verdict: verified` + `contentHashMatch: true`. Fabrication (claimed-but-not-read) and tampering are caught structurally; the signature stays over the envelope's canonical bytes, so packaging never changes what was signed. `prove` mode works through a capsule too (proof one block was in the page, no page needed). Opt-in (repeats the markdown → costs tokens); needs receipts on. First slice of the Occam Knowledge Protocol's trust layer. Docs: [MCP_API_SPEC.md](MCP_API_SPEC.md), [verified hand-off](skills/occam/references/verified-handoff.md).

- **`occam_client_capabilities` — declare LLM context so Occam sizes extracts** — MCP hosts do not expose model context windows to servers. Agents call this once with `context_tokens` (or operators set `OCCAM_CLIENT_CONTEXT_TOKENS`); Occam stores an ambient output budget (~20% of context, clamped 512–16384) and applies it to later `occam_transcode`/`occam_digest` calls that omit `max_tokens`. Returns `suggestedProfile` as advisory. Fifteenth always-on core tool; included in every `OCCAM_PROFILE`. Docs: [configuration.md](docs/configuration.md).
- **`OCCAM_PROFILE` — role-scoped MCP tool surface** — env `full` (default) | `reader` | `researcher` | `auditor` narrows which core tools appear in `tools/list` and in server instructions, so coding agents do not drift into playbook heal/save on a simple read. `researcher` = reader tools + `occam_claim_check` + `occam_verify`. Invalid values fall back to `full` with a stderr warning. Opt-in batch/watch/consensus/atlas flags unchanged. Docs: [configuration.md](docs/configuration.md), [choosing-a-tool.md](docs/choosing-a-tool.md), [getting-started.md](docs/getting-started.md).
- **Browser auto-provision (self-managing browser layer)** — when a page genuinely needs the browser backend and no browser binary is installed, occam now downloads the user-level Playwright Chromium itself, retries, and returns the rendered content with a `browser_provisioned` note — instead of a bare `ok:false`. One hook at the shared launch point covers both the one-shot and daemon-pool paths; `classifyBrowserLaunchError` keeps the boundary honest (a missing binary is auto-provisioned; missing system libraries stay the human's root step → typed `browser_required`). Off-switch `OCCAM_BROWSER_AUTOINSTALL=0`. Verified on a fresh host: occam self-downloaded Chromium (~36s) and rendered the page. Docs: [configuration.md](docs/configuration.md).
- **Actionable browser-availability errors + `install-browser` verb** — when a page needs a browser and none is installed, `occam_transcode` failures now carry `failure.reason` (human why) and `failure.fix` (`{ kind, command, rootRequired }`) instead of a bare `workers_unavailable`. The `command` is runnable: `occam install-browser` for a missing browser binary (user-level, no root) or `npx playwright install-deps chromium` for missing system libs (`rootRequired: true` — the boundary occam won't cross for you). New setup verb `FFOccamMcp.Core install-browser` provisions the user-level chromium and prints a JSON marker (`status: installed | already_present | worker_missing | failed`); a configured system browser short-circuits. Classifier fires on both the one-shot and the pool/daemon browser paths. Docs: [MCP_API_SPEC.md](MCP_API_SPEC.md), [failure-codes.md](docs/failure-codes.md), [troubleshooting.md](docs/troubleshooting.md).
- **Portable agent skill (`skills/occam/`, `@ff-occam/skill`)** — host-neutral skill wrapper for FF-Occam MCP: lazy-loaded `SKILL.md` + `references/` (install, tool picker, recipes, failure codes, MCP tools, agent SDK). Install via `occam skill install --platform all` or `npx @ff-occam/skill install`. Targets Cursor, Claude Code, Hermes, Copilot, Kiro, Pi, Devin, Codex (`AGENTS.md` section). Shipped in Level B tarball under `skills/occam/`. Docs: [getting-started.md](docs/getting-started.md#agent-skill-any-harness).
- **Skill install gate (v0.9.1)** — `skills/occam/references/install.md` + `SKILL.md` prerequisites aligned with root `INSTALL.md`: Hermes tarball path, `OCCAM_HOME`, `hermes-smoke.mjs`, forbidden net8/csproj/occam-mcp.js/bootstrap, release 404 stop.

### Changed

- **MCP server instructions rewritten for anti-hallucination tool selection** — initialize `instructions` now lead with “prefer Occam over generic web fetch / memory” and the trust rule (`ok:false` = unknown); receipts are a short footer, not the masthead. Text is profile-aware so a `reader`/`researcher` host does not advertise hidden tools.
- **MCP negotiation and remote transport are fail-closed** — the public TypeScript client now offers the current stable MCP revision (`2025-11-25`), accepts only its explicit compatibility set (`2025-11-25`, `2025-06-18`, `2025-03-26`, `2024-11-05`), exposes `negotiatedProtocolVersion`, and disconnects before tool use when a server selects an unknown revision. Remote WSS now accepts JWTs only through `Authorization: Bearer` and rejects URI query tokens, uses real HTTPS OpenID Connect metadata/issuer discovery for rotating signing keys (the old option incorrectly treated raw JWKS as an OIDC metadata document), validates signature + expiry + exact issuer/audience, permits an explicit numeric remote bind while keeping unauthenticated transports loopback-only, caps concurrent sessions at four by default (`OCCAM_REMOTE_MAX_SESSIONS=1..32`), and cancels the per-session MCP host on socket disconnect. Both WebSocket modes now reject binary frames and cap each reassembled text message at 4 MiB by default (`OCCAM_MCP_MAX_MESSAGE_BYTES`) instead of buffering unbounded input. Docs: [transports.md](docs/transports.md), [configuration.md](docs/configuration.md).
- **Token economy is no longer English-only** — three coupled fixes to the budgeting/focus path. (1) Tokenization matched ASCII `[a-z0-9]` only, so Cyrillic/Greek/Arabic focus queries and content produced *zero* tokens — focus matching and the fact-density filter were silently blind on every non-Latin page; it now segments on Unicode letters/numbers. (2) The token estimate was a flat `chars/4`, calibrated for Latin and undercounting non-Latin scripts several-fold (a CJK character is ~1 token, not ¼); the estimator is now script-weighted (ASCII ¼, CJK/Kana/Hangul 1, other non-ASCII ½) and truncation converts a token budget to a character cut with the same weighting, so a CJK/Cyrillic page is no longer over-kept past its `max_tokens`. (3) The focus/definition ranker had per-topic literals (`"closure"`, `"any piece of source code"`, a `"scope"` penalty) tuned to one MDN page; these are replaced with generic definitional-shape ranking (query-term-in-heading, definitional connectors like "X is a…"/"X refers to…", plural-tolerant term matching), so a definition of *any* concept is preserved, not just the one it was tuned to. The two divergent `TokenEstimator` copies were unified into one script-aware source of truth (`EstimatorId = "heuristic-unicode-v1"`). English behavior is unchanged (ASCII weight ¼ = the old `len/4`); the frozen MDN-closure cases still pass as a regression guard, alongside new Cyrillic-focus, CJK-estimate, and generic-definition cases. Tool signatures are unchanged; `compile.tokenEstimator` and `receipt.tokenEstimator` are additive provenance fields so consumers know the counts are heuristic rather than exact-tokenizer claims.
- **Distribution URLs no longer default to a private LAN host** — install/release/repo defaults no longer hardcode a private forge. Public defaults point at GitHub (`ContextForgeAI/occam`); operators can override with `OCCAM_RELEASE_BASE` / `OCCAM_RELEASE_BASE_URL` / `OCCAM_REPO_URL` / `OCCAM_GET_URL` (and `OCCAM_RELEASE_ALLOW_HTTP=1` only when intentionally using HTTP). Private CI-runner setup notes were removed from the public tree. Test fixtures that use RFC1918 addresses on purpose (SSRF/private-URL guards) are unchanged.
- **The browser auto-provision rule now lives in one place** — "will occam install chromium itself?" was implemented twice, once in the worker (which performs the install) and once in the host (which predicts it to decide whether to fall back to HTTP), kept in sync by hand. The two agreed, but the first channel or override added on one side only would have split them: the host would skip its HTTP fallback for a provision that never happens, turning a quiet fallback into a `playwright_missing` error. The host now asks the worker instead of mirroring it, so the rule can't drift. No behavior change; the probe is pure logic (no browser launch), runs only when no browser is installed, and its answer is cached for the process.
- **Playbook overlays now run on the warm browser daemon (and provenance only claims what was applied)** — a resolved playbook genome reached the browser worker through a per-process CLI arg, which a long-lived pooled daemon can never receive; overlay extracts were therefore forced onto a cold one-shot browser, losing warm-pool speed exactly on the hosts worth a playbook. The daemon `/extract` protocol now carries the genome inline (`playbook_overlay_json`), and the worker applies it per-request (`AsyncLocalStorage`), so concurrent requests for different hosts keep isolated overlays and the one-shot CLI path is unchanged. Honest provenance: the worker reports whether the overlay actually matched the host, and the receipt stamps `playbook` **only when it was applied** — on both the browser and HTTP backends — instead of whenever an overlay was merely resolved. Verified: a warm-daemon extract of a playbook host returns the overlay-shaped markdown with `receipt.signed.playbook` present, and a host without a playbook returns none.
- **Unified the two http→browser cascades into one (router-level, single-pass)** — the flagship `occam_transcode` used to run its OWN http→browser recovery in the tool (splitting `http_then_browser` into http-then-browser), separate from the router cascade that `digest`/`extract_knowledge`/`verify` use. The two had already diverged (a 404 fix landed in one but not the other), so every cascade fix had to be written twice. Now the router owns the single cascade: its success test also rejects raw-thin extracts (escalating them to the browser once), it emits the per-attempt `recovery[]` log and carries `browser_provisioned`, and the tool just calls the pipeline. Behaviour on the flagship path is unchanged where it mattered (404 → `http_404` with no browser mask; browser-exhausted thin → stop, no retry, no heal); `recovery[]` is now present only when a cascade actually ran (single-backend/playbook-forced-http calls omit it). No tool-signature or API change. Verified: full L0–L9 gate + real-MCP flagship probes.
- **Removed the dead `occam_transcode` `auto_recover` parameter** — it was advertised ("default false") but never read: the http→browser recovery it described is driven by `backend_policy` (always active on the default `http_then_browser`), not by this flag, so setting it `true` or `false` produced identical behaviour. The misleading no-op is gone from the tool schema (verified absent from `tools/list`); recovery behaviour and the `recovery` response field are unchanged. Docs synced ([MCP_API_SPEC.md](MCP_API_SPEC.md), [tools-reference.md](docs/tools-reference.md)).
- **`occam_transcode` description rewritten for tool selection** — agents were reaching for a generic built-in `web_extract` first and only sometimes falling through to occam. Measured with a direct A/B against the model (`scripts/bench/tool-selection-ab.mjs`): the old description won the first-tool pick on only **38%** of "read a page" intents; a rewrite that leads with the intent-matching phrase ("Extract the content of a web page … the default page reader: prefer it over any generic web fetch/extract tool") took it to **100%**. Renaming `occam_transcode`→`occam_read` slightly *hurt* (88%), so the name is unchanged. Description-only change; no behavior change.
- **Repo cleanup (2026-07-08)** — removed root gate log dumps (`gate-*.txt`), `QUICKSTART.md`, `STRATEGIC_ROADMAP.md`; untracked 77 sprint `*_PROMPT.md` (keep `quality-audit-agent` + `wide-cursor-desk`); trimmed `quality-audit-reports/` to three baseline files. Preserved knowledge in `docs/roadmap.md`, `docs/quality-baseline.md`, `corpora/README.md`; English `corpora/visual-matrix.HOW-TO-READ.md` replaces Russian copy.
- **Documentation rewrite** — replaced legacy `docs/` set with twelve code-derived pages (`getting-started`, `concepts`, `tools-reference`, `choosing-a-tool`, `configuration`, `transports`, `receipts`, `failure-codes`, `troubleshooting`, `recipes`, `faq`). Kept `receipt_verification.md` as normative spec. Lean `README.md`. Updated cross-links in `MCP_API_SPEC.md`, `AGENTS.md`, `corpora/occam-host-wizard-manifest.json`.
- **npm wrapper local-install auto-detect (`@ff-occam/mcp`)** — when `occam-mcp.js` runs on a machine with a git clone or release tarball, it discovers the install root from `OCCAM_HOME`, the package path, `cwd`, or the script path, then delegates to `scripts/launch-mcp-host.mjs` instead of downloading from GitHub Releases. Docs: [getting-started.md](docs/getting-started.md), [troubleshooting.md](docs/troubleshooting.md).

### Fixed

- **Legacy local playbooks no longer collapse an otherwise healthy default extract to `thin_extract`** — early heal/save builds persisted `extract.content_selectors` while current worker seeds use `extract.contentSelectors`. The C# resolver already made the *reported* selector look healthy through lower-tier fallback, but passed the raw higher-tier JSON to the worker; HTTP/browser therefore missed the selector and could select a 170-character Readability fragment (reproduced on the real nginx index) instead of the 1823-character `#content` extract. Selector normalization now lives at the shared worker boundary and is used by both HTTP and browser paths; the resolver recognizes the persisted alias too. Current playbook output remains canonical camelCase, and the user-local file is not rewritten.
- **The public TypeScript MCP client now performs a real handshake and owns its process lifecycle** — `createClient()` previously waited for the server to send `initialize` (the protocol requires the client to send it), so every programmatic connection timed out and could leave the AOT host, worker daemon, and recurring readiness timer alive. The client now sends `initialize` + `notifications/initialized`, clears every request timer, rejects pending calls on exit, unwraps Occam JSON from the MCP result envelope, and shuts down idempotently via stdin EOF with bounded process-tree termination. Binary discovery reuses the RID-aware package resolver, with the npm launcher as the installed-package fallback. Added `listTools()` and generic `callTool<T>()` for modular access to all core and opt-in tools. Regression coverage includes successful initialization, early host exit, stalled-call timeout, double start/stop, resolver RIDs, and a live 14-tool AOT smoke with zero new Occam processes after stop. Docs: [getting-started.md](docs/getting-started.md).
- **`@ff-occam/agent-sdk` now ships a coherent ESM and high-level API boundary** — the package previously compiled CommonJS around the ESM-only MCP client, omitted documented helpers (`createAgentClient`, `probeAndTranscode`, `mapAndDigest`, `mapThenDigest`, and utilities) from its root exports, and silently passed camelCase workflow options into snake_case client methods. It now publishes ESM, exports the documented surface, maps every option explicitly, validates resolved backend policies, and models honest workflow failures as nullable results. The README license is corrected to AGPL-3.0-or-later.
- **`occam_map` no longer silently truncates large or multibyte sitemaps** — the probe fetcher clamped every read to 256 KiB even when a caller explicitly asked for more, and treated a byte limit as a `char[]` length. Sitemap discovery (which requests 2 MiB) therefore saw only the first 256 KiB while UTF-8 pages could consume more source bytes than requested. The async reader now enforces the requested cap on source bytes, honors callers up to a 4 MiB ceiling, and keeps the 256 KiB probe default.
- **`occam_map` sitemap discovery now respects `timeout_ms` as a real total budget** — robots + up to four sitemaps were fetched sequentially at the full timeout, and the old cancellation token stopped at response headers while a slow body used blocking reads. The deadline now covers redirects, body streaming, and the whole sitemap walk. A timeout before any link returns `timeout`; a partial success adds `partial: true` and a warning instead of pretending the map is complete.
- **Probe/map/search network paths no longer block host threads** — redirect following, response-body streaming, sitemap/digest source discovery, all three search providers, JSON deserialization, and extractability reranking now use cancellation-aware async I/O. Caller cancellation is propagated; provider/page timeouts remain typed failures.
- **Worker process shutdown no longer leaves stale PIDs behind** — daemon/slot stop already killed and disposed the `Process`, but did not release it from `WorkerProcessGroup.ActivePids`/POSIX groups. A later host shutdown could act on a reused PID. `TerminateAndDispose` now releases the process-group registry before disposing; the gate starts a real Node process and proves the tracked count returns to baseline.
- **Browser pool and retry resource guards are now exact** — cancelled slot acquires cannot leak `_pendingAcquires`, acquisition telemetry reports the live waiting depth (not a stale self-inclusive snapshot), and the post-settle SPA retry shares the same 900k DOM cap as the first snapshot. Regression tests cover cancellation, telemetry depth, and DOM growth between snapshots.
- **A plain 404 on some sites reported "workers_unavailable / run doctor" instead of `http_404`** — an error response's body is deliberately never read, which left the request in flight, and the per-request connection pool was then torn down with undici's `close()`, which *waits for in-flight requests to finish*. On a large error page (e.g. MDN's 404) that wait never ended: the worker's promise never settled and node exited silently, so the host could only report `workers_unavailable` — blaming the user's install for a page that had simply 404'd. The pool is now aborted rather than drained, and the unread body is released explicitly. Two masks kept this hidden: a newer Node on dev machines tolerates the unread body, and CI set `OCCAM_ALLOW_PRIVATE_URLS=1`, which skips the SSRF check and with it the very connection-pinning path the bug lived in — so the per-PR gate no longer sets that flag and now exercises the default path every user actually runs.
- **Workers can no longer fail silently** — a one-shot worker whose extract promise never settles used to exit printing nothing at all, leaving the host to guess (`workers_unavailable`). Both the HTTP and browser one-shot workers now always emit a typed verdict — a `timeout` failure plus a stderr diagnosis — instead of dying quietly. Worker-crash messages also no longer claim "browser worker" when the HTTP worker was the one that crashed.
- **Unified worker result mapping — one-shot timings are now accurate** — the daemon and one-shot paths built the extract result three different ways, so a one-shot extract (proxy rotation, playbook overlay, or daemon disabled) reported `timings.networkMs` / `parseMs` as 0 and dropped `browserProvisioned` on the failure branch. All paths now go through the single `WorkerExtractPayloadMapper`, so timings and provision telemetry are consistent regardless of path.
- **Misconfigured `OCCAM_*` integer env vars are no longer silent** — an out-of-range or non-numeric value (e.g. `OCCAM_BROWSER_TIMEOUT_MS=300000` clamped to 180000, or a typo like `60s`) was silently clamped/defaulted, so the operator believed a setting took effect when it didn't. occam now prints a one-line `[occam.config]` warning to stderr on a parse-fail or clamp.
- **Playbook overlays are no longer silently dropped on the browser (pool/daemon) path** — a resolved genome (selectors + postMarkdown) is delivered to the worker as a per-process CLI arg, which the long-lived pooled browser daemon can't receive, so a `playbook_policy=auto` extract through the default browser pool ignored the playbook entirely while the receipt still claimed playbook provenance (a hit to the "verifiable extraction" guarantee). Browser extracts that carry an overlay now route through the one-shot worker (which forwards it), matching the HTTP path. Warm-daemon speed is retained for non-playbook browser extracts; carrying the overlay in the daemon `/extract` protocol so playbook hosts keep the pool is a separate follow-up.
- **Long-lived worker daemons no longer risk an undrained-pipe deadlock** — the HTTP and browser-pool daemons were spawned with both stdout and stderr redirected to C# pipes that were never read; a chatty daemon (Playwright/Chromium diagnostics, provision progress) could fill the OS pipe buffer, block its own `write()`, and hang — surfacing only as health-check timeouts under sustained load (the warm-daemon path is the main performance advantage, so this hit exactly where it hurts). Their stderr is now inherited (flows to the host's diagnostics stderr) and their stdout is actively drained, so neither pipe can fill.
- **Browser-daemon HTTP-client ceiling now sits above the max daemon-wait** — the anti-hang ceiling (10 min) could be *shorter* than the computed daemon-queue wait (up to 15 min under a raised `OCCAM_BROWSER_MAX_PARALLEL`), firing first and mis-reporting a truncated queue-wait as a `timeout`. It is now derived from the max daemon-wait + a 2-minute margin. Also removed a dead, never-used `"browser.daemon"` HttpClient registration (a trap for anyone "fixing" the daemon timeout there).
- **`requires_login` is now path-segment precise, not a substring match** — occam previously pre-fetch-rejected any URL whose path merely *contained* `login`/`signin` (e.g. `/blog/login-best-practices`, `/docs/login-api`, `/signin-widget-tutorial`) as `requires_login`, without ever fetching it — claiming a page requires login with zero evidence. It now matches only a dedicated login/sign-in path *segment*; content pages that discuss login are fetched normally. The content-based login-wall detector is unchanged. Docs: [failure-codes.md](docs/failure-codes.md).
- **Both-backends-failed now surfaces the more informative failure (less masking)** — when the HTTP and browser attempts both fail with no usable content, the router *and* the tool's recovery now return the more actionable of the two codes (e.g. a real `http_403` / `captcha_or_challenge` instead of the other attempt's generic `extraction_failed`), so the agent gets `configure_session_profile` / `stop` guidance rather than a misleading heal hint. Shared ranking across both cascades.
- **Content-rich pages are no longer mis-escalated as anti-bot challenges on the router path** — the router's success check now applies the same >2 KB "real content" guard as the post-processor, so a security/docs article that merely mentions Cloudflare/captcha/"just a moment" isn't treated as an interstitial (which had forced a pointless full browser render on `digest`/`verify`).
- **Minor agent-contract cleanups** — `occam_playbook_heal` is no longer suppressed for URLs that merely mention "captcha" in a content slug (only real challenge hosts / `/cdn-cgi/challenge-platform` / challenge query params); the `heal_not_applicable` hint dropped its context-blind "retry with browser" nudge; an `http`-only request no longer requires the browser backend to be ready; removed a dead `url` parameter on the internal failure-decision lookup.
- **`session_profile` headers now actually reach the extraction workers on the transcode/digest path** — the HTTP and browser backends built their worker options without the session headers file and never read the ambient header scope the pipeline opened, so cookies/authorization from a `session_profile` were silently dropped on `occam_transcode` (and digest/map): the worker fetched anonymously and login/paywall retries kept failing even after the agent followed occam's `configure_session_profile` guidance. Both backends now thread the merged headers + Playwright storage-state into the worker call. Verified end-to-end — a marker header injected via a profile now appears in the extracted content; anonymous requests omit it. Docs: [MCP_API_SPEC.md](MCP_API_SPEC.md).
- **`occam_transcode`'s default recovery path no longer masks 404s or loops on genuinely-thin pages** — the flagship tool runs its own http→browser recovery, *not* the router's `http_then_browser`, so the earlier terminal-HTTP short-circuit and browser-aware thin handling did not apply to the default path (they covered digest/knowledge/verify + explicit `backend_policy=browser`). A soft/rich 404 could be rendered in the browser and returned `ok:true`, and a page thin under *both* http and browser still told the agent to `retry_transcode(browser)`. The recovery path now (1) short-circuits terminal `http_404`/`http_410` before the browser attempt, and (2) on a double failure returns the more informative outcome — preferring the browser attempt on a tie so a browser-exhausted thin surfaces as `backend=browser` → `stop`, not a retry loop. Docs: [failure-codes.md](docs/failure-codes.md).
- **404/410 no longer masked as `extraction_failed` under `http_then_browser`** — a definitive "resource gone" HTTP status was escalated to the browser backend, which on some 404 pages throws (surfacing to the agent as `extraction_failed` / `TypeError`) and dropped the authoritative `http_404`. The router now short-circuits terminal HTTP statuses (`http_404`, `http_410`) before browser escalation, so the agent gets the real status, the correct `stop` decision ("Page not found — fix the URL"), and no pointless `occam_playbook_heal` hint. Docs: [failure-codes.md](docs/failure-codes.md).
- **`thin_extract` no longer loops a compliant agent after the browser was already tried** — a genuinely near-empty page (e.g. `example.com`) returned `thin_extract` with `retryable: true` + a `retry_transcode(browser)` hint even when the failing extract already came from the browser backend, so an agent that follows occam's hints would retry indefinitely (http → browser → probe → map …). When the browser backend was already used, occam now drops `retryable`, swaps the decision to `stop` ("genuinely thin — report the little content, do not retry or invent"), and suppresses the heal hint. HTTP-only thin (browser not yet tried) is unchanged. Docs: [failure-codes.md](docs/failure-codes.md).
- **Agent tool-set guidance for narrow reader agents** — documented that exposing the full fourteen-tool set to a small local model invites task-drift into the playbook-authoring tools on a `thin_extract`; a role-scoped reader set (`occam_transcode`/`probe`/`map`/`digest`/`extract_knowledge`/`search`) removes it. Docs: [choosing-a-tool.md](docs/choosing-a-tool.md).
- **`browserProvisioned` now surfaces on the transcode response after a branch-2 auto-provision** — previously the provisioning attempt timed out (a cold ~130 MB chromium download does not fit the normal per-page budget), so only the chromium-now-present *retry* succeeded — dropping the telemetry and wasting the first attempt. Fix is layered: (1) a provision-expected browser call gets a download-grace timeout; (2) the browser-daemon HTTP client uses its own finite-but-generous ceiling so the grace is not capped at the DI factory client's 100 s default; (3) `FinishCompile` and `TranscodeWithRecovery` now carry `browserProvisioned` through compile and across recovery attempts. Verified end-to-end on the real MCP path (chromium absent → one-attempt success with `browserProvisioned:{installed:true,…}`), full gate green.
- **Browser auto-provision (branch-2) no longer preempted by the missing-browser HTTP downgrade** — `occam_transcode` downgraded a browser request to HTTP whenever chromium was not yet installed (`playwright_browser_missing_downgrading_to_http`), which skipped the on-launch auto-provision entirely: the browser was never attempted, so branch-2 never fired and a fresh host silently got HTTP instead of the browser it asked for. The downgrade is now skipped when occam will provision the browser itself (bundled chromium + `OCCAM_BROWSER_AUTOINSTALL` on), so the browser path runs, the launch fails, and the provision triggers as designed. Found by testing the real MCP-tool path — prior verification went through the gate/daemon path, which bypasses this tool-level downgrade (a genuine my-green ≠ prod-green miss).
- **Browser auto-provision on the shared-daemon path** — the first browser call that had to download Chromium returned `timeout` on the pool/daemon path (the default), while the one-shot path worked. Two causes: the provision ran *inside* the per-extract timeout race (a one-time ~175 MB download can't fit the page budget), and the Playwright installer's progress was written to the daemon's stderr — an OS pipe the host never drains, so the buffer filled and the installer deadlocked. Now the session (and any provision) is pre-warmed *before* the per-extract timer, and the installer's output goes to a log file (`<tmp>/occam-browser-provision.log`) instead of the pipe. `browser_provisioned` also survives onto a failure response now (provision-then-thin/challenge/timeout still reports the install). Verified end-to-end: cold provision renders via the daemon (~84 s, `browser_playwright`), steady-state stays fast (~13 s, no provision), full gate green (`L6_BROWSER_POOL_OK`).
- **Login-wall heuristic substring tighten** — `LoginWallDetector` no longer flags technical docs that mention `password` inside identifiers (`ssl_password_file`) or `login` inside hostnames (`login.example.com`). Uses word-boundary matching via `TextNeedle`; bare `login` removed from the password+login combo (phrases like `sign in` / `log in to continue` still stop real walls). Gate: existing `L1bProbeUnitTests` login-wall cases.
- **Digest `if_none_match` accepts receipt `contentHash` codec** — `DigestService` now uses `ContentHashToken.Matches` (same as transcode AF-6): bare hex or `sha256:`-prefixed token both work.
- **Simple install — unified AOT binary discovery (`OccamMcp.Core`)** — `dotnet publish` emits `OccamMcp.Core` but tarballs and npm wrapper looked only for `FFOccamMcp.Core`, so clone/tarball installs fell back to slow `dotnet run` or failed to start. Shared resolver (`packages/occam-mcp/lib/resolve-host-binary.mjs`) accepts both names at repo root and publish paths; `occam-doctor` copies the binary to `$OCCAM_HOME/OccamMcp.Core` after publish; release tarballs stage `OccamMcp.Core`; `hermes-smoke.mjs` expects 14 tools. Docs: [getting-started.md](docs/getting-started.md) Hermes / production path.
- **Install URLs — public GitHub identity** — user docs and `@ff-occam/mcp` default release/download URLs to `https://github.com/ContextForgeAI/occam` (raw scripts + releases). Override with `OCCAM_RELEASE_BASE_URL` / related env vars when needed.
- **Agent-friendly install gate** — `packages/occam-mcp/bin/occam-mcp.js` refuses git clone without `OccamMcp.Core`; `launch-mcp-host.mjs` no longer silent `dotnet run` fallback. Root [INSTALL.md](INSTALL.md) + README Hermes section. Shared `scripts/lib/host-install-gate.mjs`.
- **Install hardening** — `occam-doctor` fails if csproj is not `net10.0`; in-repo `occam-mcp.js` always exits on git clone (even with a binary). `scripts/lib/assert-net10-csproj.mjs`.
- **Doctor binary gate** — `occam-doctor --skip-build` fails when `OccamMcp.Core` is missing (no false `doctor: OK` on bare clone). `scripts/lib/assert-host-binary.mjs`. [INSTALL.md](INSTALL.md) clarifies: Hermes without .NET 10 → `get-ff-occam.sh` only; no root `npm bootstrap`.
- **get-ff-occam welcome** — tool count banner updated; [INSTALL.md](INSTALL.md) documents release upload for maintainers.
- **Release CI on GitHub** — tag builds publish release assets via GitHub Releases (`softprops/action-gh-release` / `occam-release.yml`). Self-hosted CI compatibility notes were retired from the public docs surface.
- **SLSA / attest workflows** — GitHub-only signing and provenance jobs remain optional and secret-gated; they are not required for ordinary PR validation.
### Changed

- **Receipt/hash hygiene (audit follow-up)** — removed duplicate `ReceiptsEnabled()` helpers in transcode/claim-check/dataset/watch paths; all call `ReceiptsPolicy.Enabled()`. Watch content hashes use `ContentHashToken.BareHex`; digest differential uses `ContentHashToken.Matches`.
- **Honest host version in banner/receipts** — `AssemblyInformationalVersion` set to `0.9.0` in `FFOccamMcp.Core.csproj` (was unset → misleading `v0.1.0-l0` in stderr banner).

## [0.9.0] — 2026-07-04

Milestone release: working `npx @ff-occam/mcp` install path, Receipt v1 verifiable layer (14 core tools + opt-in extras), and the honesty/SSRF/trust fixes accumulated since `v0.8.12`. Tag, package versions, and release assets must all be `0.9.0`.

### Added

- **Working `npx @ff-occam/mcp` one-liner — the zero-config install path now actually runs** — the npm wrapper (`packages/occam-mcp`) was non-functional and, worse, its entry point was invisible to git: the blanket `.gitignore` `bin/` rule (meant for .NET build output) also matched `packages/occam-mcp/bin/`, so `bin/occam-mcp.js` — the file `package.json`'s `bin` points at — was never tracked, and a fresh clone had no wrapper at all. That is now un-ignored explicitly. The wrapper itself is rewritten from broken to correct: it used ESM `import` in a `.js` with no `"type":"module"` (parse error), then `require()` and an undeclared `fs` inside (ReferenceError); the package is now ESM and the file uses proper `node:` imports. The GitHub-release download path is fixed too — the sha256 manifest was fetched under the wrong name (`…tar.gz-manifest.json`, a guaranteed 404) instead of the `ff-occam-<ver>-<rid>-manifest.json` the release build actually produces, and the download used `https.get` with no redirect-follow (GitHub release assets 302 to a CDN) → switched to `fetch(redirect:"follow")`. Version is single-sourced from `package.json` (was hardcoded twice and drifting), dead `optionalDependencies`/`prepare` and unused deps (`node-fetch`/`which`/sdk) are dropped so `npx` pulls only `tar`, and the package README is corrected (License MIT → AGPL-3.0, tool table 8 → the real 14, honest local/receipt/typed-failure pitch). The local route (`OCCAM_HOME` → a built host) is verified end-to-end: a full MCP `initialize` + `tools/list` round-trip through the rewritten wrapper. Paired with **release-asset publishing** — `release-build.yml` gained a tag-gated `publish-release` job that attaches the per-RID tarballs + sha256 manifests to the GitHub Release for the tag (via `softprops/action-gh-release`), and the build now derives the version from the tag (`v1.2.3` → `1.2.3`) so the asset names match exactly what the wrapper requests. This is what turns a code-correct download path into one a stranger's `npx` can actually resolve.
- **Public offline verifier — make a receipt checkable without the host (consumer surface)** — the whole Receipt v1 apparatus was only as useful as how easily a **third party** could verify it, and until now verification lived only inside the `occam_verify` MCP tool (i.e. you had to run the extracting host). Two new self-contained CLI verbs close that loop — **no transport, no worker spawn, no network**: `FFOccamMcp.Core keys export` prints the host's public key (PEM) so a consumer can pin it (the `occam keys export` referenced in the code comments now actually exists), and `FFOccamMcp.Core verify` offline-verifies a **receipt** (signature + optional `--markdown` contentHash + time anchor), a **citation** (`--mode citation`: a block + Merkle proof against the signed root, no page), a **dataset manifest** (`--mode manifest`: reconstruct the row leaves → ordered root → detached signature), or a **watch-history chain** (`--mode history`). Input is a file path or `-` for stdin; output is a one-line verdict JSON on stdout; exit code is `0` verified / `1` not verified (tamper, wrong key, drift) / `2` usage — so it drops straight into a shell pipeline or CI. It reuses the exact Core primitives the MCP tool uses (`ReceiptVerifier`, `MerkleTree`, `DatasetManifestBuilder.Verify`, `WatchHistoryChain.Verify`), so the CLI and the tool can never diverge. Paired with a new **normative byte-level spec** — [docs/receipt_verification.md](docs/receipt_verification.md) — that documents the canonical serialization, ECDSA-P256/SHA-256 signature encoding, the Merkle leaf/roots, the dataset row-leaf join, and the RFC3161 anchor imprint, so the check can be **re-implemented in any language** without our code. This is the piece that makes "verifiable web layer" true for the reader, not just the writer. New `Cli/OccamCliVerbs.cs` dispatched at the top of `Program.cs` (the MCP CLI parser is untouched). Gate: genuine-receipt verified, wrong-key + tampered-markdown rejected, keys-export, usage errors, and non-verb fall-through in `L_CLI_VERIFY_OK`.
- **Failure atlas — a per-host closure map for smarter crawl planning (SI-10, opt-in)** — a new opt-in tool `occam_failure_atlas` (host sets `OCCAM_ATLAS_MCP=1`) reads the running host's accumulated outcome map: over the process lifetime a decorating telemetry sink aggregates, per host, how many transcodes succeeded vs failed and the failure-code breakdown, and the tool returns a **closure map** — `{ ok, hostCount, walledCount, hosts:[{ host, attempts, successes, failures, closureRate, walled, dominantFailure, byCode:[{code,count}], lastFailureAt }] }`, worst-first. The headline `walled` flag marks hosts that **never succeeded and whose dominant failure is an honest closure** (`captcha_or_challenge`, `requires_login`, `http_401/403/404/410`) — a provable dead end where retrying is wasted — distinct from hosts that only hit **transient** codes (`timeout`, `network_error`, `dns_error`, `http_429`, `http_5xx`, excluded from `closureRate` because they're worth a retry). An agent planning a multi-URL crawl can call it to skip the walls. Off by default: no aggregation cost and tool count stays 14; in-memory only, bounded to 500 hosts, never persisted (the map is scoped to the current run — the distributed/persistent version is out of scope until there are external nodes). Pure closure classifier + host summarizer in `Telemetry/FailureAtlasStore.cs`; the sink (`FailureAtlasSink`) decorates the existing logger sink so logging is unchanged. Gate: closure-vs-transient classification, walled verdict (no-success + honest-closure dominant), closureRate excluding transient, and the store aggregation round-trip in `L_ATLAS_OK`. New env: `OCCAM_ATLAS_MCP`.
- **Verifiable dataset export — a signed, auditable corpus with a Merkle manifest (SI-17)** — a new core tool `occam_dataset_export` (13 → 14 tools) turns a list of URLs (1–20) into a **signed, auditable dataset**: each URL is transcoded with `json_blocks` into a row `{url, finalUrl, ok, contentHash, blockMerkleRoot, failureCode?, rowLeaf, receipt}` carrying its own signed extraction receipt, and the whole set is bound by a **single manifest signature over the Merkle root of the per-row leaves** — so "these N extractions, exactly, were produced together" is provable and tamper-evident. It is verifiable **two ways**: each row independently via `occam_verify` (its receipt), and the *set* via the manifest — a consumer recomputes each `rowLeaf` from the row fields (`url\nfinalUrl\nok\ncontentHash\nblockMerkleRoot\nfailureCode`, SHA-256), rebuilds the ordered Merkle root, and checks the detached `manifest.sig` over the canonical manifest bytes with the host's public key; adding, dropping, editing, or reordering any row changes the root and breaks the signature (row order is significant). Failed URLs are included as honest rows (`ok:false` + `failureCode` + a signed **negative** receipt), so the dataset records misses too — you can prove a corpus was built without silently dropping the URLs that were walled. `manifest.sig`/`keyId` are omitted under `OCCAM_RECEIPTS=off` (the Merkle root still binds the set). A provenance primitive for building auditable RAG corpora / evaluation sets. Pure manifest core in `Dataset/DatasetManifest.cs` (reuses `MerkleTree` + the Receipt v1 detached-signature machinery, with a hand-written canonicalizer mirroring `ReceiptCanonicalizer`). Gate: row-leaf determinism + content-binding, root reconstruction, tamper/reorder/drop detection, and a detached-signature round-trip + wrong-key rejection in `L_DATASET_OK`.
- **Playbook lint — static genome schema validation before a live verify (SI-13)** — a new core tool `occam_playbook_lint` (12 → 13 tools) statically checks a playbook / genome JSON against the 1.x schema with **no network**, returning a graded issue list `{ grade, agentReady, errors, warnings, infos, issues:[{severity, field, code, message}] }`. `grade` ∈ `ready` (clean) / `usable` (warnings only) / `broken` (has errors); `agentReady` is true iff there are no errors (i.e. `resolve`/`save` would accept it). **Errors** flag what breaks resolve/save — missing or non-1.x `schema_version`, missing `id`, missing/empty `hosts`, missing/empty `extract.contentSelectors`; **warnings** flag quality degraders — a non-bare `hosts` entry (scheme/path/caps), an invalid `routing.preferred_backend`, a blank content selector, a `knowledge_schema` class with no `genome.page_classes` route (it would never fire), a missing `meta.title`; an **info** nudges for missing `agent_notes`. The point: an agent drafting a recipe in the heal→save loop — or an operator vetting a community genome — can fix it before spending a live `occam_playbook_save` verify. Pure/deterministic validator in `Playbooks/PlaybookLinter.cs` (reuses the same schema contract as `PlaybookDocument.TryParse`, but reports *why* instead of just null). Gate: clean-genome-ready, invalid-JSON/missing-required → broken, warnings-only → usable, the "default" knowledge class needing no route, and non-1.x rejection in `L_LINT_OK`.
- **Attest — ground an LLM report against its own citations (SI-11, capstone over claim-check)** — a new core tool `occam_attest` (11 → 12 tools) closes the report-honesty loop: give it a JSON array of `{claim, sourceUrl}` rows (an LLM's report with its citations) and it runs the SI-16 claim-check path per row, returning a per-claim **grounded / unsupported** verdict plus a report-level tally `{claimsTotal, grounded, unsupported}`. A **grounded** row carries the matched block + its **Merkle citation proof** + the **signed extraction receipt** (independently verifiable via `occam_verify citation` — leaf + proof reconstruct the signed root, no re-fetch); an **unsupported** row carries a `reason` (`no_matching_block` when the page extracted but nothing cleared the relevance floor, `invalid_arguments`, or the extraction `failure.code` with a signed negative receipt still attached). Crucially "grounded" means **the cited source provably contains matching text — never that the claim is true**; stance stays the caller's judgment, same trust model as claim-check. An agent can gate its own answer on "every citation checks out" before shipping a report. Reuses `IClaimCheckService` + the citation machinery wholesale (batch orchestration + grounded/unsupported classification only); pure classifier in `Attest/AttestClassifier.cs` (1–50 claims/call). Gate: grounded-iff-floor-cleared, report partitioning, and a grounded-evidence citation-proof round-trip in `L_ATTEST_OK`.
- **Receipt v1 — verifiable extraction receipts (flagship; SI-01 / SI-03 / SI-06)** — every `occam_transcode` success now carries a signed `receipt.signed` envelope: an **ECDsa P-256** signature over `{contentHash, blockMerkleRoot, url, finalUrl, timestamp, backend, playbook, toolchain}`, so a downstream consumer can prove "this markdown really was on that page at that time" without trusting a middleman (local key under `OCCAM_KEYS_ROOT`; on by default, `OCCAM_RECEIPTS=off` disables → telemetry-only receipt). Provable **unavailability** too: an honest `ok:false` on captcha / login / paywall / 4xx carries a signed **negative receipt** — a claim only an honest tool can make (SI-03), never emitted for transient errors (timeout/network/workers). New **`occam_verify`** tool (the 10th) closes the loop: **offline** (signature + optional contentHash match) or **live** (re-fetch `finalUrl`, compare content hash / block-Merkle root → `verified` / `drifted` / `refetch_failed`); pass `public_key` to verify a third party's receipt, or omit for this host's local key. Turns the STRATEGY-REVIEW W1 finding ("`receipt` was telemetry, not a receipt — the flagship existed only as a field name") into shipped capability. Spec: `docs-internal/SPEC-receipt-v1.md`. Gate: `L_RECEIPT_OK` (sign/verify roundtrip, tamper + wrong-key detection, canonical-form golden, Merkle determinism, negative receipts, `occam_verify` offline). New env: `OCCAM_RECEIPTS`, `OCCAM_KEYS_ROOT`.
- **Signed playbooks — self-authenticating recipes (SI-08 local foundation)** — `occam_playbook_save` now signs the saved playbook: a `provenance` block `{keyId, alg, contentHash, signature, signedAt, verify:{score, passesGate, noiseLeakage}}` is injected (ECDsa P-256, the Receipt v1 local key), so a recipe carries its author key AND cryptographic proof it passed the verify gate. The signature covers a canonical hash of the recipe body (its own provenance block excluded, so re-signing is stable); `PlaybookSignature.Verify` checks it, and the save response returns `signedKeyId`. This is the local, no-hosting foundation for a future signed registry with reputation — the distributed parts (registry hosting, "confirmed by N nodes" aggregation, heal-exchange auto-PRs, key-trust/PKI) are deliberately deferred until there are external nodes. Gate: sign→verify, content-hash ignores provenance, tampered-body + wrong-key rejected.
- **Chunk-level RAG expiry — verifiable per-fragment staleness (SI-12, layer ④)** — `occam_verify` `live` now reports **which specific chunks** went stale, not just how many. A RAG store that indexed a URL into chunks (each carrying a block leaf-hash from its receipt) can re-check them against the live page and invalidate **individual fragments** instead of re-embedding the whole document — "this fragment's source changed, do not trust." New optional `chunks` param (the leaf-hashes your store holds for the URL) scopes the check to exactly your fragments; omit it to check the receipt's own block leaves. The `live.chunkStaleness` block reports `{ total, present, stale, staleChunks[] }`. Additive (the existing `blocksTotal`/`blocksStillPresent`/`drift` count is unchanged); pure verdict core in `Receipts/ChunkStaleness.cs`. Gate: staleness pinpointing + all-present + empty-source in `L_RECEIPT_OK`. A small extension of the SI-02 live-verify path — no new tool.
- **Claim-check — ground a claim in provable source (SI-16, layer ③)** — a new core tool `occam_claim_check` (10 → 11 tools) inverts the usual flow: instead of "give me the page", ask "does this page support/refute THIS claim?" Given `{claim, url}` it extracts with `json_blocks`, ranks blocks by **BM25** relevance to the claim, and returns the top matches — each with a **Merkle citation proof** + the **signed extraction receipt** — or an honest `found:false` when nothing clears the relevance floor (a match must cover ≥ ~40% of the claim's content terms, so a single common word is not a hit). The **full** block text is returned so a third party can recompute the leaf and prove the block was in the signed extraction via `occam_verify citation` — no re-fetch. Crucially, the tool proves **which** source text is relevant but does **not** classify support-vs-refute — lexical stance is unreliable and would violate the trust model; the calling LLM reads the proven block(s) and judges, citing them. A fact-checking primitive for agent pipelines and a building block of the deep-research layer. Pure ranker in `Claims/ClaimBlockRanker.cs`; on extraction failure returns the typed `{ failure }` (+ signed negative receipt on provable unavailability). Gate: BM25 ordering, the ~40% coverage floor, empty/no-block cases, and a citation-proof round-trip in `L_CLAIM_OK`. Spec: `docs-internal/SPEC-si16-claim-check.md`.
- **Time-anchored receipts — independent proof of "no later than T" (SI-15, opt-in)** — a receipt can now carry an **RFC3161 time anchor**: a timestamp token from an external Time-Stamping Authority (TSA) over the receipt's signature, proving the signed receipt existed **no later than** `genTime` — attested by an independent third party, so the time no longer rests on the extracting node's own clock. Complements SI-14 (that is "who saw", this is "when"). Opt-in and env-gated (`OCCAM_TIME_ANCHOR=1` + `OCCAM_TSA_URL=<tsa>`, e.g. freeTSA.org): `occam_transcode` attaches an unsigned `receipt.timeAnchor` sidecar `{type, tsa, token, genTime}` on success (applied after signing; authentic because the token binds `SHA256(signature)`). The TSA URL is operator-controlled (not user-supplied per request) and SSRF-guarded, and anchoring is **fail-open** — a TSA outage never blocks the extraction. `occam_verify` `offline`/`live` now also verify the anchor and add `timeAnchor:{ present, valid, genTime, tsa, tsaSubject }` — `valid:true` confirms the token binds this receipt's signature and is internally valid. TSA chain-trust is out of scope for v1 (like receipt key-trust); `tsaSubject` is reported for the consumer to judge. Built on the .NET BCL RFC3161 support (`Rfc3161TimestampRequest`/`Token`) — AOT-clean. OpenTimestamps (Bitcoin anchoring) is a logged follow-up; the `type` field leaves room. Gate: RFC3161 token verify/reject against a captured real-TSA vector + `occam_verify` surfacing, in `L_RECEIPT_OK`. Spec: `docs-internal/SPEC-si15-time-anchor.md`. New env: `OCCAM_TIME_ANCHOR`, `OCCAM_TSA_URL`.
- **Consensus / cloaking cross-check — the local extraction notary (SI-14, opt-in)** — a new opt-in tool `occam_crosscheck` (host sets `OCCAM_CONSENSUS_MCP=1`) extracts one URL through several **vantage points** and reports whether the witnesses agree: `verdict` ∈ `consensus` (all block-Merkle roots identical) / `divergent` (roots differ → cloaking / personalization / geo) / `access_divergent` (one witness saw content while another hit a provable wall — the strongest cloaking signal) / `inconclusive` (<2 usable witnesses). Vantages come from the **backend axis** (`http` vs `browser` — bare fetch vs full Chromium, the classic cloaking vector) and, when a `session_profile` is supplied, an **anon-vs-authed axis** per backend. Each vantage carries a **signed receipt**, so the verdict is independently re-derivable from the receipts — no separate consensus signature needed (`occam_verify offline` checks each). `divergence[]` reports the pairwise block overlap (`blocksCommon` of `blocksTotal`, union-based) so an agent sees *how much* diverged. This is a single node acting as a local jury — the distributed multi-node version (remote signers, "N of M nodes") is deliberately deferred until external nodes exist, and reuses this exact comparison logic. Off by default (2+ full extracts per call → not always-on); tool count stays 10. Pure verdict core in `Consensus/ConsensusEvaluator.cs`. Gate: consensus/divergent/access_divergent/inconclusive classification, order-independence, block-overlap magnitude, wall-priority → `L_CONSENSUS_OK`. Spec: `docs-internal/SPEC-si14-consensus.md`. New env: `OCCAM_CONSENSUS_MCP`.
- **Signed watch history — a tamper-evident chronology of a page's changes (SI-05)** — `occam_watch` now keeps a signed, hash-chained log of change events per URL (previously it kept only the *latest* state — the prior snapshot was overwritten). On each real event (the first sighting and every change; an unchanged call adds nothing) it appends a `WatchHistoryEntry` `{seq, observedAt, event, contentHash, blockMerkleRoot, contentDeltaTokens, prevEntryHash, keyId, alg, sig}`: each entry is signed with the Receipt v1 local key AND carries the hash of the previous *signed* entry, so reorder / insert / drop / edit all break a chain link (the link covers the signature too, pinning it). The response always carries `historyLength` + the just-appended `latestEntry`; the full `history[]` comes with the new `include_history=true`. A consumer verifies the whole timeline offline via the new **`occam_verify` `history` mode** (consecutive links + per-entry signatures → `history_verified` / `history_invalid`) — no re-fetch. The chain is a bounded 64-entry window (stays verifiable over the retained span); signing follows `OCCAM_RECEIPTS` (off → still chains, just unsigned). This is layer ① (verifiable) × the watch feature — "prove this page changed at these timestamps, here is the cryptographic chain." Additive fields; back-compatible store (legacy files → empty history). Gate: chain build/verify, tamper (body/sig/reorder/broken-link), windowed + unsigned chains, and the `occam_verify` history mode in `L_RECEIPT_OK`. Spec: `docs-internal/SPEC-si05-watch-history.md`.
- **Resolve-side signature verification — closes the SI-08 sign→verify loop** — `occam_playbook_resolve` now returns a `signature` block classifying the **winning** recipe against this install's local key *before* it is trusted: `verified` (our key, hash + signature check out), `invalid` (claims our key but tampered on disk), `unknown_key` (a real signature from a key we don't hold — foreign author), or `unsigned` (bundled seed / site genome / hand-authored). It is a **trust signal, not a resolve failure** — the recipe is still returned, so an agent decides how much to trust the recipe's own `score`/`passesGate` (authoritative only when `status = verified`). `PlaybookSignature.Inspect` distinguishes tampering from a foreign author without trusting the recipe's own keyId claim; never throws (malformed → `unsigned`). Gate: `verified`/`unsigned`/`invalid`/`unknown_key` classification in `L_RECEIPT_OK`. Additive response field; no new params/env.
- **Receipt v1 — granular verify + citation proofs (SI-02)** — building on the receipt: `occam_verify` `live` mode now reports **how many blocks survived** (`blocksTotal`/`blocksStillPresent`/`drift`), matching the receipt's block leaves against the re-fetched page. Two new modes make a **verifiable citation**: `prove` emits a compact O(log N) Merkle proof for one block, and `citation` lets a third party prove *that specific block was in the signed extraction* from the block + proof + signed root — **without the page or the other blocks**. The block leaves ride as an unsigned `receipt.blockLeaves` sidecar (authentic because they reconstruct the signed `blockMerkleRoot`), keeping the signed envelope compact. Gate: Merkle proof roundtrip + tamper rejection + `prove`→`citation` end-to-end in `L_RECEIPT_OK`.
- **L9 golden set — deterministic extraction-fidelity regression net** — a new gate level serves **frozen HTML fixtures** from a local server (`benchmarks/l0-gate/fixtures/golden/*.html`, `L9GoldenRunner`) so assertions catch **code** regressions, not live-site drift (the probe-nuxt lesson, where a live gate URL became a Cloudflare wall and flaked). 18 cases / 88 assertions pin `ok`/`failure_code` + a char band + must-contain/must-not-contain markers + structured output across: classification (challenge → `captcha_or_challenge`, thin → `thin_extract`, login → `requires_login`); markdown fidelity (data/ragged tables, fenced code, nested lists, blockquotes, headings + links, unicode/emoji round-trip, images); boilerplate stripping (nav/sidebar/footer/ad/related/newsletter); structured opt-ins (`json_tables`, `json_feed`); and real mdn/nginx/wikipedia snapshots. A per-case `backend` override (default `http`) lets a golden case pin the **browser** worker path, so the Q-025 `json_blocks` collection-order fix is now regression-covered on **both** backends (not just http). `corpora/l9-golden.jsonl`; runs in the full gate. — Occam previously set no MCP `instructions`, so the calling model only saw 9 tool names + ~20 opt-in transcode params with no guidance on *when* to use them; powerful off-by-default features (`json_tables`/`json_feed`/`json_blocks`, `fit_markdown`+`focus_query`, `if_none_match`/`diff_against`/`occam_watch`, `prefer_llms_txt`, `session_profile`, managed escalation) were effectively invisible. `AddOccamMcpServer` now sets `ServerInstructions` (`Transport/OccamServerInstructions.cs`): a tight capability + decision guide led by the trust rule (`ok:false` = unknown, never guess), the default (just pass `url`), tool-selection hints, the opt-in gems by need, and the honest escalation story. Gate: `RunServerInstructions` in `L0InfraUnitTests` (wired + carries trust rule + key tools/opt-ins). Addresses the agent-discoverability gap (QUALITY-LEDGER Q-008).
- **Probe proactively recommends the right per-page opt-in (T3.1)** — beyond the static server instructions, `occam_probe` `agentHints.warnings` now nudge the model to the matching transcode opt-in based on what the probe actually saw: an RSS/Atom content-type → `json_feed`; a large page (HTML ≥ 750 KB) → `max_tokens` or `fit_markdown`+`focus_query`; a likely paywall → expect `thin_extract`, try `session_profile`. Fires only on a concrete probe signal (no guessing) and stays quiet on ordinary pages. Gate: `RunProbeAgentHints` in `L0InfraUnitTests`. Completes QUALITY-LEDGER Q-008 part 2.
- **`occam_watch` reports `contentDeltaTokens` freshness magnitude (T1.2)** — when a watched page changes, the response now includes the approximate token count of newly-added blocks (`0` when unchanged, omitted on first sighting), computed from the block delta. Lets an agent gauge *how much* changed (a 3-token tweak vs. a rewrite) without parsing the full diff — and it's reported even with `include_diff:false`. Additive field; pure/deterministic. Gate: `RunWatchContract` in `L0InfraUnitTests`.
- **`occam_probe` exposes `recommendation.extractability` (0–1) (T1.2)** — the probe response now carries the same cheap extractability score `occam_search` uses for rerank (`SearchExtractabilityScorer`): dead/blocked/paywall/anti-bot/JS-stub pages score low, clean docs/articles high. An agent can read it from a single cheap probe to decide whether a transcode is worth paying for, without a second tool. Additive field under `recommendation` (always present on probe success); pure/deterministic, no extra network. Gate: serialization contract in `L0InfraUnitTests`.
- **Nightly full-gate workflow (T2.2)** — new `.github/workflows/nightly-gate.yml` runs the complete **L0–L8** gate (unit contracts + live smoke + browser pool L6 + resource-safety L7) on a daily schedule and on manual dispatch, installing a real Playwright Chromium. Per-PR CI only runs the HTTP-only `--fast` subset for speed, so AOT/trimming and browser-level regressions could previously slip in unnoticed between releases; the nightly catches them. Closes audit P2-7.

### Added

- **Signed receipts on `occam_digest` items (audit follow-up)** — the research path now closes the flagship ① gap: each **ok** digest item carries its own signed Receipt v1 envelope under `items[].receipt.signed` (contentHash + provenance + ECDsa P-256 signature), so a multi-source digest is independently verifiable per source via `occam_verify` — the same guarantee as a single `occam_transcode`. Previously digest items carried only AF-3 telemetry (no `signed`), despite the receipt-backed-research positioning. No per-item time anchor (a digest would otherwise fan out N TSA calls) and no block root (digest doesn't request `json_blocks`); omitted under `OCCAM_RECEIPTS=off`. Introduced a shared `Receipts/ReceiptsPolicy.Enabled()` (the single on/off gate) that the digest path uses. Gate: a digest item with a signed envelope serializes under `receipt.signed` with its keyId + signature, in `L2_DIGEST_OK`.

### Fixed

- **Receipt integrity: blocks reconciled to the pruned markdown (audit finding E)** — when `json_blocks` was combined with a prune knob (`max_tokens` / `fit_markdown` / `content_selectors` / truncation), the receipt was internally inconsistent: `blockMerkleRoot` + `blockLeaves` (and the returned `blocks[]`) came from the worker's **full** block list, while `contentHash` was computed over the **pruned** markdown. A citation proof could then "prove" a block no longer present in the returned content, undermining the SI-02 citation layer. Fixed with a pure `Compile/BlockReconciler` that filters the block list to those whose (whitespace-normalized) text survived into the compiled markdown, applied at the compile boundary — so `blocks[]`, `blockMerkleRoot`, `blockLeaves` and `contentHash` all describe the same returned content (erring toward dropping, so the receipt only claims blocks it can stand behind). Also folded the double block-hashing pass in `BuildReceipt` into one. No effect on the common no-prune path. Gate: reconcile/consistency asserts in `L_RECEIPT_OK`.
- **`if_none_match` accepts the receipt's `contentHash` codec (audit finding C)** — the AF-6 conditional token was compared against a **bare-hex** SHA-256 while a receipt's `contentHash` is `sha256:`-prefixed, so a caller who reused `receipt.signed.contentHash` as `if_none_match` would never match and always re-fetch. Centralized the token in `Compile/ContentHashToken` whose matcher tolerates an optional leading `sha256:`, so both forms work; back-compatible with the bare hex.

### Changed

- **License confirmed & unified to AGPL-3.0** — the repository's licensing was inconsistent: the root `LICENSE` was AGPL-3.0 but the publishable package manifests (`@ff-occam/mcp`, `@ff-occam/agent-sdk`, the VS Code extension, `wasm-extractor`) and the Core `.csproj` declared `MIT`, and internal tooling declared `MIT`/`ISC`. Owner-confirmed **AGPL-3.0** as the project's license (a deliberate "stays free forever" copyleft gift — any fork or network/SaaS deployment must offer its source), and every tracked manifest now declares the SPDX id `AGPL-3.0-or-later` to match the `LICENSE` file. The AGPL network-use clause aligns with the future network tier (registry/cache/heal exchange) staying open. No code change.
- **Quality & freshness signals — decision matrix, not a collapse (SI-04 hygiene)** — the review's "unify the 3 quality scales and freshness mechanisms into 1" was evaluated (with an inventory verified on the code and an external design review) and **deliberately re-scoped as non-breaking**: the signals are not duplicated logic, they are distinct *kinds* of signal — `extractability` (0–1, a **prediction** before fetch), `confidence` (0–1, a **measurement** after extract), and playbook `verify.score` (0–100, a pass/fail **gate**) — and the five change mechanisms (`if_none_match`, `cache_ttl_s`, `diff_against`, `occam_watch`, `chunkStaleness`) answer five different questions at five scopes. Collapsing them would delete expressiveness and rename shipped MCP fields (a contract break). The actual pain — *which do I use?* — is fixed at zero API risk: a single decision matrix now maintained in [choosing-a-tool.md](docs/choosing-a-tool.md) and [concepts.md](docs/concepts.md) (referenced from `MCP_API_SPEC.md`), plus a compact SIGNALS block in the runtime server instructions (`OccamServerInstructions.cs`) so agents get the guidance in-band. Investigated for duplicated scoring logic across `ExtractQualityEvaluator` / `SearchExtractabilityScorer` / `QualityGate` — the three take different inputs and use different formulas, so **no meaningful duplication to consolidate**. One breaking normalization (`verify.score` 0–100 → 0–1, the lone off-convention range) is recorded in `docs/ROADMAP.md` as a **v1.0-window candidate**, not done now. The semantic distinctions are correct and are not collapsed. No code-behavior or signature change.
- **`occam_transcode` parameter surface grouped & clarified (T2.4)** — the tool has ~20 parameters, but **only `url` is required** and every other is an off-by-default opt-in. Made that obvious instead of changing the contract (which would break agents): the tool description now leads with "just pass `url`", and each `[Description]` is prefixed with a category tag — `[core]` / `[tokens]` / `[structured]` / `[fetch]` / `[watch]` / `[advanced]`. `docs/tool_reference.md` regroups the flat parameter table into those sections. No signature change. Also fixed a docs drift: `playbook_policy` default is `auto` (was documented as `off`) in `tool_reference.md` + `MCP_API_SPEC.md`. Addresses audit P2-9 (comprehensibility) without breaking the additive contract.

### Security

- **Credential leak — session `Cookie`/`Authorization` no longer forwarded across a meta-refresh to a different origin (Q-032)** — a security self-review found that the http worker re-sent the full `session_profile` request headers on the HTML `<meta refresh>` follow **without checking the target origin**. A `session_profile` carries `Cookie`/`Authorization` for the requested host by design, so a page that meta-refreshes to a **third-party host** (a legit SSO/CDN hop, or one injected into the page) would receive host A's session credentials — the classic "credentials survive a cross-origin redirect" leak. Fix: a shared `stripCrossOriginSensitiveHeaders()` helper (`workers/shared/lib/request-headers.mjs`) drops `Cookie`/`Authorization`/`Proxy-Authorization` when the redirect target is not same-origin (scheme+host+port; fail-safe strip on an unparseable target), mirroring browser/undici behavior; the meta-refresh loop now runs session headers through it before re-sending. Regression-locked with a new gate selftest (`request-headers.selftest.mjs`, 10 cases: same-origin keeps, cross-host/scheme-downgrade/different-port/lowercase strip, fail-safe). No new env/params.
- **SSRF on the browser backend — redirects/JS navigations to private hosts now blocked at the network layer (Q-031)** — a follow-up to Q-030 on the Playwright path. Chromium resolves DNS and follows redirects itself, so the browser worker relied on a pre-navigation host check (initial URL only) plus a `framenavigated` listener that (a) used the **literal-only** `isUrlAllowed` (no DNS resolve) and (b) was **removed right after `page.goto`**. Two gaps: a page could redirect **during** load to an internal host via a **DNS-resolving name** (literal check misses it), or navigate **after** load (`window.location`, delayed meta-refresh) entirely unmonitored — either way Chromium fetched the internal page and its content was extracted and returned. Fix: the existing `context.route("**/*")` interceptor now SSRF-validates the host of **every navigation request** (initial, 3xx, meta-refresh, JS location, iframe) with a real DNS resolve (`resolveAndValidateHost`) and aborts private targets before the request leaves — covering all navigations for the whole session, not just the initial `goto`. A navigation aborted this way is reported as `private_url_blocked` (mapped from `net::ERR_BLOCKED_BY_CLIENT`). Reuses the guard the http worker uses; `OCCAM_ALLOW_PRIVATE_URLS=1` still opts out. (Note: like Q-030, an end-to-end browser SSRF-block case isn't gate-covered because the private-URL guard is process-global — the underlying `resolveAndValidateHost` is selftested and the browser levels L6/L7 confirm no extraction regression.)
- **SSRF via meta-refresh redirect — the app-level redirect now enforces the same guard (Q-030)** — a security self-review (external-review prep) found that the http worker's initial fetch resolves + validates the target host across both address families and pins the connection (SSRF/DNS-rebinding guard), and undici's HTTP-3xx redirects are covered by the host-aware pinned lookup (Q-004) — but the **HTML `<meta http-equiv=refresh>` follow loop** re-fetched the target with a plain `egressFetch` and **no host validation**. The post-fetch `validateFinalUrl` is a **literal-only** check (localhost/.local/.internal + literal private IPs) and does **not** resolve DNS, so a malicious page could `<meta refresh>` to an internal host via a **DNS-resolving name** (e.g. a domain whose A record points at `169.254.169.254` / an internal service) and have the worker fetch it and **return the internal response body as markdown** (data-exfil SSRF); a literal-IP target was fetched blind (side effects) before being discarded. Fix: a shared `pinnedDispatcherForUrl()` guard (`workers/shared/lib/private-ip.mjs`) now resolves + SSRF-validates + pins **every** redirect target before the fetch — used by the meta-refresh loop so a `<meta refresh>` to a private host is rejected with `private_url_blocked` before any request leaves. Regression-locked: `private-ip.selftest.mjs` (+4 cases — literal, IPv4-mapped, and opt-out). No new env/params; `OCCAM_ALLOW_PRIVATE_URLS=1` still opts out.
- **SSRF taxonomy gap — `0.0.0.0/8` and IPv4-mapped IPv6 now blocked (Q-029)** — a self-review of the SSRF guard (prep for an external security review) found two addresses that slipped past both the C# `PrivacyClassifier.IsPrivateIp` and the Node `private-ip.mjs`: (1) **`0.0.0.0/8`** ("this host on this network") — the IPv4 branch checked `10/8`, `172.16/12`, `192.168/16`, `127/8`, `169.254/16` but not `0.x.x.x`, which routes to **localhost on Linux** (a real SSRF vector for a host that resolves to `0.0.0.0`); (2) **IPv4-mapped IPv6** (`::ffff:127.0.0.1`, `::ffff:169.254.169.254`, and the hex `::ffff:7f00:1` form) — the v6 branch only matched `::1`/`fe80::/10`/`fc00::/7`/`fec0::/10`, so a mapped private/metadata address (e.g. a literal `http://[::ffff:169.254.169.254]/`) was treated as public. Both are now folded to the embedded IPv4 (C# via `IsIPv4MappedToIPv6`/`MapToIPv4`; Node via dotted + hex `::ffff:` parsing) and `0.0.0.0/8` is flagged. Regression-locked on both sides: `private-ip.selftest.mjs` (+7 cases) and `RunOutboundGuardContract` in `L0InfraUnitTests` (0.0.0.0 + two mapped hosts). No new env/params; `OCCAM_ALLOW_PRIVATE_URLS=1` still opts out.
- **Docs/honesty — tool count `8 → 9` (T0.3)** — README, `MCP_API_SPEC.md`, `docs/{index,operator_journey,transport,cursor_mcp,batch_server,cli_reference,ROADMAP}.md`, the VS Code extension README, and operator scripts/banners said **eight** MCP tools while the host registers **nine** (the 8 originals + `occam_search`) plus the opt-in `occam_batch_*` / `occam_watch`. Corrected the count everywhere, added `occam_search` to the README/cursor/VS Code tool lists, and fixed the four self-referential `tool_reference.md` "When-to-use guides" links in `MCP_API_SPEC.md` to point at real per-tool anchors. Closes audit P1-5 / P2-8.
- **SSRF / DNS-rebinding hardening (T0.1a, worker boundary)** — the http and browser workers now resolve each target host across **both IPv4 and IPv6** before fetching and reject any private answer (`10/8`, `172.16/12`, `192.168/16`, `127/8`, `169.254/16`, `::1`, `fe80::/10`, `fc00::/7`), fixing the prior **IPv4-only** gap that let an IPv6-only loopback/ULA target through. On the http backend the connection is now **pinned to the validated IP** via a custom undici dispatcher, closing the **TOCTOU DNS-rebinding** window (a re-resolve can no longer swap in an internal address after the check). Shared guard `resolveAndValidateHost` + `createPinnedDispatcher` in `workers/shared/lib/private-ip.mjs` (+ `private-ip.selftest.mjs`, wired into `occam doctor`). The worker codes `private_ip_blocked` / `dns_resolution_failed` are canonicalized by the host to the documented `private_url_blocked` / `dns_error`. No new env or params; `OCCAM_ALLOW_PRIVATE_URLS=1` still skips the check for local testing.
- **SSRF guard on C# in-process fetches (T0.1b)** — the host fetches a few user-influenced URLs directly (not via the Node workers): `occam_probe` (the probed URL + each redirect hop), `robots.txt`, and the well-known genome. These bypassed the worker SSRF guard and the C# preflight only blocked **literal** private IPs. Added `Routing/OutboundHttpGuard` as a `SocketsHttpHandler.ConnectCallback` on those named HttpClients: it resolves the target host, rejects any private address across **IPv4 and IPv6**, and **pins the connection to the validated IP** (so redirects and DNS-rebinding can't reach an internal target). Also fixed `PrivacyClassifier.IsPrivateIp` to flag IPv6 unique-local `fc00::/7` (previously missed — caught by the new gate contract). Honors `OCCAM_ALLOW_PRIVATE_URLS`. Gate: `RunOutboundGuardContract` in `L0InfraUnitTests`. Completes the SSRF hardening started in T0.1a (worker boundary).
- **Dependency hygiene — remove vulnerable `tar` from the worker tree (T0.2)** — `unpdf` (and `jsdom`) declare an **optional, unused** `canvas` peer whose `@mapbox/node-pre-gyp → tar` chain carries `node-tar` high-severity advisories (`npm audit`: 2 high). `canvas` was never actually installed at runtime (PDF text extraction works without it), but it sat in the lockfile graph. Neutralized via an `overrides` stub (`canvas → @favware/skip-dependency`), dropping the entire `canvas`/`node-pre-gyp`/`tar` subtree from `workers/package-lock.json` (−804 lines, no additions). `npm audit --omit=dev` now reports **0 vulnerabilities**, and a `npm audit --omit=dev --audit-level=high` gate is added to CI (`ci.yml`) alongside the existing NuGet vuln scan. PDF + DOM extraction verified unaffected.

### Fixed

- **Session `Authorization` no longer leaks cross-origin on the browser path (Q-035)** — a strategic review found the http-path fix (Q-032) had not reached the browser path: `pickExtraHttpHeaders` fed `session_profile` headers into Playwright `extraHTTPHeaders`, which are static per-context and attach to every request (including cross-origin redirects/subresources) with no origin filter, so an `Authorization` set for host A leaked to any third-party host. `authorization`/`proxy-authorization` are now blocked from `extraHTTPHeaders` (like `Cookie`, which is re-injected domain-scoped via `addCookies`). Locked with `pickExtraHttpHeaders` assertions in the header selftest.
- **SSRF guard now covers the playbook-heal DOM-skeleton path (Q-036)** — `dom-skeleton-capture.mjs` opened a bare browser context with no per-request host validation, so the heal/skeleton fallback could reach a private host via a DNS-resolving name (the gap Q-031 closed for `browser-session`). It now installs the same navigation-request `resolveAndValidateHost` route guard.
- **Playbook-heal now applies session cookies to cookie-walled targets (Q-037)** — `dom-skeleton-capture.mjs` called `applySessionCookies(context, requestHeaders, url)` with args 2 and 3 swapped, so `Cookie` was read off the URL string (always undefined) and a cookie-walled heal target was skeletoned anonymously (likely a login/consent shell), healing the playbook against the wrong DOM. Fixed the argument order (and dropped a redundant second headers read). Locked with an `applySessionCookies` arg-order selftest.
- **Playbook-heal DOM-skeleton capture is now deterministic on content-heavy pages (Q-038)** — the heal skeleton was node-capped at 400; on pages that render nav/sidebar before main content (e.g. MDN) the DFS could exhaust the cap before reaching the main-content element, yielding `mainCandidates=0` non-deterministically (a pre-existing flake in the L3 K1 heal-capture gate — a bare baseline flaked 3/6, unrelated to Q-035..Q-037). The heal skeleton cap is raised to 600 (its clamp ceiling) and capture now waits for the primary content landmark before snapshotting; verified 8/8 on the MDN pilot.
- **Token budget — the `sandwich` truncation strategy no longer overshoots `max_tokens` (Q-034)** — a correctness self-review found `TruncateSandwichSafe` reserved `marker.Length` (the 5-char `"\n\n…\n\n"`) against the character budget but then inserted a much longer (~55-char) HTML SNIP comment (`<!-- SNIP: middle (reason: budget_exceeded) -->`), so a `max_tokens` request served via the sandwich path (focus_query set but no section matched) came back ~50 chars / ~12 tokens **over budget**. Now the actually-inserted marker length is reserved, keeping the result within `max_tokens`. Gate: new `l1a sandwich within budget` assertion in `L1aTokenEconomyTests`.
- **Content-rich pages no longer false-flagged as anti-bot challenges (Q-026)** — `occam_transcode` returned `captcha_or_challenge` (`ok:false`) for a legitimate, fully-extracted article that merely *discusses* anti-bot topics — e.g. a page about HTTP status codes (trips on "429 Too Many Requests" / "rate limit") or web security (trips on "Cloudflare" / "captcha" / "just a moment"). The probe path (which has visible-text context) stayed correct; only `ChallengePagePostProcessor`, running the keyword detector on the full markdown without size context, mistook the subject-matter vocabulary for a challenge shell. Fix: a genuine interstitial carries almost no content, so keyword challenge detection is now skipped above 2000 chars of extracted markdown — thin challenge shells are still flagged, a multi-thousand-char article about anti-bot systems is not. Locked with a new L9 golden case.
- **`json_blocks` / `json_tables` no longer collected on a Readability-mutated DOM (Q-025)** — a follow-up to Q-024: even with the mapping fixed, `json_blocks` returned **0 blocks for some real articles** while working on others. Cause was order-of-operations — `Readability.parse()` mutates the document destructively, and both workers collected blocks/tables *after* it, so for page shapes Readability prunes hard (a multi-section article beside nav/aside/footer) the live content element was already gutted. Moved `collectBlocks`/`collectTables` to run on the pristine post-strip DOM **before** Readability in both the http and browser workers (a multi-section article now yields 6 blocks instead of 0; tables unaffected). Locked with an L9 `json_blocks` golden case.
- **`json_tables` / `json_feed` now returned on successful extraction (Q-024)** — the opt-in structured outputs were **silently dropped for every `ok:true` page**: the worker-response → `ExtractRunResult` mapping copied `blocks` but omitted `tables` and `feed` on the success branch in all three code paths (`HttpExtractRunner`, `BrowserExtractRunner`, and the shared `WorkerExtractPayloadMapper` used by the http daemon), while the failure branch mapped all three — so `occam_transcode json_tables=true` / `json_feed=true` came back empty exactly when extraction worked. The Node worker was always correct (verified: real HTTP fetch → `tables:[…]`); the fix copies `tables`/`feed` in the success mappings too. Surfaced while building the L9 structured-output golden and locked by two new golden cases (data-table → json_tables, RSS fixture → json_feed); full gate green. (`json_blocks` on the http backend remains browser-only, a separate documented limitation.)
- **Table fidelity — nested tables no longer explode the parent table's columns (Q-028)** — a self-review follow-up to Q-023: the GFM table rule selected rows/cells with `querySelectorAll("tr")` / `querySelectorAll("th, td")`, which use the **descendant** combinator — so a `<table>` nested inside a cell merged its rows into the parent matrix and inflated the column count (and could leak a second header-separator row). Scoped both selections to the current table via `closest("table") === node`, so the outer table keeps its real column count and the nested table's text survives flattened into its parent cell (GFM can't nest tables). Locked with a new L9 `nested-table` golden that asserts the 3-column outer table stays 3 columns and the inner `Metric/Target` rows don't leak as their own table.
- **Table fidelity — markdown no longer drops the first column of tables (Q-023)** — stock turndown ships no table rule, so a `<table>` collapsed into a flat run of cell text and the **first cell of every row was swallowed** (a status-code reference lost its `200/301/404/500` column; the header row survived). Added a self-contained `addTableRule()` (`workers/shared/lib/turndown-table-rule.mjs`, **no new dependency** — pads ragged rows, escapes `\|`, emits a header separator) wired into both the http and browser turndown instances, so `<table>` now renders as a pipe-delimited GFM table with every column intact. The structured `json_tables` path is unchanged. Surfaced and locked by the new L9 data-table golden; full gate green and wikipedia/mdn/nginx (all table-bearing) still pass, so the change is non-regressive.
- **Failure-code hygiene — raw TLS/socket error codes no longer leak (Q-011)** — the 1k benchmark surfaced raw Node/undici error strings reaching the agent as `failure.code`: `err_ssl_tlsv1_alert_internal_error`, `err_ssl_tlsv1_unrecognized_name`, `err_ssl_ssl/tls_alert_handshake_failure`, `und_err_socket` (edgecdn.ru, herokuapp.com, mybluehost.me, telephony.goog, …). Same class as the T0.7/Q-005 JS-error-name leak, but `FailureCodeStrings.Normalize` only matched fixed strings while these carry per-host variable suffixes. Added family folding before the switch: `err_ssl*` / `*tls*alert*` → `tls_error`, `und_err*timeout*` → `timeout`, `und_err_socket` / `*socket*` → `network_error`. No raw `ERR_SSL_*` / `UND_ERR_*` code reaches the agent now. Gate: `failure normalize/resolve err_ssl…/und_err_socket` in `L1FailureTaxonomyUnitTests`.
- **`response_too_large` no longer hard-fails common heavy pages — HTTP cap default 1 MiB → 8 MiB (Q-012)** — the 1k benchmark flagged 49 `response_too_large` cases, **44 of which Firecrawl extracted fine** (cnn.com, theguardian.com, bloomberg.com, washingtonpost.com, cloudflare.com, vimeo.com, steampowered.com, gmail.com, …). Root cause: the default `OCCAM_MAX_RESPONSE_BYTES` was **1 MiB**, and in the default `fail` mode the streamed read throws `ResponseTooLargeError` *before extraction is attempted* — but modern pages routinely carry >1 MiB of raw HTML while their extracted content is tiny, so Occam rejected pages it could have compressed. Raised the default to **8 MiB** (`workers/shared/lib/response-body-cap.mjs`), well under the unchanged 16 MiB hard ceiling; genuinely oversize pages still fail honestly (no partial-cited-as-full). **Paired with a worker-heap bump:** the 8 MiB cap OOM-crashed the http worker (V8 abort → `workers_unavailable`) at its old 128 MiB Node heap, so `DefaultHttpMaxOldSpaceMb` is raised **128 → 512 MiB** (`NodeLaunchArguments`, matching the browser worker) — a ceiling, not an allocation, so small pages are unaffected. Live-verified on the 1k offenders: cnn (30k ch), bloomberg (58k), aol (63k), cnbc, spiegel, nypost, … all recover to `ok`; pages still over 8 MiB fail honestly as `response_too_large` (not a worker crash). Override unchanged via env. Gate: `response-body-cap.selftest.mjs` wired in (`response body cap selftest`, asserts 8 MiB default + 2 MiB body reads fully) and `node launch http heap default` now 512. Docs synced (`environment.md`, `backend_policies.md`, `batch_server.md`, `troubleshooting.md`).
- **Trust-model fix — visible-content floor closes low-token `ok:true` shells (T0.5)** — the 1k benchmark surfaced a second leak the T0.4 headings-only guard missed: `pixabay.com` returned `ok:true` with ~36 tokens (a consent/JS shell dressed with a heading + a few nav links, so it cleared every structural check). Added a structure-independent **trust floor** to `ExtractQualityEvaluator.LooksLikeThinExtract`: an extract whose *visible content* (link URLs/markup stripped, anchor text kept) is below ~160 chars (~40 tokens) is flagged `thin_extract` → `ok:false`, no matter how many headings/links/list items dress it up. Catches consent banners, login/JS shells, and nav stubs that the structural heuristics let through. Verified not to over-flag real short pages (400–499 char band test). Gate: `extract quality flags link-dressed tiny shell (trust floor)` + `extract quality accepts short-but-real prose` in `L0InfraUnitTests`.
- **Failure-code hygiene — raw JS error names no longer leak as failure codes (T0.7)** — the 1k benchmark showed `failure.code: "typeerror"` on heavy sites (samsung.com, bloomberg.com, ozon.ru, godaddy.com, …): the browser worker's catch-all returned `error.name` directly, so a Playwright/page `TypeError` surfaced to the agent as the bogus code `typeerror` (not in the taxonomy). Fixed at source in `browser-session.mjs` (unexpected exceptions → `timeout` for `TimeoutError`, else `extraction_failed`; raw name+message preserved in `message`), plus defense-in-depth in `FailureCodeStrings.Normalize` (JS error names `typeerror`/`referenceerror`/… → `extraction_failed`) so no future worker leak reaches the agent. Gate: `failure normalize/resolve typeerror -> extraction_failed` in `L1FailureTaxonomyUnitTests`.
- **SSRF pinned dispatcher broke cross-host redirects (TLS SAN mismatch) (T0.6)** — the 1k benchmark flagged `tls_error` on real sites where plain `fetch` and Firecrawl succeed (`oracle.com`, `yandex.ru`, `dzen.ru`, `unity3d.com`, `webex.com`, …: 23 cases, Firecrawl ok on 22/23). Root cause: the http worker's anti-DNS-rebinding pinned dispatcher (`createPinnedDispatcher` in `workers/shared/lib/private-ip.mjs`) used an undici `connect.lookup` that **ignored the hostname** and always returned the *original* host's validated IPs. When `fetch` follows a redirect to a **different** host (e.g. `oracle.com → www.oracle.com`), the socket connected to the original host's IP while presenting the new host's SNI → `ERR_TLS_CERT_ALTNAME_INVALID` → `tls_error`. It also meant a redirect target was never SSRF-validated. Fix: the lookup is now **host-aware** — the validated pins apply only to the host they were checked for; a different host is **re-resolved and SSRF-validated** on the fly (so the socket reaches the correct host/cert *and* a redirect to a private host is still blocked). New signature `createPinnedDispatcher(hostname, records)` + testable `createPinnedLookup`. Gate: host-aware lookup checks in `private-ip.selftest.mjs`. Closes QUALITY-LEDGER Q-004.
- **Trust-model fix — headings-only challenge/SPA shells no longer leak as `ok:true` (T0.4)** — a benchmark run surfaced that `occam_transcode` on an anti-bot interstitial (repro: `https://nowsecure.nl`, Cloudflare Turnstile) returned `ok:true` with ~16 tokens of skeleton markdown (`# nowsecure.nl / ### by nodriver / ## NOWSECURE`). `occam_probe` correctly classified it (`pageClass:challenge`, `extractability:0.05`, `skip_url`), but at transcode time the keyword `ChallengePageDetector` saw no `captcha`/`cloudflare` text in the *extracted* markdown, and `ThinExtractPostProcessor` exempted it because the shell carried ≥2 heading lines. Net effect: the agent got `ok:true` for a page with no real content — a direct violation of the project trust model (`ok:false` = unknown content). Fixed in `ExtractQualityEvaluator.LooksLikeThinExtract`: a short extract that is almost entirely heading text with no body prose, list items, or links (`NonHeadingProseChars < 40`) is now flagged `thin_extract` → `ok:false`. Keyword challenge detection is unchanged (still emits `captcha_or_challenge` when the markdown carries the signal). Gate: `extract quality flags headings-only challenge shell` in `L0InfraUnitTests`.
- **CI build unblocked — stop gating `CI / build` on artifact upload failures (T2.1)** — **`CI / build`** was red when a self-hosted artifact backend timed out on upload even though compile, vuln scan, and AOT publish succeeded. Removed the AOT-binary upload from the CI build job entirely — nothing downstream consumes it (gate-fast rebuilds), and the **successful AOT publish is the real gate signal**. Also bumped remaining `actions/*-artifact` majors where needed. Release workflows that upload assets remain separate from the PR gate.

### Added

- **`occam_watch` — stateful page-change watch (opt-in)** — the stateful form of Recipe W: the first call records a page's content hash + block hashes in a small JSON store (`OCCAM_WATCH_DB_PATH`, default `~/.occam/watch/watch.json`); later calls return `changed: true/false` plus a block-level `diff` (added blocks + removed hashes) when it changed, and update the baseline. The agent calls on its own cadence — **no daemon in Core**. Reuses the extraction pipeline (forces `json_blocks` for hashing), `BlockDiff`, and a pure testable `WatchEvaluator`. Registered **only when `OCCAM_WATCH_MCP=1`** — default off keeps the tool count at 9, no state store (zero change). New `Watch/{WatchModels,WatchStore,WatchService}.cs` + `Tools/OccamWatchTool.cs`; ad-hoc `--watch=<url>` gate flag. Gate: `WatchEvaluator` + `WatchStore` contracts in `L0InfraUnitTests`; verified live (nginx docs: first-seen then unchanged).
- **More managed providers — Spider + Scrapfly** — two new opt-in managed-extract backends behind the existing `IManagedProvider` abstraction (one class each), joining Firecrawl + Jina. `OCCAM_MANAGED_PROVIDER=spider` (POST `api.spider.cloud/crawl`, Bearer key, `return_format: markdown`) or `scrapfly` (GET `api.scrapfly.io/scrape`, key + `format=markdown&render_js=true`). Both keyed (`OCCAM_MANAGED_API_KEY`), source-gen JSON (AOT-safe), normalized to `ExtractRunResult` (`backend: "managed_<provider>"`), non-fatal. Off by default; per-domain opt-in unchanged. Gate: readiness assertions in `L0InfraUnitTests`. (Browserbase was evaluated and skipped — it is browser-session infrastructure, not a URL→markdown scrape API, so it doesn't fit the abstraction.)
- **PDF text extraction** — `occam_transcode` (and `occam_digest`, which shares the HTTP path) now extracts text from PDFs instead of failing with `unsupported_content_type`. Detected by content-type (`application/pdf`) or a `.pdf` URL not served as HTML; the body is read as binary (separate `OCCAM_MAX_PDF_BYTES` cap, 16 MiB default — PDFs exceed the 1 MiB HTML cap), parsed via `unpdf` (a serverless pdfjs build — text only, no `canvas`/native deps), and returned as markdown with `backend: "pdf"` (per-page `---` separators, title heading from PDF metadata). Honest failures preserved: oversize → `response_too_large`, scanned/image-only/no-text-layer → `extraction_failed` (no OCR, never invented text). `occam_probe` now classifies PDFs with `recommendation.backend: "http"` (was `none`). New `workers/shared/lib/pdf-extract.mjs` (+ selftest, wired into `occam doctor`). Verified live (arxiv).
- **Search rerank by extractability (PR-E)** — `occam_search` gains opt-in `rerank: bool`. When set, each result URL is cheaply probed (bounded parallelism + short timeout) and the list is reordered so clean HTTP-extractable pages rank above paywalls, anti-bot walls, JS stubs and dead links; each result gains `extractability` (0–1) + `recommendedBackend`. Pure deterministic scorer (`SearchExtractabilityScorer`, AOT-safe): dead/error `0`, challenge `0.05`, login/paywall `0.15`, non-HTML `0.3`, JS-stub `0.45`, browser-needed `0.55`, generic `0.7`, docs/article `0.9`. A probe failure keeps the result (mid score), never drops it. Off by default (extra probe latency); builds on `occam_search` + the probe classifier. Gate: scorer contract in `L0InfraUnitTests`.
- **Polite fetch — llms.txt preference + robots/throttle (PR-B)** — two opt-in fetch behaviors, both off by default (zero change to existing calls):
  - **`prefer_llms_txt`** (transcode param) — probes `{origin}/llms.txt` (the sanctioned LLM-friendly markdown some sites publish) via the HTTP backend and serves it (`llmsTxt:true`, `finalUrl` = the llms.txt URL) when present and non-trivial, else falls back to normal extraction. Reuses the full fetch path (egress/private-IP/size-cap); never cached.
  - **robots.txt + per-host throttle** — env-gated polite fetch wired into `TranscodePipeline` (digest/extract inherit it). `OCCAM_RESPECT_ROBOTS=1` honors the `*`-group `Disallow` (→ typed `robots_disallowed` failure) and `Crawl-delay`; `OCCAM_HOST_THROTTLE_MS` sets a per-host minimum request interval (effective interval = `max(throttle, crawl-delay)`). Both unset = no-op, no robots.txt fetch. New `Services/RobotsThrottleService.cs` (+ `RobotsRules` parser, AOT-safe, fail-open) on a timeout-bounded `occam.robots` HttpClient (`OCCAM_ROBOTS_TIMEOUT_MS`). Occam stays user-directed (not a crawler), hence opt-in. Gate: `RobotsRules` parsing contract in `L0InfraUnitTests`.
- **Async batch as MCP tools (PR-D)** — opt-in fire-and-forget batch transcode exposed as three MCP tools (`occam_batch_submit` / `occam_batch_status` / `occam_batch_results`), a thin front to the existing in-process batch engine (same engine as the standalone HTTP batch server). Submit returns a `job_id` immediately (state `queued`); a `BackgroundService` processes the queue; poll status, then page results. Registered **only when `OCCAM_BATCH_MCP=1`** — default off keeps the tool count at 9 with no background processor (zero change). The stdio/remote MCP host already runs an `IHost`, so the hosted processor runs in-process. New `Tools/OccamBatchTools.cs` (3 thin tools + `OccamBatchToolSupport` url-list parser, snake_case wire format via `BatchJsonContext`). Also added `occam_search` to `OccamToolNames` (was missed in the 9th-tool change). Gate: batch contract (url parsing, submit validation, error envelope) in `L0InfraUnitTests`.
- **RSS/Atom feed codec** — `occam_transcode` gains `json_feed: bool`. When set and the URL resolves to a feed (RSS 2.0 / Atom / RSS 1.0 RDF, detected by content-type or body sniff), the worker parses it into `feed: { title, items: [{ title, link, publishedAt, summary }] }` and renders a readable markdown item list, instead of running Readability on XML (which fails). Opt-in and additive: non-feed responses and unset flag behave exactly as before. Shared parser `workers/shared/lib/feed-items.mjs` (HTTP backend; feeds don't need a browser), threaded through Core (`WorkerExtractResponse.Feed` → `ExtractRunResult` → `TranscodeOutcome` → `OccamTranscodeSuccessResponse.feed`, source-gen JSON, omitted when null) and folded into the cache key. Gate: `json_feed` contract asserts in `L0InfraUnitTests` (RSS + Atom verified via jsdom).
- **Tables → JSON codec** — `occam_transcode` gains `json_tables: bool`. When set, the response carries `tables[]` alongside markdown: `[{ caption, headers[], rows[][], source_selector }]`, parsed cell-by-cell (row-major). Layout tables (those containing a nested `<table>`) and single-column header-less tables are skipped as non-data. Shared DOM pass `workers/shared/lib/dom-tables.mjs` reusing the `dom-blocks` selector helper (`buildElementSelector`), wired into both the HTTP (jsdom) and browser (Playwright snapshot) extractors. Threaded through Core (`WorkerExtractResponse.Tables` → `ExtractRunResult` → `TranscodeOutcome` → `OccamTranscodeSuccessResponse.tables`, source-gen JSON, omitted when null) and folded into the cache key. High value for agent analysis of tabular content. Gate: `json_tables` contract asserts in `L0InfraUnitTests`.
- **Page metadata codec** — `occam_transcode` (and the digest/extract paths that share extraction) now surface a `meta: { publishedAt, author, lang, canonical }` object on success, extracted from og/meta/JSON-LD/`<html lang>`/`<link rel=canonical>`. Always-on (no flag), additive, each field omitted when absent. High value for RAG freshness + citations. Shared worker pass `workers/shared/lib/page-metadata.mjs` (both HTTP jsdom + browser snapshot); threaded through Core (`WorkerExtractResponse.Meta` → `ExtractRunResult` → `TranscodeOutcome` → `OccamTranscodeSuccessResponse.meta`, source-gen JSON, omitted when null). Gate: metadata contract asserts in `L0InfraUnitTests`; verified live on MDN.
- **diff-codec (`diff_against`)** — `occam_transcode` gains block-level change detection for fire-and-forget watch. Supply prior block hashes (`diff_against`: JSON array or comma-sep, from a previous call's `diff.blockHashes`) → response adds `diff: { addedBlocks, removedHashes, blockHashes }`: the new/changed blocks (full content), gone hashes, and the current hash set to store. Reuses the `json_blocks` DOM pass; hash = SHA256(type+normalized-text), 16 hex. Stateless (agent holds the tiny hash list); never cached. Whole-doc `if_none_match` stays the cheap boolean gate, this is the delta. New `Tools/BlockDiff.cs`; **Recipe W** (watch = external scheduler + `if_none_match` + `diff_against`, no daemon in Core). Gate: diff contract asserts in `L0InfraUnitTests`.
- **`occam_search` — open-web search (9th tool)** — the agent's discovery step (query → result URLs) before probe/transcode/digest, closing the gap that previously forced a separate search tool/MCP. Occam does **not** crawl or index — it delegates to a configured backend and normalizes results to `{ title, url, snippet }`. Off by default; enabled via `OCCAM_SEARCH_PROVIDER` = `searxng` (self-hosted, keyless, `OCCAM_SEARCH_URL`) / `brave` / `tavily` (`OCCAM_SEARCH_API_KEY`). Provider-abstracted (`ISearchProvider` + `Searxng`/`Brave`/`Tavily` providers, source-gen JSON, AOT-safe) on a timeout-bounded `occam.search` HttpClient (`OCCAM_SEARCH_TIMEOUT_MS`). Unconfigured → typed `search_unconfigured` failure with operator guidance. Gate: search contract asserts in `L0InfraUnitTests`; ad-hoc `--search=` (verified live against mock SearXNG/Brave/Tavily). Registered in `OccamMcpServerRegistration`.
- **Managed backend adapters (Package 3)** — opt-in last-resort escalation to a third-party scraping API (Firecrawl, Jina) for anti-bot / JS-heavy pages the local `http`/`browser` backends cannot crack. Off by default; enabled via `OCCAM_MANAGED_PROVIDER` (+ `OCCAM_MANAGED_API_KEY` for keyed providers), per-domain opt-in via `OCCAM_MANAGED_DOMAINS`, creds only in env. The `http_then_browser` policy escalates to it after both local backends fail on an opted-in host; the winner surfaces as `backend: "managed_<provider>"`. Provider-abstracted (`IManagedProvider` + `FirecrawlProvider`/`JinaProvider`), normalized to `ExtractRunResult`, non-fatal (falls back to the best local outcome). New `Backends/ManagedExtractBackend.cs` + `Backends/Managed/*`; timeout-bounded `occam.managed` HttpClient (`OCCAM_MANAGED_TIMEOUT_MS`). Gate: readiness/per-domain-eligibility asserts in `L0InfraUnitTests`; ad-hoc `--managed-fetch=<url>` (verified live against mock Firecrawl + Jina).
- **Translation codec** — `occam_transcode` gains `translate_to` (language code, e.g. `ru`, `pt-BR`). When set and `OCCAM_TRANSLATE_URL` (a LibreTranslate endpoint) is configured, the response adds `translatedMarkdown` + `translatedTo` alongside the original `markdown`. Additive and non-fatal: unconfigured/failed translation returns the original markdown with a warning (`translate_endpoint_unconfigured`, `translate_http_*`, `translate_timeout`, …), never an `ok:false`. Every successful translation also emits a `translation_machine_generated` warning — machine translation is lossy for humor/idioms/wordplay/tone, so the original `markdown` is always preserved for nuance verification. New `ITranslationService`/`TranslationService` (Core, source-gen JSON, AOT-safe) on a timeout-bounded `occam.translate` HttpClient (`OCCAM_TRANSLATE_TIMEOUT_MS`, opt. `OCCAM_TRANSLATE_API_KEY`). Folded into the cache key; ad-hoc `--translate-to=` flag. Gate: translation contract asserts in `L0InfraUnitTests`.
- **Parity "traps" tier (`LT_TRAPS_OK`)** — opt-in gate tier (`--traps`) over `corpora/traps.jsonl` (T1–T9, ported from the v1 Crawl4AI parity suite). `benchmarks/l0-gate/TrapsRunner.cs` asserts robustness rubrics beyond `ok`: forbidden artifacts (`__codelineno`), balanced code fences, heading counts, `min_confidence`, and `must_fail_probe` (probe must reject a 404 shell). Hits third-party hosts, so it is **not** in the fast/default gate or CI; each case has a 150 s wall-clock guard and `allow_env_blocked` tolerance. T10–T12 dropped (need `cache_policy`/adaptive-digest params absent from L0).

### Fixed

- **agent-mvp-gate over-performance brittleness** — `run-agent-popular-hosts.mjs` scored a row as a mismatch whenever the live outcome differed from the corpus's hard-coded `expected`, even when the tool did *better* (e.g. `amazon-product` now `WORKS` where the corpus expected `FAIL_HONEST`; `stackoverflow` now `NEEDS_SESSION`). Live hosts drift, so this failed the gate for success. Now a row passes when its outcome ranks **≥** expected (`WORKS` > `NEEDS_SESSION` > `FAIL_HONEST`; `FAIL_BUG`/`ERROR` always fail) — only genuine regressions count. Also corrected the stale `rfc2616` expectation to `FAIL_HONEST` (~1.2MB exceeds the 1 MiB response cap → `response_too_large` is intended).
- **Empty-link markdown noise** — extended `genericMarkdownPrune` to strip *all* empty-text anchor links `[]( … )` (whitespace-only text too), not just `#fragment` ones: HN/Reddit upvote arrows (`[](…/vote?…)`), icon-only links, etc. A negative lookbehind preserves empty-alt images `![](url)`. (HN front page: 15.3 KB → 13.3 KB, vote-arrow links gone.)
- **agent-mvp-gate publish path (`MSB1009`)** — `occam-doctor.sh` published a stale project path `src/OccamMcp.Core/OccamMcp.Core.csproj` (the directory is `FFOccamMcp.Core`; only the assembly is `OccamMcp.Core`), so `dotnet publish` failed once the browser-launch smoke started passing. Corrected to `src/FFOccamMcp.Core/FFOccamMcp.Core.csproj` + matching publish bin path. Also fixed the same stale dir + an old doc name in `occam-command-registry.mjs`.
- **agent-mvp-gate browser launch (`libnspr4.so` missing)** — the real CI failure: `occam-doctor.sh` ran `playwright install chromium` (browser binary only, no OS libs), and a cached browser skipped even that, so Chromium failed to launch (`error while loading shared libraries: libnspr4.so`). Doctor now also runs `playwright install-deps chromium` on Linux-as-root (CI containers), idempotent and non-fatal; dev machines (Win/macOS, non-root Linux) are untouched.
- **agent-mvp-gate transient flakiness (defense in depth)** — `run-agent-popular-hosts.mjs` now retries a host once on a transient outcome (thrown error or `timeout`/`network_error`/`dns_error`/`tls_error`/`http_429`/`http_503`/`http_5xx`/`http_error`) before scoring. Live CI egress to 15 popular hosts no longer fails the gate on a single transient blip; genuine failures (4xx content, extraction bugs) still fail fast.
- **MkDocs `__codelineno` leak** — empty-text in-page anchor links (`[](#__codelineno-0-1)` prefixing every code line on MkDocs sites like docs.crawl4ai.com) now stripped in the shared `genericMarkdownPrune`. Surfaced by trap T1.
- **Browser daemon request casing (`missing_url`)** — `BrowserDaemonClient` serialized PascalCase (`Url`, `LeanAssets`, …) but the daemon reads snake_case (`url`, `lean_assets`, `force_recycle`, `headers_file`, `storage_state_file`, `max_nodes`); every browser-pool request field was silently dropped, failing with `missing_url`. Added `JsonKnownNamingPolicy.SnakeCaseLower` to `BrowserDaemonJsonContext`. Surfaced by trap T2.

### Added (modular extensions)

- **JSON-blocks codec (RAG citations)** — `occam_transcode` gains `json_blocks: bool`. When set, the response carries `blocks[]` alongside markdown: ordered DOM content blocks `{ type, text, links[], source_selector }` (`heading`/`paragraph`/`list_item`/`code`/`quote`/`table`/`figure`). `source_selector` is a real CSS path — document-absolute and round-trip-verified against the page DOM when the content root is connected (e.g. `#content > ul:nth-of-type(1) > li:nth-of-type(3)`), anchored to the extracted fragment otherwise. Implemented as a shared DOM pass (`workers/shared/lib/dom-blocks.mjs`) used by both the HTTP (jsdom) and browser (Playwright snapshot) extractors, anchored on the live content element rather than Readability's detached wrapper. Threaded through Core (`WorkerExtractResponse.Blocks` → `ExtractRunResult` → `TranscodeOutcome` → `OccamTranscodeSuccessResponse.blocks`, source-gen JSON, omitted when null) and folded into the cache key. Gate: `json_blocks` contract asserts in `L0InfraUnitTests`; ad-hoc `--json-blocks` flag for manual inspection.
- **Open Source Readiness Repointing** — repository remotes and release identity aligned for public GitHub publication (`ContextForgeAI/occam`).
- **Repository cleanup for OSS readiness** — removed 12 garbage files (session prompts, temp scripts, graphify artifacts), moved 9 internal docs to `docs-internal/` (gitignored), deleted 4 proxy doc files, renamed 18 docs to snake_case. Net: -4,400 lines.
- **GitHub CI** — `.github/workflows/ci.yml`: build (dotnet + AOT publish), L0 fast gate, doc name check. Issue templates (bug, feature) + PR template.
- **Docker support** — multi-stage `Dockerfile` (build AOT → Node 20 workers → runtime with Playwright Chromium) + `docker-compose.yml`.
- **License fix** — `Directory.Build.props` corrected from MIT to AGPL-3.0-or-later (matching LICENSE file).

### Changed

- **AOT & Trimming Compliance** — Resolved all trimmer warnings. Deleted legacy `NodeWorkerProcessRunner` and updated DI collection extensions. Updated `DigestService.cs` and `BatchServerHost.cs` JSON serialization to use source-generated `OccamDigestJsonContext` and `BatchJsonContext`. Replaced obsolete `X509Certificate2` constructor usage with `X509CertificateLoader` in `RemoteMcpTransport.cs`.
- **Client Model Optimization** — Switched default `playbook_policy` to `"auto"` in `OccamTranscodeTool.cs` to transparently resolve playbooks, and compacted failure responses by ignoring null fields on serialization.
- **Unified Sitemap & robots.txt Discovery** — Merged sitemap/robots discovery in `DigestService` with the unified `SitemapDiscovery` utility and replaced per-call `new HttpClient` creation with the shared `HttpProbeFetcher`.
- `README.md` — added CI/license/.NET/Node.js badges, updated repository layout (Dockerfile, .github, packages), fixed product surface to v0.9.
- `.gitignore` — added `.idea/`, `cursor-proxy/`, `graphify-out/`, `*_PROMPT.md`, `test_write.md`.

### Removed

- 12 root-level garbage files: `ARCHITECTURAL_AUDIT_FIX_PROMPT.md`, `P2_IMPLEMENTATION_PROMPT.md`, `REMOTE_MCP_AUTH_PROMPT.md`, `SLSA_L3_BUILD_PROMPT.md`, `test_write.md`, 3 rename scripts.
- 4 doc proxy files: `docs/04-occam-transcode.md`, `docs/13-occam-probe.md`, `docs/15-occam-digest.md`, `docs/16-occam-map.md`.
- `docs/release/` (6 files) — moved to `docs-internal/release/`.
- `docs/AGENT-FIRST-MVP.md`, `docs/OCCAM-KNOWLEDGE-COMPILER-ANALYSIS.md` — moved to `docs-internal/archive/2026-Q2/`.
- `src/graphify-out/`, `workers/graphify-out/` — build artifacts removed.

- **L8 Agent-First Gate** — `benchmarks/l0-gate/L8AgentFirstRunner.cs`: unit tests for AF-1..AF-6 contracts. Tests confidence range/serialization (AF-1), SNIP markers in FitMarkdown + TokenBudget (AF-2), receipt fields on all response types (AF-3), recovery array serialization (AF-4), discoveredLinks + sourceUrl on digest (AF-5), unchanged + if_none_match (AF-6). Gate token `L8_AGENT_FIRST_OK` printed on pass. `L0GateAssert.Finish` extended. `Program.cs` registers L8 in unit test block.
- **Sprint 2: Remote MCP Auth** — `--remote` mode with TLS + JWT Bearer authentication. New `RemoteMcpTransport` (Kestrel HTTPS + JWT middleware + MCP SDK pipeline). CLI flags: `--tls-cert`, `--tls-password`, `--jwt-issuer`, `--jwt-audience`, `--jwt-jwks-uri`. Env fallback: `OCCAM_TLS_CERT_PATH`, `OCCAM_TLS_CERT_PASSWORD`, `OCCAM_JWT_ISSUER`, `OCCAM_JWT_AUDIENCE`, `OCCAM_JWT_JWKS_URI`. JWT validation via `Microsoft.AspNetCore.Authentication.JwtBearer` (AOT-compatible). Token via WS query string `?token=<jwt>`. stdio transport unchanged.
- **Documentation overhaul** — `tool_reference.md` rebuilt from code (all 8 tools, AF-1..AF-6 features, full parameter tables). `--remote` added to `cli_reference.md`, `installation.md`, `host_integration.md`. New `testing_methodology.md` — self-use testing paradigm, visual QA workflow, comparison testing. CLAUDE.md hardened with 7 non-negotiable doc rules. Legacy files identified for archive.

### Added (Agent-First AF-1..AF-6 — originally staged as a separate 0.9.0 draft, 2026-06-18)

- **AF-1: Confidence scoring** — `confidence: 0.0–1.0` on transcode/digest/knowledge success responses. Formula: `0.30*lengthScore + 0.20*structureScore + 0.20*linkScore + 0.15*(1-truncationPenalty) + 0.15*backendScore`. Sigmoid length around 2048–8192 chars; structure from headings + lists; link density; truncation penalty; browser=1.0, http=0.85 backend confidence.
- **AF-2: Semantic transcript** — `<!-- SNIP: section "X" (N paragraphs, M tokens, reason: ...) -->` markers in fit_markdown and token-budget truncation output. Reasons: `bm25_below_threshold`, `boilerplate`, `low_fact_density`, `high_link_density`, `too_short`, `budget_exceeded`, `head_safe`, `sandwich`. LLM sees document structure even through truncation.
- **AF-3: Knowledge receipt** — `receipt: { tokensUsed, tokenEstimator, truncationStrategy, confidence, elapsedMs }` on transcode/digest success responses (`tokenEstimator` was added later as explicit heuristic provenance); knowledge extract keeps its schema-specific telemetry receipt. `OccamTranscodeReceiptInfo`, `OccamExtractKnowledgeReceiptInfo`.
- **AF-4: Auto-recovery mode** — `auto_recover: true` on `occam_transcode`. Tries http first; on thin extract falls back to browser. Returns `recovery: [{ backend, ok, latency_ms }]` metadata. One call, two backends, one response.
- **AF-5: Intent-aware digest** — `source_url` + `max_links` on `occam_digest`. Auto-discovers links via sitemap.xml/robots.txt (same-domain) with HTML link extraction fallback. Filters nonsense (assets, webpack) via `MapLinkFilter.IsNonsense`. Returns `discoveredLinks[]` and `sourceUrl` in response.
- **AF-6: Differential response** — `if_none_match: <sha256>` on transcode/digest. Computes SHA256 of final markdown; on match returns `unchanged: true` with empty body. Saves context window on repeated calls. Not a file cache — always live extract.

### Changed

- `OccamTranscodeOptions` — added `AutoRecover`, `IfNoneMatch` fields.
- `OccamTranscodeOptionsParser.TryBuild` — new `auto_recover` and `if_none_match` parameters.
- `TranscodeOutcome` — added `Confidence` field (set by `ExtractQualityEvaluator.ComputeConfidence` in `FinishCompile`).
- `DigestItemResult` — added `Confidence`, `TruncationStrategy`, `LatencyMs` fields.
- `DigestAnalysis` — added `SourceUrl`, `DiscoveredLinks`, `Unchanged` fields.
- `KnowledgeExtractResult` — added `Confidence` field.
- `BatchJobService` — updated `TryBuild` call signature.

### Files changed

- `Compile/FitMarkdown.cs` — `DroppedBlock` tracking, SNIP marker injection in `Apply()`
- `Compile/TokenBudget.cs` — SNIP markers in `TruncateSandwichSafe`, `TruncateHeadSafe`, `TruncateFocusCentered`
- `PostProcessors/ExtractQualityEvaluator.cs` — `ComputeConfidence(TranscodeOutcome)` method
- `Routing/TranscodeOutcome.cs` — `Confidence` field
- `Routing/TranscodePipeline.cs` — confidence computation in `FinishCompile()`
- `Routing/OccamTranscodeOptions.cs` — `AutoRecover`, `IfNoneMatch` fields
- `Routing/OccamTranscodeOptionsParser.cs` — new parameters
- `Tools/OccamTranscodeModels.cs` — `Confidence`, `Receipt`, `Recovery`, `Unchanged` fields; `OccamTranscodeReceiptInfo`, `OccamTranscodeRecoveryInfo` records
- `Tools/OccamTranscodeTool.cs` — `auto_recover`, `if_none_match` params; `TranscodeWithRecovery()`, `ComputeContentHash()`
- `Tools/OccamDigestModels.cs` — `Confidence`, `Receipt`, `Unchanged`, `DiscoveredLinks`, `SourceUrl` fields
- `Tools/OccamDigestTool.cs` — `source_url`, `max_links`, `if_none_match` params
- `Tools/OccamExtractKnowledgeTool.cs` — `Confidence`, `Receipt` fields; `OccamExtractKnowledgeReceiptInfo` record
- `Services/DigestService.cs` — `source_url`, `max_links`, `if_none_match` params; `DiscoverLinksFromSource()`, `DiscoverViaSitemap()`, `DiscoverViaHtml()`, `ComputeContentHash()`
- `Services/KnowledgeExtractService.cs` — `Confidence` field on result
- `docs/roadmap.md` — AF-1..AF-6 status → ✅
- `MCP_API_SPEC.md` — new params, response fields, Agent-First Enhancements section

## [0.8.13] — 2026-06-18

### Added

- **SLSA Level 3 Build + SBOM + Reproducible AOT** — GitHub Actions workflow `.github/workflows/slsa-build.yml` (slsa-github-generator@v1.9.0), `.github/workflows/sign-release.yml` (cosign keyless signing via GitHub OIDC). Reproducible build config in `Directory.Build.props` + `FFOccamMcp.Core.csproj` (`ContinuousIntegrationBuild`, `Deterministic`, `SourceLink`, embedded PDB). SBOM generation: `scripts/generate-sbom.ps1` / `.sh` (dotnet sbom + syft SPDX). Verification: `scripts/verify-install.ps1` extended with `slsa-verifier`, `cosign`, `syft`. Env vars: `OCCAM_SBOM_PATH`, `OCCAM_PROVENANCE_PATH` in `docs/environment.md`. Docs: `docs/installation.md` "SLSA L3 Verification" section.
- **npm packages (G-1 Growth Engine)** — `@ff-occam/mcp` (zero-config `npx @ff-occam/mcp` MCP server with auto-download of AOT binary from GitHub Releases) and `@ff-occam/agent-sdk` (TypeScript SDK with Recipes A–F: `transcode`, `probeAndTranscode`, `digest`, `mapAndDigest`, `research`, `resolveAndExtract`, `healAndSave`). Packages in `packages/occam-mcp/`, `packages/occam-agent-sdk/`. Auto-detects OS/arch (win-x64, linux-x64, osx-x64, osx-arm64), fallback to local `OCCAM_HOME`.
- **Unified `occam` CLI + soft TUI** — `scripts/occam.mjs` with subcommands (`doctor`, `onboard`, `smoke`, `update`, `refresh`, `help`, `status`, …); bare `occam` opens interactive control menu (TTY); `scripts/occam` / `occam.ps1` PATH wrappers; `update-check.mjs` for release comparison; current entry point: [getting-started.md](docs/getting-started.md). Legacy script paths unchanged.
- **Operator experience Phase 1** — optional `scripts/occam-onboard.mjs` + `occam-onboard.sh` (profiles `default` \| `hermes-headless` \| `mass-scrape` → `~/.occam/onboard.json`); unified `scripts/occam-help.mjs` command registry; honest stderr banner (`8 occam_*`, playbooks seeds + heal/save); `OCCAM_BANNER`/`OCCAM_LOG` aliases with `WT_OCCAM_*` fallback; `FFOccamMcp.Core --help`; current entry point: [getting-started.md](docs/getting-started.md); host manifest [corpora/occam-host-wizard-manifest.json](corpora/occam-host-wizard-manifest.json). Eight MCP tools unchanged.
- **Operator UX schema versioning** — `schema_version: "1.0"` on `~/.occam/onboard.json`, `occam-help --json`, and host wizard manifest; optional `generator` / `written_at` audit fields; `ui_contract_version` on onboard JSON renderer; gentle loader `scripts/lib/operator/onboard-schema.mjs` (warn on major mismatch, do not block MCP). Application semver (`0.8.12`, `OCCAM_REF`) stays separate from data schema. Playbook seeds unchanged.
- **Resource Hardening Phase 5** — POSIX `setpgid` / `kill(-pgid)` in `WorkerProcessGroup` (Windows Job Object unchanged); `CancellationToken` through `NodeWorkerOutputCapture` + `NodeWorkerProcessRunner`; ram-stress `MemoryTrendAnalyzer` ring buffer (cap **1000**); smoke `env_blocked_ok` for `openai-concepts` honest `http_403` (ENV_BLOCKED). Eight MCP tools unchanged.
- **Resource Safety Phase 4 — HTTP body cap** — `workers/shared/lib/response-body-cap.mjs`; env `OCCAM_MAX_RESPONSE_BYTES` (default **1 MiB**); streaming download + `Content-Length` fast-fail; failure `response_too_large`; gate `L7_RESOURCE_SAFETY_OK` + `corpora/l7-resource-safety.jsonl`. Eight MCP tools unchanged.
- **Resource hardening Phase 5** — POSIX `setpgid` orphan cleanup; `CancellationToken` through worker capture; ram-stress ring buffer (1000); smoke `openai-concepts` `env_blocked_ok`; optional HTTP `OCCAM_HTTP_OVERSIZE_MODE=partial` / batch `on_oversize: partial` → honest `response_truncated` + partial markdown.
- **Browser Pool Phase 3 — multi-daemon manager** — `IBrowserPoolManager` + `BrowserPoolManager`; env `OCCAM_BROWSER_POOL_SIZE` (default **1**, max **8**) and `OCCAM_BROWSER_POOL_BASE_PORT`; round-robin slot assignment; `IOccamTelemetrySink` pool acquire/release events (`waitMs`, queue depth); gate `L6_BROWSER_POOL_OK` + `corpora/l6-browser-pool.jsonl`. Eight MCP tools unchanged; `OCCAM_BROWSER_POOL_SIZE=1` preserves pre-Phase-3 behavior.
- **Batch Tier Phase 2 — SQLite job server** — CLI `--batch-server` (default `127.0.0.1:5051`); HTTP `POST /v1/batch/submit`, `GET …/status`, `GET …/results`; SQLite store `~/.occam/jobs/jobs.db`; background processor reuses `TranscodePipeline` + Tier-E proxy rotation; gate `L5_BATCH_OK` + `corpora/l5-batch.jsonl`. Eight MCP tools unchanged.
- **Tier-E Phase 1 — proxy rotation wire-up** — `IProxyRotationService` applied per one-shot worker spawn via `OCCAM_PROXY_LIST` / `OCCAM_PROXY_LIST_FILE`; HTTP and browser daemons skipped when a pool is configured; static `OCCAM_HTTP_PROXY` fallback unchanged.

## [0.8.12] — 2026-06-17

Infra patch — eight MCP tools unchanged; L2 egress gate stability.

### Fixed

- **L2 egress gate flake** — `PlaywrightEnvironment.ApplyTo` ignores stale `PLAYWRIGHT_BROWSERS_PATH` (e.g. sandbox cache) when no Chromium install is present; restores `L2_EGRESS_OK` for `egress-browser-nginx-mock`.

## [0.8.11-agent-mvp] — 2026-06-17

Agent-first product slice: eight MCP tools unchanged — cheat sheet, popular-host corpus, MVP gate on published AOT, Hermes CI entrypoint, docs compaction, perf hardening for long-running MCP.

### Added

- **Agent-First MVP Phase 3 (Perf + Gate)** — [`scripts/run-agent-mvp-gate.mjs`](scripts/run-agent-mvp-gate.mjs) chains `hermes-smoke.mjs` + `run-agent-popular-hosts.mjs` on published AOT (rejects `OCCAM_FORCE_DOTNET_RUN=1`); optional `--latency` via [`scripts/run-agent-mvp-latency.mjs`](scripts/run-agent-mvp-latency.mjs) + [`corpora/agent-mvp-latency.jsonl`](corpora/agent-mvp-latency.jsonl). Baseline notes: [docs/quality-baseline.md](docs/quality-baseline.md). Track **CLOSED**.
- **Hermes CI** — [`scripts/ci-agent-mvp-gate.sh`](scripts/ci-agent-mvp-gate.sh) / [`scripts/ci-agent-mvp-gate.ps1`](scripts/ci-agent-mvp-gate.ps1); workflow [`.github/workflows/agent-mvp-gate.yml`](.github/workflows/agent-mvp-gate.yml).
- **Agent Pack Phase 2** — [`scripts/run-agent-popular-hosts.mjs`](scripts/run-agent-popular-hosts.mjs) over [`corpora/agent-popular-hosts.jsonl`](corpora/agent-popular-hosts.jsonl) (15/15 honest outcomes). Baseline notes: [docs/quality-baseline.md](docs/quality-baseline.md).
- **Agent Pack Phase 1** — [corpora/agent-tool-cheatsheet.json](corpora/agent-tool-cheatsheet.json); recipes F/R; [`scripts/occam-wrapper.sh`](scripts/occam-wrapper.sh) + [`scripts/hermes-smoke.mjs`](scripts/hermes-smoke.mjs); `agentHints.decisions` on failure paths; [`scripts/occam-refresh-host.mjs`](scripts/occam-refresh-host.mjs) (stop MCP + doctor).
- **Session `storageState`** — Playwright `storageState` in session profiles; **`occam-session.mjs`** `import --all` and `export-state`.
- **Benchmark runners** (maintainer) — `run-heavy-browser-escalation-bench.mjs`, `run-occam-vs-crawl4ai-bench.py` + corpora (methods in `docs-internal/22-benchmarks.md`).

### Changed

- **Retail bot interstitial** — `ExtractQualityEvaluator` → `thin_extract` on Amazon-style walls (“continue shopping”, “click the button below”).
- **Browser extract perf** — `waitForArticleContent` budget, bare-HTML fast path, k8s/nginx recipe tuning (no MCP contract change).
- **Perf / arch** — idle daemon TTL (`OCCAM_*_DAEMON_IDLE_TTL_MS`); `occam.stage` profiler line; `OccamEnvironment`; DI HTTP clients for probe/genome; `BrowserConcurrencyGate` determinism; session temp-file cleanup warning.
- **Digest parallelism** — bounded `Parallel.For` per URL; HTTP daemon on `127.0.0.1:39218` (opt-out `OCCAM_HTTP_DAEMON=0`).
- **Quality micro M2** — nuxt install tab-bar noise stripped via `playbook-seed.mjs` (`5e1b1c1`).

### Documentation

- **Agent-first hub** — [docs/index.md](docs/index.md); [docs/roadmap.md](docs/roadmap.md) replaces public `MASTER-PLAN.md`; [MCP_API_SPEC.md](MCP_API_SPEC.md) single contract + response envelope; [docs/configuration.md](docs/configuration.md); thinned tool guides `04/13/15/16`.
- **Maintainer docs** moved to gitignored `docs-internal/` (test plans, benchmarks, master plan archive).
- **Post-MVP track** — [corpora/post-agent-mvp-track.PROMPT.md](corpora/post-agent-mvp-track.PROMPT.md).

### Does not ship

- Ninth MCP tool, file cache, full regression baseline bump as merge gate.

## [0.8.10-media-refs] — 2026-06-17

Eight MCP tools unchanged — structured media handles for RAG ingest before wide validation.

### Added

- **`mediaRefs[]`** on `occam_transcode` success and `occam_digest` items — `url`, `kind`, `alt`, `contextHeading`, `selectorHint`.
- **`workers/shared/lib/media-refs.mjs`** — DOM collect (lazy `data-src`, `srcset`, pdf/download links); cap 32; no binary fetch.
- Gate **`L2_MEDIA_REFS_OK`** — `media-refs.selftest.mjs` + `L2MediaRefsUnitTests`.

### Does not ship

- Ninth tool, `occam_list_images`, VLM in Core, media bytes in response, baseline bump.

## [0.8.9-heal-trust-completion] — 2026-06-16

Eight MCP tools unchanged — PB3 heal hints challenge-aware; audit §7 closed; tier-3 baseline 2026-06-17 retained.

### Added

- **`PlaybookHealPolicy.HasChallengeUrl`** — `finalUrl` / `requestUrl` heuristics (`js_challenge`, `captcha`, `/challenge` path/query).
- **`heal-neg-challenge-url`** — `corpora/l3-heal-learn.jsonl` negative row + `L3HealLearnUnitTests` vectors.
- **`scripts/desk-recipe-e-heal.mjs`** — optional Recipe E stdio smoke (nginx leaf fail → heal hint check).

### Changed

- **`OccamTranscodeTool`** — passes `finalUrl` + request `url` into heal policy on failure responses.
- **`PlaybookHealService`** — same challenge-aware policy on direct heal calls.
- **`HEAL_LEARN_TEST_PLAN.md` §2** — challenge URL terminal rows; **MASTER-PLAN §13.5** PB3 logically complete.
- Product surface **v0.8.9-heal-trust-completion** — closes post-v0.8.7 audit heal captcha **PARTIAL** track item.

### Does not ship

- Ninth MCP tool, in-core LLM drafting, heal rows in `l0-smoke.jsonl`, baseline bump, autonomous heal loop.

## [0.8.8-manifest-integrity] — 2026-06-16

Eight MCP tools unchanged — community manifest sha256 verified at load; Ed25519 deferred to v1.2; tier-3 baseline 2026-06-17 retained.

### Added

- **`CommunityManifest`** (C#) — parse `manifest.json`; verify SHA-256 of UTF-8 playbook bytes at community tier load; skip file on missing/mismatch row.
- **`scripts/lib/verify-community-manifest.mjs`** — doctor/CI verify all manifest rows match on-disk JSON; exit 1 on mismatch or orphan file.
- **`genome-neg-manifest-sha256`** — L4 + L0 unit negative fixture (`benchmarks/l0-gate/fixtures/community-manifest-neg/`).

### Changed

- **`profiles/playbooks/community/manifest.json`** — `sha256` on all four bundled community playbooks.
- **`PlaybookSeedResolver`** — community tier uses manifest integrity path (fail-closed).
- **`occam-doctor.ps1` / `.sh`** — runs `verify-community-manifest.mjs`.
- **`playbook-community-hygiene.selftest.mjs`** — asserts sha256 on every manifest row.
- Product surface **v0.8.8-manifest-integrity** — `GENOME_EXCHANGE_TEST_PLAN.md` §5.3 shipped · `playbook_threat_model.md` §3.3 load verify **shipped**.

### Does not ship

- Ed25519 required at load (v1.2), ninth MCP tool, auto-PR, local/user tier verify, baseline bump.

## [0.8.7-pb4c-exchange] — 2026-06-16

Eight MCP tools unchanged — PB4c maintainer publish CLI + manifest sha256 helper; tier-3 baseline 2026-06-17 retained.

### Added

- **`scripts/occam-playbook-publish.ps1` / `.sh`** — maintainer CLI: `--ack-community-review`, sanitize, export dir, `PULL_REQUEST.md`, `manifest-row.json` with `sha256`.
- **`workers/shared/lib/playbook-publish-sanitize.mjs`** — publish hygiene (forbidden keys, headers, denylist selectors, caps).
- **`scripts/lib/playbook-manifest-sha256.mjs`** — compute manifest row + optional Ed25519 metadata stub (`signature: null`).
- **`PlaybookCommunitySanitizer`** (C#) + **`L4GenomeRunner` `genome-neg-publish-cookie`** — K8 100% reject on secret fixtures.
- **`playbook-publish-hygiene.selftest.mjs`** — cookie/denylist/clean export selftest (also run from `occam-doctor`).

### Changed

- Product surface **v0.8.7-pb4c-exchange** — `AGENTS.md`, `docs/10-l0-scope.md`, `MCP_API_SPEC.md`, `docs/MASTER-PLAN.md` §18.16 PB4c shipped.
- `GENOME_EXCHANGE_TEST_PLAN.md` §5 shipped · `playbook_threat_model.md` §3.3 PB4c controls **shipped**.

### Does not ship

- `occam_playbook_publish` MCP (ninth tool), auto-PR/auto-upload, Ed25519 verify at load (v1.1), genome rows in `l0-smoke.jsonl`.

## [0.8.6-pb4b-extract] — 2026-06-16

Eight MCP tools — PB4b Recipe D structured extract; tier-3 baseline 2026-06-17 retained.

### Added

- **`occam_extract_knowledge`** — eighth MCP tool; Recipe D (`resolve` → extract); `facts[]` + `meta.koId`; failure codes §4.3.
- **`KnowledgeExtractService`** + **`CssExtractWorker`** + `workers/css-extract/css-extract.mjs` (structured field extract).
- **`L4GenomeRunner` PB4b rows** — `genome-pilot-k8s-extract`, `genome-neg-extract-no-schema`; **`L4_GENOME_OK` full** with K7.
- Desk: `scripts/desk-recipe-r.mjs` extended with `occam_extract_knowledge` on k8s concept URL.

### Changed

- Product surface **v0.8.6-pb4b-extract** — eight tools; `AGENTS.md`, `docs/10-l0-scope.md`, `MCP_API_SPEC.md`, `docs/MASTER-PLAN.md` §18.16 PB4b shipped.

### Changed (docs — PB4c unfreeze review)

- **PB4c APPROVED** — CLI-only publish scope; **eight MCP tools** unchanged; no `occam_playbook_publish`.
- `corpora/quality-sprint-pb4c-unfreeze-review.PROMPT.md` → DONE.

### Does not ship (PB4c at PB4b ship)

- Publish CLI, `occam_playbook_publish`, signed manifest enforcement, genome rows in `l0-smoke.jsonl`.

## [0.8.5-pb4a-genome] — 2026-06-16

Seven MCP tools — PB4a genome resolve extensions + `playbook_policy=auto`; tier-3 baseline 2026-06-17 retained.

### Added

- **PB4a genome:** `occam_playbook_resolve` extensions — `schema_version`, `include_lessons`, `fetch_site_genome`; response `genome`, `knowledgeSchema`, `pageClass`, `genomeFetch`, `lessons`; `WellKnownGenomeFetcher` (default off, 8s, 32 KiB); merge precedence playbook > site hints.
- **`occam_transcode` `playbook_policy`:** `off` \| `auto` (eighth parameter) — internal resolve + winning-tier worker overlay via `--playbook-overlay`.
- **Worker tier overlay:** `playbook-seed.mjs` loads **local > user > community > seed** (was local > seed only).
- **Community pilot:** `profiles/playbooks/community/kubernetes.io.json` (genome + `knowledge_schema`).
- **`L4GenomeRunner`** + `L4_GENOME_OK` on PB4a subset of `corpora/l4-genome.jsonl`.

### Changed

- Product surface **v0.8.5-pb4a-genome** — `AGENTS.md`, `docs/10-l0-scope.md`, `MCP_API_SPEC.md`, `docs/MASTER-PLAN.md` §18.16 PB4a shipped.

### Does not ship (PB4b/c)

- `occam_extract_knowledge`, publish CLI, signed manifest enforcement, genome rows in `l0-smoke.jsonl`.

### Added (prior unreleased)

- **P2-12 egress proxy v1:** `workers/shared/lib/egress-proxy.mjs`; `EgressProxyConfig` spawn compiler; env `OCCAM_HTTP_PROXY` / `OCCAM_HTTPS_PROXY` / `OCCAM_NO_PROXY`; http + browser worker parity; gate `L2_EGRESS_OK` + `corpora/l2-egress.jsonl`.

## [0.8.4-pb3-heal-learn] — 2026-06-16

Seven MCP tools — PB3 heal-learn + PB2 community resolver; tier-3 baseline 2026-06-17 retained.

### Added

- **PB3 heal-learn:** `occam_playbook_heal` (DOM skeleton + anchors), `occam_playbook_save` (local tier + `verify=true` + `lessons[]`); `PlaybookHealPolicy`; browser daemon `/skeleton`; `L3_HEAL_LEARN_OK` + `corpora/l3-heal-learn.jsonl`; transcode `agentHints.suggestedNext: occam_playbook_heal` on heal-eligible failures.
- **PB2 community seeds:** `profiles/playbooks/community/` (3 vetted playbooks + manifest); `PlaybookSeedResolver` tier precedence `local` > `WT_PLAYBOOKS_PATH` > `community` > bundled seeds; `provenance` field on `occam_playbook_resolve`; `PlaybookCommunityHygiene` + gate selftest.
- Agent handbook recipe E shipped; the heal loop is now maintained in [recipes.md](docs/recipes.md).

### Changed

- Product surface **v0.8.4-pb3-heal-learn** — `AGENTS.md`, `docs/index.md`, `docs/10-l0-scope.md`, `docs/MASTER-PLAN.md`, `docs/agent_handbook.md`, `MCP_API_SPEC.md` aligned; install examples pin `v0.8.4-pb3-heal-learn`.
- `MASTER-PLAN` §17 Recipe E → shipped; §18.12 closed; sprint prompts `quality-sprint-pb3-heal-engineering`, `quality-sprint-agent-handbook`, `quality-sprint-pb3-unfreeze-review`, `quality-sprint-v0.8.4-pb3-heal-learn-release` → DONE.

### Notes

- Gate smoke `openai-concepts` `http_403` in some sessions is **env/transient** — not a PB3 regression; `L3_HEAL_LEARN_OK` + L1b unit + live probe corpus green.
- heal cases stay in `corpora/l3-heal-learn.jsonl` only — **not** in merge-blocking `l0-smoke.jsonl`.

## [0.8.3-trust-fixes] — 2026-06-15

Digest FAIL_BUG fixes — plain-text HTTP pass-through and RFC/Wikipedia reference honesty; five tools unchanged; tier-3 baseline 2026-06-17 retained.

### Fixed

- **P2-11a:** `text/plain` and UTF-8 `application/octet-stream` pass-through in `workers/http-extract/extract.mjs` — raw GitHub README and similar URLs return real markdown (`plain_text` backend) instead of empty turndown / `extraction_failed`.
- **P2-11b:** `rfc-editor.org` RFC HTML — `IsPublicReferencePage` + domain tier `tier_a_docs` suppress false probe `login` / password heuristics; browser extract guards HTML &gt;900KB with honest `extraction_failed` (no V8 `workers_unavailable` crash); HTTP turndown path unchanged and preferred.
- **P2-11b-tail:** `OccamRouter.TranscodeHttpThenBrowser` — `http_then_browser` on public reference pages (`rfc-editor.org` `/rfc/`, `wikipedia.org` `/wiki/`, tier-A `/docs/` leaves) returns the HTTP outcome without browser escalation — RFC 9110 no longer hits browser `html_too_large` / V8 crash.
- **P2-11c (stretch):** `wikipedia.org` `/wiki/` article paths — same public-reference bypass in probe + `LoginWallDetector`; challenge widget noise suppressed when visible prose ≥500 chars.

### Added

- `workers/shared/lib/plain-text-pass-through.mjs` + gate selftest.
- Digest corpus refresh (`quality-audit-wildcard-digest.jsonl`) + re-spot reports (`2026-06-15-v0.8.3-trust-fixes-respot.md`, `respot-r2.md`).

### Changed

- Product surface **v0.8.3-trust-fixes** — `AGENTS.md`, `docs/index.md`, `docs/10-l0-scope.md`, `docs/MASTER-PLAN.md`, `docs/installation.md`, `docs/backend_policies.md` aligned; install examples pin `v0.8.3-trust-fixes`; `profiles/tiers/domain-tier.v1.json` adds `rfc-editor.org`, `wikipedia.org`.
- `MASTER-PLAN` §18.11 closed; sprint prompts `quality-sprint-v0.8.3-trust-fixes` + `quality-sprint-p2-11b-rfc-tail` + re-spot → DONE/CLOSED.

### Notes

- Gate smoke `openai-concepts` `http_403` in some sessions is **env/transient** — not a P2-11 regression; `L1B_PROBE_OK` unit + live probe corpus green.
- SO Q `11227809` re-spot still **pending** (Cloudflare/transient `http_403`).

## [0.8.2-tier-a] — 2026-06-15

Nginx module leaf login fix + Tier-A probe visible-text strip; five tools unchanged; tier-3 baseline 2026-06-17 retained.

### Fixed

- **P2-9 (`66f7ded`):** False `requires_login` on public nginx module reference pages (`ngx_http_core_module.html`) — `IsTierADocsReferencePage` + `LoginWallDetector` skips password+login heuristic on `tier_a_docs` `/docs/` reference URLs; explicit login phrases and `/login` paths unchanged; rotation `nginx-http-core-leaf` transcode OK; FROZEN `nginx-doc` unchanged.

### Added

- **P2-10 (`d1f0aae`):** Tier-A probe classifier visible-text strip — `HtmlVisibleTextScanner` span-based `script`/`style`/tag strip in `HtmlProbeClassifier` (replaces regex hot path); `SpaShellDetector` shares scanner; L1b visible-text baseline lengths locked (incl. SPA shell `visibleText=3` = title only).

### Changed

- Product surface **v0.8.2-tier-a** — `AGENTS.md`, `docs/index.md`, `docs/10-l0-scope.md`, `docs/MASTER-PLAN.md`, `docs/installation.md` aligned; install examples pin `v0.8.2-tier-a`.
- `MASTER-PLAN` §18.9–§18.10 closed; sprint prompts `quality-sprint-p2-9-nginx-leaf-login` + `quality-sprint-tier-a-phase3-classifier` → DONE.

### Notes

- Gate smoke `openai-concepts` `http_403` in some sessions is **env/transient** — not a regression; `L1B_PROBE_OK` may not print on full gate when smoke fails; L1b unit + live probe corpus green.
- SO Q `11227809` re-spot still **pending** (Cloudflare/transient `http_403`).

## [0.8.1-tier-a] — 2026-06-15

Tier-A C# span wires for map link extract and probe social meta; five tools unchanged; tier-3 baseline 2026-06-17 retained.

### Added

- **P2-8 Tier-A map wire (`7d05dd5`):** `HtmlStreamScanner` + span-based `HtmlLinkExtractor` using `VectorizedHtmlScanner` — zero-allocation anchor/`href` scan; same `MappedLink` API; `L2_MAP_OK` + infra/map unit fixtures.
- **P2-8b Tier-A probe meta wire (`6eaa16f`):** `HtmlHeadScanner` + span rewrite of `HtmlSocialMetaExtractor` — `<meta>` / `<title>` / `html lang` in `<head>`; same `SocialMeta` output; `L1B_PROBE_OK` + L1b/L0 meta unit fixtures.

### Changed

- Product surface **v0.8.1-tier-a** — `AGENTS.md`, `docs/index.md`, `docs/10-l0-scope.md`, `docs/MASTER-PLAN.md`, `docs/installation.md` aligned; install examples pin `v0.8.1-tier-a`.
- `MASTER-PLAN` §18.7–§18.8 closed; sprint prompts `quality-sprint-tier-a-html-scanner` + `quality-sprint-tier-a-probe-meta` → DONE.

### Notes

- Gate smoke `openai-concepts` `http_403` in some sessions is **env/transient** (not a P2-8b regression) — same classification as post-v0.8 audit; not auto-FAIL without FROZEN regression ≥2.
- SO Q `11227809` re-spot still **pending** (Cloudflare/transient `http_403` on 2026-06-15 release spot).

## [0.8.0-popular-hosts] — 2026-06-15

Popular hosts playbook seeds + probe tuning; browser worker heap 512 MB default; five tools unchanged; tier-3 baseline 2026-06-17 retained.

### Added

- **P2-7 popular hosts seeds:** `reddit.com`, `stackoverflow.com`, `x.com` playbook seeds (`occam_playbook_resolve` + worker domStrip/contentSelectors).
- **Probe tuning:** softer challenge detection for public `instagram.com` / `linkedin.com` pages (embedded widget noise → `try_browser` hint instead of hard stop).

### Fixed

- **Browser worker heap:** default `--max-old-space-size=512` for browser/daemon Node (`OCCAM_BROWSER_NODE_MAX_OLD_SPACE_MB`); `workers_unavailable` stderr diagnostics on heavy SPAs (e.g. Reddit).
- **`VectorizedHtmlScanner`:** Native AOT-safe ushort SIMD loads — full `l0-gate` infra tests no longer crash.

### Changed

- Wildcard/social audit corpora — live Reddit permalink (`1tlh5aj`); X status + IG post direct URLs.
- Docs: `backend_policies.md` popular-hosts table; `cursor_mcp.md` resolve + browser policy notes; `10-l0-scope.md`, `MASTER-PLAN.md` §18.6 shipped.
- Product surface **v0.8.0-popular-hosts** — `AGENTS.md`, `docs/index.md`, `docs/10-l0-scope.md`, `docs/MASTER-PLAN.md`, `docs/installation.md` aligned; install examples pin `v0.8.0-popular-hosts`.

## [0.7.9-browser] — 2026-06-15

Browser concurrency hardening (P2-6); shared/isolated execution profiles; five tools unchanged; tier-3 baseline 2026-06-17 retained.

### Added

- **P2-6 browser concurrency:** `BrowserConcurrencyGate` — `OCCAM_BROWSER_MAX_PARALLEL` / `WT_BROWSER_MAX_PARALLEL` cap on concurrent browser extracts.
- **Node heap cap:** `NodeLaunchArguments` — `--max-old-space-size=128` (override `OCCAM_NODE_MAX_OLD_SPACE_MB`).
- **Daemon extract queue:** `BrowserPool` serializes `/extract` on the shared daemon path.
- **Execution profiles:** `BrowserExecutionProfile` — `shared` (default: daemon + queue, low RAM) vs `isolated` (`parallel` / `throughput` aliases: one-shot Chromium per extract).
- **Tier-B timeouts:** queue-aware daemon wait; host browser timeout **60 s** default (`OCCAM_BROWSER_TIMEOUT_MS`); lean route blocks tracker scripts.

### Changed

- `run-l0-ram-stress.ps1` — smoke uses **shared** profile; full `-Parallel 8` uses **isolated** (`RAM_STRESS_GATE PASS`, `20260615-061019`).
- Product surface **v0.7.9-browser** — `AGENTS.md`, `docs/index.md`, `docs/10-l0-scope.md`, `docs/MASTER-PLAN.md`, `docs/installation.md`, `docs/cursor_mcp.md` aligned; install examples pin `v0.7.9-browser`.

## [0.7.8-release] — 2026-06-18

Level B tarball distribution + quality micro M1/M2/M3; five tools unchanged; tier-3 baseline 2026-06-17 retained.

### Added

- **P2-5b release tarball (Level B):** `scripts/build-release.ps1` / `build-release.sh` — per-RID AOT tarball + external sha256 manifest; `install.sh` / `install.ps1` `--from-url` / `OCCAM_RELEASE_URL` with HTTPS + checksum verify before extract; `verify-install --skip-build` on root-level host binary.

### Fixed

- **Quality micro M1:** Docker docs digest — Marlin toolbar JS stripped via `docs.docker.com` seed/recipe + `dockerMarlinToolbarStrip`; hub excerpt fallback when `focus_query` misses (`FitMarkdown.ExtractHubFallback`).
- **Quality micro M2:** `nuxtShikiCssStrip` removes package-manager tab bar on `nuxt.com/docs/getting-started/installation` (rotation Noise 3→4).
- **Quality micro M3:** `MapLinkFilter` drops nginx.org `/CHANGES*` version changelog paths from sitemap `links[]` when `filter_nonsense` is true.

### Changed

- Product surface **v0.7.8-release** — `AGENTS.md`, `docs/index.md`, `docs/10-l0-scope.md`, `docs/MASTER-PLAN.md`, `docs/installation.md` aligned; install examples pin `v0.7.8-release`.

## [0.7.7-install] — 2026-06-17

Production install path; five tools unchanged; tier-3 baseline 2026-06-17; L0 core CLOSED.

### Added

- **P2-5a production install:** `install.sh` / `install.ps1` — pin `--ref`, strict git failures, `install-preflight.mjs` (Node 20+ / .NET 10+), `verify-install.mjs` (host binary + browser smoke), `print-mcp-snippet.mjs`.
- **System browser (dev-only):** `OCCAM_BROWSER_CHANNEL` / `OCCAM_BROWSER_EXECUTABLE_PATH`; doctor verifies launch via `verify-browser-launch.mjs`.

### Changed

- **Install docs:** production path = clone + pinned ref + bundled Chromium; pipe-to-shell documented as dev-only risk; examples pin `v0.7.7-install`.
- **Tier-3 baseline:** bump to **2026-06-17** post-L0 consolidation audit (Merge PASS); L0 core line CLOSED.
- Product surface **v0.7.7-install** — `AGENTS.md`, `docs/index.md`, `docs/10-l0-scope.md`, `docs/MASTER-PLAN.md` aligned.

## [0.7.6-transport] — 2026-06-15

Dual MCP transport; five tools unchanged; gate `L2_TRANSPORT_OK`; docs/18.

### Added

- **`IMcpTransport` strategy** — `StdioMcpTransport` (Cursor parity) + `WebSocketMcpTransport` skeleton (`--mcp-server`, `127.0.0.1:5050`).
- Gate: `L2TransportRunner`, `L2TransportUnitTests`, exit token `L2_TRANSPORT_OK`, corpus `corpora/l2-transport.jsonl`.
- Docs: `docs/transport.md`; stdio default unchanged in `docs/cursor_mcp.md`.

### Changed

- Product surface **v0.7.6-transport** — dual transport (stdio default + optional local WebSocket); `AGENTS.md`, `docs/index.md`, `docs/10-l0-scope.md` aligned.

## [0.7.5-session] — 2026-06-15

`session_profile` on fetch tools; gate `L2_SESSION_OK`.

### Added

- **P2-2 session (`session_profile`):** optional param on `occam_transcode` (7 params), `occam_probe` (4), `occam_digest` (8), `occam_map` (8). Local profile JSON → merged HTTP/browser headers; `private_url_blocked` full policy; failures `session_profile_not_found`, `invalid_session_profile`, `requires_login`.
- Gate: `L2SessionRunner`, `L2SessionUnitTests`, exit token `L2_SESSION_OK`, corpus `corpora/l2-session.jsonl`.
- Core: `SessionProfileHeaders`, `RequestHeadersMerger`, `FetchHeadersScope`, `RequiresLoginPostProcessor`.

### Changed

- Product surface **v0.7.5-session** — transcode param budget 7 (+ `session_profile`); docs/MCP_API_SPEC promoted from draft.

## [0.7.4-map] — 2026-06-15

Five MCP tools; P1-3 map slim shipped; gate `L2_MAP_OK`; docs/16; agent recipe 0f.

### Added

- **L2b map slim (P1-3):** MCP tool `occam_map` — live HTTP link discovery (`homepage` \| `sitemap` \| `robots`), same-domain filter, optional `focus_query` BM25 rank, max **64** links.
- `MapService`, cherry-picked `SitemapDiscovery`, `HtmlLinkExtractor`, `MapLinkFilter`, `MapLinkRanker` (HTTP-only — no `browser_nav`).
- Gate: `L2MapRunner`, `L2MapUnitTests`, exit token `L2_MAP_OK`, corpus `corpora/l2-map.jsonl`.
- Docs: `docs/tool_reference.md`, agent recipe 0f (map → pick ≤8 → digest), now maintained in [recipes.md](docs/recipes.md).
- `MCP_API_SPEC.md` 0.7.4-map; map failure codes in `docs/failure_codes.md`.

### Changed

- Product surface **v0.7.4-map** — five tools; `docs/10-l0-scope.md`, `AGENTS.md`, `docs/index.md` aligned.

### Documentation

- P1-3 post-ship verify PASS (`972cef0`); quality audit baseline notes map optional rotation spot.

## [0.7.3] — 2026-06-15

Four MCP tools; playbook seeds + read-only resolve; tier-3 quality Merge PASS (baseline 2026-06-16); agent recipes 0c–0e.

### Added

- **PB1.1 playbook resolve:** MCP tool `occam_playbook_resolve` — read-only lookup of `profiles/playbooks/seeds/*.seed.json` (`sourcePath`, `contentSelectors`, `agentNotes`, `preferredBackend`). C# `PlaybookSeedResolver`; gate tests for golden-hosts + resolve.
- **PB0 core slim:** generic markdown prune (`workers/shared/lib/generic-markdown-prune.mjs`), playbook seeds under `profiles/playbooks/seeds/`, golden-hosts allowlist; extraction policy in docs + MASTER-PLAN §10.9.
- **F3–F5 seed flags (`558864f`):** `nuxtFooterPromos` + `nuxtShikiCssStrip` in `nuxt.com.seed.json`; `docs.docker.com.seed` `preferred_backend` + `agent_notes` for honest digest.
- **Path B agent recipes:** historical `docs/worked_examples.md` examples 0c–0e — playbook-aware probe → resolve → transcode (nginx, OpenAI concepts), resolve-before-digest (postgres + docker rotation); current recipes: [recipes.md](docs/recipes.md); 0c selector clarify (`2628ec5`).
- **Digest agentHints:** `occam_digest` success responses include `agentHints` (`suggestedReadOrder`, `warnings`, `decisions`) — read before citing `combined` when `focus_query` is set.
- **L2c digest slim (P1-2):** MCP tool `occam_digest` — linear live digest of up to 8 URLs; per-page excerpts + optional `combined` markdown.
- `DigestService`, `DigestUrlParser`, gate `L2_DIGEST_OK`, corpus `corpora/l2-digest.jsonl`.
- Docs: `docs/tool_reference.md`, `MCP_API_SPEC` 0.7.0-digest-slim.
- **Visual QA matrix:** `scripts/run-visual-matrix.ps1`, corpus `corpora/visual-matrix.jsonl`, subfolders per tool under `artifacts/l0-runs/`; the former public `14-visual-qa.md` is now maintainer-only.
- **L2a shape policies (P1-1):** `link_policy`, `table_policy`, `codec_mode`, `include_metadata` on `occam_transcode`; gate `L2_POLICY_OK`. *(Rolled back in v0.6 cleanup — see Changed.)*
- **L1 failure taxonomy (P0-3 / A1):** typed `http_*` codes on `occam_transcode` aligned with `occam_probe`; `failure.statusCode`, `failure.retryable`, `agentMeta.decisions` on transcode failures.
- `FailureCodeStrings.ResolveTranscodeFailure`, `TranscodeAgentDecisions`, gate `L1_FAILURE_TAXONOMY_OK`.
- Smoke corpus `not-found` uses stable MDN 404 URL with `failure_code: http_404`.
- **L1b probe (P0-2):** MCP tool `occam_probe` — HTTP diagnosis, classification, domain tiers, `agentHints`.
- `profiles/tiers/domain-tier.v1.json`, `ProbeService`, gate `L1B_PROBE_OK`, corpus `corpora/l1b-probe.jsonl`.
- Docs: `docs/tool_reference.md`, `MCP_API_SPEC` 0.3.0-l1b.
- **L1a token economy (P0-1):** `max_tokens`, `fit_markdown`, `focus_query`, `content_selectors` on `occam_transcode`.
- Compile layer: BM25 prune (`FitMarkdown`), selectors filter, head token truncation.
- Gate: `L1aTokenEconomyTests`, exit token `L1A_TOKEN_OK`.
- **`scripts/occam-doctor.sh`** — macOS/Linux doctor (parity with `occam-doctor.ps1`).
- **`scripts/lib/playwright-cache.mjs`** — shared default Playwright cache paths (Windows / macOS / Linux), aligned with host `PlaywrightEnvironment`.
- **`scripts/run-browser-daemon.ps1`** — sets `PLAYWRIGHT_BROWSERS_PATH` from `playwright-cache.mjs` when unset.

### Changed

- **v0.6 cleanup:** removed file transcode cache (`cache_policy`, `FFOccamMcp.Core.Cache`); rolled back L2a link/table/codec/`include_metadata` shape knobs from public API.
- **Tier-3 quality audit:** live MCP Merge PASS — FROZEN + rotation; PB0 milestones verified; baseline **2026-06-16**.
- **Path A verify (2026-06-15):** live MCP re-audit confirms openai-concepts Struct **≥4** (`openAiDocHeadings` seed) and B2 closure Fid **≥4** @512 (`PreserveDefinitionalAnchor` C#).

### Fixed

- **Post-QA polish (2026-06-15):** strict digest `focusMatched` (multi-term); `agentHints` on `occam_digest`; Nuxt noise prune on HTTP path; OpenAI docs H2 restoration; `focus_window` definitional trim for closure @512.
- **K2 token contract:** `FitMarkdown` filters list/TOC bullets by `focus_query` anchor text (cross-section), preserves index pages in lenient mode, strips MDN/Carbon boilerplate; boundary-safe truncation (`head_safe`) and focus sandwich (`sandwich`) replace naive head cuts.
- **Probe honesty:** `challenge_kind:rate_limit` requires HTTP 429 or rate-limit phrasing with low visible-text ratio **and** short prose (&lt;500 chars) — docs mentioning rate limits no longer false-positive.
- **Digest honesty:** `items[].focusMatched` signals relevance when `focus_query` is set (items are never dropped for mismatch).
- **nginx index noise:** HTTP extract collapses `ngx_*` module spam on `/en/docs/` index — keeps alphabetical directive/variable index links only.
- **Nuxt docs:** HTTP extract follows meta-refresh to current docs path; browser path strips sidebar/Carbon/emoji noise via `nuxt.com` recipe.
- **K2 focus window:** `TokenBudget` uses `focus_window` truncation to keep focus-matching sections (e.g. MDN `### Closures`) within `max_tokens`.
- Browser worker spawns now set `PLAYWRIGHT_BROWSERS_PATH` to the installed Chromium cache (fixes intermittent `nuxt-spa` / `no_json` when Playwright could not find browsers).
- Browser worker always emits JSON on launch failure (`playwright_missing` → MCP `workers_unavailable` with doctor hint).

### Documentation

- **`docs/MASTER-PLAN.md`** — pillar restructure (§9–§14 architecture, §15–§20 execution), §18 priority matrix; shipped log through v0.7.3.
- **`docs/index.md`**, **`docs/01-overview.md`**, **`docs/10-l0-scope.md`**, **`AGENTS.md`** — product surface v0.7.3; planning cross-links aligned to six pillars + §18.
- **`docs/installation.md`** — cross-platform install, doctor, Playwright cache table, AOT RIDs (Playwright commit).
- RAG data source + multimodal thesis: `docs/01` § Role in RAG + § Multimodal opt-in; MASTER-PLAN §11.1b.

## [0.1.0-l0] — 2026-06-14

First L0 release: one MCP tool, two extract backends, gates, and user docs.

### Added

- User documentation: `docs/` (01–12 + README), English, fact-checked against L0 code
- Root specs: `MCP_API_SPEC.md`, `AGENTS.md`, `CHANGELOG.md`, `INSTALL.md`
- L0 gates: fast tier (`L0_GATE_FAST_OK`), full tier (`L0_GATE_OK`)
- Browser daemon pool, RAM stress bench, visual HTML report
- MCP tool `occam_transcode` (Native AOT .NET 10 host + Node workers)
- `.cursor/rules/` (5 `.mdc` rules) + `mcp.json.example` + `docs/12-cursor-for-contributors.md`
- Nginx extract: `#content` selector + thin-extract quality gate

### Changed

- Documentation split: public `docs/` in git; engineering `docs-internal/` gitignored
- `AGENTS.md`: task discipline, doc sync matrix, no-garbage rules
