# DISCOVERABILITY-GATE (Phase 5P)

**Agent:** P5-09  
**Purpose:** Define a **future** documentation quality gate so PUBLIC_CORE / PUBLIC_ADVANCED capabilities cannot become invisible again.  
**Design only** — do **not** implement or modify `scripts/check-docs.mjs` (or any script) in this phase.  
**Inputs:** `DOCUMENTATION-EXPOSURE-MATRIX.md`, `USE-CASE-MODEL.md`, `ENVIRONMENT-VARIABLES.md`, `ENTRYPOINT-MODEL.md`, `TRUST-MODEL.md` §13.  
**Date:** 2026-07-26

---

## 0. Discoverability paths (vocabulary)

| Path ID | Surface | Role |
|---------|---------|------|
| `README` | Root `README.md` | First contact / what Occam is |
| `QUICKSTART` | Getting-started / install quick path | Minimum runnable path |
| `TASK` | Task-oriented guide (choosing-a-tool, recipes, use-case pages) | “I want to do X” |
| `CAPABILITY` | Per-family or per-system capability page | Structured product model |
| `REFERENCE` | `MCP_API_SPEC.md` / tools-reference / configuration | Contract depth |
| `HANDBOOK` | Operator / trust / architecture handbook chapters | Humans operating or auditing |
| `LLMS` | `llms.txt` | Agent documentation map |
| `MCP_DESC` | Runtime MCP tool `[Description]` / server instructions | In-band agent discovery |
| `CLI_HELP` | `occam --help` / host verb help / subcommand help | Operator discovery |

A family is **discoverable** if it has ≥ the required number of **distinct Path IDs** below, including any **mandatory path types** for its class.

---

## 1. Rule set (implementable)

### 1.1 Minimum coverage by exposure class

| Exposure class | Min distinct paths | Mandatory path types | Notes |
|----------------|-------------------:|----------------------|-------|
| `PUBLIC_CORE` | **≥ 3** | Must include **≥1 task-oriented** (`TASK` **or** `QUICKSTART`) **and** ≥1 of (`LLMS`, `MCP_DESC`) **and** ≥1 of (`REFERENCE`, `CAPABILITY`, `README`) | Stdio MCP families must appear in `MCP_DESC` when they map to a tool. |
| `PUBLIC_ADVANCED` | **≥ 2** | Must include `REFERENCE` **or** `CAPABILITY`; second path any of `TASK`, `LLMS`, `HANDBOOK`, `MCP_DESC` | Advanced must be **linked from** a TASK page (link counts as the TASK path for that family). |
| `EXPERIMENTAL` | **≥ 2** | Must include `REFERENCE` **or** `HANDBOOK`; must name the **env gate** in the same doc section as the tool/family | Must **not** appear in README feature bullets as unqualified capabilities. |
| `OPERATOR` | **≥ 2** | Must include `HANDBOOK` **or** `QUICKSTART`; must include `CLI_HELP` **or** install path in `QUICKSTART` | |
| `DEVELOPER` | **≥ 1** | `REFERENCE` or `HANDBOOK` | |
| `REFERENCE_ONLY` | **≥ 1** | `REFERENCE` | |
| `INTERNAL` | **0** required | May appear only in maintainer notes; **must not** appear in `LLMS` capability lists or README feature lists | |
| `DO_NOT_DOCUMENT_AS_FEATURE` | **0** as features | May appear only under Limits / Non-goals / Forbidden-claims sections; **must not** appear as capability headlines in README, LLMS feature lists, or MCP descriptions that overclaim | |

### 1.2 Cross-cutting mandatory rules

| ID | Rule |
|----|------|
| R1 | Every **core MCP tool name** in `OccamMcpServerRegistration.OccamToolNames` appears in ≥1 `TASK` or `QUICKSTART` page **and** in `LLMS` **and** has a non-empty `MCP_DESC`. |
| R2 | Every **opt-in env gate** (`OCCAM_BATCH_MCP`, `OCCAM_WATCH_MCP`, `OCCAM_CONSENSUS_MCP`, `OCCAM_ATLAS_MCP`) is mentioned in the same document section that names its tool(s), and in `ENVIRONMENT`→configuration cross-link. |
| R3 | Every env var listed in `docs-audit/ENVIRONMENT-VARIABLES.md` that is marked user-facing must appear in the public configuration page (when docs unfreeze) — checker uses the audit catalog as SoT until regenerated from code. |
| R4 | Every family slug with class `PUBLIC_CORE` or `PUBLIC_ADVANCED` appears **verbatim** in `llms.txt` (or in a machine-readable sibling index the gate reads). |
| R5 | Trust families (`receipts`, `verification`, `claims-attestation`, `dataset-provenance`, `consensus-crosscheck`) must link to a **Limits** subsection that covers the matching `TRUST-MODEL` §13 forbidden claims (at least by claim number or paraphrase). |
| R6 | No README / LLMS headline may use strings matching the forbidden-claim denylist (see §4). |
| R7 | `OCCAM_PROFILE` values and their tool subsets appear in REFERENCE or HANDBOOK (profile matrix). |
| R8 | Operator-only host verbs (`keys`, `verify`, `install-browser`) appear in `CLI_HELP` **or** HANDBOOK with an explicit note if unreachable via `occam` wrapper (EF-025). |
| R9 | Families classed `DO_NOT_DOCUMENT_AS_FEATURE` must **fail the gate** if they appear as positive capability bullets in README / LLMS / MCP_DESC. |
| R10 | Acquisition routing docs must not claim universal http→browser→managed without the EF-056 short-circuits (string/heuristic check against known-bad phrases). |

### 1.3 Pass / fail

- **PASS:** all families in scope satisfy §1.1 minima **and** R1–R10 hold.  
- **FAIL:** any PUBLIC_CORE/PUBLIC_ADVANCED below minimum; any R1–R10 violation; any denylist hit in protected surfaces.  
- **WARN (non-blocking):** EXPERIMENTAL missing second path; OPERATOR missing CLI_HELP but present in HANDBOOK; UNKNOWN coverage cells during migration.

---

## 2. Coverage table template

Populate when public docs are rewritten. **Current status = UNKNOWN/TBD** (docs frozen and untrusted).

| Family slug | Exposure class | Required paths (rule) | Path evidence (file#anchor) | Current status |
|-------------|---------------|------------------------|-----------------------------|----------------|
| `acquisition-routing` | PUBLIC_CORE | ≥3 incl. TASK/QUICKSTART + LLMS\|MCP_DESC + REF\|CAP\|README | TBD | UNKNOWN |
| `http-acquisition` | PUBLIC_CORE | same | TBD | UNKNOWN |
| `browser-acquisition` | PUBLIC_CORE | same | TBD | UNKNOWN |
| `managed-acquisition` | EXPERIMENTAL | ≥2 + env gate named | TBD | UNKNOWN |
| `network-safety` | PUBLIC_ADVANCED | ≥2 incl. REF\|CAP | TBD | UNKNOWN |
| `proxy-egress` | OPERATOR | ≥2 | TBD | UNKNOWN |
| `session-fetch` | PUBLIC_ADVANCED | ≥2 | TBD | UNKNOWN |
| `access-consent` | PUBLIC_ADVANCED | ≥2 | TBD | UNKNOWN |
| `token-budget` | PUBLIC_CORE | ≥3 | TBD | UNKNOWN |
| `focus-selection` | PUBLIC_CORE | ≥3 | TBD | UNKNOWN |
| `structured-materialization` | PUBLIC_ADVANCED | ≥2 | TBD | UNKNOWN |
| `differential-materialization` | PUBLIC_ADVANCED | ≥2 | TBD | UNKNOWN |
| `response-cache` | EXPERIMENTAL | ≥2 + `cache_ttl_s` | TBD | UNKNOWN |
| `quality-failure-semantics` | PUBLIC_CORE | ≥3 | TBD | UNKNOWN |
| `probe-diagnostics` | PUBLIC_CORE | ≥3 | TBD | UNKNOWN |
| `site-mapping` | PUBLIC_CORE | ≥3 | TBD | UNKNOWN |
| `web-search` | PUBLIC_ADVANCED | ≥2 + provider env | TBD | UNKNOWN |
| `digest-synthesis` | PUBLIC_CORE | ≥3 | TBD | UNKNOWN |
| `schema-knowledge-extraction` | PUBLIC_ADVANCED | ≥2 | TBD | UNKNOWN |
| `canonical-knowledge-ir` | DO_NOT_DOCUMENT_AS_FEATURE | must not feature | TBD | UNKNOWN |
| `playbook-resolution` | PUBLIC_ADVANCED | ≥2 | TBD | UNKNOWN |
| `playbook-authoring` | PUBLIC_ADVANCED | ≥2 | TBD | UNKNOWN |
| `playbook-healing` | PUBLIC_ADVANCED | ≥2 | TBD | UNKNOWN |
| `playbook-validation` | PUBLIC_ADVANCED | ≥2 | TBD | UNKNOWN |
| `receipts` | PUBLIC_ADVANCED | ≥2 + trust limits | TBD | UNKNOWN |
| `verification` | PUBLIC_ADVANCED | ≥2 + trust limits | TBD | UNKNOWN |
| `claims-attestation` | PUBLIC_ADVANCED | ≥2 + trust limits | TBD | UNKNOWN |
| `dataset-provenance` | PUBLIC_ADVANCED | ≥2 + trust limits | TBD | UNKNOWN |
| `batch-jobs` | EXPERIMENTAL | ≥2 + `OCCAM_BATCH_MCP` | TBD | UNKNOWN |
| `change-monitoring` | EXPERIMENTAL | ≥2 + `OCCAM_WATCH_MCP` | TBD | UNKNOWN |
| `consensus-crosscheck` | DO_NOT_DOCUMENT_AS_FEATURE | limits-only / no feature headline | TBD | UNKNOWN |
| `failure-atlas` | EXPERIMENTAL | ≥2 + `OCCAM_ATLAS_MCP` | TBD | UNKNOWN |
| `runtime-transports` | PUBLIC_CORE | ≥3 (stdio); WS/Remote as OPERATOR add-on | TBD | UNKNOWN |
| `mcp-exposure` | PUBLIC_CORE | ≥3 | TBD | UNKNOWN |
| `client-context` | PUBLIC_CORE | ≥3 | TBD | UNKNOWN |
| `operator-cli` | OPERATOR | ≥2 | TBD | UNKNOWN |
| `install-onboarding` | PUBLIC_CORE | ≥3 | TBD | UNKNOWN |
| `host-connectors` | OPERATOR | ≥2 | TBD | UNKNOWN |
| `packaging-distribution` | OPERATOR | ≥2 | TBD | UNKNOWN |

Machine-readable companion (future): `docs-audit/discoverability-coverage.json` mapping `slug → { class, paths[], status }` — **not created in this phase**.

---

## 3. Feasible automation design (host: `scripts/check-docs.mjs`)

**Do not implement now.** Natural extension points of the existing doc-lint script:

| Check | Mechanical method | Data source |
|-------|-------------------|-------------|
| A. Family slug presence in `llms.txt` | Load slug list from `docs-audit/canonical-capabilities.json` (or frozen exposure matrix JSON); assert each PUBLIC_CORE/ADVANCED slug string appears | Matrix + llms.txt |
| B. Core tool names in task guides | Parse `OccamToolNames` from `OccamMcpServerRegistration.cs` (regex) or generated tools-reference; grep `docs/**/*.md` under task/recipes/choosing paths | Code + docs |
| C. MCP descriptions non-empty | Reflect tool `[Description]` attributes or `tools/list` snapshot fixture; fail if empty or shorter than N chars | Code |
| D. Opt-in env gates co-located | For each gate→tool map, require same file (or linked section) contains both strings | Hardcoded map from ENTRYPOINT §5 |
| E. Env catalog ⊆ configuration page | Diff `ENVIRONMENT-VARIABLES.md` names vs `docs/configuration.md` | Audit catalog + docs |
| F. Forbidden-claim denylist | Regex/phrase list from `TRUST-MODEL` §13 against README, llms.txt, MCP description strings, docs index | Denylist file |
| G. DO_NOT feature headlines | Assert DO_NOT family display names / overclaim phrases absent from README feature sections and llms capability lists | Matrix §2 |
| H. Profile matrix present | Require `OCCAM_PROFILE` + `reader`/`researcher`/`auditor`/`full` in a reference page | Docs |
| I. Bad cascade phrases | Fail on “always escalates to managed” / “http then browser then managed” without adjacent “404/410” or “public reference” caveat | Heuristic + allowlist anchors |
| J. Coverage table CI | Optional: require `discoverability-coverage.json` statuses ≠ UNKNOWN for PUBLIC_CORE before release tag | Future JSON |

**Out of scope for mechanical lint (human/review):** prose honesty depth beyond denylist; whether a TASK walkthrough is pedagogically good; runtime repro of EF items.

**CI placement:** same job that already runs `node scripts/check-docs.mjs`; new checks as additional assertions with clear error codes (`DISC-R1` … `DISC-R10`).

---

## 4. Failure modes the gate must catch (from prior docs failure)

| Failure mode | Observed pattern | Gate catch |
|--------------|------------------|------------|
| **Tool-count myopia** | “Occam = 15 tools” hides PS-7/PS-9 | R1 + EXPERIMENTAL/OPERATOR minima; ENTRYPOINT framing in README |
| **Silent env gates** | Opt-in tools absent from tools/list and docs | R2; EXPERIMENTAL rule |
| **Master-switch lie** | `OCCAM_RECEIPTS=off` described as full off | R6 denylist + R5 |
| **Cascade myth** | Universal http→browser→managed | R10 |
| **Trust name inflation** | attest/consensus/provenance marketing | R5 + R6 + R9 |
| **CLI orphan verbs** | `keys`/`verify` undocumented vs wrapper | R8 |
| **Profile trap** | reader produces receipts, cannot verify | R7 |
| **Search appears ready** | Tool listed without provider env | R2-style for `OCCAM_SEARCH_PROVIDER` |
| **Dead code as features** | Codecs / Canonical IR / paywall code | R9 |
| **Hidden automation** | Key mint, consent dismiss, bypassCSP | HANDBOOK required for OPERATOR + trust limits pages (R5 adjacent) |
| **Packaging overclaim** | Cosign/Docker health as green | R6 phrases; OPERATOR packaging section must not claim EF-053/051 falsehoods |
| **llms.txt drift** | Agent map misses families | R4 / check A |
| **Reference-only burial** | Advanced only in API spec with no TASK link | PUBLIC_ADVANCED ≥2 including link-from-TASK |

---

## 5. Denylist seed (for check F/R6)

Non-exhaustive starters — expand from `TRUST-MODEL` §13:

- `cryptographically verified provenance` / `verified provenance` (unqualified)
- `proves the page said`
- `tamper-proof` (allow `tamper-evident`)
- `third-party verifiable` (unqualified)
- `signed by Occam` (vendor identity)
- `multi-node consensus` / `N-of-M`
- `capsules are signed bundles`
- `OCCAM_RECEIPTS=off` + `disables all signing` (same paragraph)
- `cosign-verified install` / `supply chain is signed` (unqualified)

---

## 6. Rollout order (recommendation)

1. Encode matrix classes + denylist as JSON beside `docs-audit/`.  
2. Add **WARN-only** checks A/B/D/F to `check-docs.mjs`.  
3. When handbook rewrite lands, flip PUBLIC_CORE checks to **FAIL**.  
4. Fill coverage table statuses from UNKNOWN → PASS/FAIL.  
5. Keep DO_NOT and trust denylist as **FAIL** from day one of unfreeze.

---

## 7. Uncertainty

| Item | Status |
|------|--------|
| Whether family slugs stay stable as handbook IA | Assumed stable; if renamed, gate must map aliases |
| Exact TASK corpus paths after nuke-and-regenerate | UNCERTAIN — gate should take path globs as config |
| Server instructions vs per-tool Description for MCP_DESC credit | Prefer both; credit either for R1’s MCP leg |
