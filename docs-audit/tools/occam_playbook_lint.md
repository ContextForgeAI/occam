# `occam_playbook_lint` — Deep Capability Audit (Wave 2)

**Source of truth:** current executable code only — `src/FFOccamMcp.Core/Tools/OccamPlaybookLintTool.cs`,
`src/FFOccamMcp.Core/Playbooks/PlaybookLinter.cs`, cross-referenced against the *actual* consumers of a
playbook JSON document (`PlaybookSaveService.cs` / `PlaybookDocument.cs` for `occam_playbook_save`,
`PlaybookSeedResolver.cs` for `occam_playbook_resolve`/`playbook_policy=auto`) to verify the tool's own
claim that its errors "break resolve/save." Docs (`docs/*.md`, `MCP_API_SPEC.md`) were **not** used as
evidence. `benchmarks/l0-gate/PlaybookLintUnitTests.cs` (`L_LINT_OK`) was read to confirm asserted
behavior matches unit coverage.

**CAP ID range owned by this audit:** `CAP-750`–`CAP-769` (used: CAP-750…CAP-763; remainder reserved).

**Grade: `usable`.** The tool does exactly what it says for the fields it checks — pure, deterministic,
network-free JSON linting with a sane error/warning/info split — but it is a **third, independently
hand-written playbook-schema reader** that has drifted from the two validators that actually gate
`occam_playbook_save` and `occam_playbook_resolve`. Its own MCP description overstates what its errors
guarantee (see CAP-760), and it has zero coverage of the one rule that most concretely blocks a save
(secret-key hygiene, CAP-761). Not "broken" — every check it performs is individually correct — but an
agent that trusts "lint says ready → save will succeed" or "lint says broken → resolve will ignore this
file" will be misled in specific, demonstrated cases.

---

## 0. Entry point and schema

`OccamPlaybookLintTool.Lint` (`Tools/OccamPlaybookLintTool.cs`) is a single-method MCP tool:

```
occam_playbook_lint(playbook_json: string) -> string (JSON)
```

One required parameter, no optional knobs, no `CancellationToken`-observable async work (the method is
synchronous under the hood — `cancellationToken.ThrowIfCancellationRequested()` is checked once at entry
and never again, since `PlaybookLinter.Lint` itself never awaits/yields). It calls straight into
`PlaybookLinter.Lint(playbook_json)` (`Playbooks/PlaybookLinter.cs`) and serializes the result via a
source-generated `JsonSerializerContext` (AOT-safe, camelCase). No backend, no worker process, no
`HttpClient`, no filesystem access anywhere in the call graph — confirmed by reading both files in full;
neither imports networking or I/O namespaces beyond `System.Text.Json`.

**Response shape:** `{ grade: "ready"|"usable"|"broken", agentReady: bool, errors: int, warnings: int,
infos: int, issues: [{ severity: "error"|"warning"|"info", field: string, code: string, message: string }] }`.

---

## CAP-750 — Pure, network-free, single-shot validation contract

**Evidence:** `OccamPlaybookLintTool.cs` (whole file), `PlaybookLinter.cs` (whole file). No `async`, no
`await`, no I/O. `PlaybookLinter.Lint` takes a `string?` and returns a `PlaybookLintReport` — a record, not
a class with mutable state — so two concurrent calls with different input cannot interfere. This is the
one property the tool's description promises ("no network") and it holds unconditionally; there is no env
var or parameter that could make this tool touch the network.

## CAP-751 — Grade computation from issue counts

**Evidence:** `PlaybookLinter.Report` (lines 210-217). `grade = errors > 0 ? "broken" : warnings > 0 ?
"usable" : "ready"`; `agentReady = errors == 0`. This is a strict lexicographic priority — a document with
1 error and 0 warnings is `broken`, never averaged against warning count. `agentReady` is a convenience
boolean that is always exactly `errors == 0` (i.e. `agentReady` and `grade != "broken"` are the same fact
expressed twice — no case was found where they diverge).

## CAP-752 — `schema_version` check

**Evidence:** `CheckSchemaVersion` (lines 60-72). Two independent failure paths, both `error` severity:
missing/blank (`code: "missing"`), or present but not starting with `"1."` (`code: "unsupported"`). No
upper bound is enforced — `"1.999"` passes; the check is a bare string-prefix test, not a `Version.Parse`
comparison (contrast with `PlaybookSeedResolver`'s actual resolve-time version compatibility check, which
does use `Version.TryParse` for a real major/minor comparison — see CAP-759 for the divergence).

## CAP-753 — `id` check

**Evidence:** `CheckId` (lines 74-80). Single `error` if `id` is missing/blank. No format validation
(no host-shape check, unlike `hosts` — CAP-754) — a nonsensical `id` like `"   x   "` (non-blank but
whitespace-padded) passes this check, since `GetString` returns the raw string and only
`IsNullOrWhiteSpace` is tested.

## CAP-754 — `hosts` array check (+ non-bare-host warning)

**Evidence:** `CheckHosts` (lines 82-112). `error` if `hosts` is missing, not an array, or contains no
non-blank string entries. Independently, each non-blank host that contains `"://"`, `"/"`, whitespace, or
an uppercase letter gets a `warning` (`host_not_bare`) — this anticipates the exact normalization
`PlaybookDocument.NormalizeHost`/`MatchesHost` (used by `occam_verify`'s host-matching, per Wave-1 evidence)
performs at match time, i.e. the warning is a legitimate "this will silently fail to match some URLs"
signal, not decorative.

## CAP-755 — `extract.contentSelectors` check

**Evidence:** `CheckExtract` (lines 114-144). `error` (`code: "missing"`) if the `extract` object or its
`contentSelectors` array is absent entirely; separately, each non-string/blank array entry inside an
otherwise-present array is a `warning` (`selector_blank`); and an array that is present but has zero usable
entries after filtering blanks is a **second, distinct** `error` (`code: "empty"`) — three different ways
to fail this one field, each with its own code. See **CAP-760** for why this field's "error" severity does
not actually correspond to a hard failure in either downstream consumer.

## CAP-756 — `routing.preferred_backend` validation (BUG: allow-list is stale vs. the real parser)

**Evidence:** `CheckRouting` (lines 146-160) validates against a local constant:
`ValidBackends = ["http", "browser", "http_then_browser"]`. This is optional (routing block absent → no
issue, correctly matching the real default). **Finding:** the actual backend-policy parser consulted at
resolve/transcode time — `OccamBackendPolicyParser.TryParse` (per Wave-1 `occam_transcode.md` CAP-051) —
additionally accepts the hyphenated alias `"http-then-browser"`. `PlaybookLinter.ValidBackends` does
**not** include that alias, so a playbook authored with `"preferred_backend": "http-then-browser"` (a
value that would parse and route correctly at live resolve/transcode time via
`TranscodePipeline.ResolveEffectiveBackendPolicy` → `OccamBackendPolicyParser.TryParse`) is incorrectly
flagged by the linter with a `warning`/`invalid_backend`, claiming it "will fall back to
http_then_browser" — which is false; it would actually route correctly as-is. This is a genuine,
demonstrable drift between the linter's private validation list and the parser it is supposed to be
previewing.

## CAP-757 — `knowledge_schema` unrouted-class cross-check

**Evidence:** `CheckKnowledgeSchema` (lines 162-191). Optional block. For each key in `knowledge_schema`
other than the literal string `"default"`, the key must also appear as a key in `genome.page_classes`,
else a `warning` (`unrouted_class`) fires with the exact remediation ("add a page_classes pattern or
rename to default"). This correctly models the real routing behavior documented in Wave-1's
`occam_transcode.md` (playbook `genome.page_classes` gates which `knowledge_schema` entries can ever
fire) — this check is accurate against the real consumer, unlike CAP-756/CAP-760/CAP-761.

## CAP-758 — `meta.title` / `agent_notes` soft nudges

**Evidence:** `CheckMetaAndNotes` (lines 193-208). Two independent, always-optional nudges: missing
`meta.title` → `warning` (moves the grade from `ready` to `usable` even though nothing is functionally
broken — a stylistic/operator-friendliness issue, not a correctness issue, treated at `warning` severity
rather than `info`); missing `agent_notes` → `info` only (does not affect grade at all). `agent_notes` is
confirmed live-consumed elsewhere (`PlaybookSeedResolver.cs`, `OccamPlaybookResolveTool.cs`/`Models.cs`,
`PlaybookCommunitySanitizer.cs`) — this is a real, meaningful field, not an invented one.

---

## CAP-759 — FINDING: three independent hand-rolled playbook-schema readers, no shared source of truth

**Evidence:** grep across `src/` for `TryParse`-style playbook readers turns up **three separate**
implementations, none of which call each other or share a schema definition:

1. `PlaybookLinter.Lint` (this tool) — the subject of this audit.
2. `PlaybookDocument.TryParse` (`Playbooks/PlaybookDocument.cs`) — the actual gate inside
 `PlaybookSaveService.SaveAsync` (line 28): `if (!Uri.TryCreate(...)) ...; var document =
 PlaybookDocument.TryParse(request.PlaybookJson); if (document is null) return
 Fail(..., "playbook_schema_invalid", ...)`.
3. `PlaybookSeedResolver.TryParseSeed` (private method, `Playbooks/PlaybookSeedResolver.cs` lines
 437-497) — the actual gate for every playbook consulted during `occam_playbook_resolve` /
 `playbook_policy=auto` resolution.

Each of the three checks a *different* subset of fields with *different* strictness, confirmed field-by-
field:

| Field | Lint (this tool) | Save gate (`PlaybookDocument.TryParse`) | Resolve gate (`TryParseSeed`) |
|---|---|---|---|
| `schema_version` required, 1.x | error if missing/non-1.x | returns `null` (hard reject) if missing/non-1.x | returns `null` (hard reject) if missing/non-1.x |
| `id` required | error if missing | returns `null` if missing | returns `null` if missing |
| `hosts` non-empty array | error if missing/empty | returns `null` if missing/not-array/empty | returns `null` if missing/not-array/empty |
| `extract.contentSelectors` | **error if missing/empty** | **not checked at all** | **optional — defaults to `[]`, no rejection** |
| `content_selectors` (snake_case alias) | **not recognized** | not checked | **accepted** as a fallback key name alongside camelCase |
| `knowledge_schema` route check | warning if unrouted | not checked | not checked |
| Secret-key hygiene | **not checked** | not checked (caller's own separate `PlaybookCommunityHygiene.ContainsForbiddenKeys` check, run *before* `TryParse` in `SaveAsync`) | not checked |

This is the root cause of CAP-760/CAP-761 below — it is not one bug but a structural absence of a single
canonical parser that all three tools could delegate to.

## CAP-760 — FINDING: the tool's own description overstates what its errors guarantee

**Evidence:** the tool's `[Description]` attribute (`OccamPlaybookLintTool.cs` line 18) states: *"Errors
break resolve/save (missing schema_version/id/hosts/extract.contentSelectors)."* Cross-checked against
CAP-759's table:

- `schema_version`/`id`/`hosts` missing: **true** — both `PlaybookDocument.TryParse` (save) and
 `TryParseSeed` (resolve) return `null`/reject on these, matching the claim.
- `extract.contentSelectors` missing: **false for both.** `PlaybookDocument.TryParse` never reads the
 `extract` property at all — a playbook missing `contentSelectors` entirely still parses successfully and
 `SaveAsync` will write it to disk (assuming `Verify: false`, which is the default per
 `OccamPlaybookSaveTool`'s own default — see that tool's own audit for confirmation). `TryParseSeed`
 explicitly defaults `selectors` to `Array.Empty<string>()` when the field is absent (line 477) rather than
 returning `null` — a playbook with no content selectors resolves successfully as a valid
 `PlaybookSeedDocument` with an empty selector list; nothing about resolve "breaks."

So the tool's stated contract is **partially false**: three of the four fields it lists as
resolve/save-breaking really are; the fourth (`extract.contentSelectors`, the one field this linter is
strictest about — CAP-755's two dedicated error codes) is not enforced by either real consumer at parse
time. An agent that reads only the tool description and treats every listed field as equally load-bearing
will over-trust the `contentSelectors` error.

## CAP-761 — FINDING: zero coverage of the save-path's secret-key hygiene rejection

**Evidence:** `PlaybookSaveService.SaveAsync` (`Playbooks/PlaybookSaveService.cs` lines 23-26) runs
`PlaybookCommunityHygiene.ContainsForbiddenKeys(request.PlaybookJson)` **before** any schema parsing, and
hard-rejects (`playbook_save_rejected`, *"playbook_json contains forbidden secret keys"*) if the JSON
contains, at any nesting depth, a property literally named `cookie`, `cookies`, `authorization`,
`set-cookie`, `set_cookie`, `bearer`, `bearer_token`, `api_key`, `apikey`, `password`, `secret_key`,
`session_token`, or `access_token`/`refresh_token` (`Playbooks/PlaybookCommunityHygiene.cs` lines 8-24).
`PlaybookLinter` has **no equivalent check anywhere** — a playbook JSON with e.g. a stray `"api_key":
"..."` field left over from a debugging session gets zero issues for it and can still grade `ready`, yet
`occam_playbook_save` would unconditionally reject that exact document. This is the single most consequential
gap for the tool's own stated use case ("Use before a live playbook_save") — the one save-time rejection
rule most likely to surprise a community-playbook author is invisible to the tool meant to catch problems
before that call.

## CAP-762 — FINDING: `content_selectors` (snake_case) alias silently unsupported by lint

**Evidence:** `PlaybookSeedResolver.TryParseSeed` (lines 478-482) accepts **either**
`extract.contentSelectors` (camelCase) **or** `extract.content_selectors` (snake_case) as a fallback key
name when reading selectors for resolve. `PlaybookLinter.CheckExtract` only ever reads
`extract.contentSelectors` (camelCase) — a playbook authored with the snake_case key (which resolve would
happily accept) is flagged by lint as `extract.contentSelectors` `missing`/`empty` (both `error`,
`broken` grade) even though it would resolve correctly in production. This compounds CAP-756's pattern: the
linter's private field/value knowledge is narrower than what the real consumer(s) actually accept.

## CAP-763 — Tool-surface exposure: `full` + `auditor` profiles only

**Evidence:** `Transport/OccamMcpServerRegistration.cs` — `occam_playbook_lint` is in the always-full
`OccamToolNames` catalog and is individually gated by `OccamToolProfile.IsExposed("occam_playbook_lint",
profile)`. Cross-referenced against Wave-1 `CAP-009` (`docs-audit/subsystems/runtime-mcp.md`): the
`auditor` profile is `researcher` (`reader` + `claim_check` + `verify`) **+** `attest`, `dataset_export`,
`playbook_lint` — i.e. this is the profile-narrowing behavior the task brief calls out ("Profile auditor
includes this"), confirmed directly from the registration switch statement, not just the CAP-009 table.
Not present in `reader` or `researcher` profiles; present in `full` (default) and `auditor`.

---

## Cross-cutting categories (explicit check, per shared instructions)

| Category | Used by this tool? |
|---|---|
| Proxy | Not used — no network at all. |
| Session / cookies / headers | Not used. `session_profile` is not a parameter of this tool. |
| HTTP / browser backends | Not used — no `IExtractBackend` call anywhere in the call graph. |
| Managed providers | Not used. |
| Retry / cache | Not used — single-shot pure function, no caching layer touched. |
| Diff / delta | Not used. |
| Blocks / tables / chunks | Not used — this tool has no relationship to `json_blocks`/`json_tables`/`semantic_chunking`. |
| Budget (`max_tokens` etc.) | Not used — no token budgeting; the response is small and unbounded by design (an adversarially large `issues[]` array is theoretically possible from a huge `hosts`/`contentSelectors` array, but no cap was found — see Uncertainties). |
| Receipts / Merkle / capsules | Not used — no `ReceiptSigner`/`CapsuleCodec` reference in either file. Contrast with `occam_playbook_save`'s own signed-playbook output (`PlaybookSignature.BuildSignedJson`, Wave-1 CAP-281), which this tool does not preview or validate. |
| Playbooks | **This tool's entire subject** — see CAP-750…762 above. |
| Datasets / claims | Not used. |
| Trust tags (`tag_trust`) | Not used — unrelated to block-level trust tagging. |
| Screenshots | Not used. |
| Translate | Not used. |
| `llms.txt` | Not used. |
| Feeds | Not used. |
| Profile (`OCCAM_PROFILE`) | **Used** — gates whether this tool is registered at all (CAP-763); the tool's own logic is profile-agnostic once invoked. |
| Env vars | **None** — grep of both files found zero `Environment.GetEnvironmentVariable`/`OCCAM_*` references. This is the only core tool audited so far with literally zero env-var surface. |

---

## HIDDEN / NON-OBVIOUS CAPABILITIES

Capabilities a user would never discover from the tool's one-line MCP description
(*"Statically validate a playbook/genome JSON against the 1.x schema (no network)…"*):

1. **The lint is not authoritative for save or resolve** (CAP-759/760/762) — a `ready` or `broken` grade
 from this tool can disagree with what `occam_playbook_save`/`occam_playbook_resolve` will actually do
 with the exact same JSON, in both directions (false "broken" on snake_case selectors or the
 `http-then-browser` alias; false "ready" on forbidden secret keys).
2. **Secret-key rejection is entirely absent** (CAP-761) — the single most likely reason a community
 playbook submission gets bounced by `occam_playbook_save` (leftover `api_key`/`cookie`/etc. field) is
 never flagged by lint.
3. **No numeric/structural limits are enforced** — there is no check on `hosts.length`, selector count,
 or overall document size; a 10 MB `playbook_json` is parsed in full by `JsonDocument.Parse` with no
 pre-flight size guard local to this tool (unlike, e.g., `occam_transcode`'s response-byte cap).
4. **Zero env-var surface** — nothing about this tool's behavior can be tuned or disabled by an operator;
 it is always available (subject only to `OCCAM_PROFILE`) and always behaves identically.
5. **`schema_version` upper-bound is unchecked** — the linter only rejects non-`1.x`, not
 too-new-`1.x` versions; `PlaybookSeedResolver`'s separate resolve-time compatibility check (real
 `Version.TryParse` major/minor comparison, referenced near line 239-253 of `PlaybookSeedResolver.cs`) is
 stricter than what this linter previews.

---

## Capability graph edges

```
TOOL|USES|CAP-750
TOOL|USES|CAP-751
TOOL|USES|CAP-752
TOOL|USES|CAP-753
TOOL|USES|CAP-754
TOOL|USES|CAP-755
TOOL|USES|CAP-756
TOOL|USES|CAP-757
TOOL|USES|CAP-758
PARAM:playbook_json|ENABLES|CAP-752
PARAM:playbook_json|ENABLES|CAP-753
PARAM:playbook_json|ENABLES|CAP-754
PARAM:playbook_json|ENABLES|CAP-755
PARAM:playbook_json|ENABLES|CAP-756
PARAM:playbook_json|ENABLES|CAP-757
PARAM:playbook_json|ENABLES|CAP-758
CAP-750|PRODUCES|PlaybookLintReport(json)
CAP-751|CONSUMES|issues[]
CAP-756|DIVERGES_FROM|CAP-051
CAP-755|DIVERGES_FROM|occam_playbook_save:PlaybookDocument.TryParse
CAP-755|DIVERGES_FROM|occam_playbook_resolve:PlaybookSeedResolver.TryParseSeed
CAP-761|ABSENT_VS|PlaybookCommunityHygiene.ContainsForbiddenKeys
CAP-762|DIVERGES_FROM|occam_playbook_resolve:PlaybookSeedResolver.TryParseSeed
CAP-763|GATED_BY|CAP-009
TOOL|CONSUMES|none(no session, no proxy, no worker, no cache)
TOOL|PRODUCES|advisory-only-grade
```

---

## Failure modes of the tool itself

Not "failure codes" in the `occam_transcode` sense (no `failure.code` envelope exists for this tool) —
`Lint` never throws for malformed input; every parse failure is captured and turned into a single
`error`-severity issue instead:

- Empty/whitespace `playbook_json` → 1 error, `code: "empty_input"`, `field: "(root)"`.
- Invalid JSON syntax → 1 error, `code: "json_invalid"`, `field: "(root)"`, message includes the raw
 `JsonException.Message`.
- Valid JSON but not an object (e.g. a bare array or string) → 1 error, `code: "not_object"`.

In all three short-circuit cases the function returns immediately after the single error — no attempt is
made to report on the fields that theoretically follow (correctly avoids spurious cascading errors on
unparseable input).

---

## Uncertainties

- Whether a pathologically large `playbook_json` (megabytes of `hosts`/`contentSelectors` entries) has
 any practical DoS relevance for a locally-invoked lint call was not measured — no size guard was found,
 but this tool is advisory/local-only (no network reflection of the payload), so the blast radius is
 limited to CPU time on the host process itself.
- Whether `docs/` or `MCP_API_SPEC.md` already document the resolve/save divergence found in CAP-760/761
 was not checked (out of scope per Wave 2 shared instructions — docs are untrusted/unread for behavior).
- Whether any *other* profile string (a custom `OCCAM_PROFILE` value beyond the four in CAP-009) could
 expose or hide this tool differently was not independently re-derived here — taken from Wave-1 CAP-009 by
 reference, re-confirmed only at the registration call-site level (CAP-763), not by re-deriving
 `OccamToolProfile.IsExposed`'s full internal logic from scratch.

**COMPLETENESS: COMPLETE** — both assigned files (`OccamPlaybookLintTool.cs`, `PlaybookLinter.cs`) were
read in full; every branch/field check in `PlaybookLinter.Lint` has a corresponding CAP; the tool's central
claim ("errors break resolve/save") was independently verified against the two real consumers rather than
taken at face value.
