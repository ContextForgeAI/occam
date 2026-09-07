# Q2 scoped comparison decision

Date: 2026-09-07. Protocol: `scripts/bench/README.md` § Q2.
WRB pin: `52025c304f6cdd242eb6d3fef2f0cb3700838fbd`.

## What ran

Same machine, fetch-only, competitor CLI installed via `npm install -g donsetch`
(DonSeTch **3.6.7**, git `24e6ee8`). Occam tree `6bc19e5c7c26` (workspace MCP
host, long-lived). Search and crawl arms were not run.

| Arm | Retrieval | T1 / T2 / T3 | FP | p50 / p90 (success) |
|-----|-----------|--------------|----|---------------------|
| Occam | **36/48** (75.0%) | 100% / 75.0% / 38.5% | 0 | 2109 / 4313 ms |
| DonSeTch | **42/48** (87.5%) | 100% / 100% / 53.8% | 0 | 1453 / 5918 ms |

Scorecard: [scorecard.md](scorecard.md). Raw JSON is gitignored under
`artifacts/wrb/52025c304f6c/results/`.

Latency is not a like-for-like process model: Occam reused one MCP host;
DonSeTch spawned a new CLI process per URL. p50/p90 are success-conditioned.

## Where the six-URL gap is

Occam missed and DonSeTch retrieved (substring probe hit):

| Tier | Class | URL |
|------|-------|-----|
| 2 | stackoverflow | four canonical SO question pages |
| 3 | cloudflare | `indeed.com` jobs search |
| 3 | cloudflare | `reuters.com` AI topic page |

Both missed: Crunchbase ×3, Kayak, Etsy listing, Target product page.

DonSeTch had no URL that Occam retrieved and DonSeTch missed.

The overall gap is **12.5 percentage points** (6/48). That exceeds the plan's
"investigate if >5 pp" trigger. The misses are **gated acquisition**, not
Tier-1 documentation extract. Stack Overflow remains an Occam honest miss
(`http_403` / challenge class in prior notes). Reuters was already documented
as a CloudFront shell.

## Decision

| Question | Answer |
|----------|--------|
| Declare parity? | **No.** |
| Target segment where Occam is not worse | WRB **Tier 1** (plain docs / wiki / arXiv / MDN / RFCs): **100% = 100%**. |
| Where DonSeTch is ahead | WRB Tier 2/3 acquisition (SO + two Cloudflare walls). |
| Start a TLS/HTTP2 rewrite? | **No** — plan §7 still defers a proprietary stack copy. The evidence is a short list of anti-bot hosts, not a global extract-quality deficit. |
| Change search default? | **No.** Search arm not measured. Do not set `OCCAM_SEARCH_PROVIDER=donsetch`. |
| Next engine investment if any | Honest SO/Reuters/Indeed acquisition (session / browser / documented limits), not OCR and not a universal crawler. |

Do not cite 75% or 87.5% as a product-wide success rate. WRB tiers are WRB's,
not Occam document classes. Token medians are WRB `chars/4`, not tiktoken.

Reproduce:

```bash
# Windows: WRB's runner prepends ~/.npm-global/bin (Unix). Point at the .exe:
#   set DONSETCH_PATH=%AppData%\npm\node_modules\donsetch\binaries\donsetch.exe
node scripts/bench/run-wrb.mjs --runner=donsetch --fetch-only --verbose
node scripts/bench/run-wrb.mjs --fetch-only --verbose
node scripts/bench/compare-wrb.mjs \
  artifacts/wrb/52025c304f6c/results/occam.json \
  artifacts/wrb/52025c304f6c/results/donsetch.json
```
