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
| `fit_markdown` + `focus_query` | Prune toward relevance even when the full page fits; retain locally coupled instruction blocks before budgeting |

Omit knobs for a first read — defaults are intended to be enough.

Full advantage + knob flashcard (agents and humans): [Why Occam](why-occam.md).
There is **no** public MCP parameter to select an alternate knowledge codec; live
output is Markdown via the default passthrough codec.

## Structured outputs (opt-in)

On `occam_transcode` and related tools, parameters such as `json_blocks`, `json_tables`, `json_feed`, and chunk modes add sidecars. They are **not** required for ordinary reading.

Omitted content may be summarized in an omission manifest when fitting drops material — honesty about what was cut.

Focused fitting keeps locally coupled commands, procedural lists, and adjacent explanations together. Independent navigation links remain individually filterable. When a selected instruction cannot fit, an explicit omission replaces it rather than a heading or introduction without its steps.

`compile.omitted` can report `focus_filtered` even when `compile.truncated` is false: filtering and budget truncation are different operations. SNIP comments include removals within retained sections when space permits. `completeness.incompleteReason=focus_body_filtered` means the selected answer was lost during filtering; increasing the budget is not a remedy. `focus_body_truncated` denotes budget loss, with an estimated `suggestedMinTokens` above the current budget. Completeness concerns the selected answer, not every section of the source.

Digest defaults to `fit_markdown=true`; transcode defaults to false. Digest's `focusMatched` measures lexical overlap in the excerpt, while `focus.status` measures structural section matching. `focusMatched:true` can coexist with `focus.status:weak` and does not prove answer completeness.

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
