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
