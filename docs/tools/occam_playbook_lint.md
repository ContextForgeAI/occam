# occam_playbook_lint

Statically validate a playbook/genome JSON against the 1.x schema — no network, pure and
deterministic.

## When to use

- Before a live [`occam_playbook_save`](occam_playbook_save.md) — catch schema errors without
  paying for the verify fetch.
- Vetting a community/site genome before trusting it.

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `playbook_json` | string | — | **yes** | The playbook/genome JSON to validate (a JSON object) |

## Returns

A lint report (this tool has no `ok:false` failure envelope — malformed input is itself reported as
errors):

- `grade` — `ready` | `usable` | `broken`
- `agentReady` — whether an agent can use the recipe as-is
- `errors`, `warnings`, `infos` — counts
- `issues[]` — `{severity, field, code, message}`

Severity meaning: **errors** break resolve/save (missing `schema_version` / `id` / `hosts` /
`extract.contentSelectors`); **warnings** degrade quality (bad backend, non-bare host, unrouted
`knowledge_schema` class); **infos** are nudges.

## Example

Call:

```json
{ "playbook_json": "{\"id\":\"spa.example\",\"hosts\":[\"spa.example\"]}" }
```

Trimmed response:

```json
{
  "grade": "broken",
  "agentReady": false,
  "errors": 2,
  "warnings": 0,
  "issues": [
    { "severity": "error", "field": "schema_version", "code": "missing", "message": "schema_version is required" },
    { "severity": "error", "field": "extract.contentSelectors", "code": "missing", "message": "at least one content selector is required" }
  ]
}
```

## Related

- [occam_playbook_save](occam_playbook_save.md) — the live save this lint front-runs
- [occam_playbook_heal](occam_playbook_heal.md) — where recipe drafts come from
