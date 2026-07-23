# Troubleshooting

**What you'll do:** fix common install, connection, and extraction problems.

---

## Install and host

| Symptom | Cause | Fix |
|---------|-------|-----|
| `workers_unavailable` | `OCCAM_HOME` unset or workers missing | Set `OCCAM_HOME` to repo root; run `.\scripts\occam-doctor.ps1` |
| MCP server exits immediately | Crash on startup | Run host manually; read **stderr** |
| Zero tools in client | Host failed to register | Reload MCP after doctor; check Node 20+ |
| `playwright_missing` in message / browser `failure.fix` present | Chromium not installed | Run the `failure.fix.command` — `occam install-browser` (user-level, no root). Or `cd workers/browser-extract && npx playwright install chromium` |
| Browser worker `no_json` | OOM or crash | Reload MCP; try `OCCAM_BROWSER_NODE_MAX_OLD_SPACE_MB=512` |
| `dns_error` on **every** external URL (but the host resolves fine) | Node **< 20** — the worker's `undici` needs the `File` global (Node 20+); it fails to load and the fetch path reports a misleading `dns_error` | Run occam on Node 20+. `occam-wrapper.sh` auto-prefers a newer node from `~/.local/node20/bin`, `/opt/node20/bin`, or `/usr/local/bin` if the default is older |

---

## Extraction

| Symptom | Cause | Fix |
|---------|-------|-----|
| Empty or tiny markdown | SPA or thin HTTP extract | `backend_policy=browser` or `http_then_browser` |
| `captcha_or_challenge` | Cloudflare / bot wall | Stop; export session cookies only if site allows |
| `requires_login` / `http_403` | Gated content | `occam-session.mjs export-state` → `session_profile` |
| `http_404` | Bad link | Fix URL |
| `timeout` | Slow site, cold browser, or exhausted map/probe deadline | Retry; raise per-call `timeout_ms` for map/probe or `OCCAM_BROWSER_TIMEOUT_MS` for browser extracts |
| `response_too_large` | Page exceeds cap | Raise `OCCAM_MAX_RESPONSE_BYTES` or skip |
| Mojibake / wrong encoding | Rare worker edge | Report with URL; check UTF-8 chain |

---

## MCP client

| Symptom | Cause | Fix |
|---------|-------|-----|
| Tools hang | Browser pool saturated | Lower concurrency; check `OCCAM_BROWSER_MAX_PARALLEL` |
| Stale results after config change | Client cached old server | Reload MCP servers |
| WebSocket connection refused | Host not in WS mode | Start with `--mcp-server` |
| ChatGPT / tunnel schema lags RC1 (`urls` still required; missing `rank_blocks` / `emit_capsule`…) while receipts say `1.0.0-rc.2` | Old `OccamMcp.Core.exe` still mapped by a long-lived tunnel child, or connector cached `tools/list` | Stop tunnel + host → republish/copy binary → start a **fresh** tunnel → reconnect the connector. Verify with `node scripts/check-public-mcp-contract.mjs` |

---

## Search and optional features

| Symptom | Cause | Fix |
|---------|-------|-----|
| `search_unconfigured` | No provider | Set `OCCAM_SEARCH_PROVIDER` + URL or API key |
| Translation warning | No LibreTranslate | Set `OCCAM_TRANSLATE_URL` or omit `translate_to` |
| Unsigned receipts | Signing disabled | Unset `OCCAM_RECEIPTS=off` |

---

## Diagnostic commands

```powershell
$env:OCCAM_HOME = (Get-Location).Path
.\scripts\occam-doctor.ps1
node scripts/launch-mcp-host.mjs
node scripts/check-public-mcp-contract.mjs   # tools/list contract + version-surface fingerprint
OccamMcp.Core.exe version-surface            # hostVersion + assemblyPath
```

Enable profiler on stderr:

```powershell
$env:OCCAM_LOG = "1"
```

---

## Still stuck?

1. Note `failure.code` and full JSON response.
2. Check [Failure codes](failure-codes.md) for agent actions.
3. Verify env: [Configuration](configuration.md).
