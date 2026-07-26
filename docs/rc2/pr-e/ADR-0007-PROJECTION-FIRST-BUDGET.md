# ADR-0007: Public response budget follows serialized projection

## Status

Accepted for RC.2 PR-E.

## Decision

Whole-response allocation consumes an explicitly marked public projection, never raw extraction
inventory. The projection is derived from the request before `MaterializationPlanner.Plan`; unrequested
blocks, tables, chunks, feed data, and screenshots receive zero allocation. Media remains part of the
default public projection because `mediaRefs` is an always-on response field when media exists.

The materialization planner protects a minimum answer unit: the selected heading, a bounded explanatory
body, and tightly coupled list/table/code evidence when present. If the target is found but that unit is
not retained, internal state records `Incomplete`, `focus_body_truncated`, and a bounded suggested minimum.
PR-F owns the additive public mapping of this state.

`max_tokens` is never enlarged. The same projected inventory drives pre-allocation and post-plan trim.
Codecs serialize the completed view and do not select, drop, or reprioritize knowledge.

## Consequences

- Raw internal IR remains available to the planner without public token cost.
- Requested structured fields can reduce the Markdown share; hidden fields cannot.
- Budget diagnostics retain estimated projection size, bucket contributions, retries, selected answer
  unit size, and completeness for later public mapping.
- The existing heuristic estimator remains provenance-labeled; calibration uses an explicit tolerance of
  `max(3%, 16 tokens)` for the projected payload measurement.

## Rejected alternatives

- Charging every extracted sidecar before request projection.
- Silently increasing `max_tokens` to rescue a focused answer.
- Moving planning policy into a codec.
- Returning a heading-only focus result as complete.
