# occam_playbook_resolve

Look up the saved extraction recipe (playbook/genome) for a URL or host: content selectors,
knowledge schema, agent notes, and **signature integrity status** relative to your local key.
Read-only.

Signature status is **integrity vs your local key** — not author identity, not a trusted registry, and
not proof of recipe quality.

## When to use

- Before transcode/extract on a known site — see whether a tuned recipe exists.
- Before [`occam_extract_knowledge`](occam_extract_knowledge.md) — confirm `knowledge_schema`.
- Authoring → [`occam_playbook_heal`](occam_playbook_heal.md) / [`occam_playbook_save`](occam_playbook_save.md).

See [Playbooks](../playbooks.md) for resolution order and v1 vs v2 semantics.

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `url` | string | — | **yes** | HTTP/HTTPS URL, or bare hostname |
| `schema_version` | string | `1.0` | no | Playbook schema version |
| `include_lessons` | bool | `false` | no | Export `lessons[]` from local tier (max 10) |
| `fetch_site_genome` | bool | `false` | no | Fetch `https://{host}/.well-known/agent-genome.v1.json` |

## Returns

Success envelope:

- `ok: true`, `url`, `matchedHost`, `playbookId`, `schemaVersion`, `provenance`, `sourcePath`
- `contentSelectors[]?`, `preferredBackend?`, `agentNotes?`, `pageClass?`
- `genome?` / `knowledgeSchema?`
- `signature?` — `{present, status, sigVersion?, keyId?, score?, passesGate?}`
  - `status` ∈ `unsigned` | `verified` | `invalid` | `wrong_key` | `key_mismatch` | `unsupported_version`
  - `sigVersion` — `1`, `2`, or null when unsigned
  - **v1:** body signed; gate fields in `provenance` are **unsigned**
  - **v2:** body + `keyId`, `signedAt`, gate snapshot tamper-evident — still heuristic, not quality proof

Failure envelope: `ok: false`, `playbook_not_found`, etc.

## Failure codes

`invalid_arguments`, `playbook_not_found`. See [failure codes](../failure-codes.md).

## Example

```json
{ "url": "https://nginx.org/en/docs/", "include_lessons": true }
```

```json
{
  "ok": true,
  "playbookId": "nginx.org",
  "signature": {
    "present": true,
    "status": "verified",
    "sigVersion": 2,
    "keyId": "k1:…",
    "passesGate": true,
    "score": 86
  }
}
```

## Related

- [Playbooks](../playbooks.md)
- [occam_playbook_save](occam_playbook_save.md) · [occam_playbook_lint](occam_playbook_lint.md)
- [occam_transcode](occam_transcode.md) — `playbook_policy=auto` applies resolution internally
