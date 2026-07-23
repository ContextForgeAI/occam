# Quality baseline

How maintainers judge extraction quality beyond automated gates — using only
inputs that ship in a public clone.

---

## Canonical baseline

| Field | Value |
|-------|-------|
| **Baseline date** | 2026-06-17 |
| **Reference audit era** | post-L0 consolidation / eight-tool surface (product now ships **15** core tools — extend rotation accordingly) |
| **Public claim** | Frozen golden cases must not regress ≥2 rubric points; rotation catches overfitting |
| **How to reproduce** | Commands below + `corpora/quality-audit-agent.PROMPT.md` |

Raw dated report markdown is optional private evidence and is **not** required for a
clean public clone. Prefer writing new drafts under `artifacts/quality-audit/` (gitignored).

---

## Frozen golden cases (must not regress ≥2 rubric points)

| id | Layer | Focus |
|----|-------|-------|
| `mdn-js-guide` | frozen | HTTP TOC / structure |
| `nginx-doc` | frozen | Noise prune (no ngx spam) |
| `nuxt-spa` | frozen | Footer / promo strip |
| `openai-concepts` | frozen | Struct sections (Embeddings, Tokens) |
| `not-found` | frozen | Honest `http_404` |

Inputs: `corpora/visual-matrix.jsonl`, `corpora/l0-smoke.jsonl`, L9 fixtures under
`benchmarks/l0-gate/fixtures/golden/` ([sources](maintenance/FIXTURE_SOURCES.md)).

---

## Rotation corpus

**Agent audit:** `corpora/quality-audit-rotation.jsonl` — human-oriented cases with questions and rubric hints.

**Eval harness:** `corpora/eval-harness/quality-audit-rotation.jsonl` — machine-oriented `expectedOutcome` / `expectedFailureCode` rows for the npm eval runner.

Both are intentional; do not merge without updating both consumers.

---

## Running a full audit

1. `occam doctor` + reload MCP
2. Follow `corpora/quality-audit-agent.PROMPT.md` (tier-3)
3. Save drafts under `artifacts/quality-audit/<timestamp>/` (gitignored)
4. Commit reports only on explicit maintainer request

---

## Gate markers (tier-1)

Minimum before release claims:

```text
L0_GATE_FAST_OK   # .\scripts\run-l0-fast.ps1
L0_GATE_OK        # dotnet run --project benchmarks\l0-gate
```

Compatibility: `dotnet run --project benchmarks/rc2-regression -c Release -- --regression`
Full marker list: `AGENTS.md` §8.
