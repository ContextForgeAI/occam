# PR-A validation report

## Outcome

PR-A establishes a deterministic red baseline without production fixes. The normal suites remain healthy, the frozen archive hashes remain valid, and all desired-contract failures are isolated behind an explicit command.

## Test results

| Check | Command | Result |
|---|---|---|
| PR-A build | `dotnet build benchmarks/rc2-regression/Rc2Regression.csproj -c Release` | Pass; 0 warnings, 0 errors in final build |
| Characterization | `dotnet run --project benchmarks/rc2-regression -c Release -- --characterization` | Pass; 32/32 after final D12 parser coverage |
| Spikes | `dotnet run --project benchmarks/rc2-regression -c Release -- --spikes` | Pass; 4/4 |
| Expected-red | `dotnet run --project benchmarks/rc2-regression -c Release -- --regression` | Expected exit 1; 12/12 assertions labeled `EXPECTED_RED` |
| Existing unit suite | `dotnet run --project benchmarks/l0-gate -c Release -- --unit-only` | Pass; exit 0 |
| Existing fast gate | direct `L0Gate.exe --fast` | Pass; exit 0. Wrapper produced `NativeCommandError`, so wrapper status was not used |
| Existing full gate | direct `L0Gate.exe` | Pass; exit 0; console capture was truncated after extensive pass output |
| Release publish | `dotnet publish src/FFOccamMcp.Core -c Release -r win-x64` | Pass; exit 0 |
| Docs lint | `node scripts/check-docs.mjs` | Pass after final additions: 55 documents, 316 links, 42 anchors, 15 tools |
| Frozen evidence | verify `_archives/SHA256SUMS.txt` | Pass; 21/21 archives matched before and after work |

## Deterministic expected-red inventory

1. D9 public authentication prose hard-fails in probe and transcode.
2. D19 probe and transcode disagree on public OpenID-style prose.
3. D15 numeric-only identifier fails to select the answer section.
4. D15 repeated terms select the wrong section.
5. D11 TOC/index displaces the body definition.
6. D17 exact fragment is not routed into focus ranking.
7. D10 hidden blocks/tables halve the modeled Markdown surface budget.
8. D10 constrained focus loses the answer-bearing list.
9. C10b constrained focus returns truncation without the answer.
10. Semantic attempt shape cannot distinguish transport from usability.
11. D12 `tools/list` cannot advertise native arrays plus strings.
12. D12 empty native array returns opaque framework invocation text instead of typed validation.

## Fixture integrity

| Fixture | SHA-256 |
|---|---|
| `access-neutral.html` | `616a3e63533db630ecf1a2aac5486737d2c534a4dbd5ba8d3d974bbeb781d6fa` |
| `access-openid.md` | `83fe79ad86fd4fa5413288b29adac32ea4710312e31b173fc294566c936890df` |
| `access-public-auth.html` | `3d561866857b32254b406c43522f547007b02f200d4eeb4d6685c7f903a5c823` |
| `access-public-auth.md` | `e59e99f7385e29d0cd728b6029145f0e8aae47fd3371dcde5f2d668a4e575bf9` |
| `access-real-login.html` | `6cc919856ca2bad23c8ecca432b47571cda39c4f736ca091a4f0acd9cb3a2b94` |
| `access-real-login.md` | `350b5b0f8b9fb0e84620a94eec5218e8ca5615908e0433a0c7447cd6def77537` |
| `budget-answer.md` | `cb41810c177c376b8bada86cc673f5ce65f153643d13f5c9e7b523dad4e95c41` |
| `focus-duplicates.md` | `226e58d0da8ab768f71a9ea5d449806b6e5f98f3d2b629ea6f8898489c218732` |
| `focus-large.md` | `409e7a70d01c7b9011334f07dbc22e9664c3a75d187b67fcd45acd755738953d` |
| `focus-sections.md` | `1e6cfc4342dcbac4465c80371b6d536dc50c7b81304948d3847cd1d60a55a015` |
| `focus-toc.md` | `7d969547d5b71492eb4c511ce157669ac8b2a6f8cf3bb4c98d0495e4791525eb` |

The fixture hashes above were recorded before the final D12 parser-only edit and are unchanged by it.

## Changed-file classification

- Production files: none.
- Public API/spec/configuration/changelog: none; public behavior did not change.
- Test project: new `benchmarks/rc2-regression/` only.
- Test documentation: new `docs/rc2/pr-a/` only.
- Frozen RC.1 evidence: unchanged.
- Pre-existing `.gitignore`, `docs/rc2/`, and `validation/` user work: preserved.

## Reproduction gaps

- D17 has no dedicated frozen archive; the offline test proves the current missing fragment-to-focus connection but cannot choose final precedence.
- D3 production process-tree automation is not deterministic from Core alone; the test-only identity model records the required invariant.
- Full semantic claim verdict and focus-completeness fields do not exist yet; their current ambiguity is characterized in `CURRENT_SEMANTIC_CONTRACT.md` rather than simulated as production output.

## Recommended exact PR-B scope

PR-B should change only the `occam_digest` MCP input boundary and a canonical input normalizer. It must preserve legacy string forms, accept native string arrays, return structured `invalid_arguments` for empty/mixed/nested/wrong JSON shapes, publish a truthful runtime schema, keep URL ordering/limits/deduplication unchanged, and update the public contract/docs required by `AGENTS.md`. It must not include access, focus, budget, semantic, or lifecycle work.

## Owner decisions still required

1. Approve native array plus legacy string union semantics for PR-B after confirming client interpretation of the runtime schema.
2. Decide whether D17 fragments are a hard target or a soft ranking hint, including missing/encoded behavior.
3. Approve access-classifier recall/unknown thresholds for the broader corpus.
4. Approve serialized-estimator tolerance and the minimum protected answer unit.
5. Approve compatibility names and deprecation timing for semantic outcome fields.
6. Confirm the host lifecycle integration API available to PR-G.
