# Chapter 21 — State, persistence and footprint

**Status:** STABLE · **Prerequisites:** [Chapter 3](03-standing-up-an-install.md), [Chapter 20](20-automatic-behaviors.md)

---

## Mental model

**"No file cache by design" means live extract is the default. It does not mean stateless.** Occam writes keys, sessions, playbooks, watch/batch stores, opt-in cache, host configs, and browser caches — much of it outside the install tree.

---

## Explanation

### Verdict on "no file cache"

| Slice | True? |
|-------|-------|
| Default extract does not reuse prior page content | **Yes** — omit `cache_ttl_s` or set ≤0 |
| Occam keeps no durable on-disk state | **No** |
| No extract-result cache ever | **No** when `cache_ttl_s>0` |
| No secrets on disk | **No** — sessions, keys, imports |

### State inventory (high-signal)

| Location | Contents | Sensitivity |
|----------|----------|-------------|
| `~/.occam/keys/signing-key.pem` | ECDSA PKCS8, unencrypted | **CRITICAL** — minted every start, not gated by `OCCAM_RECEIPTS` |
| `~/.occam/sessions/` | Session profiles + Playwright `storageState` | **HIGH** — cookies, auth headers |
| `~/.occam/sessions/_imports/` | Raw imported cookies (plaintext default) | **HIGH** |
| `~/.occam/playbooks/local/` | Signed playbook JSON | Medium |
| `~/.occam/watch/watch.json` | Watch URLs + history | **HIGH** |
| `~/.occam/jobs/jobs.json` | Batch results — full markdown forever | **HIGH** |
| `~/.occam/onboard.json` | Env map merged on every launch | Medium–HIGH |
| `{TEMP}/occam-cache/` or `OCCAM_CACHE_DIR` | Full post-sign transcode envelopes | **HIGH** |
| Host MCP configs + `*.occam-bak` | Connect mutations | Medium |
| Playwright browser cache | Chromium binaries | Low |
| Install tree / `OCCAM_HOME` | Workers, seeds, community playbooks | Medium |

Receipts in API responses are **portable artifacts** — persistence is caller-owned.

### Outside the install directory

Removing `OCCAM_HOME` alone does **not** uninstall Occam. Surviving footprint includes entire `~/.occam/`, host MCP configs, skill directories, Playwright cache, and temp cache orphans.

### What survives events

| Event | Survives | Lost |
|-------|----------|------|
| Host restart | All persistent + host configuration | In-memory atlas, pools, caches (recreated) |
| Upgrade (replace install tree) | `~/.occam`, host configs, Playwright cache | Install-tree files unless preserved |
| Uninstall install dir only | **Everything under `~/.occam`**, host configs, skills, Playwright | Binaries under install |
| `occam refresh` | Disk state | Running processes machine-wide |

### Unbounded growth / no cleanup

- Batch `jobs.json` — no delete API.
- Watch URL set — uncapped URLs; no MCP un-watch.
- Response cache — TTL delete on read only; no sweep.
- `_imports/` — permanent by default.
- Signing key — permanent; recreated if deleted.

### Concurrency

- Batch and watch stores: last-writer-wins across processes.
- Browser pool: new WS/Remote session may kill prior pool.
- Response cache: fragment collision risk across concurrent readers.

---

## CHECK

**LOCAL.** Snapshot filesystem before your first handbook install exercise and after [Chapter 20](20-automatic-behaviors.md) workflows. Diff paths against the inventory above — every new path should map to a known state item.

---

## Common misconception

**"Deleting the install directory uninstalls Occam."** It leaves the entire `~/.occam` footprint, host MCP configs and backups, skill directories, Playwright browser cache, and temp cache leftovers.

---

## Limitations

- Windows key permission hardening is a no-op; POSIX `chmod` failure may be swallowed.
- Multi-process safety is not guaranteed for watch/batch stores.
- Failure atlas is session memory only — not a durable leak.
- Third-party managed provider calls leave content off-machine — not a local file but privacy-relevant.

---

## Links

- [Chapter 3 — Install](03-standing-up-an-install.md)
- [Chapter 19 — Operating an install](19-operating-an-install.md)
- [Chapter 23 — Security posture](23-security-posture.md)
- User docs: [Configuration](../configuration.md) · [Trust and safety](../trust-and-safety.md)
- Audit: `docs-audit/STATE-MODEL.md`
