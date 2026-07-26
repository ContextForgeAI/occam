# RC.2 implementation invariants

These invariants constrain every RC.2 implementation stage. A stage report must name the invariants
it exercises and its validation report must record evidence that they remain true.

| ID | Invariant |
|---|---|
| INV-1 | Probe and transcode must not make access decisions through independent classifiers. |
| INV-2 | Authentication terminology alone must never imply login-required. |
| INV-3 | Digest transport compatibility terminates at one normalization boundary. |
| INV-4 | Focus selection operates on structured sections and deterministic identities, not only flat Markdown text. |
| INV-5 | Fragment and anchor identity outrank fuzzy textual relevance when exact. |
| INV-6 | TOC entries must not outrank their corresponding body section merely because they occur earlier. |
| INV-7 | Budget accounting applies to fields that are actually serialized. |
| INV-8 | The planner preserves a minimum answer-bearing unit when it fits. |
| INV-9 | Transport success, access, usability, focus, completeness, and verdict are separate semantic dimensions. |
| INV-10 | Lifecycle operations are scoped to an explicit process/host identity. |

## Change control

- An implementation may strengthen an invariant but must not silently weaken or reinterpret it.
- If a stage cannot preserve an invariant, stop before the next stage and request the smallest owner
  decision needed.
- Compatibility aliases must terminate at an explicit boundary; they must not spread transport or
  legacy semantics into the domain model.
- Tests must remain deterministic and local unless a validation plan explicitly marks them as live.
