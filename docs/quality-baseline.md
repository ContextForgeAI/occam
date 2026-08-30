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
`benchmarks/l0-gate/fixtures/golden/` ([sources](https://github.com/ContextForgeAI/occam/blob/main/docs/maintenance/FIXTURE_SOURCES.md)).

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

## Cross-project benchmark

The maintainer harness can run the external Web Research Benchmark (WRB) with
an Occam adapter at a pinned upstream revision:

```bash
python3 scripts/bench/wrb/occam_runner_selftest.py
node scripts/bench/run-wrb.mjs --fetch-only --verbose
```

See the
[`scripts/bench/README.md` benchmark guide](https://github.com/ContextForgeAI/occam/blob/main/scripts/bench/README.md)
for the full
Occam-vs-DonSeTch run and scorecard commands. The adapter maps WRB fetch to
`occam_transcode`, search to `occam_search`, and the crawl slot to focused
`occam_map`. The last mapping measures URL discovery only; it is explicit
capability-gap evidence, not a claim that Occam ships resumable content crawl.

WRB is reproducible comparative evidence, not independent certification: its
repository and initial corpus were created by the DonSeTch author, it uses
deterministic substring probes, and it estimates tokens as `chars / 4`.
The runner retains the selected `backend`, actual `final_url`, and
`failure_code` for source-level diagnostics, although the pinned upstream
summary does not copy those extension fields into its result JSON.

The 2026-08-30 diagnostic re-check separates two LeBonCoin layers. HTTP and
**bundled Playwright Chromium** still see DataDome `http_403` shells; **system
Chrome/Edge** (now preferred when installed) opens the live page, and generic
multilingual CMP dismiss is required so the French cookie wall is not extracted
as the article. Reuters' AI category still returns 401 CloudFront shells with no
faithful public content surface — its topic sitemap identifies the category but
does not contain its articles. Neither URL has a host-specific branch or a
bundled source adapter; a sitemap or unrelated-content substitution would only
game WRB's substring probe. Live access behavior may change, so the detailed
evidence and re-run guidance stay in the
[benchmark harness documentation](https://github.com/ContextForgeAI/occam/blob/main/scripts/bench/README.md).

---

## Gate markers (tier-1)

Minimum before release claims:

```text
L0_GATE_FAST_OK   # .\scripts\run-l0-fast.ps1
L0_GATE_OK        # dotnet run --project benchmarks\l0-gate
```

Compatibility: `dotnet run --project benchmarks/rc2-regression -c Release -- --regression`
Full marker list: `AGENTS.md` §8.
