# Materialization

Why Occam returns **compiled** content instead of raw HTML.

**Status:** STABLE (Markdown + budgets) · some structured/diff/cache paths LIMITED or EXPERIMENTAL

## The point

Agents have finite context windows. Occam’s job after acquisition is to produce **usable** content:

- primary output: **Markdown**
- sized to a **token budget**
- optionally **focused** on what you asked for
- optionally emitted as **structured** blocks, tables, feeds, or chunks
- optionally compared as a **diff** / conditional re-check

The hash on a receipt binds this **compiled** form — not necessarily the origin’s raw bytes.

## Token budgets

| Mechanism | Role |
|-----------|------|
| `max_tokens` | Explicit budget for this call |
| `occam_client_capabilities` / `OCCAM_CLIENT_CONTEXT_TOKENS` | Ambient budget (~20% of declared context, clamped) |
| `fit_markdown` + `focus_query` | Prune toward relevance when over budget |

Omit knobs for a first read — defaults are intended to be enough.

Full advantage + knob flashcard (agents and humans): [Why Occam](why-occam.md).
There is **no** public MCP parameter to select an alternate knowledge codec; live
output is Markdown via the default passthrough codec.

## Structured outputs (opt-in)

On `occam_transcode` and related tools, parameters such as `json_blocks`, `json_tables`, `json_feed`, and chunk modes add sidecars. They are **not** required for ordinary reading.

Omitted content may be summarized in an omission manifest when fitting drops material — honesty about what was cut.

## Cache and identity

- Default: **live extract every call** (no file cache).  
- Opt-in: `cache_ttl_s > 0` enables a **local** response replay (not a CDN).  
- Cache / materialization keys include focus-sensitive identity (including URL fragment where applicable).  
- `if_none_match` / `diff_against` compare against Occam’s content hash of compiled output.

## Related

- [How Occam works](how-occam-works.md)
- [Client capabilities tool](tools/occam_client_capabilities.md)
- [Transcode](tools/occam_transcode.md)
- Handbook: Materialization chapters
