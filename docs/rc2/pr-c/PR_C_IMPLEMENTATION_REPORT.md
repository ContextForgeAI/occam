# PR-C unified access classification implementation report

## Outcome

PR-C is independently complete. Probe and transcode now consume one evidence-based access classifier,
closing D9 and D19 without weakening real login-wall detection. Authentication prose and login-like
requested paths no longer produce hard login verdicts. No focus, budget, public semantic-envelope, or
lifecycle behavior was changed.

Starting and ending commit: `a535705c03a9bf483cbbb23aefe8c4e60cf7b48f`. The worktree remains
uncommitted by owner instruction.

## Architecture

`AccessEvidenceAdapters` translate bounded probe, worker, and conservative Markdown-fallback signals into
one `AccessEvidence`. The pure `AccessClassifier` returns one `AccessAssessment`. `HtmlProbeClassifier`
maps that assessment to existing probe signals; `RequiresLoginPostProcessor` stores the same assessment
and returns `requires_login` only for `Restricted`.

The unconditional transcode prefetch stop for login-like input paths was removed. A requested path is not
redirect evidence: the page is fetched and judged using direct signals. The legacy phrase/Markdown
classifiers remain reachable only by PR-A frozen characterization and pre-existing baseline tests.

HTTP probe captures `WWW-Authenticate`. HTTP and browser workers collect access evidence before DOM
pruning and carry it through the source-generated/AOT-safe worker model. Browser evidence additionally
confirms requested-to-final login redirects.

## Changed files

Production code includes the new `src/FFOccamMcp.Core/Access/` model/classifier/adapters; probe fetch and
classification files; the login postprocessor and transcode pipeline/outcome; worker C# models/mappers;
and HTTP/browser/shared worker modules.

Validation includes `benchmarks/rc2-regression/PrCAccessCases.cs`, cumulative PR-C wiring, production L1b
units, the worker evidence selftest, and its ordinary L0 gate registration. Contract/docs changes include
`MCP_API_SPEC.md`, failure/tool guides, `CHANGELOG.md`, this stage package, and implementation status.

The pre-existing owner changes to `.gitignore` and PR-A inputs were preserved. Frozen RC.1 evidence and
the root RC.1 host were not modified.

## Compatibility and privacy

This is a behavioral correctness change during prerelease, not a schema removal. Existing public response
shapes remain. Former false `requires_login` results can now succeed or remain non-terminal; confirmed
walls retain the same failure code and session remedy.

Workers return only bounded booleans. Stable evidence codes are suitable for diagnostics; source text,
form values, credentials, headers, and cookies are not included. See the
[evidence model](ACCESS_EVIDENCE_MODEL.md) and [ADR-0005](ADR-0005-UNIFIED-ACCESS-CLASSIFICATION.md).

## Known limitations

- PR-C does not publish the full `Open|Restricted|Unknown` dimension; that belongs to PR-F.
- The conservative Markdown fallback cannot prove every client-rendered wall. Ambiguity stays `Unknown`.
- The usable-content threshold is local and deterministic, not a universal quality metric.
- macOS/Linux Native AOT validation remains a PR-H concern; this stage directly validates win-x64.
- Eight expected-red assertions owned by PR-D through PR-F remain unchanged.
