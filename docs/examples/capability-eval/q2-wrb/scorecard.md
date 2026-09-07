# WRB scorecard: FF-Occam vs DonSeTch

Captured 2026-09-07 on one Windows machine. Fetch-only. Same WRB pin.
Raw JSON stays under gitignored `artifacts/wrb/52025c304f6c/results/`.

- FF-Occam: `occam.json` (2026-09-07T17:25:39), tree `6bc19e5c7c26`, long-lived MCP host, `OCCAM_WRB_RETAIN_COMPILE=1`
- DonSeTch: `donsetch.json` (2026-09-07T17:22:36), CLI `3.6.7` (`donsetch.exe`, git `24e6ee8`, npm global)
- WRB revision: `52025c304f6cdd242eb6d3fef2f0cb3700838fbd`

| Area | Metric | FF-Occam | DonSeTch | Better value |
|---|---|---:|---:|---|
| Fetch | retrieval | 75.0% | 87.5% | DonSeTch |
| Fetch | tier 1 | 100.0% | 100.0% | tie |
| Fetch | tier 2 | 75.0% | 100.0% | DonSeTch |
| Fetch | tier 3 | 38.5% | 53.8% | DonSeTch |
| Fetch | false positives | 0.0% | 0.0% | tie |
| Fetch | p50 | 2109 ms | 1453 ms | DonSeTch |
| Fetch | p90 | 4313 ms | 5918 ms | FF-Occam |
| Fetch | median tokens | 1250 | 1028 | DonSeTch |
| Search | — | not measured | not measured | not comparable |
| Crawl | — | not measured | not measured | not comparable |

Occam crawl slot remains an `occam_map` discovery proxy. Search was not run.
Do not set `OCCAM_SEARCH_PROVIDER=donsetch` in a discovery comparison.

WRB uses substring probes and `chars / 4` token estimates. This is comparative
evidence, not an agent-answer-quality benchmark and not a product-wide
success headline.
