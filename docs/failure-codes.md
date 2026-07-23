# Failure codes

**What you'll do:** interpret `failure.code` on `ok: false` responses and choose the right retry or stop action.

**Trust rule:** `ok: false` means page content is **unknown**. Never substitute model memory.

---

## Code reference

| Code | Typical trigger | Retryable? | Agent action |
|------|-----------------|------------|--------------|
| `workers_unavailable` | `OCCAM_HOME` wrong, doctor not run, or the browser isn't installed | No | Run `occam-doctor`; reload MCP. When the page needs a browser and none is installed, the response carries `failure.fix` — run its `command` (e.g. `occam install-browser`) |
| `timeout` | Worker/probe exceeded budget, or map exhausted its total discovery deadline | Yes | Retry once; raise the relevant per-call timeout if justified, then skip or use `browser` |
| `network_error` | Connection reset, refused | Yes | Retry once |
| `dns_error` | Host does not resolve | Yes | Check URL spelling / DNS |
| `tls_error` | Certificate invalid or expired | No | Inform user; do not bypass TLS |
| `http_401` | Unauthorized | No | Configure `session_profile` |
| `http_403` | Forbidden | No | Try `session_profile` or `backend_policy=browser` |
| `http_404` | Not found | No | Fix or remove URL |
| `http_410` | Gone | No | Remove URL |
| `http_429` | Rate limited | Yes | Back off and retry |
| `http_5xx` | Server error | Yes | Retry with backoff |
| `thin_extract` | Bad extraction (chrome / shell / near-empty) — **not** a short quality page | Until browser tried | Retry with `backend_policy=browser`; once a full browser render is **still** thin, `retryable` is dropped and the action becomes stop (see note) |
| `extraction_failed` | Worker could not produce markdown | Sometimes | Read message; try browser |
| `content_selectors_miss` | `content_selectors` matched nothing | No | Widen selectors or drop them |
| `captcha_or_challenge` | Anti-bot / Cloudflare challenge page | No | Stop; no CAPTCHA solver |
| `requires_login` | Direct access-control evidence, no session | No | Add `session_profile` |
| `session_profile_not_found` | Profile id missing on disk | No | Create profile under `OCCAM_SESSIONS_ROOT` |
| `invalid_session_profile` | Bad profile id format | No | Fix profile name |
| `private_url_blocked` | RFC1918 / localhost blocked | No | Use public URL or maintainer `OCCAM_ALLOW_PRIVATE_URLS` |
| `robots_disallowed` | robots.txt disallow (`OCCAM_RESPECT_ROBOTS=1`) | No | Respect site policy |
| `response_too_large` | Body over `OCCAM_MAX_RESPONSE_BYTES` | No | Raise cap or skip URL |
| `response_truncated` | Partial body only | No | Do not cite as full page |
| `invalid_arguments` | Bad parameters | No | Fix args per tool reference |
| `invalid_policy` | Unknown `backend_policy` | No | Use `http`, `browser`, or `http_then_browser` |
| `playbook_not_found` | No playbook for host | No | Use default transcode |
| `knowledge_schema_missing` | No schema in playbook | No | Use `occam_transcode` instead |
| `page_class_unmatched` | URL class has no schema | No | Use transcode for prose |
| `knowledge_schema_empty` | Matched class has zero fields | No | Use transcode |
| `playbook_verify_failed` | Save dry-run failed | No | Revise playbook JSON |
| `playbook_schema_invalid` | Lint/save rejected JSON | No | Fix schema |
| `playbook_save_rejected` | Save failed its dry-run verify or validation | No | Revise the playbook JSON and re-save |
| `heal_not_applicable` | Failure not healable | No | Read code; try browser |
| `heal_failed` | Heal attempt ran but could not repair the extract | No | Read message; fall back to `occam_transcode` |
| `invalid_urls` | Bad digest/map URL list | No | Fix `urls` parameter |
| `digest_failed` | All digest URLs failed | No | Retry singles with transcode |
| `sitemap_not_found` | Map source=sitemap empty | No | Retry `source=homepage` |
| `search_unconfigured` | No `OCCAM_SEARCH_PROVIDER` | No | Configure search or skip |
| `search_timeout` | Search backend slow | Yes | Retry or raise timeout |
| `search_http_<status>` | Search backend returned an HTTP error (`<status>`) | Depends on status | Check the endpoint/API key; back off on `429`/`5xx` |
| `search_error` | Search backend failed for another reason | Sometimes | Read message; check `OCCAM_SEARCH_PROVIDER` config |

HTTP codes may appear as `http_<status>` (e.g. `http_418`).

**Terminal HTTP status short-circuit:** a definitive `http_404` / `http_410` from the HTTP fetch is returned as-is under `backend_policy=http_then_browser` — occam does **not** escalate a "resource gone" status to the browser (a render cannot resurrect a missing page). You get the authoritative status and a `stop` action, not a masked `extraction_failed`.

**requires_login:** a hard login verdict requires direct access-control evidence: HTTP 401 or an
authentication challenge, a redirect from the requested page to a login route, or blocking identity UI
without usable public content. Authentication prose, password documentation, and a login-like requested
path are not enough on their own. Inconclusive evidence remains non-terminal; add a `session_profile`
only when `requires_login` is actually returned.

**Thin after browser:** `thin_extract` normally suggests retrying with the browser. When the failing extract already came from the browser backend, retrying will not help — the page is genuinely near-empty. In that case `retryable` is omitted, `agentMeta.decisions` becomes `stop`, and no heal is offered. Report the little content that came back (or that the page is nearly empty); do not loop or invent content.

**Thin ≠ short:** `thin_extract` means **bad extraction** (promo chrome, consent/nav shell, headings-only interstitial, near-empty). A short but complete page (glossary leaf, status page, `example.com`-class docs) is `ok: true` with `quality.verdict` of `short_quality` — do not escalate just because the body is small.

---

## Responses may include hints

Transcode failures can include:

- `agentMeta.decisions[]` — suggested next steps (`retry_transcode`, `configure_session_profile`, `stop`, …)
- `agentHints.suggestedNextTool` — e.g. `occam_playbook_heal` when applicable

Prefer these over guessing.

---

## Probe-specific codes

Probe failures use the same taxonomy where applicable (`invalid_arguments`, `timeout`, `dns_error`, `unsupported_content_type`, `invalid_url`, HTTP codes).

---

## Related

- [Troubleshooting](troubleshooting.md) — install and runtime symptoms
- [Concepts — trust model](concepts.md#trust-model)
- [Choosing a tool](choosing-a-tool.md)
