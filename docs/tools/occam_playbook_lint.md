# `occam_playbook_lint`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamPlaybookLintTool.cs` and `Playbooks/PlaybookLinter.cs` @ 2026-09-29  
**Profiles:** `full` and `auditor` only — not on `reader`, `researcher`, `minimal`, or `basic`  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Statically validate playbook/genome JSON against the 1.x schema. No network, no workers. Hard errors share `PlaybookSchemaGate` with save. Warnings/infos are lint-only nudges.

**Profile note:** `auditor` has lint but **not** heal/save/resolve. The heal → lint → save loop needs **`OCCAM_PROFILE=full`**.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| Cheap schema check before save | Live verify score → `occam_playbook_save` with `verify:true` (`full`) |
| Vetting hand-edited or heal draft JSON | No JSON yet — heal/author first |
| CI without fetching URLs | Default `reader` — switch to `auditor` or `full` |

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `playbook_json` | yes | — | Playbook/genome JSON object as a string |

## Response shape

**No** `{ ok, failure }` envelope. Always a lint report:

| Field | Meaning |
|-------|---------|
| `grade` | `ready` · `usable` · `broken` |
| `agentReady` | `true` iff `errors == 0` |
| `errors`, `warnings`, `infos` | Counts |
| `issues[]` | `{ severity, field, code, message }` |

Hard errors (block save): empty/invalid JSON, forbidden secrets, missing `schema_version` / `id` / `hosts` / `extract.contentSelectors`, unsupported schema major.

## Related env vars

None on the tool itself. Visibility via `OCCAM_PROFILE` — [configuration](../configuration.md).

## See also

- [`occam_playbook_heal`](occam_playbook_heal.md) · [`occam_playbook_save`](occam_playbook_save.md) · [`occam_playbook_resolve`](occam_playbook_resolve.md)
