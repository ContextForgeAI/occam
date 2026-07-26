# PR-G lifecycle identity — validation report

## Stop-gate status

**Pass.** PR-G is independently green. Do not begin PR-H until the owner requests it.

## Focused evidence

| Suite | Result |
|---|---|
| Focused `--pr-g` against candidate host | 61/61 |
| Cumulative `--regression` against candidate host | 22/22 |
| Frozen `--characterization` against RC.1 host | 32/32 |
| Production `--unit-only` | `L0_GATE_OK` |
| Fast gate | `L0_GATE_FAST_OK` |
| Full L0 | `L0_GATE_OK` |
| `node scripts/check-docs.mjs` | OK |
| `git diff --check` | OK |

## Lifecycle honesty checks

- Production `HostIdentityDescriptor` exists in Core.
- Dual host trees: targeted stop by `RuntimeId`+pid leaves the unrelated tree alive.
- Blank/missing shutdown targets are rejected.
- Overlap warnings diagnose only; no auto-kill / no global singleton type.
- Parent-side `prctl` child-PID assumption removed from `WorkerProcessGroup.Attach`.
- `lifecycle self` on the candidate host returns a populated identity JSON.

## Native AOT

- Path: `artifacts/rc2-pre-host/OccamMcp.Core.exe`
- Size: 38,630,400 bytes
- SHA-256: `184d6e7ce8024339eb560f7af91bb3860174c75725712b19b59c1d73202fdaff`

## Hygiene

No RC.1 frozen evidence paths were modified. Temporary gate captures under `artifacts/` were removed
after validation. The worktree remains uncommitted by owner instruction.
