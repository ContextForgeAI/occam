# `occam_client_capabilities` — Deep Capability Audit (Wave 2)

**Source of truth:** current executable code only (`src/FFOccamMcp.Core/**`). Documentation
(`docs/*.md`, `MCP_API_SPEC.md`, `AGENTS.md`) was **not** used as evidence — every claim below cites
a file (+ line where meaningful) that was read directly. Wave 1 subsystem audits
(`docs-audit/subsystems/materialization.md` CAP-300s, `docs-audit/subsystems/config-env.md` CAP-380s,
`docs-audit/subsystems/runtime-mcp.md` CAP-000s) already covered this exact mechanism in depth from the
*consumer* side (transcode/digest budget resolution, profile/env surface); this audit re-verifies those
claims from the *tool* side and adds the gaps Wave 1 didn't frame as tool-level product behavior.

**CAP ID range owned by this audit:** `CAP-400`–`CAP-419` (used: CAP-400…404; remainder reserved).

---

## 0. Entry point and schema

`OccamClientCapabilitiesTool` (`src/FFOccamMcp.Core/Tools/OccamClientCapabilitiesTool.cs`) is the sole
MCP handler. Its `[McpServerTool]` method takes:

```
context_tokens (int?, default null), model_id (string?, default null), clear (bool, default false)
```

All parameters optional — calling with no arguments at all is a valid, side-effect-free "inspect current
budget" call (**CAP-400**). The tool constructor injects exactly one dependency: `ClientCapabilityStore`
(`Client/ClientCapabilityStore.cs`), a singleton registered in `AddOccamCore`
(`Composition/OccamServiceCollectionExtensions.cs:25`) — process-scoped, not per-MCP-session (see
**Cross-subsystem edges** and CAP-304's own documented gap).

### Branch order (exact, `OccamClientCapabilitiesTool.cs:35-52`)

1. `clear == true` → `store.Clear()`, return immediately. **Takes precedence over `context_tokens`** —
   if a caller passes both `clear=true` and `context_tokens=50000` in the same call, `context_tokens` is
   silently ignored (the `clear` branch returns before the `context_tokens is int tokens` check is ever
   reached). No warning/error is raised for this combination — **CAP-401**.
2. Else if `context_tokens is int tokens` → `store.Configure(tokens, model_id, source: "tool")`, return
   the applied snapshot.
3. Else (both omitted) → return `store.Current` unmodified (read-only inspect) — **CAP-400**.

`ArgumentOutOfRangeException` from step 2 (out-of-range `context_tokens`) is the **only** failure path
this tool has; it is caught and mapped to `FailureCode: "invalid_arguments"` with every other field
`null` — confirmed there is no other failure code reachable on this tool (no network, no worker, no
filesystem I/O in the call path itself).

---

## CAP-400 — Idempotent inspect-only read (no side effects)

**Evidence:** `OccamClientCapabilitiesTool.cs:50-52` — the final `return Serialize(Map(store.Current, …))`
branch, reached only when `clear=false` and `context_tokens=null`. This is a genuine distinct product
capability: an agent (or a host wrapper) can call the tool with zero arguments purely to **read** the
current ambient budget state (`configured`, `contextTokens`, `outputBudgetTokens`, `suggestedProfile`,
`modelId`, `source`) without risk of mutating it — useful for an agent that isn't sure whether a prior
turn (or the env bootstrap) already configured the budget before deciding whether to declare its own
`context_tokens`. The `note` field explicitly discloses which state was observed: `"budget already
configured"` vs `"not configured — pass context_tokens once (or set OCCAM_CLIENT_CONTEXT_TOKENS)"`
(`:50-52`) — the tool tells the caller what to do next in-band rather than requiring the caller to infer
it from `configured: false`.

## CAP-401 — `clear=true` reset action (env re-bootstrap, silent precedence over `context_tokens`)

**Evidence:** `OccamClientCapabilitiesTool.cs:35-39`, `ClientCapabilityStore.Clear()` (`:57-64`).
`clear=true` does not simply null out the snapshot — it calls `ReadEnvBootstrap()` again, which performs
a **live** `Environment.GetEnvironmentVariable("OCCAM_CLIENT_CONTEXT_TOKENS")` read
(`OccamEnvironment.Get`, `Configuration/OccamEnvironment.cs:5-11` — not cached from process start), so
`clear` can re-arm a non-empty budget if the env var is set, or truly empty the snapshot
(`ClientCapabilitySnapshot.Empty`) if it is not. The response `note` for this path is a fixed string —
`"cleared; env bootstrap re-applied if set"` (`:38`) — regardless of whether the re-applied bootstrap was
actually non-empty or empty; the caller must inspect `configured`/`contextTokens` in the same response to
know which actually happened, the note text alone does not disambiguate.

**Precedence gap:** because branch 1 (`clear`) returns before branch 2 (`context_tokens`) is evaluated
(`:35-45`), a single call with **both** `clear=true` and `context_tokens=<n>` set silently drops the
`context_tokens` value entirely — there is no `invalid_arguments` for this combination, no warning field,
and the returned snapshot reflects only the env-bootstrap outcome. An agent that (mistakenly) sends both
in one call to "reset and reconfigure atomically" gets only the reset half.

## CAP-402 — `model_id` free-text label: stored, echoed, never consumed elsewhere

**Evidence:** `ClientCapabilityStore.BuildSnapshot` (`:111-118`) trims and stores `model_id` verbatim on
the snapshot; `OccamClientCapabilitiesTool.Map` (`:69-79`) echoes it back as `ModelId` in the response.
Grep across `src/FFOccamMcp.Core/**` for any other reader of `ClientCapabilitySnapshot.ModelId` or
`ClientCapabilityStore`'s `ModelId`-bearing surface found **zero** consumers beyond this tool's own
response serialization — `OccamTranscodeTool.cs`/`OccamDigestTool.cs` only call `ResolveMaxTokens`, which
returns a bare `int?` with no model-id association. **`model_id` is purely decorative bookkeeping** (a
label an agent can set for its own later `occam_client_capabilities()` inspect-call to recognize "yes,
this is my declared budget") — it has zero effect on routing, backend selection, quality thresholds, or
any per-model tuning. This is worth flagging precisely because the parameter name ("your model card")
plausibly implies model-aware behavior (e.g. different BM25 thresholds per model family) that does not
exist anywhere in the codebase.

## CAP-403 — `suggestedProfile`: advisory string sharing vocabulary with `OCCAM_PROFILE`, zero automated linkage

**Evidence:** `ClientCapabilityStore.SuggestProfile` (`:87-91`) returns exactly one of `"reader"` /
`"researcher"` / `"full"` — the identical three string literals used as `OccamToolProfile.Reader` /
`.Researcher` / `.Full` constants (`Transport/OccamToolProfile.cs:12-14`, confirmed identical values).
Despite the literal string overlap, there is **no code path** that reads `suggestedProfile` from this
tool's response and feeds it into `OCCAM_PROFILE` resolution (`OccamToolProfile.Resolve()` only reads the
environment variable, never anything from `ClientCapabilityStore`) — the two "profile" concepts are
completely independent: `OCCAM_PROFILE` narrows **which tools are exposed on `tools/list`** (set once at
process/host start, operator-controlled), while `suggestedProfile` here is a **per-context-size text
hint** ("if your context window is this small, you might want the reader tool surface") that a human or
agent would have to read and manually translate into an env var change on a **restart** — it cannot be
applied mid-session even if the agent wanted to, since `OccamToolProfile.Resolve()` is only read once at
MCP server construction time (`OccamMcpServerRegistration.AddOccamMcpServer`, confirmed no re-resolution
call site elsewhere). An agent unfamiliar with this could reasonably (and incorrectly) assume calling
`occam_client_capabilities` might narrow its own visible toolset.

## CAP-404 — Ambient budget mutates transcode/digest cache & materialization identity for calls that omit `max_tokens`

**Evidence:** `Client/ClientCapabilityStore.cs:70-79` (`ResolveMaxTokens`) feeds directly into
`OccamTranscodeOptionsParser.TryBuild` (`OccamTranscodeTool.cs:78`) and `DigestService.DigestAsync`
(`OccamDigestTool.cs:54`); the resolved `max_tokens` value becomes part of `OccamTranscodeOptions.MaxTokens`,
which — per `docs-audit/tools/occam_transcode.md` CAP-093 (`MaterializationKey`) and its cache-key
sibling — is one of the **output-affecting options** hashed into both `TranscodeCacheKey`
(cache lookup identity) and `MaterializationKey`/`ContentHashToken` (the hash `if_none_match` compares
against, and what a Receipt v1 signature is computed over). Consequence not previously called out at the
tool level: **calling `occam_client_capabilities(context_tokens=…)` mid-session changes the effective
cache/materialization identity of every subsequent same-URL `occam_transcode`/`occam_digest` call that
omits `max_tokens`**, even though the visible tool call (`occam_transcode({url})`) is textually identical
before and after. Two consequences: (a) a cache entry populated before a `context_tokens` change becomes
unreachable by later omitted-`max_tokens` calls (safe, just a cache miss, not a correctness bug); (b) an
agent computing/comparing `if_none_match` hashes across a session boundary where it also called
`occam_client_capabilities` in between should not be surprised the hash changed even though "the page
didn't change" — the ambient budget, not the page, changed the materialization. This compounds the
concurrency risk already flagged in Wave 1's CAP-304 (shared mutable process-wide state): in a
multi-client `WebSocketMcpTransport` deployment, client A's budget declaration can also silently change
client B's cache/materialization identity for B's own omitted-`max_tokens` calls, not just B's *effective
token count*.

---

## Existing Wave-1 capabilities this tool activates or is scoped by (reused, not re-minted)

- **CAP-007** — this tool is one of the fifteen always-registered core MCP tool names
  (`OccamMcpServerRegistration.OccamToolNames`).
- **CAP-008 / CAP-009** — `occam_client_capabilities` is a member of the baseline `ReaderTools` set
  (`Transport/OccamToolProfile.cs:19`), so it is present in **every** profile (`full`, `reader`,
  `researcher`, `auditor`) — the one core tool that is never hidden by `OCCAM_PROFILE` narrowing (all four
  profile sets are supersets of `ReaderTools`, confirmed by reading `GetExposedToolNames`).
- **CAP-010** — profile-aware MCP `instructions` text (`Transport/OccamServerInstructions.cs:42-44,
  62, 84`) explicitly instructs the calling agent to invoke this tool once at session start — this is the
  only core tool whose *own onboarding instructions* are injected into the server's global `initialize`
  response rather than left to its own `[Description]` text alone.
- **CAP-304** — `max_tokens`/`per_url_max_tokens` ambient-default resolution via
  `ClientCapabilityStore.ResolveMaxTokens`; `OutputFractionOfContext = 0.20`; process-scoped singleton
  concurrency gap (re-verified directly against `ClientCapabilityStore.cs` and both consumer call sites in
  this audit — confirmed, not just cited).
- **CAP-305** — the `max_tokens < 128` floor (`OccamTranscodeOptionsParser`) and `DigestService.MinTokenBudget
  = 128` are independent literals from `ClientCapabilityStore`'s own clamps (`MinOutputBudget = 512`,
  `MaxOutputBudget = 16_384`) — i.e. **this tool's own output-budget floor (512) is stricter than the
  floor the consuming tools enforce on an explicit `max_tokens` (128)**; a `context_tokens` value small
  enough to be accepted (`MinContextTokens = 1_024`) always produces an `OutputBudgetTokens ≥ 512`, so the
  ambient path can never itself produce a value that would trip the 128 floor — the two floors are
  independently declared but happen to never conflict in practice given `MinContextTokens`'s own value.
- **CAP-384 / CAP-385** — `OCCAM_PROFILE` (orthogonal tool-exposure mechanism, see CAP-403 for the naming
  collision with this tool's own `suggestedProfile`) and the `OCCAM_CLIENT_CONTEXT_TOKENS` /
  `OCCAM_CLIENT_MODEL_ID` env bootstrap this tool's constructor reads via `ReadEnvBootstrap` at process
  start and again on `clear=true`.

## Cross-cutting category checklist (per shared instructions)

| Category | Used by this tool? | Evidence |
|---|---|---|
| proxy | Not used | No `HttpClient`/network call anywhere in `ClientCapabilityStore`/`OccamClientCapabilitiesTool` |
| session (`session_profile`) | Not used | No parameter, no `Session/*` reference |
| cookies / headers | Not used | No worker/HTTP call at all — this tool is pure in-memory state |
| http / browser backends | Not used | No `IExtractBackend` dependency injected |
| managed providers | Not used | No `Backends.Managed.*` reference |
| retry | Not used | No retry logic; single synchronous store call |
| cache | Indirect only | See **CAP-404** — does not cache itself, but mutates the cache-key inputs consumed by `occam_transcode`/`occam_digest` |
| diff (`diff_against`/`if_none_match`) | Indirect only | See **CAP-404** — same mechanism, content-hash identity |
| blocks / tables / chunks | Not used directly | Budget flows into `ResponseBudgetPlanner` bucket sizing for *other* tools' calls only |
| budget | **Core function** | This tool's entire purpose — CAP-304/305, CAP-400/401 |
| receipts / merkle / capsules | Not used directly | No `ReceiptSigner`/`CapsuleCodec` dependency; only indirectly touched via CAP-404's materialization-key coupling |
| playbooks | Not used | No `Playbooks/*` reference |
| datasets / claims / trust tags | Not used | No `Dataset`/`Claims`/`BlockTrust` reference |
| screenshots / translate | Not used | No `capture_screenshot`/`TranslationService` reference |
| llms.txt / feeds | Not used | No `prefer_llms_txt`/feed-parsing reference |
| profile (`OCCAM_PROFILE`) | Yes, two independent senses | CAP-008/009 (this tool's own exposure), CAP-403 (its `suggestedProfile` output field shares vocabulary with, but has zero automated link to, `OCCAM_PROFILE`) |
| env | Yes | `OCCAM_CLIENT_CONTEXT_TOKENS`, `OCCAM_CLIENT_MODEL_ID` (CAP-385) |

---

## Capability graph edges

```
TOOL|USES|CAP-007
TOOL|USES|CAP-008
TOOL|USES|CAP-009
TOOL|USES|CAP-010
TOOL|USES|CAP-304
TOOL|USES|CAP-305
TOOL|USES|CAP-385
TOOL|USES|CAP-400
TOOL|USES|CAP-401
TOOL|USES|CAP-402
TOOL|USES|CAP-403
TOOL|USES|CAP-404
PARAM:context_tokens|ENABLES|CAP-304
PARAM:context_tokens|ENABLES|CAP-403
PARAM:model_id|ENABLES|CAP-402
PARAM:clear|ENABLES|CAP-401
CAP-400|PRODUCES|client_capability_snapshot(read-only)
CAP-401|CONSUMES|env:OCCAM_CLIENT_CONTEXT_TOKENS
CAP-401|CONSUMES|env:OCCAM_CLIENT_MODEL_ID
CAP-304|ROUTES_TO|OccamTranscodeTool
CAP-304|ROUTES_TO|OccamDigestTool
CAP-304|FALLS_BACK_TO|env:OCCAM_CLIENT_CONTEXT_TOKENS
CAP-404|PRODUCES|TranscodeCacheKey-shift
CAP-404|PRODUCES|MaterializationKey-shift
CAP-403|PRODUCES|suggestedProfile(advisory,unenforced)
```

---

## Hidden / non-obvious capabilities (a user would never discover from the short MCP description)

The tool's own description string is honest and fairly complete for the *documented* flow
(declare once → transcode/digest inherit ~20%). What it does **not** tell a caller:

1. **CAP-400** — omitting all arguments is a valid, meaningful, side-effect-free "what's my current
   budget" probe; the description reads as if the tool's only job is to *set* something.
2. **CAP-401** — `clear=true` doesn't just "reset to nothing", it re-arms from the environment variable
   if one is present, and it silently wins over a `context_tokens` value passed in the same call.
3. **CAP-402** — `model_id` ("you know it from your model card") sounds like it tunes behavior; it is a
   pure label with zero downstream effect anywhere in the codebase.
4. **CAP-403** — `suggestedProfile` in the response is plain advisory text; it is not, and cannot be,
   auto-applied to `OCCAM_PROFILE` (which is env/startup-only) — a caller might reasonably (and wrongly)
   expect a feedback loop here given the identical string vocabulary.
5. **CAP-404** — declaring (or clearing) a budget mid-session silently changes the cache/materialization
   identity of *other* tools' future calls that omit `max_tokens` — a caller diffing content hashes across
   a session that also touched this tool should know the hash can move for a reason unrelated to the
   target page changing.
6. Wave-1's own CAP-304 gap (process-wide shared mutable state, no per-session isolation) is real and
   independently re-confirmed here by direct code read — worth restating since it is the single biggest
   "gotcha" for any host running `WebSocketMcpTransport` with multiple concurrent MCP clients.

## Uncertainties

- Whether any WS/Remote transport session-scoping wraps `ClientCapabilityStore` per-connection anywhere
  outside `AddOccamCore`'s plain singleton registration was not independently re-verified in this audit —
  Wave 1's `runtime-mcp.md`/CAP-304 already flags this as an open item for "CAP-connectivity subagent";
  this audit's own read of `OccamServiceCollectionExtensions.cs:25` confirms the DI lifetime is
  `AddSingleton` with no scoping wrapper visible from that file alone, consistent with (not contradicting)
  the Wave-1 finding.
- Whether `OccamEnvironment.Get`'s live (uncached) `Environment.GetEnvironmentVariable` read means an
  in-process `Environment.SetEnvironmentVariable` call from elsewhere in the host (none found in this
  audit's grep scope) could retroactively change what `clear=true` re-bootstraps to — theoretical, no
  evidence of such a call site found, flagged only for completeness.

## Completeness

**COMPLETE** — every parameter, branch, and dependency of `OccamClientCapabilitiesTool` and
`ClientCapabilityStore` was traced to its terminal effect (response field or, for the ambient-budget
path, the two downstream tool call sites). No worker/network/filesystem code paths exist on this tool to
audit further.
