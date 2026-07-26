# Playbook validation

**Slug:** `playbook-validation` · **Product system:** PS-5 Playbooks · **CAPs:** 14 · **Public relevance:** HIGH.

## What it is

`occam_playbook_lint` is a pure, network-free advisory checker for selected playbook JSON fields. It returns issues, counts, `ready|usable|broken`, and `agentReady` (CAP-750–763).

It is not the canonical parser for save or resolve. Three independent schema readers have diverged, so lint outcomes can disagree with both consumers (CAP-759–762; EF-015).

## Why it exists

- Catch basic 1.x/id/hosts/selector/routing/schema mistakes before authoring calls (CAP-752–757).
- Produce machine-readable severity and grade for an agent workflow (CAP-751).
- Add optional title/notes quality nudges without network or side effects (CAP-758).

## User-visible entrypoints

MCP `occam_playbook_lint(playbook_json)` is available in `full` and `auditor`, hidden in `reader`/`researcher` (CAP-763). No worker, CLI, or filesystem entrypoint is involved.

## Core behavior

1. Parse input JSON; empty, malformed, or non-object input becomes one root error, never an exception (CAP-750).
2. Check schema version, id, and hosts (CAP-752–754).
3. Check camelCase `extract.contentSelectors` (CAP-755).
4. Check preferred backend against a local allow-list (CAP-756).
5. Warn when knowledge schema classes lack genome page-class routes (CAP-757).
6. Add metadata/notes warnings and info (CAP-758).
7. Compute grade: errors → broken; otherwise warnings → usable; otherwise ready. `agentReady == errors==0` (CAP-751).

## Advanced behavior

| Check | Severity/semantics | Evidence |
|---|---|---|
| `schema_version` | Error if missing/non-`1.` prefix; no upper minor bound | CAP-752 |
| `id` | Error only when blank; no format constraints | CAP-753 |
| `hosts` | Error if no usable string; warning for URL/path/space/uppercase | CAP-754 |
| selectors | Missing/empty errors; blank member warnings | CAP-755 |
| backend | Warning outside `http|browser|http_then_browser` | CAP-756 |
| knowledge routes | Warning for non-default class absent from page_classes | CAP-757 |
| meta/notes | Missing title warning; missing notes info | CAP-758 |

## Automatic / silent behavior

- Cancellation is checked only once because the operation is synchronous (CAP-750).
- `agentReady` duplicates “no errors”; it is not an independent readiness model (CAP-751).
- `"1.999"` passes prefix validation even if resolver compatibility is stricter (CAP-752/759).
- Valid alias `http-then-browser` is falsely warned because lint's local list is stale (CAP-756; EF-015).
- Valid snake_case `content_selectors` is falsely classified broken (CAP-762).
- Forbidden secret-key properties can pass lint as ready but fail save (CAP-761).

## Parameters

| Name | Required/default | Effect | Evidence |
|---|---|---|---|
| `playbook_json` | required string | Entire lint input | `OccamPlaybookLintTool.cs`; CAP-750 |

No URL, network, profile-within-call, strictness, schema version target, size cap, session, receipt, or autofix parameter exists.

## Configuration

None.

The tool reads no environment variables. `OCCAM_PROFILE` affects registration only (CAP-763).

## Backends

None. Pure in-process JSON parsing and deterministic checks (CAP-750).

## Sessions / state

None. No files, caches, sessions, keys, or persisted reports.

## Network behavior

None. The no-network guarantee is structural: no async I/O, HttpClient, worker, or filesystem dependency (CAP-750).

## Artifacts produced

ART-018 ephemeral lint grade/issues (`ARTIFACT-ONTOLOGY.md:86`). It is advisory, unsigned, unhashed, and not required by save/resolve.

## Trust / provenance properties

No signature, receipt, provenance verification, or sanitizer is used. A lint grade proves only that this specific checker emitted those issues for the supplied JSON.

CAP-758 older prose cited `PlaybookCommunitySanitizer`; that class is Core-dead (C3/EF-047). Lint does not establish community hygiene or author identity.

## Failure / fallback behavior

There is no transcode-style failure envelope:

- Blank input → one `empty_input` root error.
- Invalid JSON → one `json_invalid` root error, with parser message.
- Non-object JSON → one `not_object` root error.
- Valid objects accumulate field issues.

The method returns a report in all these cases and does not invoke save/resolve fallback (CAP-750).

## Platform differences

None. JSON parsing and checks are platform-neutral.

## Composition with other capabilities

- Intended before `playbook-authoring`, but a ready result does not guarantee save acceptance (CAP-761).
- Intended to preview `playbook-resolution`, but broken can still resolve due to aliases/optional selectors (CAP-760/762).
- Does not inspect signatures, quality-gate metrics, lessons, community manifest integrity, or live extraction.

## Known limitations

- Three independent schema readers; no shared source of truth (CAP-759).
- Description falsely says missing selectors break save/resolve (CAP-760).
- No secret-key hygiene check (CAP-761).
- Does not recognize snake_case selector alias (CAP-762).
- Stale backend alias list (CAP-756).
- No overall input-size, host-count, selector-count, or issue-count cap.
- No live selector testing or schema extraction test.

## Engineering findings

- EF-015: lint/parser contract drift.
- EF-047: cited community sanitizer is dead; local save skips publish sanitizer.
- CAP-759–762 are findings, not features.
- C3 correction: active secret hygiene is `PlaybookCommunityHygiene` on save/community load, not `PlaybookCommunitySanitizer`.

## Code evidence

- `src/FFOccamMcp.Core/Tools/OccamPlaybookLintTool.cs`
- `src/FFOccamMcp.Core/Playbooks/PlaybookLinter.cs`
- `src/FFOccamMcp.Core/Playbooks/PlaybookDocument.cs`
- `src/FFOccamMcp.Core/Playbooks/PlaybookSeedResolver.cs`
- `src/FFOccamMcp.Core/Playbooks/PlaybookCommunityHygiene.cs`
- CAP-750–763; ART-018; EF-015/047.

## Public-doc relevance

High, with strong qualification. Document exact checks and grades, then state that lint is advisory and can disagree with save/resolve. Do not promise “ready means save succeeds” or “broken means resolve fails.”

## Handbook relevance

Use as a fast preflight checklist, not a gate. Pair it with save hygiene/verification and resolve inspection. Include a divergence table for backend alias, selector alias/optionality, and forbidden-key handling.
