# `occam_client_capabilities`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamClientCapabilitiesTool.cs` @ 2026-09-29  
**Profiles:** `reader` (default), `researcher`, `auditor`, `full` — **not** exposed on `minimal` or `basic` (`OccamToolProfile`)  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Declare this LLM’s context window so Occam can store a process-scoped output budget and apply it to later `occam_transcode` / `occam_digest` calls that omit explicit token limits. MCP does not expose model context size; the agent (or operator env bootstrap) must supply it once per host process.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| Session start and you know the model context window in tokens | The host profile is `minimal` or `basic` (tool hidden — set `OCCAM_CLIENT_CONTEXT_TOKENS` instead) |
| Before reads that omit `max_tokens` / `per_url_max_tokens` and should inherit a safe default | Every read already passes explicit `max_tokens` or `per_url_max_tokens` |
| You need to inspect or clear the in-process override | You only need a one-off full payload and no ambient budget is configured |

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `context_tokens` | No (omit to inspect) | `null` | Context window in tokens. Required **value** to configure; omit to inspect. When set, must be in **1024–2 000 000** (`ClientCapabilityStore`). |
| `model_id` | No | `null` | Optional model id string for the response. |
| `clear` | No | `false` | If true, clear the session override and re-read `OCCAM_CLIENT_CONTEXT_TOKENS` env bootstrap. |

## Response shape

JSON object (camelCase):

- **Success (`ok: true`):** `configured`, `contextTokens`, `outputBudgetTokens`, `suggestedProfile`, `modelId`, `source`, `note`.
  - After configure, `outputBudgetTokens` ≈ **20%** of `context_tokens`, rounded and clamped to **512–16 384**.
  - `suggestedProfile` is **advisory only** (`reader` if context < 8192, `researcher` if < 32768, else `full`).
  - `source` is `tool` when set via this call, or `env` when loaded from operator env.
- **Failure (`ok: false`):** top-level `failureCode` (not nested `failure.code`), `note`, other fields nulled. This tool does not describe page content.

When a budget **is** configured, later `occam_transcode` / `occam_digest` calls that omit `max_tokens` / `per_url_max_tokens` inherit `outputBudgetTokens`. Explicit limits still win. When **not** configured, omitting those params does not apply a budget.

## Common failure codes

| Code | When |
|------|------|
| `invalid_arguments` | `context_tokens` outside 1024–2 000 000 |

Full taxonomy: [failure codes](../failure-codes.md).

## Related env vars

| Variable | Role |
|----------|------|
| `OCCAM_CLIENT_CONTEXT_TOKENS` | Operator bootstrap (1024–2 000 000); re-read after `clear: true` |
| `OCCAM_CLIENT_MODEL_ID` | Optional label on env bootstrap |

Full catalog: [configuration](../configuration.md).

## See also

- [occam_transcode](occam_transcode.md) — inherits ambient budget via `max_tokens`
- [occam_digest](occam_digest.md) — inherits via `per_url_max_tokens`
- [Choosing a tool](../choosing-a-tool.md)
