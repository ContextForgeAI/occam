# Example: Cross-check vantages (EXPERIMENTAL)

> **EXPERIMENTAL · opt-in:** host must start with `OCCAM_CONSENSUS_MCP=1`. Tool absent from default `tools/list`.

Compare HTTP vs browser (and optional session) vantages on the same URL. Use when you suspect cloaking, personalization, or access-walling — **not** for ordinary reads.

```json
{
  "name": "occam_crosscheck",
  "arguments": {
    "url": "https://news.example/story",
    "vantages": "http,browser"
  }
}
```

With authenticated comparison:

```json
{
  "name": "occam_crosscheck",
  "arguments": {
    "url": "https://news.example/story",
    "session_profile": "news-example"
  }
}
```

## Verdict honesty

| Verdict | Meaning |
|---------|---------|
| `consensus` | Vantages agreed on content — **agreement ≠ correctness** |
| `divergent` | Materially different content between vantages |
| `access_divergent` | Some vantages walled, others succeeded |

Each vantage extract may carry a signed Receipt v1. The **agreement verdict is computed, not signed** — do not describe crosscheck as "consensus proof."

Runs **2+ full extracts** per call (expensive).

## Next

- [`occam_crosscheck`](../tools/occam_crosscheck.md)
- [Verify a receipt](verify-receipt.md)
