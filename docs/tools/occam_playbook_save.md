# `occam_playbook_save`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamPlaybookSaveTool.cs` @ 2026-09-29  
**Profiles:** `full` only (`OCCAM_PROFILE=full`). Hidden on default `reader`, `researcher`, `auditor`.  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Save a drafted playbook/genome JSON to the **local playbook tier only**. Default `verify=true` dry-runs a live transcode with the draft overlaid and rejects the write if schema or quality gate fails. Every successful write is signed locally (`playbook-sig-v2`); independent of `OCCAM_RECEIPTS`.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| Final step after heal → review → lint | Ordinary page read → [`occam_transcode`](occam_transcode.md) |
| Persist a reviewed recipe for later resolve | Default `reader` — set `full` for authoring |
| Append `lesson_note` on a good save | Overwriting bundled seeds under `profiles/playbooks/seeds/` |

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `url` | yes | — | Host key URL for playbook id resolution |
| `playbook_json` | yes | — | Full playbook JSON (`schema_version` 1.x) |
| `verify` | no | `true` | Dry-run transcode before write |
| `verify_url` | no | `url` | Verify URL (must match playbook `hosts[]`) |
| `lesson_note` | no | omit | Note appended to `lessons[]` on success (max 500 chars) |
| `failure_reason` | no | omit | Optional failure-code stored with the lesson |
| `host_id` | no | omit | Optional host id for the lesson entry |

## Response shape

**Success:** `playbookId`, `writtenPath`, optional `verify` metrics, `lessonAppended`, `signedKeyId?`.

**Failure:** `failureCode`, `message`; optional `verify`, `agentHints`.

## Common failure codes

`invalid_url`, `playbook_schema_invalid`, `playbook_save_rejected`, `playbook_verify_failed`, `playbook_verify_low_score`, `playbook_verify_high_noise`.

Full taxonomy: [failure-codes](../failure-codes.md).

## Related env vars

| Variable | Effect |
|----------|--------|
| `OCCAM_PROFILE` | Must be `full` |
| `OCCAM_PLAYBOOKS_LOCAL_ROOT` | Local-tier write directory (default `~/.occam/playbooks/local`) |

## See also

- [Playbooks](../playbooks.md) · [`occam_playbook_heal`](occam_playbook_heal.md) · [`occam_playbook_lint`](occam_playbook_lint.md) · [`occam_playbook_resolve`](occam_playbook_resolve.md)
