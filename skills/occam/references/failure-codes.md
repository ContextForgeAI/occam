# Failure codes — agent actions

`ok: false` means page content is **unknown**. Never substitute model memory.

---

## Common codes

| Code | Retryable? | Agent action |
|------|------------|--------------|
| `workers_unavailable` | No | `occam doctor`; fix `OCCAM_HOME`; reload MCP |
| `timeout` | Yes | Retry once; then `backend_policy=browser` |
| `network_error` | Yes | Retry once |
| `dns_error` | Yes | Check URL spelling |
| `tls_error` | No | Inform user; do not bypass TLS |
| `http_401` / `http_403` | No | `session_profile` or `backend_policy=browser` |
| `http_404` / `http_410` | No | Fix or remove URL |
| `http_429` / `http_5xx` | Yes | Back off and retry |
| `thin_extract` | Yes | `browser` or `http_then_browser` |
| `extraction_failed` | Sometimes | Read message; try browser |
| `captcha_or_challenge` | No | **Stop** — no CAPTCHA solver |
| `requires_login` | No | Add `session_profile` |
| `session_profile_not_found` | No | Create profile under sessions root |
| `private_url_blocked` | No | Use public URL only |
| `robots_disallowed` | No | Respect site policy |
| `invalid_arguments` | No | Fix params per tool reference |
| `invalid_policy` | No | `http` \| `browser` \| `http_then_browser` |
| `knowledge_schema_missing` | No | Use `occam_transcode` instead |
| `playbook_verify_failed` | No | Revise playbook JSON |
| `digest_failed` | No | Retry singles with transcode |
| `sitemap_not_found` | No | Map with `source=homepage` |
| `search_unconfigured` | No | Configure `OCCAM_SEARCH_PROVIDER` or skip search |

---

## Response hints

Transcode failures may include:

- `agentMeta.decisions[]` — e.g. `retry_transcode`, `configure_session_profile`, `stop`
- `agentHints.suggestedNextTool` — e.g. `occam_playbook_heal`

Prefer these over guessing.

---

## Probe failures

Same taxonomy where applicable: `invalid_arguments`, `timeout`, `dns_error`, `unsupported_content_type`, `invalid_url`, HTTP codes.

Full table: FF-Occam `docs/failure-codes.md`.
