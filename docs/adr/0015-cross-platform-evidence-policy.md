# ADR-0015 — Cross-platform claims require committed artefacts

**Status:** Accepted
**Date:** 2026-09-15

## Context

"Works on Windows, macOS and Linux" is the single easiest false claim to make about a .NET project.
The target framework implies it, the SDK does not complain, and nobody checks. The existing
documentation discipline in this repository already forbids unmeasured claims about extraction
success rates; platform support had no equivalent rule.

The concrete risk is not theoretical. The cross-platform run that accompanied this decision found a
real defect that every local check had passed: `Utf8JsonWriter` with `Indented = true` defaults its
newline to `Environment.NewLine`, so the published canary vector file was CRLF on Windows and LF
elsewhere. All eight sentinels matched on every platform — the *semantic* check was green — while
the artefact the protocol publishes as byte-portable was not.

## Decision

A statement about platform support in this repository must resolve to a committed artefact.

**The rule.** For each supported platform, `docs/testing/<platform>/` holds: `runtime-info.txt`,
`build.log`, `test.log`, `coverage-summary.txt`, `canary-smoke.log`, `canary-vectors.log`,
`aot-publish.log`, `binary-size.txt`, `hash.txt`, `canary-aot.log`. `docs/testing/RESULTS.md`
summarises them and carries a mandatory **"Explicitly not verified"** section.

**Consequences for wording.** No "should work on". A platform is either in the table with logs, or
it is in the not-verified list with a reason. `docs/testing/status.md` records which machines were
reachable, so an unreachable host produces a visible gap instead of a missing row.

**Byte-level, not just semantic.** Where the protocol publishes a file as portable, the check
compares the file's SHA-256 across platforms — not just the values inside it. That distinction is
what caught the newline defect.

**Artefact hygiene.** Raw Cobertura reports (~15 MB each) are *not* committed; a derived
`coverage-summary.txt` is. Evidence should be readable in a diff.

## Consequences

**Good.** The README's platform claim is checkable by a stranger. A regression on one platform
surfaces as a diff in a log file. The policy paid for itself immediately by finding the newline bug.

**Costs.** Real cost: a platform claim now requires access to that platform. This repository is
verified on three and explicitly unverified on Linux arm64, macOS x64, Windows arm64, musl and
FreeBSD — including the concrete note that the published Linux binary is glibc-linked and will not
run on Alpine.

**Interaction with CI.** GitHub Actions matrices cover the same three OS families and are the right
place for continuous enforcement. CI runs are not a substitute for the committed artefacts: a CI log
expires, and a reader of the repository at a given commit should be able to see what was verified at
*that* commit.

## Alternatives considered

**Trust the CI matrix alone.** Rejected: CI logs are ephemeral and are not visible to someone
reading the repository at a tag. The artefacts are the durable record.

**Test only on the maintainer's machine and say so.** Honest, and much weaker. Since SSH access to
all three platforms exists, using it costs one script.

**Commit the raw coverage XML for full auditability.** Rejected: ~45 MB of generated XML across
three platforms for data that a summary conveys. The raw reports stay under `artifacts/`, which is
gitignored, and can be regenerated in one command.
