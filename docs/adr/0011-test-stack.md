# ADR-0011 — Test stack: xunit, FsCheck, coverlet

**Status:** Accepted
**Date:** 2026-09-15

## Context

Before this decision the repository had no test project. Verification ran entirely through
`benchmarks/l0-gate`, a console application that performs live extraction against real URLs and
asserts by printing markers such as `L0_GATE_OK`.

That gate is valuable and stays: it catches the failures that matter most in this product (a real
site changed, a worker died, a browser pool leaked). But it cannot do three things:

1. **Run without the world.** It needs `npm ci`, Playwright Chromium and network access, so it
   cannot be the thing a contributor runs in thirty seconds, and it cannot verify a platform where
   installing a browser is out of scope.
2. **Report coverage.** It is not instrumented, so there is no number to hold a threshold against.
3. **Test properties.** Assertions like "no two distinct sessions ever share a sentinel" are
   statements about an input space, not about three examples.

The proof-of-read canary is deterministic, network-free cryptographic code — precisely the code that
should be covered by fast unit tests and generated inputs.

## Decision

Add `tests/OccamMcp.Core.Tests` with:

- **xunit 2.9** — the mature line, not xunit.v3. This is a first test project in an AOT-heavy
  repository; the goal was to remove variables, not to be current. xunit 2.9 works with
  `Microsoft.NET.Test.Sdk`, `coverlet.collector` and `FsCheck.Xunit` without version archaeology.
- **coverlet.collector** — `dotnet test --collect:"XPlat Code Coverage"` works identically on all
  three target platforms with no extra tooling, which is what makes the per-platform coverage
  comparison in `docs/testing/RESULTS.md` possible.
- **FsCheck.Xunit 3.3** — property-based tests. Specifically for the collision and injectivity
  properties of the canary, where example-based tests can only confirm the cases we thought of.

These are test-only dependencies: `PrivateAssets` where applicable, and nothing enters the shipped
AOT binary.

## Consequences

**Good.** 149 tests running in ~350 ms with no network, no browser and no Node. 91.4 % line and
75.6 % branch coverage on `OccamMcp.Core.Canary`, identical on all three platforms. A contributor
can verify a change without provisioning a browser.

**Costs.** Three new packages to keep current, and a second place where "run the tests" means
something. `docs/testing/README.md` documents the split so the two layers are not confused, since
the naive reading of a Cobertura report over this repository ("1.16 % covered") is badly misleading.

**Not decided here.** Whether the L0 gate should eventually be instrumented or partially folded into
xunit. That is a much larger change and needs its own record.

## Alternatives considered

**xunit.v3.** Rejected for now: newer, and the interaction with FsCheck and coverlet in this
configuration was unverified. Revisit once FsCheck ships first-class v3 support.

**NUnit / MSTest.** No technical objection; xunit is the de facto default for new .NET libraries and
the one a drive-by contributor is most likely to recognise.

**Hand-rolled property tests with a seeded PRNG.** Genuinely considered, because it adds zero
dependencies and the repository rule is that new dependencies need an ADR. Rejected because
shrinking is the valuable part of property testing: a hand-rolled generator reports "failed with
this 400-character string", while FsCheck reports the two-character counterexample.

**Extending the L0 gate with unit assertions.** This is what the repository did historically
(`benchmarks/l0-gate/*UnitTests.cs`). Rejected for new work: it produces no coverage data, has no
test discovery, and mixes network-dependent and deterministic checks in one exit code.
