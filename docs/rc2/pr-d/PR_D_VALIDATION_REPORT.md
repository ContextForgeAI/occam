# PR-D structural focus validation report

## Stop-gate result

**Pass.** PR-D is independently green and may hand off to PR-E. INV-4, INV-5, and INV-6 are exercised by
focused fixtures, production units, cumulative regression, and AOT compilation.

## Results

| Check | Command | Result |
|---|---|---|
| Focused PR-D | isolated candidate host with `dotnet run --project benchmarks/rc2-regression -- --pr-d` | Pass; 34/34 cumulative B/C/D |
| Frozen characterization | root RC.1 host with `--characterization` | Pass; 32/32 using explicit legacy characterization seams |
| Cumulative expected-red | candidate host with `--regression` | Expected exit 1; 12 pass, two D10 plus semantic attempt remain red |
| Production unit gate | `dotnet run --project benchmarks/l0-gate -- --unit-only` | Pass; exit 0; `L0_GATE_OK` after marker compatibility fix |
| Fast gate | `scripts/run-l0-fast.ps1` | Pass; exit 0; `L0_GATE_FAST_OK` |
| Native AOT | isolated win-x64 publish to `artifacts/rc2-prd-host` | Pass; final artifact recorded below |
| Docs/hygiene | docs checker, diff check, frozen evidence, temp audit | Pass; 68 docs, clean diff check, zero frozen diff, zero temporary patches |

Focused coverage includes hierarchy/span identity, numeric identifiers, multi-term heading coverage, exact
and encoded fragments, malformed/missing fragments, TOC penalty, duplicate tie-break, fragment-free fetch,
TokenBudget consumption, stable traces, and large-document determinism.

## Performance observation

The final focused measurement indexed a 56,576-character synthetic document containing 1,600 sections in
2.853 ms and allocated 3,196,312 bytes on the local Windows run. The algorithm performs local linear
index construction plus deterministic candidate scoring. No acquisition or hosted-model work is added.

## Compatibility incident resolved during gate

The first unit run exposed loss of the existing AF-2 `SNIP: ... unchosen` marker and a mismatched worker
selftest success marker. Both assertions were preserved: structural selection now emits the bounded marker
within the token estimate, and the worker selftest follows the ordinary `name: OK` convention. The repeated
unit gate passed; no assertion was weakened.

## AOT and warnings


A later final run exposed two ranking edges: plural query terms could tie their singular section headings,
and an unrestricted definitional bonus could beat complete heading coverage. The ranker now applies bounded
plural tolerance and a hierarchy-aware definitional weight. Both the legacy closure assertions and focused
D15 wrong-section assertion pass without fixture-specific keywords.
The final post-fix Native AOT publish succeeded:

- Path: `artifacts/rc2-prd-host/OccamMcp.Core.exe`
- Size: 38,291,456 bytes
- SHA-256: `ba76244b23d6197937d883d427d6e31b1b944077fb35fdc324d71d1c86ac2e1d`

Four unrelated nullable warnings in `MaterializedProvenanceResolver.cs` remain; PR-D does not modify that
file and emitted no new trimming warning.

## Stop-policy audit

- Frozen characterization is 32/32 against the preserved RC.1 host.
- D15 and supported exact D17 are green with observable anchors/traces.
- Earlier PR-B and PR-C acceptance remains green.
- Remaining D10/semantic failures are visible and correctly assigned.
- No canonical IR replacement, breaking removal, frozen evidence edit, or RC.1 artifact overwrite occurred.

Result: proceed only to PR-E after final fast/docs/AOT/hygiene checks pass.
