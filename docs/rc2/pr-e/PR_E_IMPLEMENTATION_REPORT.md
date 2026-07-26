# PR-E projection-first budget implementation report

## Scope

PR-E owns D10, the budget portion of D11, and C10b. It implements INV-7 and INV-8 without publishing the
full PR-F semantic response.

## Production changes

- `ResponseProjection` distinguishes raw extraction inventory from the fields eligible for serialization.
- `BudgetOwnership` rejects unmarked raw inventory as a public charge.
- `MaterializationRequest` carries explicit projection flags for every budgeted sidecar family.
- `TranscodePipeline` projects before allocation and reuses the same projection after reconciliation.
- `AnswerUnitSelector` protects heading, explanatory prose, and coupled structured evidence.
- `MaterializationAssessment` distinguishes retained partial focus from found-but-incomplete focus.
- `ResponseBudgetDiagnostics` retains calibration and completeness state for PR-F.

No public parameter or legacy response field was removed. The codec boundary remains unchanged: codecs
receive a completed `MaterializedKnowledgeView` and own presentation only.

## Compatibility and limitations

`compile.truncated`, `compile.omitted`, and `compile.budget` remain the current public truncation surface.
The new dimensioned completeness state is intentionally internal until PR-F adds the public fields and
migration documentation. Stable envelope metadata is outside the existing per-bucket allocation; the
focused calibration measures the projected payload represented by `compile.budget`.
