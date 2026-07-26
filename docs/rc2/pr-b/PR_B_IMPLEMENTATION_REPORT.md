# PR-B digest MCP boundary implementation report

## Outcome

PR-B is independently complete. `occam_digest.urls` now accepts the approved additive union: a native
string array is preferred and the legacy string transport remains temporarily supported. Every accepted
form terminates at one bounded normalizer before the domain service, satisfying INV-3. No access,
focus, budget, semantic-result, or lifecycle behavior was changed.

Starting and ending commit: `a535705c03a9bf483cbbb23aefe8c4e60cf7b48f`. The worktree remains
uncommitted by owner instruction.

## Architecture

The boundary has four explicit responsibilities:

1. MCP binding accepts `JsonElement?`, preventing the SDK binder from rejecting an allowed union arm.
2. Runtime registration replaces only the inferred `urls` property with a truthful `oneOf` schema.
3. `DigestInputNormalizer` validates shape, size, element types, and URLs, then produces
   `IReadOnlyList<DigestUrlEntry>`.
4. `DigestService` consumes only canonical entries; it no longer parses JSON or delimiter syntax.

The schema adapter uses `JsonDocument` and `Utf8JsonWriter`, not reflection serialization or dynamic
JSON nodes. The Native AOT publish validates the registration path.

## Preserved behavior

- Input URL order and existing case-insensitive deduplication are preserved.
- `max_urls` remains clamped to 1–8; extra normalized entries are dropped as before.
- `source_url` still wins when both inputs are present, so ignored `urls` content is not normalized.
- Legacy JSON object entries retain per-entry `focus_query` compatibility.
- Empty `source_url` discovery remains `invalid_urls`; transport normalization failures are now
  `invalid_arguments`.

## Bounds and diagnostics

Normalization accepts at most 256 transport entries and 65,536 input characters. It performs a single
bounded pass plus the existing URL validation/deduplication pass. Error messages distinguish missing,
empty, mixed/nested, wrong-shape, oversized, and invalid-URL input. No input content is written to logs.

## Changed files

Production:

- `src/FFOccamMcp.Core/Digest/DigestInputNormalizer.cs`
- `src/FFOccamMcp.Core/Digest/DigestInputContract.cs`
- `src/FFOccamMcp.Core/Digest/DigestUrlParser.cs`
- `src/FFOccamMcp.Core/Services/DigestService.cs`
- `src/FFOccamMcp.Core/Tools/OccamDigestTool.cs`
- `src/FFOccamMcp.Core/Transport/OccamMcpServerRegistration.cs`

Regression and compatibility call sites:

- `benchmarks/rc2-regression/DigestNormalizerCases.cs`
- `benchmarks/rc2-regression/PrBRegressionCases.cs`
- `benchmarks/rc2-regression/McpBoundaryCharacterization.cs`
- `benchmarks/rc2-regression/Program.cs`
- `benchmarks/rc2-regression/README.md`
- `benchmarks/l0-gate/DiscoveryFocusLiveTests.cs`
- `benchmarks/l0-gate/GateSyncBridge.cs`
- `benchmarks/l0-gate/L2DigestUnitTests.cs`
- `benchmarks/l0-gate/PublicMcpContractUnitTests.cs`
- `benchmarks/l0-gate/Rc1RegressionRunner.cs`

Contract and user/SDK guidance:

- `MCP_API_SPEC.md`
- `docs/tools-reference.md`
- `docs/tools/occam_digest.md`
- `docs/choosing-a-tool.md`
- `docs/recipes.md`
- `packages/occam-mcp/lib/index.ts`
- `packages/occam-skill/skill/SKILL.md`
- `packages/occam-skill/skill/references/recipes.md`
- `CHANGELOG.md`
- `docs/rc2/IMPLEMENTATION_INVARIANTS.md`
- `docs/rc2/RC2_IMPLEMENTATION_STATUS.md`
- `docs/rc2/pr-b/DIGEST_INPUT_COMPATIBILITY.md`
- this report and the PR-B validation report.

The pre-existing owner changes to `.gitignore` and the PR-A inputs under `benchmarks/rc2-regression/`,
`docs/rc2/`, and `validation/` were preserved. Frozen RC.1 evidence was not modified.

## Compatibility impact

This is additive during prerelease. Native arrays that previously failed before the handler now bind and
normalize. Existing strings continue to work, but are documented as deprecated. The C# domain service
signature changes from a transport string to canonical entries; this is an internal seam, not a public
MCP removal. See `DIGEST_INPUT_COMPATIBILITY.md` for client migration guidance.

## Known limitations

- Native structured per-entry objects are not added; only native string arrays are approved. Legacy
  JSON-object strings remain available for per-entry focus during the compatibility window.
- The checked-in public schema fingerprint still describes the frozen RC.1 host path. PR-B validates
  the changed schema against an isolated AOT host; candidate-wide fingerprint rotation belongs to the
  later integration stage so the frozen RC.1 binary is not overwritten.
- PR-C through PR-G defects remain intentionally red and are not masked by this stage.
