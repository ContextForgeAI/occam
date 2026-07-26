# RC.2 validation plan

**Purpose:** prove correctness, compatibility, semantic honesty, and operational safety of the proposed
RC.2 design. Frozen offline fixtures are release-blocking; live URLs are a non-blocking drift/soak layer.

## 1. Evidence tiers

| Tier | Input | Purpose | Release role |
|---|---|---|---|
| Unit/property | Synthetic records and generated Markdown/DOM | Invariants, boundaries, redaction, deterministic ranking | Blocking |
| Frozen replay | RC.1 archived pages plus curated login controls | Reproduce D9–D19 without network drift | Blocking |
| MCP contract | Published host through stdio and `tools/list` | Binding, schema, typed failures, serialized budgets | Blocking |
| Platform/AOT | Release publishes on Windows, Linux, macOS targets | Trimming, JSON metadata, process lifecycle | Blocking |
| Live soak | Current public URLs and controlled authenticated target | Detect ecosystem drift | Informational unless a product invariant fails |

All gate commands capture stdout, stderr, exit code, resolved binary, commit, RID, environment overrides,
and fixture hashes. The published AOT host is used for final stdio evidence.

## 2. Frozen corpus

The minimum set contains:

- public authentication prose: httpwg RFC 9110/7235, datatracker RFC 9110/7235, OpenID Core,
  oauth.net, plus rfc-editor and MDN controls;
- real access controls: HTTP 401/403 with challenge, redirect-to-login, blocking password form,
  non-password identity form, public page containing a harmless login form, and login prose only;
- focus: RFC 401 definition, nginx `client_max_body_size`, Python simple requests, WHATWG Fetch,
  MDN and GitHub documentation sections;
- focus perturbations: short/polluted queries, quoted phrases, numeric/dotted/snake/hyphen identifiers,
  exact/missing/encoded fragments, duplicate headings, TOC-only mentions;
- budgets: D10 700/2,000, nginx 4k/12k, all optional blocks/tables/media/receipt projections;
- semantics/lifecycle: D13 contradicted claim, D16 backend recovery, D3 dual host trees, D18 quiet and
  verbose Hermes controls.

Fixtures retain provenance and hashes but redact credentials, cookies, authorization values, and private
URLs. A real authenticated target uses disposable test credentials outside committed artifacts.

## 3. Contract and compatibility matrix

### Digest input

| Input | Expected result |
|---|---|
| Native string array | Success; order preserved |
| JSON-array string | Same canonical URL list |
| Newline string | Same canonical URL list |
| Empty/mixed/nested array | JSON `invalid_arguments`; handler/host remains healthy |
| Number/object/null where invalid | JSON `invalid_arguments`, never framework prose |
| Maximum and maximum+1 items/bytes | Deterministic accept/reject boundary |

Snapshot the runtime `tools/list` schema and exercise it with at least two MCP clients that interpret
`oneOf`. Verify old RC.1 string requests byte-for-byte where the response is otherwise unchanged.

### Response compatibility

- Old aliases remain present and keep RC.1 meanings during the migration window.
- New outcome objects survive unknown-field-tolerant old clients.
- Canonical JSON casing, enum spelling, optional/null behavior, and typed failures are snapshotted.
- Receipt verification is rerun for every projection and truncation path.

## 4. Access classifier validation

Measure a labeled confusion matrix for `login_likely` against direct access-control ground truth. Report
precision, recall, false-positive rate on public prose, and `unknown` rate separately; do not fold
`unknown` into success.

Blocking invariants:

- zero hard login decisions from text phrases alone;
- zero false positives on the named public-reference frozen corpus;
- all controlled HTTP challenge, redirect, and blocking password-wall fixtures classify
  `login_likely` with direct evidence codes;
- probe and transcode produce the same disposition from the same evidence record;
- absence of DOM evidence produces `unknown` when status/redirect evidence is inconclusive;
- diagnostics and logs contain no secret/header/form values.

Owner-approved numeric recall/unknown thresholds for the broader login corpus are required before PR-C
merges; proposed starting thresholds appear in the open questions.

## 5. Focus and fragment validation

For each case, label the expected section/anchor and one or more answer-body needles. Measure top-1 exact
section accuracy, top-3 recall, reciprocal rank, focus calibration (`hit`/`weak`/`miss`), and TOC false
positive rate. Report short and polluted queries separately.

Blocking invariants:

- numeric and technical identifiers remain query features;
- every named D11/D15 case selects an answer-bearing section, not a TOC mention;
- exact valid fragments resolve deterministically and missing fragments are explicit;
- `hit` requires a body needle or equivalent structural evidence;
- same input yields identical candidate order across runs/platforms;
- a weak/missing target is never reported as a confident hit.

Add metamorphic tests: inserting an unrelated TOC, repeating a common query term, or adding authentication
prose elsewhere must not displace an exact anchor/answer section.

## 6. Budget and materialization validation

Property tests enumerate requested public projections and assert:

- an unrequested field receives zero public allocation;
- allocation totals equal the projected inventory;
- serialized estimated/actual size remains within the documented estimator tolerance;
- increasing `max_tokens` never removes a previously protected answer unit;
- optional context is removed before protected focus body;
- inability to fit is returned as `incomplete`, never a silent success.

The D10 700-token case must either retain the labeled minimum answer unit within the contract or return
explicit `focus_body_truncated` plus `suggestedMinTokens`. Passing by silently exceeding 700 is forbidden.
The nginx 4k case must no longer reserve blocks/tables when neither is requested.

Estimator calibration reports actual serialized tokens versus estimated tokens at p50/p95/max for each
codec/projection. The owner must approve the tolerance; the suggested initial bound is max(3%, 16 tokens),
measured rather than assumed.

## 7. Semantic honesty validation

Scenario tests assert each dimension independently:

- an HTTP extract can have `transportOk=true`, `usable=false`, and an escalation reason;
- a browser recovery can become usable without rewriting the prior attempt;
- a relevant contradicted claim is `retrieved=true`, not implicitly supported;
- focus body loss yields `focus=weak|miss` and `completeness=incomplete` even when extraction is healthy;
- agent hints recommend session/retry/trust only from the corresponding structured fields.

Run an agent interpretation test with and without prose docs. A correct decision must be derivable from
the JSON fields alone; prose may explain but not repair ambiguous field names.

## 8. Performance, memory, and security

Use the same frozen inputs and process lifetime for baseline RC.1 and candidate RC.2. Report median and
p95 wall time, CPU time, peak working set, allocation volume, output tokens, backend attempts, and worker
payload size by page-size bucket. Do not headline one global percentage.

Proposed non-functional guardrails, subject to owner approval:

- Core-only focus planning p95 CPU overhead no more than 10% or 25 ms, whichever is larger;
- peak working-set increase no more than 10% on the large-document bucket;
- no additional network request and no hosted-model dependency;
- DOM signal payload bounded independently of source page size;
- no catastrophic regex/backtracking; fuzzed query and HTML records complete within test timeouts;
- JSON/source-generation and trimming warnings are clean in Release AOT publishes.

Security cases include deceptive login text, hidden password inputs, public content plus a login widget,
credential-bearing URLs, oversized arrays, deep JSON, anchor normalization collisions, and malicious
heading/link density. Verify redaction and bounded parsing.

## 9. Lifecycle validation

Run a process-tree matrix on supported OS families:

- normal client close, SIGINT, SIGTERM, launcher crash, Core crash, worker crash, and forced timeout;
- launcher and binary paths containing spaces;
- two legitimate profiles with different roots/owner labels;
- two overlapping instances from dashboard and gateway;
- exact-instance stop versus a nonmatching process;
- repeated start/stop cycles with no surviving owned child.

Acceptance requires correct exit propagation, bounded shutdown, no process-name-wide kill, and an
instance descriptor sufficient to explain every surviving tree. Hermes verbose resume behavior remains a
separate integration observation unless it invokes an Occam lifecycle contract.

## 10. Release gates and evidence report

Before RC.2 tagging:

1. All new unit/property/frozen/MCP contract suites pass.
2. `occam-doctor`, L0 fast and full gates pass with real exit codes.
3. Release AOT publish/stdio smoke passes for supported RIDs.
4. `node scripts/check-docs.mjs` and environment catalog checks pass.
5. Performance/security/lifecycle reports satisfy owner-approved guardrails.
6. Live soak is recorded by URL tier with observed date and does not replace frozen evidence.
7. Every exception is classified as accepted risk with owner and expiry; no P0/P1 honesty exception is
   accepted for RC.2.

The final report must link fixture hashes, commands, raw stdout/stderr, schema snapshots, metric tables,
and the exact commit/binaries. A green aggregate label without raw evidence is insufficient.
