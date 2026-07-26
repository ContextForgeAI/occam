# Quality and failure semantics

**Slug:** `quality-failure-semantics` · **Product system:** PS-2 Materialization · **CAPs:** 6 · **Public relevance:** HIGH

**Member CAPs:** CAP-094, CAP-097, CAP-098, CAP-105, CAP-106, CAP-108  
**Product capability:** CAP-094  
**Engineering findings:** None on family ledger (CAP-106 correction notes ThinExtractBrowserExhausted).

## What it is

Post-extract **quality classification**, **failure code normalization**, **recovery/attempt logging**, and **agent decision hints** that turn raw backend outcomes into honest `ok` / typed `failure` envelopes. Sits at the PS-1→PS-2 boundary: post-processors run after routing; thin detection also gates router success; materialization may still emit `thin_extract` / `content_selectors_miss`.

## Why it exists

Enforce the trust rule: `ok:false` means content **unknown**; thin chrome is not a short quality page; agents get actionable `failure.code` + `agentMeta.decisions` instead of empty success shells.

## User-visible entrypoints

| Surface | Role | Evidence |
|---------|------|----------|
| Every TranscodePipeline success candidate | PP pipeline | CAP-094 |
| Failure JSON | Normalized codes | CAP-105 |
| `recovery[]` | Attempt log | CAP-098 |
| `agentMeta.decisions` / heal hints | CAP-106 | tool policy |
| Worker exit mapping | CAP-108 | exit-13 → timeout etc. |
| `quality` / `confidence` on success | Related signals | EQM / AF |

## Core behavior

### Post-processor order (CAP-094)

Registered order (`OccamServiceCollectionExtensions.cs:34-36`; pipeline `:153-156`):

| Order | Processor | Effect |
|-------|-----------|--------|
| 100 | ChallengePage | short MD + keywords → `captcha_or_challenge` |
| 150 | RequiresLogin | Restricted + no session → `requires_login` |
| 200 | ThinExtract | EQM thin → `thin_extract` |

Only mutate **ok** outcomes; failures pass through.

### Thin detection (CAP-097)

`ExtractQualityEvaluator.LooksLikeThinExtract` — chrome/shell/near-empty. Distinct from `quality.verdict=short_quality` on genuine short pages (`ok:true`). Router uses same thin check for escalation success gate (`OccamRouter.cs:194-199`).

### Recovery log (CAP-098)

Cascade attempts recorded (`backend`, ok, latency, usable, failureCode, escalationReason). Survives on final outcome.

### Failure taxonomy (CAP-105)

Canonical string codes via `FailureCodeStrings.ResolveTranscodeFailure` — see `docs/failure-codes.md` for public list; audit SoT is code + `FAILURE-BEHAVIOR-MAP.md`. Representative: `invalid_arguments`, `workers_unavailable`, `timeout`, `thin_extract`, `captcha_or_challenge`, `requires_login`, `http_*`, `private_url_blocked`, `dns_error`, `tls_error`, `network_error`, `response_too_large`, `content_selectors_miss`, `robots_disallowed`, managed_* , …

### Agent decision hints (CAP-106)

Maps failures to stop/retry/heal-style decisions. **Correction:** `ThinExtractBrowserExhausted` is an additional stop when thin after browser already tried (`OccamTranscodeTool.cs:569-578`; GAP-016) — omit from older CAP-106 prose that missed it.

### Worker lifecycle failures (CAP-108)

Process crashes, unfinished top-level await (exit 13 → typed `timeout` via worker-exit-guard), spawn failures → host codes (`workers_unavailable` vs timeout honesty limits).

## Advanced behavior

| Topic | Notes |
|-------|-------|
| Challenge threshold 2000 | Shared router/PP |
| short_quality vs thin | Do not heal short_quality successes |
| Materialization late fail | `content_selectors_miss` / thin after compile |
| FailureRanking | Uses these codes for dual-fail surface (routing) |

## Automatic / silent behavior

| Behavior | Automation |
|----------|------------|
| PP always on pipeline | #4 |
| Thin HTTP → browser escalate | Router |
| Heal hint policy on typed failures | Tool/PlaybookHealPolicy — may suggest heal; not auto-heal content |

## Parameters

No dedicated quality params. Indirect:

| Name | Effect on semantics |
|------|---------------------|
| `session_profile` | Skips RequiresLogin conversion |
| `backend_policy` | Whether thin can escalate |
| `content_selectors` | May produce selectors_miss |

## Configuration

Post-processors always registered for core DI. No env to disable Challenge/Thin PPs. Atlas/opt-in tools separate (PS-7).

## Backends

Interprets outcomes from all acquisition backends; does not fetch.

## Sessions / state

Recovery is per-response. FailureAtlasStore (opt-in) is separate in-memory per session (EF-024 withdrawn — not a leak).

## Network behavior

None beyond interpreting network-typed codes from workers.

## Artifacts produced

Failure objects; recovery arrays; quality/confidence on success; decision hints. No separate quality artifact files.

## Trust / provenance properties

**Primary honesty layer for agents.** `ok:false` forbids memory-fill. Thin ≠ short. Probe may still mis-label SSRF as `network_error` (EF-042) — outside this family’s fix, but affects perceived taxonomy honesty.

## Failure / fallback behavior

Align with `FAILURE-BEHAVIOR-MAP.md`:

| Trigger | Behavior |
|---------|----------|
| Challenge short MD | `captcha_or_challenge` |
| Login wall no session | `requires_login` |
| Thin shell | `thin_extract`; stop if browser exhausted |
| Dual-fail | Ranked code via FailureRanking |
| Worker exit 13 | Mapped timeout |

## Platform differences

Worker process kill / exit codes OS-specific; mapped into shared taxonomy (CAP-108).

## Composition with other capabilities

- Consumes `acquisition-routing` outcomes; feeds PS-2 materialize only if still ok.
- Overlaps `access-consent` (challenge/login PPs).
- Informs heal policy (PS-5) — heal is not automatic content invention.
- Ranking table shared with routing dual-fail.

## Known limitations

- Heuristic thin/challenge detectors.
- CAP-106 historically incomplete on browser-exhausted thin (corrected).
- Some upstream dishonest codes (probe SSRF) flow into taxonomy unchanged.
- Quality family name must not imply “page quality score as product grade.”

## Engineering findings

| ID | Notes |
|----|-------|
| GAP-016 | ThinExtractBrowserExhausted under-documented in older CAP-106 |
| EF-042 | Probe code mask pollutes taxonomy honesty |

## Code evidence

- `PostProcessors/*PostProcessor.cs`
- `Routing/OccamRouter.cs:188-218`
- `Routing/FailureRanking.cs:10-21`
- `Tools/OccamTranscodeTool.cs:569-578` (browser-exhausted thin)
- `docs-audit/FAILURE-BEHAVIOR-MAP.md`
- `docs-audit/tools/occam_transcode.md` (PP sections; cascade prose superseded by EF-056)

## Public-doc relevance

**HIGH.** Core trust teaching: ok/false, thin vs short_quality, decision hints, recovery[].

## Handbook relevance

Failure atlas for agents; “what to do when” matrix keyed by CAP-105 codes.
