# Wave 4 shared instructions (blind discovery agents)

**You are a fresh adversarial negative-space auditor.** SoT = current shipped executable code. Docs and prior `docs-audit/*` are UNTRUSTED — your job is to prove the existing model INCOMPLETE.

## Hard constraints
- Discovery only. **No** product-code edits, no bug fixes, no doc rewrites, no git push/merge/PR, no rename/rebrand.
- English in the committed report.
- Only write your ONE assigned report file under `docs-audit/negative-space/`. Do not touch other agents' files, `capabilities.json`, or `ENGINEERING-FINDINGS.md` (the orchestrator consolidates).

## Mandatory ordering (do NOT invert)
1. **CODE DISCOVERY FIRST.** Read your assigned scope. Independently enumerate every *externally meaningful* behavior (affecting users/agents/operators/security/privacy/network/config/persistence/artifacts/trust/performance/failure/routing/backend selection). Do this BEFORE opening any prior audit artifact.
2. Write the independent inventory into your report (section "## 1. Blind inventory").
3. **THEN** compare against existing model files (only now): `CAPABILITY-INVENTORY.md`, `capabilities.json`, `CAPABILITY-GRAPH.md`, `ARTIFACT-MAP.md`, `CODE-DERIVED-WORKFLOWS.md`, `NONCORE-SURFACE-MAP.md`, relevant `subsystems/*.md` / `tools/*.md`.
4. Write section "## 2. Gap classification" using these labels per finding:
   `COVERED_EXACTLY | COVERED_PARTIALLY | COVERED_WRONG | MISSING_CAPABILITY | MISSING_EDGE | MISSING_ARTIFACT | MISSING_WORKFLOW | MISSING_CONFIG | MISSING_RUNTIME_SURFACE | MISSING_FAILURE_SEMANTIC | MISSING_SECURITY_SEMANTIC | DEAD_CODE_MISTAKEN_AS_PRODUCT | PRODUCT_MISTAKEN_AS_INTERNAL`
5. Every gap needs concrete code evidence (`path:line`, symbol, observed semantics).

## Cross-cutting lenses (apply within your scope)
- **Config reverse audit:** every `Environment.GetEnvironmentVariable` / CLI switch / JSON key / constant-as-feature-gate you touch. Flag config read-but-ineffective, surprising defaults, multi-switch features.
- **Error-path audit:** `catch`/`throw`/fallback/retry/timeout/cancellation/partial/degraded/rollback. What happens when the happy path fails? Hidden degradation/recovery? Dangerous silent semantics?
- **Automatic/silent behavior:** anything triggered without explicit user request. Record TRIGGER / VISIBLE? / CONFIGURABLE? / DISABLEABLE? / ARTIFACT / TRUST / PERF effect.
- **Platform difference:** `OperatingSystem.Is*`, `RuntimeInformation`, RID, path sep, exe suffix, ps1 vs sh. Capability differences (not just packaging).
- **Artifacts:** structures that cross a boundary (returned/written/persisted/signed/hashed/cached/exported/imported/verified).
- **Dead vs shipped:** remember the Core csproj compiles the WHOLE glob — "dead at runtime" still ships. Distinguish DEAD_CODE_MISTAKEN_AS_PRODUCT vs PRODUCT_MISTAKEN_AS_INTERNAL.

## ID discipline
- Prefer a **new graph edge / artifact relation / workflow / correction** over minting a new CAP.
- If a genuinely distinct NEW capability exists, propose it as `CAP-NEW-<OWNER>-<n>` (e.g. `CAP-NEW-A-1`) — do NOT assign a real CAP number (orchestrator allocates from CAP-1050+).
- Engineering findings: propose as `EFC-<OWNER>-<n>` — do NOT use canonical `EF-NNN` (Wave 3 is CLOSED at EF-040; orchestrator allocates EF-041+).

## Return envelope (compact — full evidence stays in the file)
```
OWNER: W4-<x>
SCOPE_FILES_READ: <count + notable paths>
BLIND_BEHAVIORS: <count>
GAPS: covered_exact=<n> partial=<n> wrong=<n> missing_cap=<n> missing_edge=<n> missing_artifact=<n> missing_workflow=<n> missing_config=<n> missing_failure=<n> missing_security=<n> dead_as_product=<n> product_as_internal=<n>
TOP_MISSED: <up to 8 one-liners with path:line>
NEW_CAP_CANDIDATES: <CAP-NEW-x-n list or none>
NEW_EDGES: <top few>
NEW_ARTIFACTS: <list or none>
NEW_WORKFLOWS: <list or none>
AUTOMATIC_SILENT: <top>
FAILURE_FALLBACK: <top>
CONFIG_GAPS: <top>
PLATFORM_DIFFS: <top>
EFC: <EFC-x-n: class/confidence one-liners, or none>
CONVERGENCE_IN_SCOPE: <did independent discovery stop finding major unmodeled behavior? YES/NO + why>
UNCERTAINTIES: <bounded list>
```
