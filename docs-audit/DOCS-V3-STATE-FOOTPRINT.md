# DOCS-V3-STATE-FOOTPRINT (Phase 8I)

**Branch:** `docs/v3-canonical`  
**Date:** 2026-07-26  
**Sources:** `docs-audit/STATE-MODEL.md` (code-first; EF-054 import default corrected against shipped docs), public `docs/handbook/21-state-and-footprint.md`, `docs/sessions.md`, `docs/receipts.md`, `docs/configuration.md`.

**Verdict on “no file cache by design”:** Default extract does not reuse prior page content. Occam is **not** stateless — keys, sessions, playbooks, watch/batch stores, opt-in cache, host configs, and Playwright binaries persist on disk.

---

## Footprint table

| State | WHERE STORED | WHEN CREATED | CONTENTS | PERSISTENCE | HOW TO REMOVE | SECURITY NOTE |
|-------|--------------|--------------|----------|-------------|---------------|---------------|
| **Signing key** | `~/.occam/keys/signing-key.pem` (`OCCAM_KEYS_ROOT`) | First host start (`LoadOrCreate`) | ECDSA PKCS8 PEM, unencrypted | Until manual delete; reminted on next start if deleted | Delete file or entire `keys/` dir | **CRITICAL** — local identity; not gated by `OCCAM_RECEIPTS` |
| **Session profiles** | `~/.occam/sessions/<id>.json` (`OCCAM_SESSIONS_ROOT`) | `occam session import` / `export-state` / manual | Cookies, headers, metadata | Until operator deletes | `occam session` delete flows; rm files | **HIGH** — live credentials |
| **Playwright storageState** | `~/.occam/sessions/states/<name>.json` | `occam session export-state` | Browser cookies + localStorage | Until deleted | Same as sessions root | **HIGH** |
| **Import sources (`_imports/`)** | `~/.occam/sessions/_imports/` | `occam session import --keep-import` only | Plaintext cookie files if operator opted in | Until manual delete | Delete `_imports/` or omit `--keep-import` on future imports | **HIGH** when retained — default import does **not** keep copy (P6) |
| **Ephemeral header files** | `{TEMP}/occam-headers-{guid}.json` | Per fetch with merged headers | Request header bag | Deleted on scope dispose | Automatic (best-effort) | **HIGH** while alive |
| **Local playbooks** | `~/.occam/playbooks/local/` (`OCCAM_PLAYBOOKS_LOCAL_ROOT`) | `occam_playbook_save` | Signed playbook JSON + verify metadata | Until overwrite/delete | Delete files; overwrite by id | Medium — site recipes; save always signs |
| **Operator playbooks** | `WT_PLAYBOOKS_PATH` (if set) | Operator | Playbook JSON | Operator-owned | Operator manages path | Medium–HIGH |
| **Community / seed playbooks** | `{OCCAM_HOME}/profiles/playbooks/{community,seeds}/` | Install / upgrade | Unsigned seeds + manifest-checked community | Install lifetime | Reinstall; preserve on upgrade if tree kept | Medium — not authenticated publisher identity |
| **Transcode response cache** | `OCCAM_CACHE_DIR` or `{TEMP}/occam-cache/` | Transcode success when `cache_ttl_s > 0` | Full post-sign JSON envelope | TTL delete on read only; orphans may remain | Delete cache dir; set `cache_ttl_s=0` | **HIGH** — page markdown + receipt |
| **Watch store** | `~/.occam/watch/watch.json` (`OCCAM_WATCH_DB_PATH`) | `occam_watch` when `OCCAM_WATCH_MCP=1` | URLs, hashes, history chain | Opt-in; URL set uncapped; history capped 64/URL | Manual delete; `reset` per URL | **HIGH** — private URLs |
| **Batch job store** | `~/.occam/jobs/jobs.json` (`OCCAM_BATCH_DB_PATH`) | Batch MCP / `--batch-server` when enabled | Full job markdown results | **No eviction API** | Manual delete file | **HIGH** — retained page content |
| **Failure atlas** | In-memory DI singleton | `occam_failure_atlas` when `OCCAM_ATLAS_MCP=1` | Hostnames + failure codes (session-local) | Process/session lifetime | Process exit | Low — no body text |
| **Client ambient budget** | RAM (`ClientCapabilityStore`) | `occam_client_capabilities` or env bootstrap | Declared context tokens | Process lifetime | Restart host | Low |
| **Onboard config** | `~/.occam/onboard.json` (`OCCAM_CONFIG`) | `occam onboard` (may run before verify completes) | Env key/value map merged on launch | Permanent until deleted | Delete file | Medium–HIGH if secrets in env map |
| **Connect last-run** | `~/.occam/connect-last.json` | `occam connect` | Last connect report metadata | Permanent | Manual delete | Low |
| **Host MCP configs + backup** | Host-specific paths (e.g. `.cursor/mcp.json`) + `*.occam-bak` | `occam connect` | Launch command, env, server name | Until host or operator changes | Manual restore from `.occam-bak` (limits apply) | Medium — may reference `OCCAM_HOME` |
| **Skill install trees** | `~/.cursor/skills/occam`, etc. | Skill install scripts | Skill card copies | Until reinstall wipe | `rm` skill dir | Low |
| **Playwright browser cache** | OS cache paths / `PLAYWRIGHT_BROWSERS_PATH` | doctor / provision | Chromium binaries | Until manual clean | OS cache tools; doctor paths | Low |
| **Install tree (`OCCAM_HOME`)** | Release extract directory | Installer | Workers, seeds, scripts | Until uninstall/replace | Installer replaces tree (destructive) | Low — not user secrets |
| **Receipts in API responses** | Caller storage (not auto-persisted by host) | Eligible tool success | `receipt.signed`, hashes, optional TSA | Caller-owned | Caller deletes | Medium–HIGH if archived |
| **Dataset export artifacts** | Caller / CLI verify input | `occam_dataset_export` | Rows + manifest | Caller-owned | Caller deletes | Medium–HIGH |
| **Genome fetch cache** | Host RAM | Well-known fetch when enabled | Remote genome JSON | ~1h TTL or process exit | Disable fetch flags | Low–Medium |

---

## Outside the install directory

Removing `OCCAM_HOME` alone **does not** uninstall Occam. Surviving footprint:

- Entire `~/.occam/**` (unless manually removed)
- Host MCP configs and `*.occam-bak`
- Agent skill directories
- Playwright browser cache
- Temp cache orphans under `{TEMP}/occam-cache/`
- Third-party managed provider logs (off-machine) when configured

Documented in [handbook/21-state-and-footprint.md](../docs/handbook/21-state-and-footprint.md) · [trust/installation-safety.md](../docs/trust/installation-safety.md).

---

## Unbounded / weak cleanup paths

| State | Issue | Documented |
|-------|-------|------------|
| Batch `jobs.json` | No delete API; full markdown retained | [experimental.md](../docs/experimental.md) · handbook 17/21 |
| Watch URL set | Uncapped URLs; no MCP un-watch product path | [tools/occam_watch.md](../docs/tools/occam_watch.md) · handbook 17 |
| Response cache | TTL on read only; no sweep | [materialization.md](../docs/materialization.md) |
| Signing key | Permanent; remint if deleted | [receipts.md](../docs/receipts.md) |
| Multi-process batch/watch | Last-writer-wins races | handbook 21 |

---

## Privacy summary

| Sensitivity | Items |
|-------------|-------|
| **Credentials** | Session profiles, storageState, optional `_imports/`, ephemeral header temps |
| **Private key** | `signing-key.pem` |
| **Page content** | Opt-in cache, batch store, caller-persisted receipts/capsules |
| **Private URLs** | Watch store, batch jobs, stderr logs (operator capture) |
| **Third-party egress** | Managed/search/TSA paths (off-machine) |

---

## Phase 8I verdict

**PASS** — public handbook ch. 21, sessions, receipts, configuration, and operators docs align with code-first STATE-MODEL for operator-relevant footprint. **Note:** `docs-audit/STATE-MODEL.md` ST-03 row still describes pre-P6 `_imports/` default; public docs and shipped CLI default to **no retain** unless `--keep-import`.
