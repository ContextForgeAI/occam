# Q2 scoped comparison decision

Date: 2026-09-07. Protocol: `scripts/bench/README.md` § Q2.
WRB pin: `52025c304f6cdd242eb6d3fef2f0cb3700838fbd`.

## What ran

Same machine, fetch-only, competitor CLI installed via `install your external CLI (BYO binary)`
(external CLI **3.6.7**, git `24e6ee8`). Occam tree `6bc19e5c7c26` (workspace MCP
host, long-lived). Search and crawl arms were not run.

| Arm | Retrieval | T1 / T2 / T3 | FP | p50 / p90 (success) |
|-----|-----------|--------------|----|---------------------|
| Occam | **36/48** (75.0%) | 100% / 75.0% / 38.5% | 0 | 2109 / 4313 ms |
| external CLI | **42/48** (87.5%) | 100% / 100% / 53.8% | 0 | 1453 / 5918 ms |

Scorecard: [scorecard.md](scorecard.md). Raw JSON is gitignored under
`artifacts/wrb/52025c304f6c/results/`.

Latency is not a like-for-like process model: Occam reused one MCP host;
external CLI spawned a new CLI process per URL. p50/p90 are success-conditioned.

## Where the six-URL gap is

Occam missed and external CLI retrieved (substring probe hit):

| Tier | Class | URL |
|------|-------|-----|
| 2 | stackoverflow | four canonical SO question pages |
| 3 | cloudflare | `indeed.com` jobs search |
| 3 | cloudflare | `reuters.com` AI topic page |

Both missed: Crunchbase ×3, Kayak, Etsy listing, Target product page.

external CLI had no URL that Occam retrieved and external CLI missed.

The overall gap is **12.5 percentage points** (6/48). That exceeds the plan's
"investigate if >5 pp" trigger. The misses are **gated acquisition**, not
Tier-1 documentation extract. On this frozen run Stack Overflow failed as
`http_403` because the browser worker abort-ed on navigation status before
DOM quality. That fail-fast is removed in-tree (P0); these 36/48 numbers
are not a re-run. Reuters was already documented as a CloudFront shell.

## Decision

| Question | Answer |
|----------|--------|
| Declare parity? | **No.** |
| Target segment where Occam is not worse | WRB **Tier 1** (plain docs / wiki / arXiv / MDN / RFCs): **100% = 100%**. |
| Where external CLI is ahead | WRB Tier 2/3 acquisition (SO + two Cloudflare walls). |
| Start a TLS/HTTP2 rewrite? | **No** — plan §7 still defers a proprietary stack copy. The evidence is a short list of anti-bot hosts, not a global extract-quality deficit. |
| Change search default? | **No.** Search arm not measured. Do not set `OCCAM_SEARCH_PROVIDER=external_cli`. |
| Next engine investment if any | **P0+P1 measured.** Occam in-tree WRB is still **36/48**. external CLI `archive=off` is **39/48** (frozen 42/48 used `archive=auto`). Fair live gap is 3 URL (one SO + Indeed + Reuters), not 6. Cookie retry is in the ladder; it did not close those three. TLS rewrite still not the next step. |

Do not cite 75% or 87.5% as a product-wide success rate. WRB tiers are WRB's,
not Occam document classes. Token medians are WRB `chars/4`, not tiktoken.

Reproduce:

```bash
# Windows: WRB's runner prepends ~/.npm-global/bin (Unix). Point at the .exe:
#   set EXTERNAL_SEARCH_PATH=%AppData%\npm\node_modules\external_cli\binaries\external_cli.exe
# Frozen 36/48 used the default outputs occam.json / external_cli.json.
# P1 measurement (in-tree host, external CLI archive=off):
#   set OCCAM_FORCE_DOTNET_RUN=1
#   set OCCAM_WRB_RETAIN_COMPILE=1
node scripts/bench/run-wrb.mjs --fetch-only --verbose --output occam-p1.json
node scripts/bench/run-wrb.mjs --runner=external_cli --fetch-only --verbose --output external_cli-archive-off.json
node scripts/bench/compare-wrb.mjs \
  artifacts/wrb/52025c304f6c/results/occam.json \
  artifacts/wrb/52025c304f6c/results/external_cli.json
```
