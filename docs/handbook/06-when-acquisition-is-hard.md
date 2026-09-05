# Chapter 6 — When acquisition is hard: walls, sessions, egress

**Part B — Reference path** · Prerequisites: [Ch 2](02-honesty-contract.md), [Ch 5](05-acquisition-ladder.md) · Next: [Chapter 7](07-materialization-token-contract.md)

---

## Mental model

**Occam names walls; it does not climb them.**

Every wall maps to a typed `failure.code` and at most one honest lever: real cookies/`session_profile`, proxy, or forced local `backend_policy=browser`. There is no CAPTCHA solver, no third-party scrape escalation, no identity rotation, no universal bypass.

---

## Explanation

### Obstacle → lever (honest)

| Wall | Typed signal | Levers that can work |
|------|--------------|----------------------|
| Login / session | `requires_login`, `http_401` | `session_profile` + often `backend_policy=browser` |
| Cookie consent overlay | (may affect quality) | Browser worker dismisses common banners; not anti-bot |
| CAPTCHA / bot challenge | `captcha_or_challenge` | `session_profile` + local browser (sometimes); else stop — no solver in product |
| Geo / IP / bot blocking | `http_403`, thin extract | Session, proxy list, local browser |
| TLS / network | `tls_error`, `network_error`, `dns_error` | Fix network, proxy; note probe may mask SSRF as `network_error` |
| Robots disallow | `robots_disallowed` | Respect policy or disable via env (off by default; fails open on robots fetch error) |
| Private URL | `private_url_blocked` | `OCCAM_ALLOW_PRIVATE_URLS=1` (dangerous; tool-dependent) |

When no lever applies, return `ok:false` and stop—do not invent content.

### Session profiles — three tiers

Session data can include HTTP headers and Playwright `storageState`. **Not every tool forwards both.**

| Tier | Callers | Headers | Browser `storageState` |
|------|---------|---------|------------------------|
| **1** | `occam_transcode`, digest, claim paths | Yes | Yes |
| **2** | `occam_probe`, `occam_map` | Yes (HTTP only) | **No** — HTTP-only tools |
| **3** | `occam_playbook_heal`, `occam_extract_knowledge` | Headers forwarded | **`storageState` dropped silently** on browser leg |

Task R: changelog behind login — import session (`occam session import` / export-state), transcode with `session_profile` and `backend_policy=browser`. Same profile on `occam_probe` reaches **less**—by design.

### Egress and proxy

- `OCCAM_HTTP_PROXY` / `OCCAM_HTTPS_PROXY` reach **worker egress**, not all Core `HttpClient`s (probe, map, search may ignore them).
- Proxy rotation lists may not reach HTTP daemon, browser pool, css-extract, dom-skeleton spawns.
- Playwright proxy resolution can fail open to no proxy.
- Empty `OCCAM_PROXY_LIST_FILE` suppresses inline list rather than falling back.

### Managed provider

When configured, a third party fetches on your behalf after local dual failure. Treat as **trusted intermediary**—content returned may be what gets signed.

### Security hygiene

- Session import may retain raw cookies under `_imports/` **plaintext by default**—secure-delete after use.
- Pooled browser contexts are **not** a strong isolation boundary between anonymous extractions.
- Robots compliance is off by default—not a polite crawler.

---

## CHECK

**LOCAL/NETWORK** — Session tier mismatch.

1. Create a session profile with both headers and `storageState` (export from a logged-in browser session).
2. Call `occam_transcode` with `session_profile` on a URL that requires auth.
3. Call `occam_probe` with the same `session_profile`.

Inspect responses and logs: transcode should use browser state; probe remains HTTP-only and may still report login/challenge signals without authenticated body.

---

## Common misconception

**"A session profile reproduces the same authenticated state in every tool."**

Three tiers exist. Tier 2 never uses `storageState`; tier 3 drops it on some tools. Always match tool to wall type.

---

## Limitations

- No CAPTCHA solving—ever.
- `tag_trust` does not prevent prompt injection; injected text can be signed like any content ([Chapter 14](14-what-a-receipt-proves.md)).
- css-extract and Nuxt eval paths are **not safe for untrusted URLs** today ([Chapter 13](13-typed-field-extraction.md)).
- Context bleed across pooled browser sessions is an known out-of-scope threat.

---

## Links

**Public docs:** [Concepts](../concepts.md) (sessions) · [Configuration](../configuration.md) (proxy, robots) · [Failure codes](../failure-codes.md) · [Troubleshooting](../troubleshooting.md)

**Next chapter:** [Chapter 7 — Materialization: the token contract](07-materialization-token-contract.md)
