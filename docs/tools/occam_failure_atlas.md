# Failure atlas (opt-in)

**Canonical tool:** `occam_failure_atlas`

Read the running host's per-host failure atlas: which hosts look **walled in this session**
(captcha / login / 4xx dominance — retrying may be wasted) vs which merely had transient failures.
In-memory over the current run; not persisted; **not proof** a host is permanently unreachable.

> **Opt-in tool.** Absent from `tools/list` unless the host starts with `OCCAM_ATLAS_MCP=1`
> (which also turns on the per-host aggregation).

## When to use

- Planning a crawl mid-session: skip hosts the atlas classifies as walled for this run.
- Note the atlas is empty at startup and forgets everything on restart.

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `only_walled` | bool | `false` | no | Return only hosts classified as walled in this session |

## Returns

- `ok: true`, `hostCount`, `walledCount`, `timestamp`
- `hosts[]` (worst-first) — `{host, attempts, successes, failures, closureRate, walled,
  dominantFailure, byCode:[{code, count}], lastFailureAt}`

`walled` = the host never succeeded **and** its dominant failure is an honest closure
(captcha/login/4xx).

## Example

```json
{ "only_walled": true }
```

Trimmed response:

```json
{
  "ok": true,
  "hostCount": 12,
  "walledCount": 2,
  "hosts": [
    { "host": "paywalled.example", "attempts": 4, "successes": 0, "failures": 4, "closureRate": 1.0, "walled": true, "dominantFailure": "requires_login", "byCode": [ { "code": "requires_login", "count": 4 } ] }
  ]
}
```

## Related

- [Failure codes](../failure-codes.md) — what each code means
- [occam_probe](occam_probe.md) — pre-flight check for hosts not yet in the atlas
