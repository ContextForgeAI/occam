# Samples

Runnable examples. Each one either works end to end or is not here.

| Sample | What it shows | Status |
|--------|---------------|--------|
| [`canary-demo/`](canary-demo/) | All four proof-of-read verdicts against a live probe server, over real HTTP | ✅ runnable |
| [`cascade-demo/`](cascade-demo/) | Cascade selftest marker + optional live `cascade run` | ✅ runnable |
| [`exam-harness/`](exam-harness/) | Offline exam grade → `OCCAM_PROFILE`; fixtures for weak/strong | ✅ runnable |

## Basic usage

There is no `basic-usage/` directory, because the basic usage is one command and duplicating it in a
sample folder would create a second thing to keep in sync:

```bash
occam read https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Functions \
  --focus "function scope closures" --fit --max-tokens 800
```

Recorded runs with prompts, settings, hashes and omissions live in
[`docs/examples/golden-workflows/`](../docs/examples/golden-workflows/) — those are captured against
named releases, which makes them more useful than a sample script.

## Cascade

Shipped as MCP tool `occam` + CLI `cascade selftest|run`. See
[ADR-0017](../docs/adr/0017-cascade-facade.md) and [`cascade-demo/`](cascade-demo/).
The specialised opt-in surface remains on `occam_transcode`. Backend escalation inside a single
transcode (`backend_policy=http_then_browser`) is documented in [concepts](../docs/concepts.md).
