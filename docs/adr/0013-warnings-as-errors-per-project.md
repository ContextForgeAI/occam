# ADR-0013 — Warnings-as-errors per project, not repo-wide

**Status:** Accepted
**Date:** 2026-09-15

## Context

A research-grade repository is expected to build with `TreatWarningsAsErrors`. Turning it on
repo-wide in `Directory.Build.props` was the obvious move, and it is wrong here.

`OccamMcp.Core` currently emits six warnings, measured identically on all three platforms
(`docs/testing/*/build.log`):

| Warning | Location | Kind |
|---|---|---|
| CS8604 ×2, CS8602 ×2 | `Knowledge/MaterializedProvenanceResolver.cs` | nullable-reference |
| IL2026, IL3050 | `Playbooks/PlaybookHealDraftBuilder.cs` | trim / AOT analysis |

The nullable four are a genuine latent bug worth fixing. The two IL warnings are a reflection-based
`JsonSerializer.Serialize` call on an AOT-compiled path — a real defect, not noise, and the fix is a
source-generated serializer context, which is a behavioural change to playbook heal-draft output.

Neither fix is covered by a unit test today, and the repository rule is not to refactor working code
without tests. Flipping the flag repo-wide would therefore force one of two bad outcomes: rush both
fixes untested, or blanket-suppress the warnings and lose the signal.

## Decision

Enable `TreatWarningsAsErrors` **per project**, starting with new projects:

- `tests/OccamMcp.Core.Tests` — enabled, builds with **0 warnings** on all three platforms.
- `src/FFOccamMcp.Core` — not yet enabled; the six warnings stay visible in build output.

`.editorconfig` keeps analyzer severities at `warning`/`suggestion` rather than `error` for the same
reason: a style rule promoted to error would block work on ~300 files that predate the file.

Promotion path: fix a warning class, add a test that would have caught it, then enable the flag for
that project in the same change. The flag follows the tests; it does not lead them.

## Consequences

**Good.** New code cannot introduce warnings — the test project proves the toolchain is capable of
zero-warning builds on all three platforms. The six existing warnings stay loud instead of being
suppressed into invisibility.

**Costs.** "Does this repo build warning-free?" has a per-project answer rather than a single yes.
`docs/testing/RESULTS.md` states the partial status explicitly rather than implying repo-wide
cleanliness.

**Honest status at the time of this decision.** Definition-of-done item "warnings as errors" was
**partially met**, and recorded as partial in `RESULTS.md §6` rather than claimed as done.

## Follow-up (2026-09-16) — the promotion path was executed

Recorded here rather than in a new ADR, because the decision above already specified the path; this
is its outcome, not a change of mind.

All six warnings in `OccamMcp.Core` were fixed rather than suppressed, and the flag was enabled for
that project in the same change:

- **The four nullable warnings** were annotation gaps, not latent nulls.
  `MaterializedProvenanceResolver.TryLocateChain` guarantees claim, evidence and source are set on
  its `true` path; adding `[NotNullWhen(true)]` makes that contract visible to the compiler.
  `KnowledgeProvenance` was deliberately left unannotated — a resolved chain with no receipt
  provenance is a normal outcome.
- **The IL2026/IL3050 pair** was a real trimming hazard: a reflection-based
  `JsonSerializer.Serialize` on a shipped path in a `TrimMode=full` AOT binary. Replaced with a
  source-generated `HealDraftJsonContext`. As this ADR required, the test came first:
  `PlaybookHealDraftBuilderTests.DraftJsonIsByteStable` pins the emitted bytes, so the swap is
  provably output-neutral. Writing that test also surfaced that the em-dash is emitted as
  `\u2014`, which is now pinned deliberately rather than by accident.

`src/FFOccamMcp.Core` now builds with `TreatWarningsAsErrors=true` and **zero warnings**, including
under `dotnet publish -r <rid>` where the ILC trim and AOT analysers run. Verified on all three
platforms (`docs/testing/RESULTS.md`).

The repo-wide `dotnet format` gap is unchanged: 138 pre-existing files predate `.editorconfig`, and
that remains out of scope.

## Alternatives considered

**Repo-wide, with `NoWarn` for the six.** Rejected: a suppression list is where warnings go to die.
The IL2026/IL3050 pair in particular describes a real AOT hazard on a shipped path.

**Repo-wide, fixing all six now.** Tempting and nearly done — the nullable four are a small guard.
Rejected in this pass because the AOT pair needs a source-generated serializer for
`HealDraftDocument`, which changes playbook heal output, and there is no test covering that output
yet. Doing it blind is how a "hardening" pass breaks a working feature.

**Leave it off everywhere.** Rejected: then nothing stops the next contribution from adding warning
number seven.
