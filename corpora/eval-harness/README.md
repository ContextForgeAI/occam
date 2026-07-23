# @ff-occam/eval-harness — Lighthouse for Web Extraction

Standardized evaluation harness for **any MCP server** implementing the FF-Occam tool contract. Run the same corpora, get the same metrics, compare apples-to-apples.

## Why?

Different extraction tools (FF-Occam, crawl4ai, firecrawl, jina.ai, etc.) use different APIs and benchmarks. This harness provides:

- **Standardized corpora** — `l0-smoke`, `l4-genome`, `quality-audit-rotation`
- **Unified metrics** — accuracy, latency, token efficiency, focus match honesty
- **MCP-native** — works with any MCP server via stdio or WebSocket
- **CI-ready** — GitHub Action template included

## Quick Start

```bash
# Install
cd corpora/eval-harness
npm install
npm run build

# Run against local AOT host (preferred for git clone / CI)
node dist/run.js --corpus=l0-smoke --server ../../OccamMcp.Core

# Or via the canonical launcher (resolves publish output under OCCAM_HOME)
node dist/run.js --corpus=l0-smoke --server node --args scripts/launch-mcp-host.mjs

# Run with config file
node dist/run.js --config=config.yaml
```

## Corpora

| Corpus | Description | Use Case |
|--------|-------------|----------|
| `l0-smoke` | 5 live URLs (MDN, Nginx, OpenAI, Nuxt, 404) | Fast smoke test (~30s) |
| `l4-genome` | Genome pilot + extract test cases | Playbook resolution validation |
| `quality-audit-rotation` | 9 monthly rotating URLs | Ongoing quality tracking |

## Configuration

Create `config.yaml`:

```yaml
mcpServer:
  command: "node"
  args: ["scripts/launch-mcp-host.mjs"]
  env:
    OCCAM_HOME: "."

corpora:
  - "l0-smoke"
  - "l4-genome"
  - "quality-audit-rotation"

output:
  dir: "artifacts/eval"
  format: "both"

thresholds:
  minAccuracy: 0.95
  maxAvgLatencyMs: 5000
  minFocusMatchHonesty: 0.8
```

## Metrics

| Metric | Description | Target |
|--------|-------------|--------|
| **Accuracy** | % of cases matching expected ok/failure | ≥95% (smoke) |
| **Avg Latency** | Mean transcode latency (ms) | ≤5s (smoke) |
| **Focus Match Honesty** | Does `focus_query` actually filter results? | ≥80% |
| **Failure Code Accuracy** | Correct failure codes on negative cases | 100% |

## Output

### JSON Report (`eval-report-<timestamp>.json`)

```json
{
  "timestamp": "2026-06-18T...",
  "summary": {
    "totalCases": 15,
    "passed": 14,
    "failed": 1,
    "accuracy": 0.93,
    "avgLatencyMs": 1234,
    "focusMatchHonesty": 0.87
  },
  "results": [...],
  "byCorpus": { ... }
}
```

### HTML Report (`eval-report-<timestamp>.html`)

Visual dashboard with:
- Summary cards (accuracy, latency, focus honesty)
- Per-case table with pass/fail status
- Per-corpus breakdown
- Expandable failure reasons

## CI Integration

Use the provided GitHub Action (`.github/workflows/eval-harness.yml`):

```yaml
# Runs on every push/PR
# Tests against multiple corpora in parallel
# Uploads HTML + JSON reports as artifacts
# Comments on PR with summary table
```

### Custom MCP Server

Test any MCP server:

```bash
# WebSocket server
npx @ff-occam/eval-harness --transport=websocket --ws-url=ws://localhost:5050

# Custom command
npx @ff-occam/eval-harness --server=./my-mcp-server --args="--stdio"
```

## Extending

### Add Custom Corpus

Create `my-corpus.jsonl` in `corpora/`:

```jsonl
{"url":"https://example.com","expectedBackend":"http","expectedOutcome:"ok","minMarkdownLength":100}
{"url":"https://example.com/404","expectedBackend":"http","expectedOutcome:"failure","expectedFailureCode":"http_404"}
```

Run:
```bash
npx @ff-occam/eval-harness --corpus=my-corpus
```

### Custom Thresholds

```bash
npx @ff-occam/eval-harness \
  --threshold-accuracy=0.90 \
  --threshold-latency=10000 \
  --threshold-focus=0.7
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
                    @ff-occam/eval-harness
├─────────────────────────────────────────────────────────────┤
  run.ts          →  CLI entry, MCP client, evaluation loop
  config.yaml     →  Default configuration
├─────────────────────────────────────────────────────────────┤
  corpora/        →  Standardized test corpora (JSONL)
    l0-smoke.jsonl
    l4-genome.jsonl
    quality-audit-rotation.jsonl
├─────────────────────────────────────────────────────────────┤
  .github/workflows/eval-harness.yml  →  CI template
  report.html     →  HTML report template
└─────────────────────────────────────────────────────────────┘
```

## Relationship to FF-Occam Gates

| Layer | FF-Occam Gate | Eval Harness |
|-------|---------------|--------------|
| L0 | `L0_GATE_OK` (merge-blocking) | `l0-smoke` corpus |
| L1 | Token/probe/failure taxonomy | Custom corpora |
| L2 | Digest/map/session/transport/egress | Custom corpora |
| L3 | Heal/learn | Not yet |
| L4 | Genome | `l4-genome` corpus |
| Quality | Tier-3 audit | `quality-audit-rotation` |

The eval harness **complements** gates — gates are merge-blocking CI; eval harness is for ongoing quality tracking and cross-server comparison.

## License

MIT — see [LICENSE](../../LICENSE)