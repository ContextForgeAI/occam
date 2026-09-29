# `occam_playbook_resolve`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamPlaybookResolveTool.cs` @ 2026-09-29  
**Profiles:** `full` only (`OCCAM_PROFILE=full`). Hidden on default `reader`, and on `researcher` / `auditor`.  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Look up the saved extraction recipe (playbook/genome) for a URL or host: content selectors, `knowledge_schema` (when it matches the URL), agent notes, optional lessons, optional site well-known genome fetch metadata, and **signature trust status** against your local verification key. Read-only.

Signature status is **integrity vs your local key** — not author identity, not a trusted registry, and not proof of recipe quality.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| Inspect `knowledgeSchema` / `pageClass` before extract (on `full`) | You only need page markdown → [`occam_transcode`](occam_transcode.md) |
| Authoring — check what exists before heal/save | Host uses default `reader` — tool not on surface; set `full` or use internal resolve |
| Signed-recipe `signature.status` before trusting selectors | You expect live page body — resolve never fetches the page (only optional well-known JSON) |

Tier order: [Playbooks](../playbooks.md).

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `url` | yes | — | HTTP(S) URL, search handle (`S1`, `H…`), or bare hostname. |
| `schema_version` | no | `1.0` | Schema version to negotiate; empty → `1.0`. |
| `include_lessons` | no | `false` | Export redacted `lessons[]` from the **local** tier only (max 10). |
| `fetch_site_genome` | no | `false` | Fetch well-known agent-genome JSON; also when `OCCAM_SITE_GENOME_FETCH` is `1`/`true`. |

## Response shape

### Success (`ok: true`)

Always: `url`, `matchedHost`, `playbookId`, `schemaVersion`, `provenance`, `sourcePath`, `timestamp`.

Optional: `contentSelectors`, `preferredBackend`, `agentNotes`, `genome`, `knowledgeSchema` (URL-matched only), `pageClass`, `genomeFetch`, `lessons`, `schemaVersionWarning`, `signature` (`present`, `status`, `keyId?`, `score?`, `passesGate?`, `sigVersion?`).

`provenance`: `local` \| `user` \| `community` \| `seed` \| `site`.

### Failure (`ok: false`)

`url`, `failureCode`, `message`, `timestamp`; optional `agentHints`. Never infer page content.

## Common failure codes

| Code | When |
|------|------|
| `invalid_arguments` | Missing/invalid URL or hostname |
| `playbook_not_found` | No tier match and no successful well-known genome |
| `unknown_handle` / `stale_handle` | Search handle missing or expired |

Full taxonomy: [failure-codes](../failure-codes.md).

## Related env vars

| Variable | Effect |
|----------|--------|
| `OCCAM_PROFILE` | Must be `full` for this tool in `tools/list` |
| `OCCAM_SITE_GENOME_FETCH` | Enables site genome fetch like the param |
| `OCCAM_HOME` | Seeds / community under install tree |
| `OCCAM_PLAYBOOKS_LOCAL_ROOT` / `WT_PLAYBOOKS_PATH` | Local / user playbook dirs |

Full catalog: [configuration](../configuration.md).

## See also

- [Playbooks](../playbooks.md) · [`occam_playbook_heal`](occam_playbook_heal.md) · [`occam_playbook_save`](occam_playbook_save.md) · [`occam_playbook_lint`](occam_playbook_lint.md)
- [`occam_transcode`](occam_transcode.md) · [`occam_extract_knowledge`](occam_extract_knowledge.md)
