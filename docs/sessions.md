# Sessions and authenticated access

How `session_profile` actually works — **not** “the same everywhere.”

**Status:** USABLE_WITH_LIMITATIONS

## Three tiers (mental model)

| Tier | What is applied | Typical consumers |
|------|-----------------|-------------------|
| **Full browser + headers** | Playwright `storageState` (cookies/localStorage) **and** HTTP headers from the profile | Browser-backed reads (`occam_transcode` / digest with browser policy, heal paths that drive the browser) |
| **Headers only** | Cookie/header bag for HTTP workers | Probe, map, many HTTP-only extracts; some tools never load `storageState` |
| **Ignored / N/A** | Parameter accepted or absent but browser storage unused | Tools that never open a browser; mis-set profiles on HTTP-only calls |

**Do not say** “`session_profile` works the same on every tool.” Check the tool page or [configuration](configuration.md) for that call path.

## Secrets on disk

- Profiles live under `OCCAM_SESSIONS_ROOT` (default under `~/.occam/…`).  
- They contain **secrets** (cookies, headers). Protect the directory like credentials.  
- `occam-session import` defaults to **not** retaining plaintext import sources under `_imports/`; use an explicit keep flag only when you intend to keep them.  
- Occam does **not** solve CAPTCHAs. Sessions carry *your* authenticated state.

## Operator workflow

1. Export / import a browser storage state or cookie jar via `occam-session` helpers.  
2. Pass `session_profile=<id>` on tools that support it.  
3. Prefer `backend_policy=browser` when the wall is client-side.  
4. Rotate/delete profiles when done.

## Related

- [Guide: Sessions](guides/sessions.md)
- [Example: session profile](examples/session-profile.md)
- [Acquisition](acquisition.md)
- [Installation safety](trust/installation-safety.md)
