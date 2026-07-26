# PR-F semantic result contract — implementation report

## Scope

PR-F owns INV-9 public mapping: transport, access, usability, focus, completeness, and verdict are
independent dimensions. Digest boundary (PR-B), AccessClassifier (PR-C), SectionIndex (PR-D), and
planner/budget logic (PR-E) were not redesigned; PR-F only publishes their existing internal truth and
splits overloaded recovery/`ok` meanings.

## Production changes

- `TranscodeAttempt` adds `TransportOk`, `Usable`, `FailureCode`, and `EscalationReason`. Legacy `Ok`
  remains the transport-completion alias.
- `OccamRouter` records per-attempt usability from the existing success policy and stamps escalation
  reasons on later attempts without rewriting prior entries.
- `Semantics/SemanticOutcomeMapper` maps PR-C `AccessAssessment` and PR-E `MaterializationAssessment`
  onto additive public `access` / `focus` / `completeness` objects.
- Transcode success/failure, probe success, and digest items publish those additive fields. Transcode
  also emits `verdict: not_evaluated`.
- `occam_claim_check` adds `retrieved` (alias of `found`) and `verdict: not_evaluated`.
- Agent hints append structured completeness/focus/access warnings derived from those fields.
- `TranscodePipeline` preserves `Access` / `AccessAssessment` across materialize rebuilds so the public
  access dimension is not dropped on the success path.

## Compatibility

No public field was removed. Legacy `ok`, `confidence`, `focusMatched`, and `found` retain their
previous meanings. Migration guidance lives in `LEGACY_FIELD_MIGRATION.md`.

## Limitations

- Focus confidence is not yet a separate scoped score; extraction `confidence` remains extraction-only.
- Digest focus status falls back to lexical `focusMatched` mapping when materialization assessment is
  absent.
- Claim tools remain retrieval-only; semantic support/refute stays with `occam_attest`.
