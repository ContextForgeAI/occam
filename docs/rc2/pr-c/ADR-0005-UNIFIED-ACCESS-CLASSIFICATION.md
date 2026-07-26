# ADR-0005: unified access classification

Status: accepted for RC.2 PR-C on 2026-07-22.

## Context

Probe and transcode previously made independent login decisions. Probe treated phrases such as
`authentication required` as decisive, transcode reclassified extracted Markdown, and the pipeline could
reject a login-like requested path before fetching it. Public authentication documentation therefore
produced false hard failures and the two tools could disagree.

## Decision

One pure `AccessClassifier` owns the decision for both tools. Its input is bounded structured evidence;
its output is `Open`, `Restricted`, or `Unknown`, with a scoped confidence, evidence stage, stable evidence
codes, and recommended action.

`Restricted` requires at least one direct signal:

- HTTP 401;
- an authentication challenge header;
- a redirect from the requested URL to a dedicated login route; or
- blocking identity UI: a password control plus identity/action/heading context, without usable content.

Usable public content produces `Open`. Authentication terminology, a password control without blocking
context, a login-like requested path, or insufficient evidence produces `Unknown`; none can independently
produce `requires_login`.

Workers collect bounded boolean DOM signals before destructive extraction. They do not return control
values, labels, page text, credentials, or cookies. The extracted-Markdown adapter is a conservative
fallback for worker-less paths.

## Consequences

- Probe and transcode cannot drift in their final access rule.
- Some former hard failures become non-terminal unknown/open outcomes.
- Existing public fields remain compatible in PR-C; PR-F owns the additive public semantic dimensions.
- The legacy classifiers remain only as frozen characterization seams until the RC.2 integration cleanup.
- Thresholds and adapters remain testable implementation details; direct-evidence ownership is the durable
  architectural constraint.

## Rejected alternatives

- Retuning authentication phrases: prose remains indistinguishable from a wall.
- Adding host exemptions: correctness would depend on an incomplete allowlist.
- Treating any login path or password control as decisive: both occur on public pages and widgets.
- Publishing raw DOM evidence: it would expand payloads and risk leaking sensitive values.

## Evidence

Acceptance is pinned by the PR-A characterization suite, PR-C focused D9/D19 cases, production L1b units,
and the worker evidence selftest. See [validation report](PR_C_VALIDATION_REPORT.md).
