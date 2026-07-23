# Occam session profiles (local only)

This folder is **`OCCAM_SESSIONS_ROOT`** — JSON header maps for `session_profile` on fetch tools.

**Never commit these files to git.** Occam never uploads cookie values in MCP responses.

## Layout

```text
~/.occam/sessions/
  README.md              ← this file (your notes welcome)
  .gitignore             ← ignores *.json if you symlink this folder into a repo
  _imports/              ← drop raw browser exports (cookies.txt) here
  states/                ← Playwright storageState JSON
  <site>.<purpose>.json  ← profiles Occam reads
```

## Naming convention

Use **flat** ids (no subfolders — API v1):

| Pattern | Example id | When |
|---------|------------|------|
| `<host>.<purpose>` | `stackoverflow.com.work` | Login or site-specific headers |
| `<host>.cf-export` | `reddit.com.cf-export` | After Cloudflare challenge (may still fail — see docs) |
| `<host>.probe-ua` | `stackoverflow.com.probe-ua` | UA-only workaround (no cookies) |

MCP: `session_profile: "<id>"` (filename without `.json`).

## Commands (from FFOccamMCP repo root)

```powershell
node scripts/occam-session.mjs init
node scripts/occam-session.mjs list
node scripts/occam-session.mjs import --from path\to\cookies.txt --host example.com --id example.com.work
node scripts/occam-session.mjs import --from path\to\all_cookies.txt --all --id browser.export
node scripts/occam-session.mjs export-state --url https://example.com --id example.com
```

Full guide: `docs/sessions.md` in your `OCCAM_HOME` clone.

## Refresh cadence

| Cookie type | Typical TTL | Action |
|-------------|-------------|--------|
| `__cf_bm` | ~30 minutes | Re-import often |
| `cf_clearance` | longer | TLS-bound — paste rarely helps workers |
| Login `session` | site-specific | Re-import on `http_401` |
