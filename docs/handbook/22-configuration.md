# Chapter 22 — Configuration and its negative space

**Status:** STABLE · **Prerequisites:** [Chapter 5](05-acquisition-ladder.md), [Chapter 14](14-what-a-receipt-proves.md), [Chapter 18](18-exposure.md), [Chapter 20](20-automatic-behaviors.md)

---

## Mental model

**Env vars are inputs to behavior, not behavior.** The teachable content is negative space: coverage holes, fail-open defaults, and names that promise more than they gate.

---

## Explanation

The full environment-variable catalog lives in user docs ([Configuration](../configuration.md)) and Appendix B of the handbook plan — **this chapter explains the holes**, not every knob.

### Names that overpromise

| Variable | What people assume | What code does |
|----------|-------------------|----------------|
| `OCCAM_RECEIPTS=off` | All signing off | Key still minted; playbook save still signs; `contentHash` and Merkle math still ship |
| `OCCAM_HTTP_PROXY` / `OCCAM_HTTPS_PROXY` | All network obeys proxy | Worker egress only — Core C# `HttpClient`s (probe, map, managed, search) **ignore** proxy env |
| `OCCAM_MANAGED_DOMAINS` unset | No managed hosts | **All hosts eligible** when provider configured |
| `OCCAM_BATCH_DB_PATH` ending in `.db` | SQLite store | Store forces `.json` — file is JSON |
| `OCCAM_CHUNK_SIZE` | Tokens | **Characters**, not tokens |
| `OCCAM_RESPECT_ROBOTS` | Polite crawling | Off by default; **fail-open** on robots fetch error |
| Playwright proxy resolution | Always uses proxy when set | **Fail-open** to no proxy on resolution failure |
| Empty `OCCAM_PROXY_LIST_FILE` | Fall back to inline list | **Suppresses** inline list entirely |

`OCCAM_RECEIPTS` is parsed in two places and gates neither key minting nor playbook signing.

### Proxy rotation gaps

Rotation does not reach HTTP daemon, browser pool, css-extract, or dom-skeleton spawns. Core's own HTTP clients ignore `OCCAM_*` proxy entirely.

### Profile vs opt-in

`OCCAM_PROFILE` subsets core tools. Opt-in flags (`OCCAM_BATCH_MCP`, `OCCAM_WATCH_MCP`, `OCCAM_CONSENSUS_MCP`, `OCCAM_ATLAS_MCP`) are **orthogonal** — not profile-filtered.

### Platform deltas

Job objects vs process groups, cache paths, SIMD tier, path separators — **mechanism only**. No semantic behavior differences to hunt for across OS.

### Mapping surprises to variables

For each surprise in [Chapter 20](20-automatic-behaviors.md), ask which env var would — or provably would **not** — have prevented it. Key mint: no disable. Save signing: no disable. Refresh kill: no scope flag. Onboard merge: delete `~/.occam/onboard.json`.

---

## CHECK

**NETWORK.** Set `OCCAM_HTTP_PROXY` to a dead address. Run `occam_probe` on a public URL — it succeeds, proving the Core HTTP client never used the proxy.

**LOCAL.** Set `OCCAM_RECEIPTS=off`, restart host, call `occam_playbook_save` — observe signing still occurs.

---

## Common misconception

**"`OCCAM_HTTP_PROXY` routes every network operation."** It reaches worker egress only. Probe, map, managed acquisition, and search use Core HTTP clients that ignore it; rotation additionally misses several worker spawn paths.

---

## Limitations

- Negative space changes with releases — verify against `ENVIRONMENT-VARIABLES.md` and gate selftests.
- Onboard.json env merge has no per-key audit trail in MCP responses.
- Managed provider keys and domains are operator secrets — not documented here as values.
- Cosign and npm are configuration-adjacent packaging topics — see [Chapter 19](19-operating-an-install.md).

---

## Links

- [Chapter 20 — Automatic behaviors](20-automatic-behaviors.md)
- [Chapter 19 — Operating an install](19-operating-an-install.md)
- User docs: [Configuration](../configuration.md)
- Audit: `docs-audit/CONFIG-NEGATIVE-SPACE.md` · `docs-audit/ENVIRONMENT-VARIABLES.md`
