# PR-F semantic result contract — validation report

## Stop-gate status

**Pass.** PR-F is independently green. Do not begin PR-G until the owner requests it.

## Focused evidence

| Suite | Result |
|---|---|
| Focused `--pr-f` against candidate host (`OCCAM_RC2_HOST`) | 54/54 |
| Cumulative `--regression` against candidate host | 15/15 |
| Frozen `--characterization` against RC.1 host (no `OCCAM_RC2_HOST`) | 32/32 |
| Production `--unit-only` | `L0_GATE_OK` |
| Fast gate | `L0_GATE_FAST_OK` |
| Full L0 | `L0_GATE_OK` (L1A–L11 markers present) |
| `node scripts/check-docs.mjs` | OK |
| `git diff --check` | OK |

## Semantic honesty checks

- Transport success with `usable=false` is expressible on `TranscodeAttempt` / `recovery[]`.
- Browser recovery can be usable while preserving prior `escalationReason`.
- Retained constrained focus maps to `completeness=partial`; body loss maps to `incomplete`.
- `claim_check` publishes `retrieved` beside `found` with `verdict=not_evaluated`.
- Public success envelope retains `ok` / `confidence` and adds `access` / `focus` / `completeness` / `verdict`.

## Native AOT

- Path: `artifacts/rc2-pre-host/OccamMcp.Core.exe`
- Size: 38,458,880 bytes
- SHA-256: `84fa0c32670c8fde6a017de46f7ed253532804ed71082bed5b0969410aed5694`

## Hygiene

No RC.1 frozen evidence paths were modified. Temporary gate stdout/stderr captures and `artifacts/probe.tmp`
were removed after validation. The worktree remains uncommitted by owner instruction.
