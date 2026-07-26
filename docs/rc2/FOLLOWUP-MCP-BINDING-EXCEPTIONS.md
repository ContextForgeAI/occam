# Follow-up: MCP required-parameter binding → Event Log "unhandled exception"

**Status:** **RESOLVED** in Core via `McpArgumentBindingGuard` + `AddCallToolFilter` (this change set).
**Observed:** RC.2 overnight Phase 2 (2026-07-23), Windows Application log EventId 1000.
**HEAD at observation:** `b17c51b92dd4ec89a37d6c97988097559c84b6d8`

## Symptom (historical)

Calling MCP tools without required arguments caused the host to log an unhandled
`System.ArgumentException` from `Microsoft.Extensions.AI.AIFunctionFactory` parameter marshalling,
for example:

- `occam_claim_check` — missing required parameter `url`
- `occam_attest` — missing required parameter `claims`
- `occam_dataset_export` — missing required parameter `urls`

Wrong-type declared values (e.g. `url: 123`) similarly escaped as
`System.Text.Json.JsonException` ("could not be converted…") through the same unhandled path.

Clients received opaque MCP tool text (`An error occurred invoking '…'.`) with `isError: true`.
Required-parameter validation itself was already strict; the defect was error *classification /
logging*, not acceptance of bad input.

## Root cause

```
tools/call
→ McpServerImpl CallTool pipeline
→ AIFunctionMcpServerTool.InvokeAsync
→ MEAI ReflectionAIFunction parameter marshaller
   throws ArgumentException(ParamName="arguments", "missing a value for the required parameter '…'")
   or JsonException("could not be converted…")
→ exception escapes past tool body
→ McpServerImpl.ToolCallError logs "\"{ToolName}\" threw an unhandled exception." (EventId 1433779783)
→ opaque CallToolResult { IsError=true, Content=["An error occurred invoking '…'."] }
```

## Fix (narrow)

**Interception layer:** `IMcpServerBuilder.WithRequestFilters` → `AddCallToolFilter` in
`OccamMcpServerRegistration.AddOccamMcpServer`, implemented by
`Transport/McpArgumentBindingGuard.cs`.

User CallTool filters run *inside* the SDK's outermost try/catch, so a handled binding failure
never reaches `ToolCallError` / Event Log unhandled logging.

**Classified exception shapes only:**

1. `ArgumentException` with `ParamName == "arguments"` and message containing
   `missing a value for the required parameter`
2. `JsonException` (including as `InnerException`) whose message contains `could not be converted`

**Response:** typed Occam tool JSON (same honesty model as digest/probe failures):

```json
{"ok":false,"failureCode":"invalid_arguments","message":"…","timestamp":"…"}
```

`CallToolResult.IsError` is left **unset** (wire-equivalent to existing tool returns for
`invalid_arguments` / `http_404` / etc.). See “MCP isError semantics” below.

**Logging after fix:** stderr line `[occam.mcp] argument binding rejected tool=…` only — not
`ToolCallError`.

## MCP isError semantics (decision)

| Layer | Meaning in Occam |
|---|---|
| JSON-RPC / MCP protocol error | Transport/framing failure; rare |
| `CallToolResult.isError=true` + opaque text | Unexpected invoke failure (SDK `ToolCallError` path) |
| Tool content `{ok:false, failureCode}` with isError unset/false | **Expected typed domain/input failure** — public contract |

Live host comparison (RC.2 AOT, 2026-07-23): digest empty/mixed urls, transcode bad policy,
claim_check empty url, verify invalid capsule, and transcode `http_404` all return **isError
absent** with **ok:false** and a typed code. Binding rejection must match that shape so clients
keep reading the Occam envelope rather than treating the call as an opaque MCP tool crash.

The MCP SDK *allows* `isError=true` while still carrying `Content`, but Occam’s established
public contract intentionally routes expected validation failures through the JSON envelope
instead. Changing binding rejection to `isError=true` would introduce a second incompatible
contract for the same `invalid_arguments` class.

## Why unrelated exceptions are not masked

The filter uses `catch (Exception ex) when (IsClientInputBindingFailure(ex))`. Any exception that
fails the predicate is rethrown and keeps the SDK unhandled / Event Log path. Unit tests cover
`NullReferenceException`, bare `InvalidOperationException`, and non-binding `ArgumentException`.

## Tests added

- Offline: `benchmarks/l0-gate/McpArgumentBindingGuardUnitTests.cs` (`L_MCP_BINDING_GUARD_OK`)
- Live MCP: `McpBoundaryCharacterization.RunBindingGuardCasesAsync` (wired into `--regression` /
  `--pr-g` desired-contract path): missing url/claims/urls, wrong-type url, host continuity

## Residual limitations

- Only binding failures with the MEAI/STJ message shapes above are mapped. A future SDK that
  changes exception text/ParamName would need a classifier update.
- Tool-body exceptions that already return typed JSON are unchanged; this guard does not alter
  schemas, required declarations, or business logic.
- Windows Event Log cleanliness is validated by ensuring the exception never escapes the CallTool
  filter (portable). Spot-check Application log around live probes when convenient.
