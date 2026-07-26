# Example: Session profile

Use a session when anonymous reads hit `requires_login` or return thin/empty content for authenticated pages.

## Create a profile (operator)

```bash
export OCCAM_HOME="${OCCAM_INSTALL_DIR:-$HOME/.local/share/ff-occam}"
export PATH="$OCCAM_HOME/scripts:$PATH"

occam session init

# Cookie jar only (headers tier — works on all tiers for HTTP cookies):
occam session import --from ~/Downloads/cookies.txt --host example.com --id example-com

# Full browser state (storageState — Tier 1 tools only):
occam session export-state --url https://example.com/login --id example-com
```

**Import honesty:** by default, `import` does **not** keep a plaintext copy in `_imports/`. Add `--keep-import` only if you want the raw file retained on disk.

## Read with session (Tier 1 — full)

```json
{
  "name": "occam_transcode",
  "arguments": {
    "url": "https://example.com/account",
    "session_profile": "example-com",
    "backend_policy": "browser"
  }
}
```

Use `backend_policy=browser` when auth or content is client-side (SPA).

## Probe with the same profile (Tier 2 — headers only)

```json
{
  "name": "occam_probe",
  "arguments": {
    "url": "https://example.com/account",
    "session_profile": "example-com"
  }
}
```

Probe never loads `storageState`. If auth lives only in localStorage, probe may still look anonymous even when transcode works.

## Extract knowledge (Tier 3 — headers only, storageState dropped)

```json
{
  "name": "occam_extract_knowledge",
  "arguments": {
    "url": "https://example.com/product/123",
    "session_profile": "example-com"
  }
}
```

If the wall needs `storageState`, run `occam_transcode` first or ensure cookie headers in the profile are sufficient.

## Expected

- `ok: true` with non-empty content when the session is valid and the tier matches your auth shape.
- Profiles are local files under `OCCAM_SESSIONS_ROOT` — do not commit them.

## What Occam will not do

- Solve CAPTCHAs or bypass bot walls automatically.
- Sync session state back to disk after a fetch (profiles are static input).

## Next

- [Guide: sessions](../guides/sessions.md)
- [Sessions overview](../sessions.md)
- [Trust: local-first](../trust/local-first.md)
