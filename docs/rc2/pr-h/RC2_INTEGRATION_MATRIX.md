# RC.2 integration matrix

Base commit: `a535705c03a9bf483cbbb23aefe8c4e60cf7b48f`
Candidate host: `artifacts/rc2/win-x64/OccamMcp.Core.exe`
Recorded: 2026-07-22 (local Windows x64)

## Stage completion

| Stage | Focused suite | Cumulative regression ownership | Local PR-H recheck |
|---|---|---|---|
| PR-A | characterization 32/32 | baseline | Pass |
| PR-B | 13/13 | D12 boundary | Pass |
| PR-C | 22/22 | D9 / D19 | Pass |
| PR-D | 34/34 | D15 / D17 / structural D11 | Pass |
| PR-E | 43/43 | D10 / C10b | Pass |
| PR-F | 54/54 | semantic honesty | Pass |
| PR-G | 61/61 | D3 lifecycle | Pass |
| PR-H | integration + soak + docs | all of the above | Pass (local) |

## Blocking product gates

| Gate | Marker / score | Status |
|---|---|---|
| Cumulative RC.2 `--regression` | 22/22 | Pass |
| Frozen characterization | 32/32 | Pass |
| Unit | `L0_GATE_OK` | Pass |
| Fast L0 | `L0_GATE_FAST_OK` | Pass |
| Full L0 | `L0_GATE_OK` | Pass |
| Docs lint | `docs-check: OK` | Pass |
| `git diff --check` | exit 0 | Pass |
| win-x64 Native AOT | built + hashed | Pass |
| linux-x64 Native AOT | not buildable cross-OS | Pending remote |
| osx-arm64 Native AOT | not buildable cross-OS | Pending remote |
| Local soak | 3 iterations / 0 failures | Pass |
| macOS remote pack execution | script prepared | Pending |
| Linux/Hermes remote pack execution | script prepared | Pending |

## Defect-to-stage map (original RC.1 reds)

| Defect | Owning stage | Integration status |
|---|---|---|
| D12 digest binding | PR-B | Green |
| D9 / D19 access | PR-C | Green |
| D15 / D17 / D11 focus | PR-D | Green |
| D10 / C10b budget | PR-E | Green |
| SEMANTIC honesty | PR-F | Green |
| D3 lifecycle | PR-G | Green |

## Compatibility impact

| Surface | Impact |
|---|---|
| `occam_digest.urls` | Native array preferred; legacy string accepted |
| Access classification | Shared evidence; prose non-decisive |
| Focus / fragments | Structure-aware; fragment intent local |
| Budget | Projection-first; no silent `max_tokens` expansion |
| Semantic envelope | Additive dimensions; legacy aliases retained |
| Lifecycle | Identity diagnostics; no global singleton |

## Approved external-environment limitations

1. Cross-OS Native AOT from Windows is unsupported by .NET ILCompiler on this host.
2. Remote macOS ARM64 and Linux x64 validation hardware were not available in this PR-H session.
3. Hermes/OpenRouter remain external MCP clients; no Occam-invented Hermes API was validated.
