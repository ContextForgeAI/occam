# occam_watch

Stateful page-change watch. The first call records the page; later calls return `changed:
true/false` plus a block-level diff when it changed. State is kept server-side, keyed by URL.

> **Opt-in tool.** Absent from `tools/list` unless the host starts with `OCCAM_WATCH_MCP=1`.
> There is no daemon — the agent calls on its own cadence.

## When to use

- Recurring "did this page change since I last looked?" checks, with a signed change history.
- The stateless equivalent (you keep the state) is `occam_transcode` with `if_none_match` +
  `diff_against` — see [occam_transcode](occam_transcode.md).

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `url` | string | — | **yes** | HTTP or HTTPS URL to watch |
| `backend_policy` | string | `http_then_browser` | no | `http`, `browser`, or `http_then_browser` |
| `focus_query` | string? | null | no | Focus prune — narrows what counts as a change |
| `session_profile` | string? | null | no | Headers profile id |
| `playbook_policy` | string | `auto` | no | `off` or `auto` |
| `include_diff` | bool | `true` | no | Include the block-level diff (`addedBlocks`/`removedHashes`) when changed |
| `reset` | bool | `false` | no | Overwrite prior state; treat this call as the first sighting (`changed:false`) |
| `include_history` | bool | `false` | no | Return the full signed change-history chain in `history`; the response always carries the history length + latest entry regardless |

## Returns

Success envelope: watch state (`changed`, content hashes, optional block diff, history summary and
— with `include_history=true` — the signed `history[]` chain, verifiable via
[`occam_verify`](occam_verify.md) `mode=history`).

Failure envelope: `ok: false`, `url`, `failure: {code, message}`.

## Failure codes

`invalid_arguments`, plus the transcode fetch taxonomy for the underlying extraction (`timeout`,
`http_*`, `thin_extract`, …). See [failure codes](../failure-codes.md).

## Example

Second call on a changed page (trimmed):

```json
{
  "ok": true,
  "changed": true,
  "diff": { "addedBlocks": [ { "text": "…", "sourceSelector": "…" } ], "removedHashes": ["…"] },
  "historyLength": 3
}
```

## Related

- [occam_transcode](occam_transcode.md) — stateless change-detection (`if_none_match`, `diff_against`)
- [occam_verify](occam_verify.md) — verify the signed history chain
