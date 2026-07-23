# occam_playbook_heal

When a transcode fails on a hard site with no recipe: capture the page's DOM skeleton plus selector
candidates so **you** can draft a playbook for it. This tool gathers evidence; it does not write a
recipe.

## When to use

- After an `ok:false` transcode whose failure response suggested it
  (`agentHints.suggestedNext: "occam_playbook_heal"`), typically on `thin_extract`.
- Then: draft the recipe JSON → [`occam_playbook_lint`](occam_playbook_lint.md) →
  [`occam_playbook_save`](occam_playbook_save.md).
- Not for provable walls (captcha/login) — those are not healable by selectors.

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `url` | string | — | **yes** | Absolute HTTP(S) URL to heal |
| `failure_reason` | string | — | **yes** | The prior `failure.code` from `occam_transcode` (e.g. `thin_extract`) |
| `session_profile` | string? | null | no | Same profile you used with the failing transcode |
| `max_skeleton_nodes` | int | `600` | no | Max skeleton nodes (cap 600) |

## Returns

Success envelope:

- `ok: true`, `url`, `failureReason`
- `domSkeleton` — `{root, stats: {nodeCount, maxDepth, interactiveCount}}`; each node is
  `{tag, id?, class[]?, role?, testId?, aria?, text?, interactive, children[]?}`
- `anchors` — `{landmarks[], dataTestIds[], mainCandidates[]}`; each candidate is
  `{selector, textAnchor?, score}` — the ranked guesses for the page's main content selector
- `agentHints` — `{suggestedNext, doNot[], maxVerifyRetries}`

Failure envelope: `ok: false`, `url`, `failureReason`, `failureCode`, `message`, `agentHints?`.

## Failure codes

`heal_not_applicable` (this failure isn't selector-healable), `heal_failed`, plus fetch-level codes
(`timeout`, `network_error`, `captcha_or_challenge`, …) when the capture itself could not load the
page. See [failure codes](../failure-codes.md).

## Example

Call:

```json
{ "url": "https://spa.example/app/article/42", "failure_reason": "thin_extract" }
```

Trimmed response:

```json
{
  "ok": true,
  "url": "https://spa.example/app/article/42",
  "failureReason": "thin_extract",
  "anchors": {
    "landmarks": ["main", "article"],
    "dataTestIds": ["article-body"],
    "mainCandidates": [ { "selector": "[data-testid=article-body]", "textAnchor": "…", "score": 0.87 } ]
  },
  "agentHints": { "suggestedNext": "occam_playbook_save", "doNot": ["do not loop heal more than once per url per turn"], "maxVerifyRetries": 2 }
}
```

## Related

- [occam_playbook_lint](occam_playbook_lint.md) — validate the recipe you draft
- [occam_playbook_save](occam_playbook_save.md) — save + verify it
- [Failure codes](../failure-codes.md)
