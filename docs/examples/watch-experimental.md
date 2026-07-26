# Example: Watch a page (EXPERIMENTAL)

> **EXPERIMENTAL · opt-in:** host must start with `OCCAM_WATCH_MCP=1`. Tool absent from default `tools/list`.

Stateful change detection with a signed history chain.

```json
{
  "name": "occam_watch",
  "arguments": {
    "url": "https://status.example.com"
  }
}
```

First call records baseline (`changed: false`). Later calls return `changed: true` with a block diff when content shifted.

```json
{
  "name": "occam_watch",
  "arguments": {
    "url": "https://status.example.com",
    "include_history": true
  }
}
```

Verify signed history entries with [`occam_verify`](../tools/occam_verify.md) `mode=history`.

**State:** URLs and baselines persist under `OCCAM_WATCH_DB_PATH` (default `~/.occam/watch/watch.json`).

**Stateless alternative:** `occam_transcode` with `if_none_match` + `diff_against` — you keep the state.

## Next

- [`occam_watch`](../tools/occam_watch.md)
- [Configuration — opt-in tools](../configuration.md)
