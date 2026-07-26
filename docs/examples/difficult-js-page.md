# Example: Difficult JS page

When HTTP returns empty or thin content for a client-rendered SPA, force the browser backend.

```json
{
  "name": "occam_transcode",
  "arguments": {
    "url": "https://app.example.com/docs/getting-started",
    "backend_policy": "browser"
  }
}
```

## When to escalate

1. **Default first:** omit `backend_policy` (uses `http_then_browser` — HTTP first, browser when HTTP is unusable).
2. **Known SPA:** set `backend_policy=browser` when you already know HTTP will not render the app shell.
3. **Login wall:** add `session_profile` and keep `browser` — see [Session profile](session-profile.md).

## Expected

- `ok: true` with non-empty `markdown` when the page renders in Chromium.
- `thin_extract` or empty body on `http` alone — not a bug; signal to use browser.

Occam does **not** solve CAPTCHAs. Sessions carry your authenticated state only.

## Next

- [How Occam works](../how-occam-works.md)
- [Acquisition](../acquisition.md)
- [Failure codes](../failure-codes.md)
