# occam_playbook_save

Save an extraction playbook/genome JSON locally. By default (`verify=true`) dry-runs a transcode with
the recipe and rejects failures of the **local quality gate heuristic**.

Saved playbooks are **signed unconditionally** — `OCCAM_RECEIPTS=off` does not disable playbook
signing. New saves emit **playbook-sig-v2** (tamper-evident gate snapshot). Signature proves
**integrity relative to your local key**, not author identity or guaranteed recipe quality.

## When to use

- Last step of the heal loop: [`occam_playbook_heal`](occam_playbook_heal.md) → draft →
  [`occam_playbook_lint`](occam_playbook_lint.md) → save.
- Lint first — catches schema errors without a live verify fetch.

See [Playbooks](../playbooks.md).

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `url` | string | — | **yes** | Host key URL for playbook id resolution |
| `playbook_json` | string | — | **yes** | Full playbook JSON (schema_version 1.x) |
| `verify` | bool | `true` | no | Dry-run transcode before write; rejects on gate failure |
| `verify_url` | string? | null | no | URL for verify transcode (default: `url`) |
| `lesson_note` | string? | null | no | Lesson note on verified save (1–500 chars) |
| `failure_reason` | string? | null | no | Failure-code echo for lesson entry |
| `host_id` | string? | null | no | Host id for lesson entry |

## Returns

Success envelope:

- `ok: true`, `playbookId`, `writtenPath`
- `verify?` — `{passesGate, score, noiseLeakage}` — **heuristic gate snapshot** (signed in v2, but not
  a quality guarantee)
- `lessonAppended`
- `signedKeyId?` — local key id used to sign — not third-party attestation

Failure envelope: `playbook_schema_invalid`, `playbook_verify_failed`, `playbook_save_rejected`.

## Failure codes

See [failure codes](../failure-codes.md).

## Example

```json
{
  "url": "https://spa.example",
  "playbook_json": "{\"schema_version\":\"1.0\",\"id\":\"spa.example\",\"hosts\":[\"spa.example\"],\"extract\":{\"contentSelectors\":[\"[data-testid=article-body]\"]}}",
  "lesson_note": "SPA shell; article body under data-testid=article-body"
}
```

```json
{
  "ok": true,
  "playbookId": "spa.example",
  "verify": { "passesGate": true, "score": 86, "noiseLeakage": 0.04 },
  "signedKeyId": "k1:…"
}
```

## Related

- [Playbooks](../playbooks.md)
- [occam_playbook_lint](occam_playbook_lint.md) · [occam_playbook_resolve](occam_playbook_resolve.md)
- [occam_playbook_heal](occam_playbook_heal.md)
