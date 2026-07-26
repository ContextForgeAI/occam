# `occam_playbook_save` — Deep Capability Audit (Wave 2)

**Source of truth:** current executable code only (`src/FFOccamMcp.Core/**`). Documentation
(`docs/*.md`, `MCP_API_SPEC.md`, `AGENTS.md`) was **not** used as evidence.

**CAP ID range owned by this audit:** `CAP-560`–`CAP-589` (used: CAP-560…CAP-577; remainder
reserved, not exhausted).

**Files inspected:**
`Tools/OccamPlaybookSaveTool.cs`, `Playbooks/PlaybookSaveService.cs`,
`Playbooks/PlaybookSaveVerifier.cs`, `Playbooks/PlaybookSignature.cs`, `Playbooks/QualityGate.cs`,
`Playbooks/PlaybookDocument.cs`, `Playbooks/PlaybookCommunityHygiene.cs`, `Playbooks/PlaybookPaths.cs`,
`Playbooks/PlaybookVerifyScope.cs`, `Playbooks/PlaybookHealModels.cs` (request/result records),
`Playbooks/PlaybookSeedResolver.cs` (`ClearCacheForTests`), `Receipts/ReceiptSigner.cs`,
`Receipts/ReceiptsPolicy.cs`, `Composition/OccamServiceCollectionExtensions.cs` (DI registration),
cross-referenced against `Tools/OccamTranscodeTool.cs`, `Claims/ClaimCheckService.cs`,
`Dataset/DatasetExportService.cs`, `Services/DigestService.cs`, `Watch/WatchService.cs` (the five other
`ReceiptsPolicy.Enabled() ? signer : null` gate call sites, for the EF-005 contrast).

---

## 0. Entry point and schema

`OccamPlaybookSaveTool.Save` (`Tools/OccamPlaybookSaveTool.cs`) takes: `url` (required),
`playbook_json` (required), `verify` (bool, default `true`), `verify_url` (optional, defaults to
`url`), `lesson_note` (optional, 1–500 chars), `failure_reason` (optional), `host_id` (optional).
It delegates entirely to `PlaybookSaveService.SaveAsync`, injected as a DI singleton alongside a
singleton `ReceiptSigner` (`OccamServiceCollectionExtensions.AddOccamCore`, line ~23/133).

---

## CAP-560 — `url` (host-key resolution input, not the write target)

**Trace:** validated only for `Uri.TryCreate(..., UriKind.Absolute, ...)` (`invalid_url` on
failure). Unlike `occam_transcode`'s `url`, this `url` is **not** passed to `PrivacyClassifier` —
it never triggers a live fetch itself; it exists purely so the playbook can be filed and so the
default verify target (`verify_url` omitted) has a value. No SSRF check is needed here because
the only outbound network call in this tool (`PlaybookSaveVerifier` → `TranscodePipeline`) already
re-runs its own preflight (`FetchPreflight`, see `occam_transcode` CAP-100) on `verify_url`.

## CAP-561 — `playbook_json` (draft input, schema + hygiene gated)

**Trace:** `PlaybookDocument.TryParse` (`Playbooks/PlaybookDocument.cs`) requires
`schema_version` starting with `"1."`, a non-empty `id`, and a non-empty `hosts[]` array — anything
else returns `playbook_schema_invalid`. Independently, `PlaybookCommunityHygiene.ContainsForbiddenKeys`
recursively walks the **entire** JSON tree (any depth, any array) for a fixed case-insensitive
denylist of property names (`cookie`, `cookies`, `authorization`, `set-cookie`, `bearer`,
`bearer_token`, `api_key`, `apikey`, `password`, `secret_key`, `session_token`, `access_token`,
`refresh_token`) — a hit anywhere → `playbook_save_rejected`, **before** schema parsing is even
attempted. A malformed (non-JSON) `playbook_json` is treated as "contains forbidden keys" by the
hygiene scanner's own catch-block (`catch (JsonException) { return true; }`), so a syntactically
broken document is rejected via `playbook_save_rejected` from the hygiene check rather than reaching
the `playbook_schema_invalid` path — a minor failure-code precedence quirk (hygiene runs first).

## CAP-562 — `verify=true` dry-run quality gate (the tool's own headline behavior)

**Trace:** `PlaybookSaveVerifier.VerifyAsync` (`Playbooks/PlaybookSaveVerifier.cs`):
1. `verify_url`'s host must match one of `playbook_json`'s `hosts[]` (`PlaybookDocument.HostMatches`
 — suffix-aware, `www.` normalized) — else `playbook_schema_invalid`.
2. Resolves an effective backend policy from the playbook's own `routing.preferred_backend` (falls
 back to `http_then_browser` if absent/unparseable — **does not** consult the caller's environment
 default the way `occam_transcode` does).
3. Pushes the **draft** JSON into `PlaybookVerifyScope` (CAP-568) so the dry-run transcode applies
 the *unsaved* recipe, not any already-resolved playbook for that host.
4. Runs a real `TranscodePipeline.TranscodeAsync` call. Failure, or markdown `< 100` chars, or
 `QualityGate.AssessExtraction` scoring below `MinScore=70` or noise above `MaxNoise=0.12`
 (`QualityGate.cs`) → save is **rejected** (`playbook_verify_failed` /
 `playbook_verify_low_score` / `playbook_verify_high_noise`) and **nothing is written to disk** —
 confirmed: the failure return in `PlaybookSaveService.SaveAsync` happens before the
 `File.WriteAllText` call, so a failed dry-run leaves zero trace on the local playbook tier.

This is a genuinely strict gate (not just a warning): `passesGate=false` on the dry-run is a hard
stop for `verify=true`, unlike e.g. `occam_transcode`'s thin-extract detection which still returns
content with a warning.

## CAP-563 — `verify=false` bypass (unverified save still writes AND signs)

**Trace:** `PlaybookSaveService.SaveAsync` — when `request.Verify` is `false`, `verifyMetrics` stays
`null` and the quality-gate block is skipped entirely; execution falls straight through to the
lesson-append and signing steps. The playbook is written to disk **and cryptographically signed**
(CAP-571) with `passesGate: false` and no `score`/`noiseLeakage` fields recorded in its own
`provenance.verify` block — i.e. the saved artifact **honestly declares** it was never quality-gated
(a consumer reading `provenance.verify.passesGate=false` via `occam_playbook_resolve`'s
`PlaybookSignature.Inspect` — CAP-282 — can distinguish this from a gate-passed save), but the
signature itself does not depend on having passed any gate. This is a deliberate escape hatch (e.g.
for authoring workflows that verify separately via `occam_playbook_lint` + manual testing) rather
than an oversight, but it means "signed" never implies "quality-verified" on this tool.

## CAP-564 — `verify_url` (independent verify target + host-match enforcement)

**Trace:** `PlaybookSaveVerifier.VerifyAsync`. Defaults to `url` when omitted
(`PlaybookSaveService.SaveAsync` line ~49). Its host **must** belong to the playbook's declared
`hosts[]` — this prevents verifying a save against an unrelated site and then persisting a recipe
whose `hosts[]` claims coverage it was never actually tested against for *that* URL. There is no
cross-check that `verify_url` is reachable from `url` in any other sense (e.g. same page) — only
host-membership is enforced.

## CAP-565 — `lesson_note` (bounded lesson journal with rotation)

**Trace:** Tool-level length guard (`1–500` chars, trimmed) returns `playbook_schema_invalid`
before the service is even called — the only tool-layer validation not delegated to the service.
`PlaybookDocument.AppendLesson` reads any existing `lessons[]` array, appends a new entry via
`PlaybookJsonElementWriter.CreateLesson(note, failureReason, verifyScore, hostId)`, and trims the
list from the **front** (oldest first) down to `PlaybookHealPolicy.MaxLessonsPerFile` if it grows
past that cap — a bounded, self-pruning append-only log embedded inside the playbook file itself,
shared in kind with the `occam_playbook_heal` lesson mechanism. `AppendLesson` runs **after** the
verify gate but **before** signing, so an appended lesson's text also gets covered by the content
hash/signature (CAP-572).

## CAP-566 — `failure_reason` / `host_id` (lesson metadata, never secrets by construction)

**Trace:** Both are plain optional strings passed straight into `CreateLesson`. Neither is validated
against the forbidden-key hygiene list (CAP-561 only scans `playbook_json`'s structure, not these
tool parameters) — an operator could still type a secret-looking string into `failure_reason`
manually; the hygiene scan is a structural playbook-content guard, not a general PII/secret filter
over every string parameter. `host_id` is explicitly commented in the tool schema as "never
secrets" — a documented convention, not an enforced one.

## CAP-567 — Local-tier-only write confinement (path traversal + bundled-seed protection)

**Trace:** `PlaybookSaveService.SaveAsync`. Target path is
`Path.Combine(PlaybookPaths.ResolveLocalRoot(), $"{document.Id}.playbook.json")` — `document.Id`
comes from the parsed JSON (attacker/author-controlled string), so the resolved **full** path is
re-checked with `Path.GetFullPath(...).StartsWith(localRootFull, OrdinalIgnoreCase)`; a `document.Id`
containing `../` sequences that would escape the local playbook root is rejected with
`playbook_save_rejected` ("Refusing to write outside local playbook tier") — the same defensive
pattern family as `occam_transcode`'s `session_profile` ID hardening (CAP-069). A second,
independent check (`IsBundledSeedPath`) string-matches the resolved path against
`/profiles/playbooks/seeds/` and refuses to overwrite anything landing there — this only matters if
`OCCAM_PLAYBOOKS_LOCAL_ROOT` were itself pointed at (or nested under) the seeds directory; under the
default root (`PlaybookPaths.ResolveLocalRoot()` → user data dir `playbooks/local`) this check is
normally unreachable, but it is a defense-in-depth guard against a misconfigured
`OCCAM_PLAYBOOKS_LOCAL_ROOT`.

## CAP-568 — `PlaybookVerifyScope` (draft-overlay dry-run mechanism)

**Trace:** `Playbooks/PlaybookVerifyScope.cs`. An `AsyncLocal`-based scope (same family as
`occam_transcode`'s `FetchHeadersScope`/`OccamFeaturesScope`) that writes the **draft** JSON to a
temp file (`occam-playbook-verify-<guid>.json`) and exposes both the temp path and the raw JSON
string (`ActiveJson`) for the duration of the dry-run call — the raw-JSON channel exists specifically
so a **warm browser-daemon pool** can apply the draft overlay in-memory without needing shared-filesystem
access to the temp file (comment: "sent inline to the browser daemon so the warm pool applies it
without the temp-file/shared-fs coupling"). `Push(playbookJson, strict: true)` defaults to **strict**
mode for save-verify (selector-only, no Readability fallback) — distinct from an auto-resolved genome
overlay reached via `playbook_policy=auto`, which is non-strict. `Dispose()` best-effort-deletes the
temp file; a process crash mid-verify would leak one orphan temp file (low-severity, not flagged as
an engineering finding given the narrow window and OS temp-dir cleanup).

## CAP-569 — `QualityGate.AssessExtraction` (shared heuristic quality scorer)

**Trace:** `Playbooks/QualityGate.cs`. Computes a 0–100 score from five weighted 0–1 factors —
content retention (30%, from markdown length buckets), inverse noise (25%), structure/heading
density (20%), page completeness (15%, same value as retention in the `AssessExtraction` shortcut),
backend confidence (10%, hardcoded `0.85` for this call site — not sourced from the actual
`TranscodeOutcome`'s own confidence signal). Noise is estimated via a fixed 10-term boilerplate
phrase list (`"cookie"`, `"subscribe"`, `"sign in"`, `"accept all"`, `"advertisement"`,
`"privacy policy"`, `"table of contents"`, `"related posts"`, `"share this article"`,
`"sign up for free"`) with the first hit discounted (`Math.Max(0, noiseHits - 1)`) before scaling —
i.e. a page needs **two or more** distinct boilerplate phrases before noise starts counting against
it at all. `MinScore=70` / `MaxNoise=0.12` are hardcoded constants, not tunable via any
`occam_playbook_save` parameter or environment variable.

## CAP-570 — Host-match enforcement is host-suffix, not exact

Restated distinctly from CAP-564 for CAP-ID completeness: `PlaybookDocument.HostMatches` /
`MatchesHost` normalizes both sides (strip leading `www.`) and accepts either an exact match or a
subdomain suffix match (`host.EndsWith($".{pattern}")`) — so a playbook declaring `hosts: ["example.com"]`
can be verify-saved against `verify_url=https://docs.example.com/...` without the caller needing to
list every subdomain explicitly. This is the same host-matching primitive used by
`occam_playbook_resolve`'s own tiering (not independently re-audited here — out of this tool's scope
per the assignment).

---

## CAP-571 — HIDDEN/EF-005 RESOLVED: unconditional Receipt v1 signing, `OCCAM_RECEIPTS` NOT consulted

**Evidence:** `PlaybookSaveService.SaveAsync` (`Playbooks/PlaybookSaveService.cs`, lines ~86–93) calls
`PlaybookSignature.BuildSignedJson(jsonToWrite, verifyMetrics?.Score, verifyMetrics?.PassesGate ?? false,
verifyMetrics?.NoiseLeakage, signer)` **unconditionally** — every successful save (verify=true-and-passed,
or verify=false) reaches this line and gets a `provenance` block with `keyId`/`alg`/`contentHash`/
`signature`/`signedAt` injected. **No call to `ReceiptsPolicy.Enabled()` exists anywhere in
`PlaybookSaveService.cs` or `PlaybookSignature.cs`.**

This is confirmed by contrast: `ReceiptsPolicy.Enabled()` (`Receipts/ReceiptsPolicy.cs`, off when
`OCCAM_RECEIPTS` is `off`/`0`/`false`) is explicitly checked at **five independent call sites** before
a `ReceiptSigner` reference is ever used for signing elsewhere in the codebase:

- `Tools/OccamTranscodeTool.cs` (3 sites): `ReceiptsPolicy.Enabled() ? receiptSigner : null`
- `Claims/ClaimCheckService.cs`: `var effectiveSigner = ReceiptsPolicy.Enabled() ? signer : null;`
- `Dataset/DatasetExportService.cs`: same pattern
- `Services/DigestService.cs`: same pattern
- `Watch/WatchService.cs`: `private ReceiptSigner? EffectiveSigner() => ReceiptsPolicy.Enabled() ? signer : null;`

`PlaybookSaveService` is constructor-injected with the **raw** `ReceiptSigner` singleton (registered
unconditionally in `OccamServiceCollectionExtensions.AddOccamCore`: `services.AddSingleton(_ =>
ReceiptSigner.LoadOrCreate())` — no conditional registration either) and never gates its use. Setting
`OCCAM_RECEIPTS=off` has **zero effect** on `occam_playbook_save`: every saved playbook still gets a
real ECDSA P-256 signature written to disk, still generates/loads the same on-disk signing key under
`OCCAM_KEYS_ROOT` on first use, and the response still echoes a non-null `SignedKeyId`. An operator who
sets `OCCAM_RECEIPTS=off` expecting **no signing anywhere** (a reasonable reading of "the single global
kill-switch", per `ReceiptsPolicy`'s own doc comment) will be surprised to find every saved playbook on
disk still carries a live signature.

**This resolves EF-005 (`docs-audit/ENGINEERING-FINDINGS.md`):** the claim "`playbook_save` may sign
unconditionally" is **PROVEN TRUE** — it does not consult `ReceiptsPolicy` at all, unlike every other
receipt-emitting subsystem in the codebase, which all instantiate the identical
`ReceiptsPolicy.Enabled() ? signer : null` guard. This is a genuine **inconsistency**, not a matter of
by-design scope: SI-08's own code comment frames playbook signing as building on the *same* Receipt v1
signer identity as everything else ("the basis for a future signed registry + reputation"), which
implies it was intended to share the same on/off semantics, not bypass them.

**Whether this is a bug or intentional-by-design is a judgment call left to the orchestrator** —
arguments for "intentional": playbook self-authentication (who authored this recipe) is arguably a
different concern from content-provenance receipts (was this markdown really extracted from this URL),
so gating them identically may not make semantic sense; a locally-authored playbook signature is cheap
(one ECDSA sign over a small JSON document, not a full Merkle tree over extraction blocks) and has no
external network/privacy cost. Arguments for "bug": (a) the kill-switch's own doc comment claims to be
"the single gate ... so the transcode/digest/claim-check/dataset paths share one definition" — playbook
save is conspicuously absent from that list despite existing at audit time; (b) an operator relying on
`OCCAM_RECEIPTS=off` as a blanket "disable all local key material generation and signing" switch (e.g.
for a locked-down/air-gapped deployment that wants zero cryptographic key state on disk) gets a false
sense of that guarantee.

## CAP-572 — Signature scope: canonical hash excludes `provenance` (idempotent re-sign)

**Evidence:** `PlaybookSignature.ContentHash` / `WriteCanonical` (`Playbooks/PlaybookSignature.cs`).
The canonical hash is computed over the full document with keys sorted alphabetically at every level,
**excluding the top-level `provenance` key only** (nested objects are not exclusion-filtered — a
`provenance`-named key nested inside e.g. a `selectors` object would NOT be stripped, though this is a
theoretical edge case given the playbook schema's own shape). `BuildSignedJson` always **replaces** any
existing `provenance` block wholesale (`if (prop.Name == "provenance") { continue; }` when copying,
then a fresh block is appended) — re-saving an already-signed playbook produces a fresh, valid signature
over the (possibly lesson-appended) new content rather than erroring on "already signed".

## CAP-573 — `SignedKeyId` self-authentication echo (response-level trust signal)

**Evidence:** `OccamPlaybookSaveTool.cs` response construction; `PlaybookSaveResult.SignedKeyId`.
The success response's `signedKeyId` field lets the calling agent immediately know which local signer
identity produced the save (useful for multi-key/multi-host operator setups) without needing a separate
`occam_verify`/`occam_playbook_resolve` round-trip just to learn the key id — a minor convenience but a
genuine capability distinct from the signature itself.

## CAP-574 — Verify-gate proof is embedded **inside** the signed payload, not just alongside it

**Evidence:** `PlaybookSignature.BuildSignedJson` writes `provenance.verify.{score, passesGate,
noiseLeakage}` **inside** the same object that gets hashed and signed — so the verify-gate claim is
part of what the signature attests to, not a separate unsigned field an attacker/tamperer could alter
independently. `PlaybookSignature.Inspect` (consumer side, reached from `occam_playbook_resolve`, not
independently re-audited here — cross-ref CAP-282) reads this same embedded block when reporting a
resolved playbook's trust status.

## CAP-575 — HIDDEN: immediate in-process resolver cache invalidation on save (mislabeled "ForTests")

**Evidence:** `PlaybookSaveService.SaveAsync` calls `seedResolver.ClearCacheForTests()`
unconditionally after every successful write (`Playbooks/PlaybookSaveService.cs` line 94); the method
itself (`PlaybookSeedResolver.ClearCacheForTests`, `Playbooks/PlaybookSeedResolver.cs` line 181) is
`internal` and named/commented as a test helper, but it is invoked from the **production** save code
path, not from any test fixture. This is a real, load-bearing product capability with a misleading
name: it clears `PlaybookSeedResolver`'s in-process cache (and cascades into
`WellKnownGenomeFetcher.ClearCacheForTests()`) so that a playbook saved via `occam_playbook_save` is
**immediately visible** to the very next `occam_playbook_resolve` or `occam_transcode
(playbook_policy=auto)` call in the same running MCP host process, with no restart and no TTL wait —
a caller would never guess this from the tool's short description ("Save an extraction
playbook/genome JSON you drafted (local only)"), which says nothing about cache freshness.

## CAP-576 — Failure-code agent hints on save rejection

**Evidence:** `OccamPlaybookSaveTool.cs` (`FailureAgentHints.ForCode(code)`,
`Agent/TranscodeAgentDecisions.cs`). Every failure response (schema-invalid, hygiene-rejected,
verify-failed with either sub-reason, host-mismatch) carries a machine-actionable `agentHints.decisions`
array — e.g. `playbook_verify_low_score`/`playbook_verify_high_noise` hint at loosening selectors or
re-checking the target page; `playbook_schema_invalid`/`playbook_save_rejected` hint at running
`occam_playbook_lint` first (matching the tool's own description text). This reuses the same
`ProbeDecision` hint vocabulary as `occam_transcode`'s failure path (CAP-106), not a bespoke one.

## CAP-577 — Best-effort JSON pretty-printing on write (silent fallback to raw on parse failure)

**Evidence:** `PlaybookSaveService.FormatJson` — re-parses the signed JSON string and re-serializes it
indented via `Utf8JsonWriter`; if that throws for any reason, the **raw unformatted** `signedJson`
string is written instead (`catch { return json; }`), with no warning surfaced to the caller. Since
`signedJson` was just built by `PlaybookSignature.BuildSignedJson` from a document `JsonDocument.Parse`
already validated it, this fallback path is effectively unreachable in practice — defensive code for a
case that should not occur, not a live behavior a caller would ever observe.

---

## Cross-cutting capability check (per shared instructions)

| Category | Used by this tool? | Evidence |
|---|---|---|
| Proxy | Not directly | Only indirectly, if `verify=true` reaches `TranscodePipeline` → `OCCAM_HTTP_PROXY`/`OCCAM_HTTPS_PROXY` apply to that dry-run fetch exactly as they would for `occam_transcode` (not re-audited here). |
| Session (`session_profile`) | **Not used** | No `session_profile` parameter on this tool; the dry-run verify transcode always runs unauthenticated. A playbook targeting a login-walled page cannot be verify-saved against real authenticated content. |
| Cookies | Rejected, not consumed | `PlaybookCommunityHygiene` explicitly denylists `cookie`/`cookies`/`set-cookie` keys in the playbook JSON itself (CAP-561). |
| Headers | Not used | No custom-header parameter; dry-run inherits whatever `TranscodePipeline` does by default. |
| HTTP / Browser | Used indirectly | Verify dry-run resolves `http_then_browser` or the playbook's own `routing.preferred_backend` (CAP-562). |
| Managed (third-party scraping) | Reachable only transitively | If `OCCAM_MANAGED_PROVIDER` is configured, the dry-run's `TranscodePipeline` call could in principle cascade to it exactly as `occam_transcode` would — not a distinct capability of this tool. |
| Retry | Not used | No retry logic in `PlaybookSaveService`/`PlaybookSaveVerifier`; a single dry-run attempt either passes or the save is rejected. |
| Cache (`cache_ttl_s`) | Not used | `PlaybookSaveVerifier` calls `pipeline.TranscodeAsync` directly, bypassing `OccamTranscodeTool`'s cache-eligibility layer entirely — no caching parameter exists on this tool and none is exercised. |
| Diff / blocks / tables / chunks | Not used | The dry-run only inspects `outcome.Markdown`; no `json_blocks`/`json_tables`/`diff_against`-equivalent path exists here. |
| Budget (`max_tokens`) | Not applied | The verify dry-run does not pass any token budget to the pipeline call, so it runs at whatever ambient/ no-limit default `TranscodeAsync` uses internally without truncation-aware option wiring. |
| **Receipts / merkle** | **Used, but ungated — see CAP-571** | Signs with `ReceiptSigner.SignDetached` (a detached signature, not a full Merkle-rooted `ReceiptEnvelope`) — this is the SI-08 playbook-signature primitive, a lighter-weight sibling of Receipt v1's block-Merkle receipts, sharing only the signer key. |
| Capsules | Not used | `emit_capsule`-style capsule encoding is transcode-specific; not present here. |
| Playbooks | Core subject of this tool | Writes to the **local** tier only (`PlaybookPaths.ResolveLocalRoot`); cannot write to community/seed tiers. |
| Datasets / claims / trust tags | Not used | No overlap. |
| Screenshots / translate / llms.txt / feeds | Not used | No overlap. |
| Profile (`OCCAM_PROFILE`) | Not specifically checked here | `occam_playbook_save` is one of the always-on core 15 tools per `OccamToolNames` (Wave 1 CAP-007); profile-based filtering is out of this tool's own behavior (cross-ref CAP-008/009, not re-verified in this pass). |
| Env vars | `OCCAM_PLAYBOOKS_LOCAL_ROOT` (write root), `OCCAM_KEYS_ROOT` (signing key, transitively via `ReceiptSigner`) | `PlaybookPaths.ResolveLocalRoot`, `ReceiptSigner.DefaultKeysRoot`. **Not** `OCCAM_RECEIPTS` (CAP-571) and **not** `WT_PLAYBOOKS_PATH` (that env var is resolve-only, read-only tier — never a write target for save). |

---

## Failure code catalog for `occam_playbook_save`

| Code | Source | Notes |
|---|---|---|
| `playbook_schema_invalid` | Tool (`lesson_note` length), `PlaybookSaveService` (missing/empty json), `PlaybookDocument.TryParse` (bad schema_version/id/hosts), `PlaybookSaveVerifier` (verify_url host mismatch) | Multiple distinct causes share one code |
| `playbook_save_rejected` | `PlaybookCommunityHygiene` hit, path-traversal guard, bundled-seed overwrite guard | Also the generic fallback code (`result.FailureCode ?? "playbook_save_rejected"`) |
| `invalid_url` | `PlaybookSaveService` (`url` not absolute), `PlaybookSaveVerifier` (`verify_url` not absolute) | |
| `playbook_verify_failed` | `PlaybookSaveVerifier` — dry-run transcode failed, or markdown `<100` chars, or generic gate-fail fallback | |
| `playbook_verify_low_score` | `QualityGate` — score `<70` | |
| `playbook_verify_high_noise` | `QualityGate` — noise `>0.12` (and score otherwise fine) | |

---

## Hidden / advanced findings (summary)

1. **CAP-571 (EF-005 RESOLVED)** — `occam_playbook_save` signs every successful save with a real
 ECDSA key **unconditionally**; `OCCAM_RECEIPTS=off` has no effect on it, unlike five other
 receipt-emitting call sites that all consult `ReceiptsPolicy.Enabled()`.
2. **CAP-575** — saving a playbook busts the in-process resolver cache immediately, via a method
 literally named `ClearCacheForTests` — a real hot-reload capability hidden behind test-sounding
 naming, invisible from the tool's schema/description.
3. **CAP-563** — `verify=false` still writes a **signed** playbook to disk with an honestly-false
 `passesGate` claim baked into the signed payload — "signed" never implies "quality-checked" on
 this tool, a distinction a downstream consumer of `occam_playbook_resolve`'s trust status must
 understand (see CAP-282 in `docs-audit/subsystems/trust-receipts.md`).
4. **CAP-561** — a malformed (unparseable) `playbook_json` surfaces as `playbook_save_rejected`
 (hygiene-scanner catch-all), not `playbook_schema_invalid`, because the hygiene check runs before
 schema parsing.
5. **CAP-569** — `QualityGate`'s noise heuristic discounts the first boilerplate-phrase hit, so a
 page needs two or more distinct noise phrases before noise starts penalizing the score at all —
 not obvious from the tool's failure code names alone.

## Uncertainties

- Whether the `PlaybookSaveVerifier`'s dry-run transcode call is subject to the same robots.txt /
 host-throttle policy (`OCCAM_RESPECT_ROBOTS`/`OCCAM_HOST_THROTTLE_MS`) as a normal
 `occam_transcode` call — it calls `pipeline.TranscodeAsync` directly rather than going through
 `OccamTranscodeTool`, and whether `TranscodePipeline.CheckAndThrottle` runs unconditionally inside
 `TranscodeAsync` itself (in which case yes) versus being invoked only from the tool layer (in which
 case save's dry-run would bypass throttling) was not conclusively traced in this pass — would need
 a read of `TranscodePipeline.TranscodeAsync`'s own body to resolve definitively.
- Whether `PlaybookSaveVerifier`'s bypass of `OccamTranscodeTool`'s response-shaping also means it
 bypasses post-processors (challenge/login/thin-extract) — evidence suggests `TranscodePipeline`
 (not the tool wrapper) owns post-processor execution (consistent with CAP-094 in the transcode
 audit, which cites `Routing/TranscodePipeline.cs` as the post-processor host), so this is likely
 **not** bypassed, but was not independently re-verified by reading `TranscodePipeline.TranscodeAsync`
 line-by-line in this pass.

## Capability graph edges

```
TOOL|USES|CAP-560
TOOL|USES|CAP-561
TOOL|USES|CAP-562
TOOL|USES|CAP-563
TOOL|USES|CAP-564
TOOL|USES|CAP-565
TOOL|USES|CAP-566
TOOL|USES|CAP-567
TOOL|USES|CAP-568
TOOL|USES|CAP-569
TOOL|USES|CAP-570
TOOL|USES|CAP-571
TOOL|USES|CAP-572
TOOL|USES|CAP-573
TOOL|USES|CAP-574
TOOL|USES|CAP-575
TOOL|USES|CAP-576
TOOL|USES|CAP-577
TOOL|USES|CAP-281
TOOL|USES|CAP-069
TOOL|USES|CAP-106
PARAM:url|ENABLES|CAP-560
PARAM:playbook_json|ENABLES|CAP-561
PARAM:verify|ENABLES|CAP-562
PARAM:verify|DISABLES|CAP-562
PARAM:verify_url|ENABLES|CAP-564
PARAM:lesson_note|ENABLES|CAP-565
PARAM:failure_reason|ENABLES|CAP-566
PARAM:host_id|ENABLES|CAP-566
CAP-562|ROUTES_TO|TranscodePipeline
CAP-562|CONSUMES|CAP-568
CAP-562|PRODUCES|PlaybookVerifyMetrics
CAP-569|CONSUMES|markdown
CAP-571|CONSUMES|ReceiptSigner
CAP-571|PRODUCES|signed-playbook-provenance
CAP-571|FALLS_BACK_TO|none (OCCAM_RECEIPTS not checked)
CAP-572|PRODUCES|contentHash
CAP-573|PRODUCES|signedKeyId
CAP-574|CONSUMES|CAP-562
CAP-575|PRODUCES|resolver-cache-invalidation
CAP-575|ROUTES_TO|PlaybookSeedResolver
CAP-567|CONSUMES|OCCAM_PLAYBOOKS_LOCAL_ROOT
CAP-561|FALLS_BACK_TO|playbook_save_rejected
CAP-561|FALLS_BACK_TO|playbook_schema_invalid
CAP-562|FALLS_BACK_TO|playbook_verify_failed
CAP-562|FALLS_BACK_TO|playbook_verify_low_score
CAP-562|FALLS_BACK_TO|playbook_verify_high_noise
```
