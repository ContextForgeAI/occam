# Chapter 25 — Diagnosing a bad result

**Status:** STABLE · **Prerequisites:** [Chapter 2](02-honesty-contract.md), [Chapter 5](05-acquisition-ladder.md), [Chapter 6](06-when-acquisition-is-hard.md), [Chapter 12](12-authoring-playbook.md), [Chapter 20](20-automatic-behaviors.md)

---

## Mental model

**A decision tree keyed on `failure.code` + `recovery[]` + `quality.verdict`,** with an explicit branch for "the response looks fine and the cause is invisible." Diagnosis is partly elimination — not every cause is reported in the response.

---

## Explanation

### Start here: `ok` and honesty contract

| Signal | Meaning | Next action |
|--------|---------|-------------|
| `ok:false` | Content **UNKNOWN** — never fill from model memory | Read `failure.code`; inspect `recovery[]` and `agentMeta.decisions` |
| `ok:true` + `quality.verdict=thin_extract` | Should not happen on success path — investigate quality block |
| `ok:true` + `short_quality` | Genuinely short good page — do **not** heal or escalate for size alone |
| `ok:false` + `thin_extract` | Bad extraction — escalate/heal/playbook candidate |

### Failure-code branches (acquisition)

| Code | Likely cause | Lever |
|------|--------------|-------|
| `http_404` / `http_410` | Ladder **terminated** — no browser | Do not expect browser in `recovery[]` |
| `http_403` / `captcha_or_challenge` / `requires_login` | Wall | Session + browser; not CAPTCHA solver |
| `timeout` | Backend exhausted | Retry, different policy, or accept failure |
| `workers_unavailable` | Missing workers or backend not ready | `occam doctor`, check `OCCAM_HOME` |
| `thin_extract` | Post-processor downgrade | Browser escalation if not terminated; playbook heal |
| `private_url_blocked` | SSRF policy | Expected for private URLs on guarded tools |
| `network_error` | DNS/TLS/network — **may mask SSRF block on probe** | Do not retry private URLs forever |

Read `recovery[]` for attempted backends and `escalationReason`.

### Playbook and pool causes

- Playbook stopped matching → compare `playbook_policy=auto` vs `off`.
- Browser pool died → second WS session or refresh collateral — invisible in response.
- Public-reference HTTP failure → browser skipped **silently** — looks like ordinary HTTP fail.

### Operator verbs when stuck

1. `occam doctor` — dependencies and worker paths.
2. Inspect env — proxy holes, receipts policy, profile, opt-in flags.
3. `occam session` — credentials for login walls.
4. Avoid `occam refresh` unless you accept machine-wide kill.

### Causes with no signal (elimination required)

| Invisible cause | How you notice |
|-----------------|----------------|
| Public-reference browser skip | HTTP failure only; no "skipped browser" flag |
| TSA anchor failure | Vanishes silently — receipt identical to unanchored |
| Corrupt watch store | Resets empty |
| Batch persist IO failure | Swallowed |
| Playwright proxy fail-open | Fetch without proxy |
| ThinExtractBrowserExhausted | Under-described in `agentMeta.decisions` |
| Unknown host CLI arg | Stdio starts and blocks |

Use [Chapter 20](20-automatic-behaviors.md) and [Chapter 22](22-configuration.md) when the response fields run dry.

---

## CHECK

**LOCAL.** Deliberately unset `OCCAM_HOME`, call `occam_transcode`, and match the response against the tree — expect `workers_unavailable` or similar, then restore env and confirm doctor fixes paths.

---

## Common misconception

**"`workers_unavailable` means the network failed."** It means worker paths are missing or, under `http_then_browser`, that **either** backend is not ready. Run doctor and check `OCCAM_HOME`.

---

## Limitations

- Probe may mis-predict browser-only pages (HTTP-only, never escalates).
- Map on JS sites may return empty link sets.
- Not every automatic behavior leaves a response field.
- Failure atlas (opt-in) is session-scoped diagnostic aid — not durable ops telemetry.
- Diagnosis cannot prove what the origin "really" showed — only what Occam reported.

---

## Links

- [Chapter 2 — Honesty contract](02-honesty-contract.md)
- [Chapter 5 — Acquisition ladder](05-acquisition-ladder.md)
- [Chapter 6 — Walls and sessions](06-when-acquisition-is-hard.md)
- [Chapter 19 — Operating an install](19-operating-an-install.md)
- User docs: [Failure codes](../failure-codes.md) · [Troubleshooting](../troubleshooting.md)
- Audit: `docs-audit/FAILURE-BEHAVIOR-MAP.md`
