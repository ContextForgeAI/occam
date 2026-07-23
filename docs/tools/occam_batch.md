# occam_batch_submit / occam_batch_status / occam_batch_results

Fire-and-forget asynchronous transcode of a URL list: submit returns a `job_id` immediately; poll
status; then page the results.

> **Opt-in tools.** All three are absent from `tools/list` unless the host starts with
> `OCCAM_BATCH_MCP=1` (which also registers the job store and the background processor).

## When to use

- Many URLs where you don't need the answers inline — submit and come back.
- Need a signed, auditable corpus instead → [`occam_dataset_export`](occam_dataset_export.md)
  (synchronous, 1–20 URLs, provenance manifest).
- Up to 8 URLs and you want the synthesis now → [`occam_digest`](occam_digest.md).

## occam_batch_submit

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `urls` | string | — | **yes** | JSON array or newline/comma/semicolon-separated list |
| `backend_policy` | string | `http_then_browser` | no | `http`, `browser`, or `http_then_browser` |
| `focus_query` | string? | null | no | Focus keywords for the per-URL prune |
| `max_tokens` | int? | null | no | Per-URL output token budget (min 128) |
| `fit_markdown` | bool | `true` | no | Paragraph prune per URL |
| `session_profile` | string? | null | no | Applied to every URL |
| `playbook_policy` | string | `auto` | no | `off` or `auto` |
| `idempotency_key` | string? | null | no | Re-submitting the same key within 24h returns the existing job |
| `on_oversize` | string | `fail` | no | HTTP oversize handling: `fail` or `partial` |

Returns the job descriptor with `job_id` and state `queued`.

## occam_batch_status

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `job_id` | string | — | **yes** | Id returned by submit |

Returns the job state (`queued` / `running` / `done` / `failed`) and progress counts.

## occam_batch_results

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `job_id` | string | — | **yes** | Id returned by submit |
| `cursor` | int | `0` | no | Pass the prior `next_cursor` to continue |
| `limit` | int | `50` | no | Max items per page (1–200) |

Returns `items[]` (per-URL transcode outcomes, using the transcode taxonomy for failures) and
`next_cursor` (`null` when exhausted).

## Failure codes

Errors are returned as `{failure: {code, message}}` — `invalid_arguments`, unknown `job_id`, and
per-item transcode codes inside results. See [failure codes](../failure-codes.md).

## Example

```json
{ "urls": "https://a.example/1\nhttps://a.example/2", "max_tokens": 512 }
```

→ `{ "job_id": "…", "state": "queued" }` — then poll `occam_batch_status`, then page
`occam_batch_results`.

## Related

- [occam_digest](occam_digest.md) — synchronous small-batch synthesis
- [occam_dataset_export](occam_dataset_export.md) — signed corpus export
