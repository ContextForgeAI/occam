# occam_playbook_save

Save an extraction playbook/genome JSON you drafted — locally only. By default (`verify=true`) it
dry-runs a transcode with the recipe and rejects one that fails the quality gate.

## When to use

- The last step of the heal loop: [`occam_playbook_heal`](occam_playbook_heal.md) → draft JSON →
  [`occam_playbook_lint`](occam_playbook_lint.md) → save.
- Lint first — it catches schema errors without paying for the verify fetch.

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `url` | string | — | **yes** | Host key URL for playbook id resolution |
| `playbook_json` | string | — | **yes** | Full playbook JSON document (schema_version 1.x) |
| `verify` | bool | `true` | no | Dry-run transcode before write; rejects on gate failure |
| `verify_url` | string? | null | no | URL used for the verify transcode (default: `url`) |
| `lesson_note` | string? | null | no | Lesson note appended on a verified save (1–500 chars) |
| `failure_reason` | string? | null | no | Failure-code echo for the lesson entry |
| `host_id` | string? | null | no | Host id for the lesson entry (never secrets) |

## Returns

Success envelope:

- `ok: true`, `playbookId`, `writtenPath`
- `verify?` — `{passesGate, score, noiseLeakage}` when a verify ran
- `lessonAppended` — whether the lesson note was recorded
- `signedKeyId?` — the key the saved playbook was signed with (the recipe is self-authenticating)

Failure envelope: `ok: false`, `url`, `failureCode`, `message`, `verify?` (the failing gate
numbers), `agentHints?`.

## Failure codes

`playbook_schema_invalid` (rejected JSON / bad `lesson_note`), `playbook_verify_failed` (dry-run
failed the quality gate — revise selectors), `playbook_save_rejected`. See
[failure codes](../failure-codes.md).

## Example

Call:

```json
{
  "url": "https://spa.example",
  "playbook_json": "{\"schema_version\":\"1.0\",\"id\":\"spa.example\",\"hosts\":[\"spa.example\"],\"extract\":{\"contentSelectors\":[\"[data-testid=article-body]\"]}}",
  "lesson_note": "SPA shell; article body only mounts under data-testid=article-body"
}
```

Trimmed response:

```json
{
  "ok": true,
  "playbookId": "spa.example",
  "writtenPath": "…/playbooks/spa.example.json",
  "verify": { "passesGate": true, "score": 86, "noiseLeakage": 0.04 },
  "lessonAppended": true,
  "signedKeyId": "k1:…"
}
```

## Related

- [occam_playbook_lint](occam_playbook_lint.md) — static check before the live verify
- [occam_playbook_resolve](occam_playbook_resolve.md) — read it back
- [occam_playbook_heal](occam_playbook_heal.md) — where the draft evidence came from
