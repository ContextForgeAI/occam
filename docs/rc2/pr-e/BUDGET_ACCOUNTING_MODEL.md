# PR-E budget accounting model

## Order of operations

1. Build the internal extraction inventory.
2. Derive `MaterializationRequest` and its public opt-in flags.
3. Project the inventory to fields the success response can serialize.
4. Allocate the whole-response budget and pass only the Markdown/surface share to the planner.
5. Protect the minimum answer unit, then add lower-priority context while space remains.
6. Reconcile structured evidence to returned Markdown and trim only the projected sidecars.
7. Record allocation and internal completeness diagnostics.

An unmarked `ResponseBudgetSidecars` value is raw inventory and is not chargeable. Only
`ResponseProjection.Project` produces chargeable inventory. This fail-closed distinction implements
INV-7 and prevents a future caller from accidentally billing hidden IR.

## Projection mapping

| Bucket | Included when |
|---|---|
| Markdown | Normal success response |
| Blocks | `json_blocks=true` or block diff requires them |
| Tables | `json_tables=true` |
| Chunks | `semantic_chunking=true` |
| Media | Media exists; `mediaRefs` is part of the default response |
| Feed | `json_feed=true` and feed extraction succeeds |
| Screenshot | `capture_screenshot=true` and browser capture succeeds |
| Receipt | The normal success receipt/telemetry path is expected |

## Answer-unit and completeness rules

The minimum unit contains the selected heading and the smallest explanatory body. A nearby list, table,
or code block and its short label are coupled to that unit. Lower-priority context is appended only when
it fits. Increasing the budget must not remove the protected unit.

Internal completeness is:

- `Complete` when the selected answer and context are not truncated;
- `Partial` with `context_truncated` when the answer unit remains but wider context is cut;
- `Incomplete` with `focus_body_truncated` when the target was found but its answer unit was lost;
- `Incomplete` with `source_missing` when no focus target exists.

These states are internal in PR-E. PR-F maps them additively to the public semantic contract.

## Measurement

Focused validation records estimated allocation, a serialized projected-payload measurement, Markdown
and structured contributions, planner retries, answer-unit tokens, completeness, and planning latency.
The estimator tolerance is `max(3%, 16 tokens)`; it is measured, not used to expand a caller budget.
