# PR-A spike results

## Scope and method

All prototypes live in `benchmarks/rc2-regression/TechnicalSpikes.cs`. They are isolated from DI, transport registration, workers, production models, and public schemas. Measurements are diagnostic samples from one Windows Release run, not performance guarantees.

## SectionIndex representation

- Result: a lightweight heading/anchor index over existing Markdown is feasible with deterministic duplicate-anchor suffixes.
- Measured cost: 24 sections, about 14,092 microseconds on the final regex-initializing run, 35,864 allocated bytes.
- Architectural implication: offsets, levels, labels, and anchors can be carried as bounded records without embeddings or an LLM.
- AOT implication: the spike uses source-generated regex plus records; final PR-D must still prove trimming and worker-to-Core serialization.
- Unresolved risk: reliable anchors should come from extracted DOM/IR where available, not be reconstructed from lossy Markdown alone. Encoded fragments and collisions need property tests.
- Recommendation: proceed to PR-D with an IR-backed index; keep Markdown reconstruction only as a fallback/test oracle.

## AccessEvidence model

- Result: direct evidence can produce `login_likely` while authentication prose alone remains `unknown`.
- Measured cost: 10,000 classifications in about 2.29 ms with 86,648 allocated bytes for the test result array; the classifier itself is branch-only.
- Evidence shape: status, login redirect, password control, and blocking form. No matched values or host allowlist are stored.
- Architectural implication: probe and transcode can share one pure classifier if workers expose bounded DOM facts.
- Unresolved risk: non-password identity walls and absence of DOM evidence require an explicit `unknown` policy and a broader labeled corpus.
- Recommendation: proceed to PR-C after the worker signal serialization/AOT contract is proven; do not grant hard-decision authority to prose.

## Serialized budget accounting

- Result: projection-first inventory gives hidden fields a cost of zero and preserves an explicit Markdown floor.
- Measured example: markdown-only projected sidecars cost 0 tokens; blocks cost 480; all modeled sidecars cost 1,028; a 700-token request retains the 128-token floor.
- Architectural implication: construct the public projection before `BudgetOwnership.PrepareSurfaceBudget`, then charge only emitted buckets.
- Unresolved risk: the final estimator must include the actual JSON envelope/receipt representation and be calibrated against serialized tokens; a simple integer bucket model is not acceptance evidence.
- Recommendation: proceed to PR-E after PR-D stabilizes answer-bearing units. Keep projection accounting separate from semantic section selection.

## Lifecycle spike

The characterization harness records owner, parent PID, child PID, endpoint, and session and proves targeted removal leaves an unrelated host tree alive. This validates the test model, not production control. No global kill or singleton enforcement is recommended. Production automation remains PR-G.

## Proceed decisions

| Spike | Proceed? | Next PR | Condition |
|---|---|---|---|
| SectionIndex | Yes | PR-D | Use canonical IR/DOM anchors and add platform determinism tests |
| AccessEvidence | Yes | PR-C | Prove bounded worker signals, redaction, and AOT serialization |
| Serialized budget | Yes | PR-E | Project first and calibrate against real serialized payloads |
| Lifecycle identity | Design only | PR-G | Confirm host integration callbacks and exact ownership semantics |
