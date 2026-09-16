# Architecture Decision Records

An ADR records a decision that was **expensive to make and would be expensive to reverse**: a
dependency, a wire format, a policy that other code now assumes. Routine implementation choices do
not get one.

Format: context → decision → consequences → alternatives considered. Written once, then amended only
with a superseding record; an ADR is a historical document, not living documentation.

## Index

| # | Title | Status |
|---|---|---|
| 0001–0002 | Open knowledge-representation layer; planner ↔ codec separation | Accepted — recorded inline in `src/FFOccamMcp.Core/Codecs/` and `Knowledge/` |
| 0005 | Access classification | Accepted — [`docs/architecture/semantic-contract.md`](../architecture/semantic-contract.md) |
| 0006 | Structure-aware focus | Accepted — [`docs/architecture/semantic-contract.md`](../architecture/semantic-contract.md) |
| 0007 | Projection-first budget | Accepted — [`docs/architecture/semantic-contract.md`](../architecture/semantic-contract.md) |
| [0010](0010-proof-of-read-canary.md) | Proof-of-read via HMAC time-bucket sentinels | Accepted |
| [0011](0011-test-stack.md) | Test stack: xunit, FsCheck, coverlet | Accepted |
| [0012](0012-central-package-management.md) | Central package management and SDK pinning | Accepted |
| [0013](0013-warnings-as-errors-per-project.md) | Warnings-as-errors per project, not repo-wide | Accepted |
| [0014](0014-timeprovider-over-test-clock-package.md) | `TimeProvider` with an in-product manual clock | Accepted |
| [0015](0015-cross-platform-evidence-policy.md) | Cross-platform claims require committed artefacts | Accepted |
| [0016](0016-capability-exam.md) | Capability exam and behaviour-derived tool tiers | Accepted (engine); administration partially implemented |
| [0017](0017-cascade-facade.md) | Cascade facade `occam(url, task, budget)` | Accepted |

Numbers 0001–0009 predate this directory. They were recorded in the documents listed above and in
source comments; they are indexed here so the numbering line stays single and unambiguous. New
records start at 0010.
