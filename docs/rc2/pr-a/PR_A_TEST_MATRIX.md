# PR-A test matrix

## Classification

- **Green characterization:** records stable RC.1 behavior and must exit 0.
- **Red regression:** encodes desired RC.2 behavior and must fail on RC.1. These tests run only with `--regression`.
- **Quarantined environment:** requires an external platform, host integration, or artifact not controlled by the offline suite.

The harness is `benchmarks/rc2-regression`. Synthetic fixtures are minimized from the frozen evidence and contain explicit answer needles so generic success cannot satisfy a focus assertion.

| Defect | Source evidence | Current RC.1 expectation | Desired RC.2 expectation | Layer | Fixture | Current result | Red? | Future PR |
|---|---|---|---|---|---|---|---|---|
| D12 native array | `D12/digest2-*/arm-array.json` | Schema is `string|null`; binding fails before handler with opaque invocation text | Array binds or returns typed `invalid_arguments` | Published MCP stdio | Inline JSON-RPC | Green current / red desired | Yes | PR-B |
| D12 empty/mixed array | `D12/digest2-*` plus boundary design | Binder rejects before parser | Typed validation with host still healthy | Published MCP stdio | Inline JSON-RPC | Green current / red desired | Yes | PR-B |
| D12 string forms | `D12/digest2-*/arm-string.json`, `arm-newlines.json` | JSON-string, delimiter, single, and multiple strings reach normalization; malformed forms return typed `invalid_urls` | Preserve compatibility through the new boundary | Parser plus MCP handler | Inline strings | Green | No | PR-B |
| D9 public prose | `D9/d9-bound-*`, `d9-html-*`, `fp-login-*` | `authentication required` produces probe and transcode login verdicts | Text-only topical evidence cannot produce hard login | Pure classifier | `access-public-auth.*` | Green current / red desired | Yes | PR-C |
| D9 real wall | D9 controls and design corpus | Password UI/login path is detected | Retain a direct-evidence true positive | Pure classifier | `access-real-login.*` | Green | No | PR-C |
| D9 neutral page | D9 controls | No login verdict | Remain open | Pure classifier | `access-neutral.html` | Green | No | PR-C |
| D19 disagreement | `D19/d19-mac-*`, `d9-openid-*` | Probe is open while Markdown detector returns login | Shared disposition from shared evidence | Pure probe/transcode classifiers | `access-openid.md` | Green current / red desired | Yes | PR-C |
| D15 numeric identifier | `D15/d10-rfc-*`, corpus rollup | Numeric-only `401` has no usable focus term | Exact numeric identifier selects the target section | Focus planner | `focus-sections.md` | Green current / red desired | Yes | PR-D |
| D15 wrong section | `D15/d15-corpus-*` | Repeated lexical/definitional terms select a non-answer section | Coverage/proximity/anchor ranking selects answer needle | Focus planner | `focus-sections.md` | Green current / red desired | Yes | PR-D |
| D15 duplicate labels | D15 deterministic tie requirement | Stable document order resolves equal scores | Stable order with unique anchors and reason trace | Focus planner | `focus-duplicates.md` | Green characterization | No | PR-D |
| D17 exact fragment | `D17/INDEX.md` evidence gap plus source trace | URL fragment is not an input to `TokenBudget` | Exact valid fragment resolves its anchor | Test adapter over current planner | `focus-sections.md` | Green current / red desired | Yes | PR-D |
| D17 missing fragment | RC.2 validation design | No current explicit state | Explicit fallback/miss according to owner decision | Design-only | Planned fixture variant | Blocked pending policy | Quarantined | PR-D |
| D11 TOC selection | `D11/focus-recover-*` | Leaf TOC/index match displaces body definition | Body answer outranks TOC | Focus planner | `focus-toc.md` | Green current / red desired | Yes | PR-D/PR-E |
| C10b focus plus budget | `C10b/nginx-nofocus-*` | Constrained output truncates without answer | Answer retained or explicit incomplete state | Focus/materialization | `focus-toc.md` | Green current / red desired | Yes | PR-E |
| D10 hidden sidecars | `D10/d10-*` compile allocation | Raw unrequested blocks/tables reduce 700-token surface from 652 to 326 | Hidden fields consume zero serialized budget | Response budget | Synthetic sidecar inventory | Green current / red desired | Yes | PR-E |
| D10 answer body | `D10/focus700.md`, `focus2000.md` | 128-token reduction loses answer list; 512 retains it | Preserve minimum answer unit when it fits | Token materialization | `budget-answer.md` | Green current / red desired | Yes | PR-E |
| Semantic transport/usability | D16 recovery evidence | `TranscodeAttempt` has only `Backend`, `Ok`, `LatencyMs` | Separate `transportOk` and `usable` | Contract reflection | In-memory attempt | Green current / red desired | Yes | PR-F |
| Semantic focus/completeness | D10/D15 responses | Success and confidence coexist with wrong/incomplete focus | Independent focus and completeness dimensions | Characterization truth table | Cross-case | Documented | Quarantined | PR-F |
| Semantic claim retrieval/verdict | D13 contradicted claim | `found` is relevance, not support | Retrieval and verdict are distinct | Contract characterization | Frozen D13 evidence | Documented | Quarantined | PR-F |
| D3 dual host trees | `D3/d3-tele-*` | Multiple valid owners can coexist; no Core identity descriptor | Exact-owner diagnostics and targeted shutdown | Test-only lifecycle model | In-memory identities | Green model / production gap | Quarantined | PR-G |

## Required observations

Focus tests print the selected heading, observable anchor, TOC/body source, strategy, answer-needle presence, and explicitly state when ranking score/trace is unavailable. Budget tests print requested budget, estimated serialized tokens, markdown allocation, structured inventory, truncation strategy, and answer presence. Access tests print probe/transcode decisions and non-sensitive evidence categories.
