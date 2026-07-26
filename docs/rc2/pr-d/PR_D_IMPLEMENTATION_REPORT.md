# PR-D structural focus implementation report

## Outcome

PR-D is independently complete. Constrained transcode focus now selects structured sections using stable
identities and traces. D15 numeric/wrong-section cases and the deterministically supported D17 exact
fragment are green; TOC/body distinction is demonstrable. No canonical IR replacement, embedding/LLM
dependency, public field removal, or site-specific rule was introduced.

Starting and ending commit: `a535705c03a9bf483cbbb23aefe8c4e60cf7b48f`. The worktree remains
uncommitted by owner instruction.

## Architecture

`SectionIndex` parses the existing Markdown surface into hierarchical, bounded section records.
`SectionRanker` owns technical-token scoring, exact fragment/anchor priority, TOC penalties, deterministic
tie-breaking, and diagnostic traces. `TokenBudget` consumes the selected section while preserving its
existing truncation strategy/marker compatibility.

`FocusIntent` removes the fragment before preflight, robots checks, routing, and extraction, then passes
the decoded fragment through internal transcode/materialization options. It is not a new MCP parameter.
Global definitional-anchor recovery is constrained to the winning section, preventing a losing repeated
term from being reattached after structural selection.

## Changed areas

- Production: new `Compile/SectionIndex.cs` and `Compile/FocusIntent.cs`; `TokenBudget`,
  `TranscodeCompiler`, internal options/materialization request, and `TranscodePipeline` wiring.
- Validation: focused PR-D cases/cumulative mode, ordinary L0 SectionIndex units, and a test-only frozen
  RC.1 focus adapter so characterization remains meaningful after production behavior changes.
- Contract/docs: MCP/tool guidance, changelog, ADR/design, implementation status, and this report pair.

PR-A owner inputs and prior PR-B/PR-C work are preserved. Frozen RC.1 evidence and the root RC.1 host were
not modified.

## Compatibility and scope

Existing calls without a response constraint remain byte-compatible because no selection is needed.
Existing `focus_query` calls gain deterministic structural ranking when a constrained surface must be
selected. URL fragments are no longer sent to the backend, matching HTTP semantics; they become local
focus intent. No public response dimension is added before PR-F.

D11's structural fixture and C10b's answer presence became green as consequences of correct section
selection. PR-E still owns projection-first accounting, minimum answer-unit retention under tighter
budgets, and explicit completeness. Two D10 assertions therefore remain red.

## Known limitations

- Missing-fragment public behavior has no dedicated frozen D17 archive; only internal `Miss` is defined.
- Source anchors are preserved when represented in Markdown; broader DOM-native identity propagation is
  a future compatible refinement.
- The current index stores section strings rather than only spans; measured impact is bounded in the
  validation report.
- macOS/Linux AOT validation remains PR-H work.
