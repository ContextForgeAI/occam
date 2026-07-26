# RC.2 independent evidence review

**Review target:** frozen tag `v1.0.0-rc.1`, commit `a535705c03a9bf483cbbb23aefe8c4e60cf7b48f`
**Review date:** 2026-07-22
**Scope:** architecture and solution design only. No RC.1 code, binaries, tests, or evidence were changed.

## Executive finding

The evidence supports three critical architectural risks:

1. **Classification ownership is split.** Probe and transcode independently infer login from different
   representations and different rules. Both allow authentication prose to become a hard access verdict.
2. **Focus is ranked over untyped Markdown sections.** Numeric identifiers, anchors, TOC membership,
   heading/body coverage, and answer-bearing structure are not modeled as first-class signals.
3. **The response budget charges for internal sidecars that are later hidden.** This silently halves the
   Markdown allowance in representative RC.1 calls and magnifies both body loss and TOC retention.

D12 is separate and localized: `urls` is exposed as `string?`, so a native JSON array fails in MCP
binding before Occam can return a typed error.

## Method and evidence integrity

Important claims were checked against both raw evidence and source at the frozen RC.1 tag. The existing
`graphify-out/graph.json` was used only to locate code; every cited graph lead was verified in source.

- All 21 archives listed in `validation/evidence/rc1/_archives/SHA256SUMS.txt` match their SHA-256 values.
- The mission names `validation/evidence/rc1/SHA256SUMS.txt`; that file does not exist. The actual manifest
  is under `_archives/`. This is a pack-path disagreement, not an integrity failure.
- `validation/` is untracked in the working tree. It was treated as frozen input.
- `d10-rfc-20260722-025959` is classified here as D15. Its raw outputs show wrong-section selection, not
  D10 body truncation.

Classification used below:

- **FACT** — directly shown by source and raw evidence.
- **HYPOTHESIS** — plausible causal explanation with incomplete isolation.
- **UNKNOWN** — missing evidence or an owner/product decision.

## 1. D12 — digest argument contract

### Verdict

| Classification | Finding |
|---|---|
| FACT | A native `urls` array produces the non-JSON text `An error occurred invoking 'occam_digest'.` (`D12/digest2-*/arm-array.json`). |
| FACT | Stringified JSON and newline-delimited strings succeed in the same run (`summary.jsonl`, lines 2–3). |
| FACT | The MCP method declares `string? urls` (`OccamDigestTool.cs:17-19`). |
| FACT | `DigestUrlParser` accepts an array only after it has arrived inside a string (`DigestUrlParser.cs:19-65`). |
| FACT | The contract test explicitly requires `urls` to be `string` (`PublicMcpContractUnitTests.cs:64-80`). |
| HYPOTHESIS | The generic text is emitted by the MCP framework after parameter binding fails; the handler is never entered. The source shape and sub-400 ms response strongly support this, but the framework exception is not captured. |
| UNKNOWN | Whether the current MCP SDK can publish an honest `oneOf: [array,string]` schema for a custom union without manual tool registration. This needs a contract spike before implementation. |

### Evidence/source agreement

The pack is correct. This is not a serialization failure inside `DigestService`, and changing
`DigestUrlParser` alone cannot fix it. The repair boundary is MCP schema/binding plus a normalization
layer that runs before the existing URL parser/service.

## 2. D9 and D19 — authentication/login classification

### Ownership trace

```text
occam_probe
  HttpProbeFetcher -> HtmlProbeClassifier(raw sampled HTML)

occam_transcode
  backend extract -> Markdown -> RequiresLoginPostProcessor
                  -> LoginWallDetector(extracted Markdown)
```

These paths share `DomainTierRegistry` and `TextNeedle`, but not a classifier or evidence model.

### D9 verdict

| Classification | Finding |
|---|---|
| FACT | httpwg RFC 9110/7235 and datatracker RFC 9110/7235 probe as login and transcode as `requires_login`; rfc-editor and MDN controls succeed (`D9/*/summary.jsonl`). |
| FACT | Raw HTML contains `authentication required` primarily in 407 status-code prose (`D9/d9-html-*/phrase-contexts.txt`). |
| FACT | Probe treats bare `authentication required` as sufficient (`HtmlProbeClassifier.cs:49-54`). |
| FACT | Transcode treats the same bare substring as sufficient (`LoginWallDetector.cs:17-25`). |
| FACT | `ContainsAnyPhrase` is raw substring matching (`TextNeedle.cs:34-44`), so `log in` also appears inside `server log information`. |
| FACT | The public-reference exemption covers rfc-editor `/rfc/`, Wikipedia `/wiki/`, and tier-A `/docs/`; it does not cover httpwg or datatracker (`DomainTierRegistry.cs:85-123`). |
| HYPOTHESIS | Host exemptions explain the clean rfc-editor control, while prose-triggered rules explain the failing httpwg/datatracker cases. This is high-confidence because the phrase-count sweep correlates zero `authentication required` with success. |
| UNKNOWN | Wild-web false-negative rate after narrowing phrase rules; the pack has real-login fixtures but no broad login-wall corpus. |

### D19 verdict

| Classification | Finding |
|---|---|
| FACT | OpenID Core and oauth.net probe `likelyLoginRequired=false` but transcode `requires_login` on both Mac and Linux (`D19/*/rollup.json`, `summary.txt`). |
| FACT | Probe's dedicated-wall branch requires `type=password` (`HtmlProbeClassifier.cs:125-135`). |
| FACT | Post-extract classification requires only whole-word `password` plus a substring such as `log in` or `sign in` (`LoginWallDetector.cs:28-31`). |
| FACT | `RequiresLoginPostProcessor` owns the hard transcode failure after extraction (`RequiresLoginPostProcessor.cs:10-38`). |
| FACT | The failure hint then directs the agent to configure a session and stop (`TranscodeAgentDecisions.cs:27-38`). |
| HYPOTHESIS | Instructional identity prose satisfies the post-extract combination rule while the absence of a password input keeps probe open. Raw previews and the rule asymmetry make this high-confidence. |
| UNKNOWN | Whether a real wall can be classified safely from extracted Markdown when worker DOM evidence is unavailable. Preferred design treats this as `unknown`, not `requires_login`. |

### Answers to the mission questions

- **Why does probe disagree with transcode?** Independent classifiers, different inputs, and different
  thresholds. Probe asks for password-field UI in its combination branch; transcode does not.
- **Who owns `requires_login`?** Three places can create it: the transcode prefetch login-path check
  (`TranscodePipeline.cs:127-137`), the post-extract `RequiresLoginPostProcessor`, and status/failure
  normalization. Content-based ownership is the post-processor; probe owns only a recommendation signal.
- **Can authentication words alone justify login?** No. They are topical evidence and must have zero
  authority to produce a hard login verdict without access-control/UI/status evidence.
- **How should diagnostics improve?** Return evidence codes, stage, confidence, and disposition; never
  expose matched secret/header values. A login decision should explain whether it came from status,
  redirect, password control, blocking form/overlay, or path—not a generic phrase.

## 3. D15 and D17 — wrong-section focus

### D15 verdict

| Classification | Finding |
|---|---|
| FACT | The `d10-rfc-*` arms omit the 401 definition at 700, 1,500, 3,000, and 8,000 tokens. This is D15 (`D15/d10-rfc-*/summary.jsonl`). |
| FACT | The eight-case corpus achieves only 5/8 short-query hits and 4/8 polluted-query hits (`D15/d15-corpus-*/rollup.json`). |
| FACT | `FocusMatcher.Matches` accepts any query term of length at least four (`FocusMatcher.cs:19-34`). The numeric token `401` is therefore ignored. |
| FACT | `TokenBudget.ScoreSection` starts with a flat match score, adds heading occurrences and a leaf-heading bonus, then a generic definitional boost (`TokenBudget.cs:108-142`). |
| FACT | There is no document-level IDF, exact-anchor score, all-term coverage requirement, phrase proximity, or TOC penalty in that section scorer. |
| FACT | Markdown sections split only at `##` and `###` (`TokenBudget.cs:451-473`), so a large section can remain a coarse ranking unit. |
| HYPOTHESIS | OR-any matching plus generic definition boosts select sections that mention `Unauthorized`/credentials or generic query words but do not define 401. The exact winning feature contribution is not logged in RC.1. |
| UNKNOWN | The optimal weights and confidence threshold across all target documentation families. |

The later `d15fix` summary initially appears to show a short-query success, but the notebook corrects
that heuristic: it landed on the Expect section. The stronger corpus uses an answer-body needle and is
the reliable source.

### D17 verdict

| Classification | Finding |
|---|---|
| FACT | No transcode path maps `Uri.Fragment` into `FocusQuery`, content selectors, or section rank. Repository search finds fragment removal for unrelated object IDs, not focus planning. |
| HYPOTHESIS | HTTP fetch semantics discard fragments, so `#section-15.5.2` cannot change worker output unless Core separately preserves and interprets it. |
| UNKNOWN | D17 has no dedicated frozen archive; the observational D15 hunt is insufficient to set final fragment precedence or normalization rules. |

## 4. D11, C10b, and D10 — TOC, budget, and body retention

### D11 / C10b verdict

| Classification | Finding |
|---|---|
| FACT | Six nginx focus variants at 4k omit the directive definition (`D11/focus-recover-*/summary.jsonl`). |
| FACT | `client_max_body_size` at 12k retains the definition and default; the no-focus 4k arm is TOC-only (`C10b/*/summary.jsonl`). |
| FACT | TOC links are retained in `FitMarkdown`'s lenient index mode and a matching list item can establish section focus (`FitMarkdown.cs:319-450`). |
| FACT | The focus-window scorer has no TOC/index feature or penalty. |
| HYPOTHESIS | A TOC occurrence ranks as ordinary focus evidence and consumes a constrained surface budget before the definition. This is high-confidence, but the 4k→12k threshold is not finely sampled. |
| UNKNOWN | Minimum stable budget threshold by page family; 12k is an observed workaround, not a product default. |

### D10 verdict

| Classification | Finding |
|---|---|
| FACT | At 700, the correct `Simple requests` heading and intro remain but GET/HEAD and allowed headers are gone; at 2k they are present (`D10/*/focus700.md`, `focus2000.md`). |
| FACT | The outer compile strategy is `focus_window`, while the in-band cut is `head_safe`. `TruncateSectionFocusAware` falls back to `TruncateHeadSafe` when no scored body block fits (`TokenBudget.cs:324-420`). |
| FACT | The 700-token whole-response budget gives Markdown only 275 tokens. The returned compile allocation charges 40 blocks, 34 tables, 183 media, and 96 receipt tokens. |
| FACT | `json_blocks` and `json_tables` were not returned. Pipeline budgeting receives raw internal blocks/tables before `ProjectBlocks`/`ProjectTables` hides them (`TranscodePipeline.cs:249-257,352-395`). |
| FACT | The same pattern appears on nginx: the 4k response allocates 1,958 Markdown tokens and 1,978 tokens to non-returned blocks/tables. |
| HYPOTHESIS | Phantom sidecar reservation is the main reason D10 reaches the internal head-safe fallback and a major reason C10b needs 12k. Body-block scoring/list grouping still contributes. |
| UNKNOWN | After correcting public budget inventory, which remaining body-retention cases require planner changes rather than simply receiving their intended Markdown budget. |

### Architectural disagreement with current comments

`BudgetOwnership` correctly separates whole-response and semantic layers in principle. The live call
violates the public-budget meaning by estimating unrequested internal sidecars. Internal IR may guide
planning and receipts, but it must not be charged as serialized payload unless it is actually returned.

## 5. Agent semantic honesty

| Classification | Finding |
|---|---|
| FACT | D10 returns confidence 0.8244 despite losing the requested list body. Transcode has no explicit focus hit/weak/miss field. |
| FACT | Digest computes `focusMatched` only on the final excerpt and warns when all items miss (`DigestService.cs:336-363`, `DigestAgentHints.cs:27-60`). This is better than transcode but still lexical. |
| FACT | `recovery[].ok` copies raw backend `ExtractRunResult.Ok`, while the router separately applies `IsSuccessfulExtract` (`OccamRouter.cs:112-129`). D16 therefore reports HTTP `ok:true` even though it escalated because the extract was unusable. |
| FACT | Hermes interpreted both recovery attempts as successful (`D16/hermes-multi-t2.txt`). |
| FACT | `claim_check.found` means relevant candidate retrieval, not support; the contradicted D13 claim still returns `found:true`. Source documentation says the caller judges support/refute. |
| HYPOTHESIS | Generic booleans (`ok`, `found`, `focusMatched`) are being read as semantic verdicts because response field names carry stronger meaning than prose documentation. |
| UNKNOWN | Which compatibility aliases can be deprecated in RC.2 versus retained through GA. |

RC.2 should separate transport/extraction success, usability, relevance, semantic verdict, and
completeness. Confidence must be dimensioned; extraction quality cannot stand in for focus correctness.

## 6. Hermes and process lifecycle

| Classification | Finding |
|---|---|
| FACT | D3 shows two independent Core hosts: published under the Hermes gateway and legacy under the dashboard's Node launcher (`D3/*/snapshot.txt`, `trees.txt`). |
| FACT | The stale launcher was still owned by a live dashboard process. This is not a leaked Node/Chromium worker. |
| FACT | Worker children are tracked with process groups/job objects and best-effort shutdown hooks (`WorkerProcessGroup.cs:7-10,140-205`). |
| FACT | `launch-mcp-host.mjs` forwards child exit to its parent but does not install explicit SIGTERM/SIGINT forwarding to the Core child (`launch-mcp-host.mjs:26-38`). |
| FACT | `TrySetDeathSignal(process.Id)` calls `prctl` in the Core process (`WorkerProcessGroup.cs:125-136,381-413`). `PR_SET_PDEATHSIG` affects the calling process, not the supplied child PID; the method name/comment overstate protection. |
| FACT | D18 is a verbose Hermes/model stream hang with a clean quiet-mode counterexample. No Occam tool call caused it. |
| HYPOTHESIS | Dashboard/gateway configuration ownership and missing launcher signal forwarding make stale host trees easier to preserve across reconfiguration. |
| UNKNOWN | Whether Hermes exposes a reliable lifecycle callback or instance lease that Occam can integrate with. Automatic global deduplication would be unsafe because multiple valid host profiles can coexist. |

## Evidence disagreements and limits

1. Checksum path in the mission is wrong; `_archives/SHA256SUMS.txt` is correct.
2. `d10-rfc-*` is D15, as instructed and as raw content confirms.
3. D10's pack describes an unresolved fit-vs-budget stage. Compile metadata and source narrow it:
   outer `focus_window`, internal `head_safe`, strongly amplified by public-budget reservation for hidden
   sidecars. A stage-instrumented regression remains appropriate.
4. D17 remains evidence-limited.
5. D18 is not an Occam RC.2 production-code defect.
6. Live URLs are temporal evidence. Frozen HTML fixtures are required for deterministic acceptance.

## Review conclusion

The P0 evidence is sufficient to design RC.2 without changing RC.1. Architecture should converge on:

- one evidence-based access classifier shared by probe and transcode;
- one structure-aware focus planner that owns ranking and body retention;
- a public response budget based only on serialized fields;
- an MCP boundary that accepts native digest arrays and always returns typed errors;
- explicit semantic statuses instead of overloaded booleans;
- host-instance diagnostics and signal-safe launchers, without global process deduplication.
