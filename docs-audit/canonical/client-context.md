# Client context

**Slug:** `client-context` · **Product system:** PS-8 Runtime and exposure · **CAPs:** 5 · **Public relevance:** HIGH

**Member CAPs:** CAP-400…CAP-404  
**Product capability:** CAP-400  
**Engineering findings:** none listed on family

## What it is

Ambient LLM context-window budget stored in process singleton `ClientCapabilityStore`, set by MCP tool `occam_client_capabilities` and/or env `OCCAM_CLIENT_CONTEXT_TOKENS`. When callers omit `max_tokens` on budgeted tools, the store supplies an advisory output budget (~20% of context window per server instructions / store math).

## Why it exists

Agents know their model context size; Occam should size reads without requiring `max_tokens` on every call. Operator override via env for headless hosts (`ClientCapabilityStore.cs`; `OccamServerInstructions.cs:44,62`).

## User-visible entrypoints

| Entrypoint | Role | Evidence |
|------------|------|----------|
| `occam_client_capabilities` | Inspect / configure / clear | `OccamClientCapabilitiesTool.cs:14-52` |
| `OCCAM_CLIENT_CONTEXT_TOKENS` | Bootstrap at store construct + on clear | `ClientCapabilityStore.cs:95-104` |
| Consumers | `occam_transcode` / `occam_digest` when `max_tokens` omitted | `OccamTranscodeTool.cs:48`; digest tool |

Present in **all** profiles including `reader` (`PROFILE-TOOL-MATRIX.md`).

## Core behavior

### Branch order (exact)

1. `clear=true` → `store.Clear()` (re-reads env live) — **precedes** `context_tokens` (`CAP-401`).
2. Else `context_tokens` int → `Configure(tokens, model_id, source:"tool")`.
3. Else → inspect-only return of `store.Current` (`CAP-400`).

Only failure: out-of-range tokens → `invalid_arguments` (`ArgumentOutOfRangeException`).

### Inspect fields

Response includes `configured`, `contextTokens`, `outputBudgetTokens`, `suggestedProfile`, `modelId`, `source`, plus a `note` guiding next action (`CAP-400`).

## Advanced behavior

| Behavior | Notes | CAP |
|----------|-------|-----|
| `model_id` | Stored & echoed; **never read** by routers/workers elsewhere | CAP-402 |
| `suggestedProfile` | Advisory string sharing vocabulary with `OCCAM_PROFILE`; **zero automated linkage** | CAP-403 |
| Ambient budget identity | Mutates cache/materialization identity for calls omitting `max_tokens` | CAP-404 |
| Clear + tokens same call | Tokens silently ignored | CAP-401 |

## Automatic / silent behavior

| Silent | Effect |
|--------|--------|
| Env bootstrap at construction | Budget may exist before any tool call |
| Clear re-applies env | “Clear” is not always empty |
| No auto profile switch | `suggestedProfile` never sets `OCCAM_PROFILE` |

## Parameters

| Name | Default | Effect |
|------|---------|--------|
| `context_tokens` | null | Set ambient window (range enforced in store) |
| `model_id` | null | Label only |
| `clear` | false | Reset + env re-bootstrap |

## Configuration

| Env | Effect |
|-----|--------|
| `OCCAM_CLIENT_CONTEXT_TOKENS` | Bootstrap / clear re-read; out-of-range → stderr ignore (`ClientCapabilityStore.cs:104`) |

Range bounds: `MinContextTokens`…`MaxContextTokens` in store (see code; tool maps violations to `invalid_arguments`). Output budget fraction is derived inside `BuildSnapshot` (documented to agents as ~20% of context in `OccamServerInstructions` and tool Description) — treat the **returned `outputBudgetTokens`** as SoT for a given call, not a remembered percentage.

`source` field distinguishes `"tool"` vs env bootstrap vs empty — callers can tell who configured the budget (`CAP-400` note strings).

## Backends

Not applicable.

## Sessions / state

| State | Class | Notes |
|-------|-------|-------|
| `ClientCapabilityStore` | PROCESS (ST-19) | Singleton in `AddOccamCore` — **not** per-MCP-session on stdio |
| WS/Remote | New DI per connection | Fresh store per session (`CAP-1000` interaction) |

## Network behavior

None. Pure in-process.

## Artifacts produced

None on disk. JSON snapshot in tool response. Related ART-023.

## Trust / provenance properties

Not a trust signal. Does not affect receipts. Budget changes can change **which** content is returned (truncation/focus) for identical URLs when `max_tokens` omitted — materialization identity impact (`CAP-404`), not provenance.

## Failure / fallback behavior

| Case | Behavior |
|------|----------|
| Out-of-range tokens | `ok:false`, `invalid_arguments` |
| Env out-of-range | Ignored with stderr; store stays empty/unconfigured |
| No budget configured | Tools that omit `max_tokens` return full payload (per tool descriptions) |

## Platform differences

None.

## Composition with other capabilities

- **Upstream of** PS-2 token-budget family when ambient path used (`OccamTranscodeTool` / `OccamDigestTool` inject store into option build when `max_tokens` omitted).
- Independent of `OCCAM_PROFILE` despite `suggestedProfile` naming (`CAP-403`) — **do not document as auto-profile**.
- Explicit `max_tokens` on a tool call **overrides** ambient budget for that call (tool Description / options parser) — ambient is a default, not a ceiling over explicit params.
- Does not affect probe/map/search/extract_knowledge token surfaces the same way (those tools either lack the ambient path or use different budgets — see materialization family cards).
- Peer: `AUTOMATION-MODEL.md` / materialization budget ownership; `STATE-MODEL.md` ST-19.

## Known limitations

- `suggestedProfile` is naming coincidence with env profile — no linkage.
- `model_id` is decorative.
- Clear+configure atomicity does not exist in one call.
- Stdio store is process-wide across MCP “sessions” in one host.
- WS/Remote reconnect loses ambient budget unless env bootstrap repopulates (`CAP-1000`).
- Changing ambient mid-session can invalidate mental models of “same URL → same markdown” when callers omit `max_tokens` (`CAP-404`).

## Engineering findings

None assigned to this family. Related: EF-033 smoke profile blindness does not involve this tool’s budget math.

## Code evidence

- `src/FFOccamMcp.Core/Tools/OccamClientCapabilitiesTool.cs:14-52`
- `src/FFOccamMcp.Core/Client/ClientCapabilityStore.cs` (ctor bootstrap, `Configure`, `Clear`, `BuildSnapshot`, env read `:95-104`)
- `src/FFOccamMcp.Core/Composition/OccamServiceCollectionExtensions.cs:24-25`
- `src/FFOccamMcp.Core/Transport/OccamServerInstructions.cs:44,62,84`
- Consumers: `OccamTranscodeTool.cs:25,48`, `OccamDigestTool.cs:15`
- Deep: `docs-audit/tools/occam_client_capabilities.md`
- Peer: `STATE-MODEL.md` ST-19; `PROFILE-TOOL-MATRIX.md` (present in all profiles)

## Public-doc relevance

**HIGH** — session-start ritual. State: optional; env alternative; `suggestedProfile` is advisory only.

## Handbook relevance

**Agent quickstart step 1.** Show inspect → configure → omit `max_tokens` on transcode/digest; warn against assuming profile auto-switch.
