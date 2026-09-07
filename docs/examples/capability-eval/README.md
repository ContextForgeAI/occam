# Capability evaluation

Two gated spikes from the 2026-09-05 growth plan. Neither adds an MCP tool.

| ID | Question | Result |
|----|----------|--------|
| Q2 | Is Occam worse than DonSeTch on WRB fetch? | **On gated T2/T3, yes** (36/48 vs 42/48). Tier 1 tied. No parity. [Decision](q2-wrb/). |
| A1 | Does cheap ranking put useful sources earlier? | Yes on three **held-out** SERP fixtures. Used by `occam research` only. [`occam_search`](../../tools/occam_search.md) default order is unchanged. |
| A2 | Should Occam ship a bundled OCR / table-PDF engine? | **No.** Score current text-layer + opt-in helper, then estimate later work. [PDF/OCR decision](pdf-ocr/). |

## A1 — discovery ranking

Ranker: `scripts/lib/operator/discovery-rank.mjs` (lexical 0.55 + extractability
0.37 + docs-path bonus 0.08). Provider identity stays on the hit.

Held-out tasks live in
`scripts/lib/operator/fixtures/discovery/held-out.jsonl`. Do not tune the
ranker against that file.

Re-run:

```bash
node scripts/lib/operator/discovery-rank.selftest.mjs
```

Latest local run (2026-09-06, n=3, K=3):

| Metric | Baseline order | Ranked |
|--------|----------------:|-------:|
| P@3 | 0.44 | 0.67 |
| R@3 | 0.67 | 1.00 |
| Time-to-useful-source (1-based rank) | 2.00 | 1.00 |

This is a fixture spike, not a live-web quality claim.

## A2 — PDF / OCR / difficult acquisition

See [the decision page](pdf-ocr/). Occam does not ship an OCR engine.

## Q2 — WRB vs DonSeTch

See [the decision page](q2-wrb/) and [scorecard](q2-wrb/scorecard.md). Fetch-only
head-to-head ran 2026-09-07. Do not declare parity. Search and crawl were not
measured.
