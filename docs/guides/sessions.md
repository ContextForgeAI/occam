# Guide: Sessions / authenticated pages

## What is this?

Pass a local **session profile** (cookies / storage state) so Occam can fetch pages behind a login you already completed.

## When should I use it?

`requires_login` or similar access walls after a normal anonymous fetch.

## Minimal flow

1. Export browser state with the operator session tools (`occam session` / `occam-session.mjs` — see [Getting started](../getting-started.md)).  
2. Store JSON under `OCCAM_SESSIONS_ROOT`.  
3. Pass `session_profile: "<id>"` on extract tools.  

```json
{
  "name": "occam_transcode",
  "arguments": {
    "url": "https://example.com/private",
    "session_profile": "my-site",
    "backend_policy": "browser"
  }
}
```

## Expected result

Same as a normal transcode when the session is valid.

## What can go wrong?

Expired cookies, wrong profile id, or CAPTCHA (Occam does **not** solve CAPTCHAs).

## Next

- [Example: session profile](../examples/session-profile.md)
- [Trust: local-first](../trust/local-first.md)
