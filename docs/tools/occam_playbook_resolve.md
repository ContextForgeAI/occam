# occam_playbook_resolve

Look up the saved extraction recipe (playbook/genome) for a URL or host: content selectors,
knowledge schema, agent notes, and a signature trust status. Read-only.

## When to use

- Before transcode/extract on a known site — see whether a tuned recipe exists and what it routes.
- Before [`occam_extract_knowledge`](occam_extract_knowledge.md) — confirm the host has a
  `knowledge_schema`.
- Authoring a recipe → [`occam_playbook_heal`](occam_playbook_heal.md) /
  [`occam_playbook_save`](occam_playbook_save.md); this tool never writes.

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `url` | string | — | **yes** | HTTP/HTTPS URL, or bare hostname (e.g. `nginx.org`) |
| `schema_version` | string | `1.0` | no | Playbook schema version to negotiate |
| `include_lessons` | bool | `false` | no | Export `lessons[]` from the local tier (max 10) |
| `fetch_site_genome` | bool | `false` | no | Fetch `https://{host}/.well-known/agent-genome.v1.json` |

## Returns

Success envelope:

- `ok: true`, `url`, `matchedHost`, `playbookId`, `schemaVersion`, `provenance`, `sourcePath`, `timestamp`
- `contentSelectors[]?`, `preferredBackend?`, `agentNotes?`, `pageClass?`
- `genome?` / `knowledgeSchema?` — the raw JSON blocks of the winning recipe
- `genomeFetch?` — `{ok, wellKnownUrl, failureCode?, cacheHit}` when `fetch_site_genome=true`
- `lessons?`, `schemaVersionWarning?`
- `signature?` — trust signal for the winning recipe: `{present, status, keyId?, score?, passesGate?}`
  with `status` ∈ `unsigned` | `verified` | `invalid` | `unknown_key`

Failure envelope: `ok: false`, `url`, `failureCode`, `message`, `agentHints?`, `timestamp`.

## Failure codes

`invalid_arguments`, `playbook_not_found` (no recipe matches the host — just use plain
[`occam_transcode`](occam_transcode.md)). See [failure codes](../failure-codes.md).

## Example

Call:

```json
{ "url": "https://nginx.org/en/docs/", "include_lessons": true }
```

Trimmed response:

```json
{
  "ok": true,
  "matchedHost": "nginx.org",
  "playbookId": "nginx.org",
  "schemaVersion": "1.0",
  "provenance": "local",
  "contentSelectors": ["#content"],
  "preferredBackend": "http",
  "signature": { "present": true, "status": "verified", "keyId": "k1:…", "passesGate": true }
}
```

## Related

- [occam_extract_knowledge](occam_extract_knowledge.md) — uses the resolved `knowledge_schema`
- [occam_playbook_save](occam_playbook_save.md) / [occam_playbook_lint](occam_playbook_lint.md)
- [occam_transcode](occam_transcode.md) — `playbook_policy=auto` applies this resolution internally
