# Phase 5 shared instructions (canonical product model synthesis)

You are a **model synthesis** agent, not a discovery agent. Waves 1–4 are COMPLETE. Do **not** rerun discovery unless a concrete unresolved gap blocks you (then bound it and say so).

## Hard constraints
- **Do NOT modify**: `README.md`, `INSTALL.md`, `docs/`, `llms.txt`, `mkdocs.yml`, any product code, any test. No git push/merge/PR. No rename/rebrand. No bug fixes.
- Write **only** your assigned output file(s) under `docs-audit/`. Do not edit other agents' files, `capabilities.json`, `capability-graph.json`, or `ENGINEERING-FINDINGS.md` (orchestrator owns reconciliation).
- English only in committed files.
- **Never invent behavior from prose.** Every claim needs code evidence (`path:line`) or a canonical audit ID (CAP/EF/ART/FLOW/GAP).

## Evidence precedence (from `CANONICAL-AUDIT-INDEX.md`)
1. Executable code (SoT)
2. Wave-4 correction layer: `WAVE4-REPORT.md`, `NEGATIVE-SPACE-GAPS.md`, `SHIPPED-CODE-MAP.md`, `FAILURE-BEHAVIOR-MAP.md`, `AUTOMATIC-BEHAVIORS.md`, `CONFIG-NEGATIVE-SPACE.md`, `PLATFORM-DIFFERENCES.md`
3. Canonical ledgers: `capabilities.json` (674 CAPs), `ENGINEERING-FINDINGS.md` (EF-001…057), `ARTIFACT-MAP.md` (ART-001…039), `CODE-DERIVED-WORKFLOWS.md` (FLOW-001…022), `ENVIRONMENT-VARIABLES.md`, `DEAD-OR-UNREACHABLE.md`
4. `subsystems/*.md`, `tools/*.md` (deep mechanism evidence, pre-Wave-4)
5. `negative-space/*-blind.md` (agent-local evidence)
6. Public docs — **untrusted, frozen**

**Wave-4 corrections override older prose.** Read `CANONICAL-AUDIT-INDEX.md` §Conflicts (C1–C9) before writing. Notably: cascade prose CAP-052/104 is WRONG (EF-056); `OCCAM_RECEIPTS` is not a complete master switch (EF-005/044); dead code still SHIPS (whole-glob compile); atlas leak EF-024 is WITHDRAWN.

## ID discipline
- **Never renumber or delete** canonical IDs: CAP-001…1041, EF-001…057, ART-001…039, FLOW-001…022, GAP-001…044.
- You may propose **new canonical relationships** (parent/alias/classification), never new CAP numbers.
- New EF candidates → `EFC-P5-<agent>-<n>` (orchestrator allocates EF-058+ if warranted).
- New artifacts/workflows → `ART-NEW-<agent>-<n>` / `FLOW-NEW-<agent>-<n>`.

## Anti-goal: do NOT produce a 674-item feature list
The raw inventory is **evidence**, not documentation structure. Compress hierarchically:

```
PRODUCT → PRODUCT SYSTEM → SUBSYSTEM → CAPABILITY FAMILY → CAPABILITY → MECHANISM/PARAM/CONFIG → CODE EVIDENCE
```

## Working taxonomy HYPOTHESIS (challenge it with evidence — do not blindly adopt)

Orchestrator's provisional 9 product systems, derived from the corpus. **Merge / split / reject / rename with evidence.** If you change it, say exactly why.

| # | Provisional system | Rough scope |
|---|--------------------|-------------|
| PS-1 | **Acquisition** | `OccamRouter`, backend policy, http/browser/managed backends, escalation ladder, proxy + rotation, egress/SSRF policy, robots/throttle, sessions-for-fetch, domain tiers |
| PS-2 | **Materialization** | compile pipeline: fit_markdown, token budget, focus, section index, blocks/tables/feed/chunks, diff, translate, codecs, omitted manifest, response cache |
| PS-3 | **Discovery** | probe, map, search providers, sitemap discovery, link filter/rank, extractability scoring |
| PS-4 | **Knowledge extraction** | `occam_extract_knowledge`, css-extract worker, field specs, schema planning, (mostly dead Canonical IR) |
| PS-5 | **Playbooks** | resolve/save/heal/lint, genome merge + well-known fetch, seeds, community tier, signature, quality gate |
| PS-6 | **Trust & provenance** | receipts v1, Merkle, capsules, time anchor, verify (4 modes), claim_check, attest, dataset export + manifest |
| PS-7 | **Monitoring & multi-source** | watch + history chain, crosscheck/consensus, failure atlas, batch jobs |
| PS-8 | **Runtime & exposure** | transports (stdio/WS/Remote), batch server, profiles, opt-in env gates, server instructions, client capabilities, DI composition, telemetry/banner |
| PS-9 | **Operator surface** | host CLI verbs, `occam` wrapper + subcommands, doctor, install/onboard, connect (host adapters), session CLI, refresh/process control, skill install, packaging (tarball/npm/Docker/CI) |

Cross-cutting lenses that are **not** systems (they are properties): configuration, platform differences, automatic behavior, failure semantics, state/persistence, security/privacy.

## Required framing every agent must respect
- **Product capability ≠ MCP tool count.** 15 core tools is one exposure surface among many.
- **Distinguish**: intended product semantics vs engineering findings (bugs). Never document a bug as a feature; flag it instead.
- **Distinguish**: shipped vs reachable vs modeled (see `SHIPPED-CODE-MAP.md`).
- **Be conservative on trust claims.** No marketing language. If something proves less than its name suggests, say exactly what it does and does not prove.

## Output style
- Tables and short declarative prose. Copy-paste-ready code refs.
- Every significant row carries evidence: `path:line` and/or CAP/EF/ART/FLOW/GAP IDs.
- Mark uncertainty explicitly as `UNCERTAIN` with what would resolve it. Do not hide it.

## Return envelope (compact — full content goes in your file)
```
AGENT: P5-xx
FILES_WRITTEN: <paths>
TAXONOMY_VERDICT: <accept/modify hypothesis + why, one or two lines>
KEY_STRUCTURE: <the main structural conclusion, <=8 lines>
COUNTS: <e.g. families=n, artifacts=n, states=n>
CORRECTIONS_TO_PRIOR_MODEL: <list>
EFC: <EFC-P5-xx-n or none>
UNCERTAIN: <bounded list or none>
ANSWERS_CONTRIBUTED: <which of the 28 final product questions your file answers>
```
