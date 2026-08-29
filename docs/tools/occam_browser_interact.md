# occam_browser_interact

**Opt-in.** Absent from `tools/list` until `OCCAM_BROWSER_ACTIONS_MCP=1`.

Run a short declarative browser action plan, then materialize Markdown + Receipt v1
(same honesty rules as `occam_transcode`).

## When to use

- Need a click / type / wait before the page is readable
- Prefer a closed action vocabulary over raw page JavaScript

## Parameters

| Param | Required | Notes |
|-------|----------|-------|
| `url` | yes | Target page |
| `actions` | yes | JSON array, max 16 steps |
| `session_profile` | no | Operator cookie profile |
| `focus_query` | no | Token focus after materialize |
| `max_tokens` | no | Budget override |
| `deadline_ms` | no | Per-call deadline |

Allowed `do` values: `wait`, `wait_selector`, `wait_text`, `click`, `hover`, `type`, `press`, `scroll`.
First failure stops remaining steps (`failure.code=action_failed` — see [failure codes](../failure-codes.md)).
Typed text is **redacted** from `actionTrace`. Results are **never cached**. Raw page JS
(`js_before_wait` / `wait_for.js`) is **not** exposed. On success, `actionPlanHash` is returned on
the response and, when receipts are on, also folded into `receipt.signed.actionPlanHash`.

## Related

- [Tools reference — Browser interact](../tools-reference.md#browser-interact-occam_browser_actions_mcp1)
- [Experimental](../experimental.md)
- [Opt-in surfaces (handbook)](../handbook/17-opt-in-surfaces.md)
