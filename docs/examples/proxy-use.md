# Example: Proxy use

Route HTTP extract traffic through an operator-controlled proxy.

**Prerequisites:** set proxy env vars **before** starting the MCP host (or in `~/.occam/onboard.json` via connect/onboard).

```bash
export OCCAM_HTTP_PROXY=http://127.0.0.1:8080
export OCCAM_HTTPS_PROXY=http://127.0.0.1:8080
export NO_PROXY=localhost,127.0.0.1
```

```json
{
  "name": "occam_transcode",
  "arguments": {
    "url": "https://example.com"
  }
}
```

## Scope honesty

| Path | Proxy? |
|------|--------|
| HTTP extract worker | Yes — respects `OCCAM_HTTP(S)_PROXY` |
| Browser extract | Partial — Playwright proxy when configured; not identical to every HTTP helper |
| Managed provider | Provider egress — not your local proxy rotation |

Proxy rotation is **not** fingerprint rotation and does not bypass CAPTCHAs or bot walls.

## Next

- [Networking and proxies](../networking.md)
- [Configuration](../configuration.md)
