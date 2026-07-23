# occam_crosscheck

Cross-check a URL across vantage points (http vs browser, anonymous vs session) and report whether
they agree. Divergence is evidence of cloaking, personalization, or access-walling — and each
vantage carries a signed receipt, so the verdict is independently re-derivable.

> **Opt-in tool.** Absent from `tools/list` unless the host starts with `OCCAM_CONSENSUS_MCP=1`.
> It runs 2+ full extracts per call, which is why it is not always-on.

## When to use

- You suspect a page shows different content to bots vs browsers, or anonymous vs logged-in.
- Ordinary reads don't need this — use [`occam_transcode`](occam_transcode.md).

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `url` | string | — | **yes** | HTTP or HTTPS URL to cross-check |
| `vantages` | string | `"http,browser"` | no | Comma list of backends to compare: `http`, `browser`. Empty/duplicates collapse to the default pair |
| `session_profile` | string? | null | no | Adds an authenticated vantage per backend (anon-vs-authed axis) |
| `focus_query` | string? | null | no | Focus prune applied identically to every vantage |

## Returns

Success envelope: a verdict — `consensus` | `divergent` | `access_divergent` | `inconclusive` —
plus per-vantage results, each with its signed receipt.

Failure envelope: `ok: false`, `url`, `failureCode`, `message`, `timestamp`.

## Failure codes

`invalid_arguments` (e.g. a vantage other than `http`/`browser`), plus fetch-level codes when no
vantage could extract at all. See [failure codes](../failure-codes.md).

## Example

Call:

```json
{ "url": "https://news.example/story", "session_profile": "news-example" }
```

A `divergent` verdict means the vantages saw materially different content; `access_divergent`
means some vantages were walled while others got through.

## Related

- [occam_transcode](occam_transcode.md) — single-vantage read
- [occam_verify](occam_verify.md) — verify each vantage's receipt
