# ADR-0012 — Central package management and SDK pinning

**Status:** Accepted
**Date:** 2026-09-15

## Context

Package versions were declared inline in `src/FFOccamMcp.Core/FFOccamMcp.Core.csproj`. With one
project that is fine. The repository now has seven project files — the host, the L0 gate, three
benchmark projects, a docs-audit repro and a test project — and all of them compile against the same
`OccamMcp.Core` assembly. A version bump applied to one and not another produces a mismatch that
shows up as a confusing runtime failure in the gate rather than a build error.

Separately, there was no `global.json`. The cross-platform run surfaced why that matters: the Linux
host installed SDK **10.0.302** while macOS and Windows had **10.0.301**. Without a pin, that
difference is invisible until something behaves differently; with the wrong pin, the build simply
refuses to start on one of the three machines.

## Decision

**Central Package Management.** `Directory.Packages.props` with
`ManagePackageVersionsCentrally=true` and `CentralPackageTransitivePinningEnabled=true`. Project
files reference packages without a `Version` attribute.

**SDK pin with a deliberate roll-forward.** `global.json` pins `10.0.100` with
`rollForward: latestFeature` and `allowPrerelease: false`:

- it will not silently build on .NET 11;
- it accepts any .NET 10 feature band and patch, so 10.0.301 and 10.0.302 both work;
- it rejects prerelease SDKs, which are a common source of "works on my machine".

Transitive pinning is enabled because this host ships as a single Native AOT binary: an unpinned
transitive dependency is an unpinned part of a shipped artefact, and `dotnet list package
--vulnerable --include-transitive` only helps if the resolved graph is stable.

## Consequences

**Good.** One place to bump a version. A transitive CVE can be pinned out without waiting for a
direct dependency to update. The SDK pin is permissive enough that provisioning a fresh machine with
`dotnet-install.sh --channel 10.0` just works, which the cross-platform run confirmed on two hosts.

**Costs.** Adding a package now means editing two files. A contributor who has only seen inline
versions may be briefly confused by a `PackageReference` with no version — the comment in the
project file points at this record.

**Verified.** Restore, build, test and Native AOT publish all succeed under CPM on macOS arm64,
Linux x64 and Windows x64 (`docs/testing/RESULTS.md`).

## Alternatives considered

**Leave versions inline.** Rejected: the failure mode is a silent version skew across seven
projects, and it gets worse as the benchmark projects grow.

**`rollForward: disable` with an exact patch pin.** Rejected: it would have blocked the Linux host
outright, and pinning a patch version means every contributor must install that exact patch. The
value of a pin here is preventing a *major* drift, not a patch one.

**`rollForward: latestMajor`.** Rejected: it defeats the purpose. A .NET 11 SDK building a `net10.0`
target is exactly the kind of drift a pin should surface.

**Directory.Packages.props without transitive pinning.** Rejected: the product is a single static
binary, so transitive versions are part of what ships.
