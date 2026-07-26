# Guide: Sessions / authenticated pages

## What is this?

Pass a local **session profile** (cookies, headers, and optionally Playwright `storageState`) so Occam can fetch pages behind a login you already completed in a real browser.

**Do not assume** `session_profile` behaves the same on every tool. Occam applies three **session tiers** depending on the call path.

## Three session tiers

| Tier | What is applied | Typical tools |
|------|-----------------|---------------|
| **1 — Full browser + headers** | HTTP headers **and** Playwright `storageState` (cookies + localStorage) | `occam_transcode`, `occam_digest`, `occam_claim_check`, `occam_attest`, `occam_dataset_export`, opt-in batch/watch/crosscheck |
| **2 — Headers only (HTTP path)** | Cookie/header bag for HTTP workers; **no** `storageState` | `occam_probe`, `occam_map` |
| **3 — Headers only (browser path, storageState dropped)** | HTTP headers forwarded; **`storageState` is silently ignored** | `occam_playbook_heal`, `occam_extract_knowledge` |

**Headers-only vs `storageState`:** a profile can store auth in two shapes:

- **Header/cookie bag** — works on Tier 1–3 for HTTP header injection.
- **`storageState` file** — Playwright cookie jar + localStorage; required for many SPAs and client-side auth walls. Only Tier 1 tools load it. On Tier 2/3, the file is not read — no error is returned, so check the tier before you debug a failed login.

See also: [Sessions overview](../sessions.md) · [Configuration — session profiles](../configuration.md#session-profiles)

## When should I use it?

- `requires_login`, `captcha_or_challenge` (session may help only if **you** already passed the wall — Occam does **not** solve CAPTCHAs), or empty/thin anonymous extracts on authenticated content.
- Prefer `backend_policy=browser` when the wall is client-side (SPA, JS-gated content).

## Minimal flow

1. **Init** the sessions directory (once): `occam session init` or `node scripts/occam-session.mjs init`.
2. **Create a profile:**
   - **Cookies only:** `occam session import --from cookies.txt --host example.com --id example-com`
   - **Full browser state:** `occam session export-state --url https://example.com/login --id example-com` (headed browser; you log in, then Occam saves `storageState`).
3. Profiles live under `OCCAM_SESSIONS_ROOT` (default `~/.occam/sessions/`).
4. Pass `session_profile: "<id>"` on tools that support it.

```json
{
  "name": "occam_transcode",
  "arguments": {
    "url": "https://example.com/private",
    "session_profile": "example-com",
    "backend_policy": "browser"
  }
}
```

## Secrets on disk

Session profiles are **credentials on disk**:

| Location | Contents | Risk |
|----------|----------|------|
| `OCCAM_SESSIONS_ROOT/<id>.json` | Profile metadata, header map, `storageState` path | Cookies, auth headers |
| `OCCAM_SESSIONS_ROOT/states/` | Playwright `storageState` JSON | Cookies, localStorage tokens |
| `OCCAM_SESSIONS_ROOT/_imports/` | Raw import sources **only when you opt in** | Plaintext cookie files |

**Import default:** `occam session import` does **not** retain a plaintext copy under `_imports/` unless you pass `--keep-import`. The default is safer; use `--keep-import` only when you intend to keep the source file.

Protect `OCCAM_SESSIONS_ROOT` like a password store. Do not commit profiles. Do not put LLM API keys in Occam's environment.

## Expected result

Same as a normal transcode when the session is valid **and** the tool tier matches how your auth is stored.

## What can go wrong?

| Symptom | Likely cause |
|---------|--------------|
| Still `requires_login` | Expired cookies, wrong profile id, or Tier 2/3 tool with `storageState`-only auth |
| `session_profile_not_found` | Id missing under `OCCAM_SESSIONS_ROOT` |
| `captcha_or_challenge` | CAPTCHA or bot wall — Occam does **not** solve CAPTCHAs; sessions carry *your* state only |
| Silent auth failure on heal/extract | Tier 3 dropped `storageState`; use transcode first or ensure cookie headers are enough |

## Next

- [Example: session profile](../examples/session-profile.md)
- [Sessions overview](../sessions.md)
- [Trust: local-first](../trust/local-first.md)
