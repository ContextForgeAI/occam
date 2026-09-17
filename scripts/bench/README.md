# FF-Occam benchmark harness

Reproducible, resumable benchmark for Occam extraction vs raw `fetch` vs Firecrawl.
**Purpose is quality discovery (find pages where Occam does the wrong thing), not marketing numbers.**
A "bad" run (many failures) is the *most* valuable — it's the bug list. The durable assets here
(this harness + the corpus + the golden anchor + the quality ledger) survive any single run's outcome.

## Files

| File | Role |
|------|------|
| `build-corpus.mjs` | Build a content-focused corpus from a Tranco CSV (drops CDN/API/DNS infra domains). |
| `sweep.mjs` | Resumable 3-arm sweep (Occam / raw fetch / Firecrawl). Appends one JSON line per URL. |
| `summarize.mjs` | Aggregate a `results.jsonl` into honest stats + flag trust-model violations. |
| `run-wrb.mjs` | Run the external Web Research Benchmark at a pinned revision with the Occam or external CLI runner. |
| `compare-wrb.mjs` | Render two WRB JSON results as a direction-aware Markdown scorecard. |
| `wrb/occam.py` | WRB adapter: transcode → fetch, search → search, map → crawl URL-discovery proxy. |
| `package.json` | Declares the only dep (`tiktoken`, o200k tokenizer). `npm install` here before first run. |

Inputs/outputs live outside git: corpus in `corpora/bench-1k.jsonl` (tracked), raw run data in
`artifacts/<out>/results.jsonl` (gitignored — large, non-deterministic). Commit the *summary* + the
*golden set*, not the raw sweep.

## Run

```bash
cd scripts/bench && npm install            # once — installs tiktoken
cd ../..                                    # back to repo root

# 1. Build corpus (Tranco CSV must be at artifacts/bench-scratch/tranco-topN.csv)
node scripts/bench/build-corpus.mjs --listid=XNW4N --want=1000 --pull=6000

# 2. Sweep (resumable — re-run to continue; skips done ids). Runs for hours at scale.
node scripts/bench/sweep.mjs --corpus=corpora/bench-1k.jsonl --count=1000 --out=bench-1k-<date>

# 3. Aggregate
node scripts/bench/summarize.mjs artifacts/bench-1k-<date>/results.jsonl
```

Firecrawl key: read from `.secrets/benchmark.env` (`FIRECRAWL_API_KEY=...`, gitignored). Rotate after use.

## Cross-project WRB scorecard

WRB is maintained in a separate repository. The launcher pins commit
`52025c304f6cdd242eb6d3fef2f0cb3700838fbd`, checks it out under
`artifacts/wrb/`, injects the Occam adapter, and records JSON results there.
Each result is stamped with the full WRB and Occam revisions plus runner
configuration. Pinning prevents an upstream task edit from silently changing a
comparison; the scorecard rejects results stamped with different WRB revisions.

```bash
# Contract self-test (no network, fake MCP host)
python3 scripts/bench/wrb/occam_runner_selftest.py

# Fetch only — the first useful capability comparison
node scripts/bench/run-wrb.mjs --fetch-only --verbose

# Full current surface. Unconfigured occam_search fails honestly.
node scripts/bench/run-wrb.mjs --verbose

# Same WRB revision with the competitor's native runner (external_cli on PATH)
# In-tree host (not the published AOT) + retain transcode debug fields:
#   OCCAM_FORCE_DOTNET_RUN=1 OCCAM_WRB_RETAIN_COMPILE=1
node scripts/bench/run-wrb.mjs --fetch-only --verbose --output occam-p1.json

# external CLI live-only (scripts/bench/wrb/external_cli.py forces --archive off)
node scripts/bench/run-wrb.mjs --runner=external_cli --verbose

# Compare the two saved results
node scripts/bench/compare-wrb.mjs \
  artifacts/wrb/52025c304f6c/results/occam.json \
  artifacts/wrb/52025c304f6c/results/external_cli.json
```

Set `OCCAM_SEARCH_PROVIDER` only when the scorecard explicitly declares that
configuration. When unset, Occam's search arm uses keyless DuckDuckGo HTML
(`provider=duckduckgo`) — record that provider in the run notes. Do not set
`OCCAM_SEARCH_PROVIDER=external_cli` for an Occam-vs-external-CLI search comparison:
that would benchmark external CLI through Occam and produce circular evidence.
For a fair vs-competitor search arm, either both sides use their native
keyless defaults, or both use an explicitly declared dedicated backend.

The adapter uses one long-lived MCP host, matching normal Occam use. Fetch maps
to `occam_transcode`. Search maps to `occam_search`. WRB's crawl slot currently
maps to focused `occam_map` (sitemap first, then homepage fallback), so it
measures URL discovery and exposes the resumable-crawl gap; it must not be
reported as full crawl parity. The fetch adapter also retains `backend`,
`final_url`, and `failure_code` in its runner response for direct diagnostics.
Set `OCCAM_WRB_RETAIN_COMPILE=1` to also keep `compile`, `completeness`, and
`focus` from the MCP payload (raw JSON only; the WRB scorecard still drops
extra fields).
On failures, `backend` comes from the signed negative receipt because the
top-level failure payload has no successful-content backend.
The pinned WRB report currently discards those extra fields, so preserve the
raw runner response when source-level evidence is required.

WRB uses deterministic substring probes and `chars / 4` token estimates. Its
repository and initial task set were created by the external CLI author. Report it
as reproducible comparative evidence, not independent certification or an
agent-answer-quality score.

### Known honest misses / anti-bot notes (2026-08-30)

- `https://www.leboncoin.fr/`: HTTP and **bundled Playwright Chromium** still
  receive a short DataDome `http_403` shell (~771 bytes). **System Chrome/Edge**
  (default when installed; `OCCAM_BROWSER_CHANNEL=chrome` or auto-prefer) opens
  the real page. Multilingual CMP dismiss plus consent-dialog HTML strip keep
  Readability off the French cookie wall so the homepage Markdown includes
  listings chrome (e.g. « Déposer une annonce »), not CMP prose. No site-specific
  host branch and no public sanctioned adapter. Re-checked on the pinned WRB
  fetch arm (2026-08-30, same pin): LeBonCoin is a true_positive when a system
  Chrome/Edge is available; headline retrieval can stay flat if another Tier-3
  URL (e.g. Kayak) flips on the same run. Reuters remains an honest miss.
  Browser-path CMP dismiss is time-budgeted so a miss does not dominate
  successful-fetch latency tails; Wikipedia-scale HTTP extracts still set p90
  on this corpus when system Chrome makes LeBonCoin a success.
- `https://www.reuters.com/technology/artificial-intelligence/`: HTTP and
  browser both received a 401 CloudFront shell (about 771 bytes). Reuters'
  public topic sitemap names this category but contains no category articles;
  content feeds and APIs are authenticated or unavailable. Mapping the request
  to the sitemap or unrelated AI news would be a false benchmark success.
  Remains an honest miss.

## README historical fetch table (2026-08-30)

The root README cites one pinned WRB **fetch** run: overall **36/48**, plus
WRB-assigned tier rates and success-conditioned latency. Those tiers are
**WRB's**, not Occam's document classes. Corpus denominators (same 48-URL
WRB fetch set): Tier 1 **19**, Tier 2 **16**, Tier 3 **13**.

Do not treat 75% as a product-wide success headline. Do not mix WRB crawl
scores with extract quality: crawl is an `occam_map` discovery proxy.

### Fresh fetch-only arm (2026-09-05)

Same WRB pin `52025c30…`, Occam tree `6bc19e5`, one long-lived MCP host,
`--fetch-only`, `OCCAM_WRB_RETAIN_COMPILE=1`. Result:
`artifacts/wrb/52025c304f6c/results/occam.json`.

| Observation | 2026-08-30 README | 2026-09-05 re-check |
|-------------|------------------:|--------------------:|
| Overall retrieval | 36/48 | 36/48 |
| Tier 1 / 2 / 3 | 100% / 75% / 38.5% | 100% (19/19) / 75% (12/16) / 38.5% (5/13) |
| False-positive rate | 0.0% | 0.0% (36 TP, 12 TN) |
| Successful fetch p50 | 630 ms | **2141 ms** |
| Successful fetch p90 | 1,973 ms | 4,359 ms |

Honest misses unchanged in kind on the Occam arm: Stack Overflow (T2),
Crunchbase, Kayak, Indeed, Reuters, Etsy, Target. LeBonCoin succeeded with
system Chrome. Search/crawl arms were not run. Latency is
success-conditioned; do not hide the p50/p90 rise in an overall average.
Investigate before treating the August speed line as current.

A 2026-09-07 Occam refresh on the same pin still reads **36/48** (p50 2109 ms,
p90 4313 ms). external CLI 3.6.7 fetch-only on the same machine is **42/48**.
See the Q2 decision below.

## Q2 scoped comparison protocol

Reuse this harness. Do not add a second dashboard.

1. Pin Occam, WRB (`52025c304f6cdd242eb6d3fef2f0cb3700838fbd`), browser, and
   tokenizer. Record cold vs warm separately.
2. Fetch-only arm first (`--fetch-only`). Keep native keyless search as a
   **separate** arm. Never set `OCCAM_SEARCH_PROVIDER=external_cli` in a
   head-to-head discovery comparison.
3. Count tokens with the same tokenizer on both sides. Preserve wall and
   challenge failures; report success-conditioned latency and failure rate
   apart from task completion.
4. Semantic retention is gated in `benchmarks/l0-gate` frozen fixtures
   (`fixtures/semantic/`), not in WRB substring checks.
5. After a fresh run, decide the target segment from the raw artifacts —
   do not pre-declare parity.

**2026-09-07:** competitor fetch-only arm ran (external CLI **3.6.7**, git
`24e6ee8`). Occam 36/48 vs external CLI 42/48. Do not declare parity. The six-URL
gap is gated acquisition (four Stack Overflow pages, Indeed, Reuters), not
Tier-1 documentation extract. Search and crawl were not measured. On Windows
the WRB external CLI runner prepends `~/.npm-global/bin` (Unix); set
`EXTERNAL_SEARCH_PATH` to the npm `external_cli.exe` if `external_cli` is not on `PATH`.
Do not set `OCCAM_SEARCH_PROVIDER=external_cli`. Decision:
[docs/examples/capability-eval/q2-wrb/](../../docs/examples/capability-eval/q2-wrb/).

## Method notes (honesty rules — see HANDOFF §5c)

- Tokens via **tiktoken o200k_base**, never `length/4`.
- Occam failure code is read from `failure.code` (nested), not a top-level field.
- `content_found` = `ok && tokens >= 50` (a trust-model proxy: `ok:true` with near-zero content is a **suspect bug**, not a success).
- Corpus is Tranco-sourced but infra-filtered; bare homepages, so it under-represents deep article pages — slice/interpret accordingly.
- Publish the corpus + script + raw data **including where Occam loses** (e.g. `http_403`/`tls_error` on anti-bot hosts). Cherry-picking kills OSS credibility.

## Latency benchmark (`extract-bench.mjs`)

A small, honest **latency** comparison for the **warm** extraction path — measured the way a long-lived
MCP host actually runs (the daemon persists), not a cold one-shot spawn. Separate from the quality sweep
above.

```bash
node scripts/bench/extract-bench.mjs                     # occam (warm daemon) vs jina vs raw fetch
node scripts/bench/extract-bench.mjs --runs=10 --no-jina --urls=my-urls.txt
```

It spawns the Occam HTTP daemon, **discards a warm-up run** per (engine, url), then times N runs and
reports **p50/p95 over successful runs only**, plus success-rate and median output size (fast-but-empty
is not a win). No API keys; Node 18+.

**Sample (5 doc pages × 4 runs, one laptop — your numbers will vary with network):**

| engine | success | p50 ms | p95 ms | min ms | median chars |
|--------|:---:|:---:|:---:|:---:|:---:|
| occam (warm daemon) | 100% | 944 | 2374 | 611 | 14025 |
| jina (hosted) | 100% | 497 | 4663 | 328 | 43126 |
| raw fetch (HTML, no extract) | 100% | 243 | 469 | 202 | 112716 |

**Honest read — we do NOT claim "fastest":**
- Occam is **~1s warm**, returns **small clean Markdown** (14k chars from a page whose raw HTML is 113k)
  **+ a signed receipt**, all **locally** — the URL and its content never leave the machine.
- Jina's **median is lower** (hosted, CDN-cached, prunes less → 3× more chars) but its **p95 tail is ~2×
  worse** and it is a **remote dependency** (privacy).
- `raw fetch` is the network floor (no extraction): most of Occam's time is the fetch itself; extraction
  adds a few hundred ms and shrinks 113k → 14k.

The honest pitch is therefore **competitive latency + local + honest failures + a verifiable receipt**,
not "fastest". `median chars` is an honesty column (did it extract substantial content), not
"bigger is better" — Occam deliberately returns the *smallest faithful* Markdown.

## Tranco list

Get a list id + CSV:
```bash
curl -sL "https://tranco-list.eu/api/lists/date/latest"     # -> {"list_id":"XNW4N",...}
curl -sL "https://tranco-list.eu/download/<list_id>/<N>" -o artifacts/bench-scratch/tranco-top<N>.csv
```
