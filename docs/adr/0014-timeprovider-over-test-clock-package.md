# ADR-0014 — `TimeProvider` with an in-product manual clock

**Status:** Accepted
**Date:** 2026-09-15

## Context

Almost everything interesting about the canary is a time transition: a bucket rolls over, a sentinel
leaves the fresh window, a stale horizon expires, a rate-limit window resets, an issuance record is
evicted after its retention. Testing those by calling `Thread.Sleep` would make the suite slow and
flaky, and the stale-horizon transition (two hours of wall clock by default) is untestable that way
at all.

`TimeProvider` (BCL, .NET 8+) is the right abstraction. The conventional companion is
`Microsoft.Extensions.TimeProvider.Testing`, which supplies `FakeTimeProvider`.

There is a second requirement that package does not serve. The cross-platform evidence in
`docs/testing/` has to be reproducible on a machine that has **no test runner installed** — the
point of `occam canary selftest` is that it runs from the shipped AOT binary. A selftest that proves
bucket transitions needs a settable clock inside the product assembly.

## Decision

Take `TimeProvider?` in every canary constructor, defaulting to `TimeProvider.System`.

Ship `CanaryManualClock : TimeProvider` in the **product** assembly (`OccamMcp.Core.Canary`), not in
the test project, and use it from both `occam canary selftest` and the xunit suite. No new package.

Its class comment states why it lives there, so the next reader does not "clean up" test
infrastructure they find in production code.

## Consequences

**Good.** The full state machine — including the two-hour stale horizon — is asserted in
milliseconds, both by `dotnet test` and by the shipped binary. The cross-platform logs in
`docs/testing/*/canary-aot.log` show the AOT binary proving the same 17 transitions the unit suite
proves. One fewer dependency, and the repository rule "no new dependency without an ADR" is
satisfied by not needing one.

**Costs.** A small amount of test-shaped code ships in the product assembly (about 30 lines, AOT
trimmed only if unreferenced — it is referenced, so it ships). A reviewer may reasonably flag it;
the comment is the answer.

**Precedent.** This is consistent with what the repository already does: `CodecBench`,
`PlannerBench` and the `OCCAM_GATE` conditional all put verification affordances in the product
assembly so the shipped binary can prove things about itself.

## Alternatives considered

**`Microsoft.Extensions.TimeProvider.Testing`.** Rejected: it would cover the xunit suite and leave
the shipped selftest unable to move a clock, which is the harder and more valuable half.

**`internal` clock plus `InternalsVisibleTo`.** Rejected: `InternalsVisibleTo` already exists here
for `L0Gate` and `Abr512`, and extending that pattern hides the type from the CLI verb that needs
it — the verb is in the same assembly, so this would work, but the type is genuinely part of the
canary's public testing surface and pretending otherwise adds no safety.

**Static mutable "now" hook.** Rejected: process-global mutable time makes parallel test execution
unsound, and xunit runs collections in parallel by default.
