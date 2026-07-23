# occam_client_capabilities

Declare the client's context window once per host session so later reads can choose a safe default
output budget. Call with no arguments to inspect the current setting.

## When to use

- At session start when the model context size is known.
- Before `occam_transcode` or `occam_digest` calls that omit explicit token limits.
- To inspect or clear a previously configured session override.

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `context_tokens` | int? | null | no | Context window in tokens; required to configure, omitted to inspect |
| `model_id` | string? | null | no | Optional model label returned with the capability snapshot |
| `clear` | bool | `false` | no | Clear the session override and re-read the environment bootstrap |

Configured context sizes must be between 1024 and 2,000,000 tokens. The derived output budget is
approximately 20% of the context, clamped to 512–16,384 tokens. Explicit `max_tokens` or
`per_url_max_tokens` values still take precedence.

## Returns

Success returns `ok: true` with `configured`, `contextTokens`, `outputBudgetTokens`,
`suggestedProfile`, `modelId`, `source`, and `note`.

Invalid input returns `ok: false` with `failureCode: "invalid_arguments"`.

## Examples

Configure:

```json
{ "context_tokens": 128000, "model_id": "example-model" }
```

Inspect:

```json
{}
```

Clear:

```json
{ "clear": true }
```

## Related

- [Configuration — client context budget](../configuration.md#client-context-budget-occam_client_context_tokens)
- [occam_transcode](occam_transcode.md)
- [occam_digest](occam_digest.md)
