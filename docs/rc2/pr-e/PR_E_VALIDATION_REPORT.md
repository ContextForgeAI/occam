# PR-E projection-first budget validation report

## Stop-gate status

**Pass.** PR-E is independently green and may hand off to PR-F.

## Focused evidence

- Focused cumulative suite: 43/43 against the final candidate AOT host.
- Cumulative expected-red suite: 14/15 green; only the PR-F semantic-attempt assertion remains red.
- Frozen characterization: 32/32 through explicit test-only RC.1 budget and focus seams.
- Production unit gate: `L0_GATE_OK` after updating the budget parity fixture to mark projected inventory.
- Fast gate: `L0_GATE_FAST_OK`.
- Full gate: `L0_GATE_OK`, including L1A through L11 coverage.
- Projection calibration at 700 tokens: estimated 457, serialized projected measurement 439, tolerance 21.
- Minimum answer fixture at 128 tokens: `ANSWER_BODY` plus GET/HEAD/POST retained; estimate 126.
- Completeness: retained constrained focus is `Partial/context_truncated`; heading-only loss is
  `Incomplete/focus_body_truncated` with `suggestedMinTokens=128`.
- Final local 200-iteration projection plus answer-planning observation: 19.753 ms, zero planner retries.

## Full-gate incident

The first full L0 run exposed an earlier PR-C integration omission: the HTTP worker computed bounded DOM
access evidence but did not copy it into its success payload. The frozen `golden-login` case therefore
fell back to Markdown-only evidence and incorrectly succeeded. One payload field was restored. Worker
syntax/selftest, cumulative PR-C 22/22, and the repeated full L0 gate passed. No assertion or golden
fixture changed.

## Native AOT

- Path: `artifacts/rc2-pre-host/OccamMcp.Core.exe`
- Size: 38,320,640 bytes
- SHA-256: `7d6790b1335860098aba6416a1c30d3f1d49df87fe9675b02f397f4bf8a75a54`
- Publish result: pass with four pre-existing nullable warnings in `MaterializedProvenanceResolver.cs` and
  no new trimming warning.

## Hygiene

The final audit runs `check-docs.mjs`, `git diff --check`, frozen-evidence diff, and temporary-patch scan.
No RC.1 artifact or evidence path was modified. The worktree remains uncommitted by owner instruction.
