# Chapter 17 — Opt-in surfaces: watch, batch, crosscheck, atlas

**Status:** EXPERIMENTAL (all four surfaces) · **Prerequisites:** [Chapter 10](10-many-sources-digest.md), [Chapter 14](14-what-a-receipt-proves.md), [Chapter 18](18-exposure.md)

---

## Mental model

**Four surfaces the default deployment does not have, each with a named gate and a named ceiling.** They are opt-in because of what they lack, not because they are new. Enable only when you accept those limits.

---

## Explanation

Default product `tools/list` uses **`OCCAM_PROFILE=reader`** (8 tools). Set `OCCAM_PROFILE=full` for all **15 core tools**. Four additional env flags add **6 more MCP tools** (opt-in are **not** profile-filtered — `OCCAM_PROFILE=reader` with `OCCAM_CONSENSUS_MCP=1` still exposes `occam_crosscheck`). A fifth flag adds browser interact.

| Env gate | Tool(s) | Class |
|----------|---------|-------|
| `OCCAM_WATCH_MCP=1` | `occam_watch` | EXPERIMENTAL |
| `OCCAM_BATCH_MCP=1` | `occam_batch_submit`, `occam_batch_status`, `occam_batch_results` | EXPERIMENTAL |
| `OCCAM_CONSENSUS_MCP=1` | `occam_crosscheck` | EXPERIMENTAL — multi-source comparison only |
| `OCCAM_ATLAS_MCP=1` | `occam_failure_atlas` | EXPERIMENTAL |
| `OCCAM_BROWSER_ACTIONS_MCP=1` | `occam_browser_interact` | EXPERIMENTAL — declarative browser actions + materialize |

### `occam_watch` (change monitoring)

- **No daemon** — cadence is the agent's job.
- **No un-watch** — `IWatchStore.Remove` has no product caller; URL set is uncapped while per-URL history caps at 64 entries.
- Corrupt store can silently reset to empty; multi-process writes race.
- History entries may carry signatures when receipts are enabled; **`history_verified` requires all entries signed and verified** (chain integrity is reported separately for unsigned chains).

### Batch jobs

- Produces **no Receipt v1** on job results.
- Retains full markdown in `jobs.json` indefinitely — **no delete API**.
- Last-writer-wins across processes.
- Related but distinct: `--batch-server` HTTP mode (loopback, no auth) vs MCP batch tools.

### `occam_crosscheck` (multi-source comparison)

- Same host, same process, same egress IP, same proxy — vantages differ by engine (HTTP vs browser) and session cookies.
- Agreement excludes one cloaking axis (bot-vs-browser, anon-vs-authed).
- Verdict is **unsigned observation** — no shipped tool re-derives it from vantage receipts.
- **Never "consensus proof."** Never multi-node attestation. Never proof of correctness.

Per owner decision OD-8: canonical concept is **multi-source comparison / source agreement**.

### `occam_failure_atlas`

- Per-session in-memory telemetry only — not durable analytics.
- Enabling it **replaces** the host telemetry sink.
- Not a cross-session leak (atlas is session-scoped).

### Should you enable?

| Need | Consider | Reject if |
|------|----------|-----------|
| Notice page hash changes over time | watch | You need a daemon, guaranteed eviction, or multi-process safety |
| Bulk URL jobs via MCP | batch | You need signed results or retention control |
| Compare HTTP vs browser fingerprints | crosscheck | You need proof of genuineness or third-party attestation |
| Session failure patterns | atlas | You need durable ops analytics |

---

## CHECK

**LOCAL.** Enable `OCCAM_CONSENSUS_MCP=1` together with `OCCAM_PROFILE=reader`. Call `tools/list` and assert `occam_crosscheck` appears — opt-ins are not profile-filtered.

**NETWORK (optional).** Run crosscheck on a URL; inspect that the verdict JSON has no signature and cannot be verified by any `occam_verify` mode.

---

## Common misconception

**"Crosscheck proves the content is genuine."** All vantages leave one process, one egress IP, and one proxy configuration. Agreement is an unsigned observation that narrows one cloaking hypothesis — not consensus proof, not multi-party attestation, not cryptographic evidence of correctness.

---

## Limitations

- All four surfaces are EXPERIMENTAL — behavior and retention gaps are known.
- Batch: no receipts, unbounded retention, no delete API.
- Watch: no daemon, no un-watch, concurrency races.
- Crosscheck: unsigned verdict; DO NOT_DOCUMENT_AS_FEATURE for trust claims.
- Atlas: session memory only; enabling swaps telemetry sink.
- None of these surfaces establish truth, origin, identity, or trusted time.

---

## Links

- [Chapter 18 — Exposure](18-exposure.md)
- [Chapter 19 — Operating an install](19-operating-an-install.md)
- [appendix-status-labels.md](appendix-status-labels.md)
- User docs: [Configuration](../configuration.md) · [Tools index](../tools/index.md)
- Audit: `docs-audit/DOCUMENTATION-EXPOSURE-MATRIX.md` §1–§2 · OD-8
