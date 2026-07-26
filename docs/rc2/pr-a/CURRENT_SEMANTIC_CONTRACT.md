# Current semantic contract

## Finding

RC.1 response fields mix transport, access, usability, focus, completeness, and verdict. The current values are not redefined by PR-A; this document records what they can and cannot prove.

| Current field | What it currently establishes | What it does not establish |
|---|---|---|
| Top-level `ok` | The selected tool path returned its current success shape | Correct focus, complete answer, claim support, or open access in every intermediate stage |
| `recovery[].ok` | The backend worker returned raw `ExtractRunResult.Ok` | That the extract passed thin/challenge usability gates |
| Probe `likelyLoginRequired` | A probe heuristic matched path, phrase, or password-wall signals | A shared access decision with transcode |
| Transcode `failureCode=requires_login` | A prefetch path or post-extract detector made a hard login decision | Direct UI/status evidence; agreement with probe |
| Digest `focusMatched` | The final excerpt meets the current lexical/scored focus matcher | Exact section identity or answer completeness |
| Transcode `confidence` | Current extraction-quality confidence | Focus correctness or body completeness |
| Claim `found` | Relevant candidate material was retrieved | That the claim is supported or true |
| `truncated` / `omitted` | Some budget/materialization loss is reported | A dimensioned answer-completeness verdict |

## Truth table

`T` means the dimension is positively established, `F` means negatively established, and `?` means the current public shape cannot decide it independently.

| Scenario | `ok` | `found` | confidence | status/error | `requires_login` | Transport | Access | Usability | Focus | Completeness | Verdict |
|---|---:|---:|---|---|---:|---|---|---|---|---|---|
| Healthy extraction, relevant complete answer | T | n/a | extraction score | success | F | T | open-ish | T | ? | ? | n/a |
| Transport succeeds, HTTP extract is thin, browser recovers | T final; T in both recovery entries | n/a | final score | success | F | T/T | ? | F/T | ? | ? | n/a |
| Public auth prose false positive | F in transcode | n/a | n/a | `requires_login` | T | T | incorrectly closed | F by policy | ? | ? | n/a |
| Probe open, transcode login | F in transcode | n/a | n/a | disagreement | T only in transcode | T | contradictory | ? | ? | ? | n/a |
| Wrong focused section | T | n/a | may be high | success | F | T | ? | T | F | F or ? | n/a |
| Correct section with answer body truncated | T | n/a | may be high | success + truncation | F | T | ? | T | partial | F | n/a |
| Relevant but contradicted claim | tool-specific | T | retrieval score | candidate found | n/a | T | ? | T | relevant | depends on leaf set | contradicted, not implied by `found` |
| Successful extraction with no relevant answer | T | n/a | extraction score | success | F | T | ? | T | F | ? | n/a |

## Characterization rules for PR-A

1. Never infer semantic success from `ok` alone.
2. Treat `recovery[].ok` as raw attempt completion until PR-F adds usability.
3. Treat phrase-only login decisions as current classifier output, not ground truth.
4. Treat `focusMatched` as lexical evidence, not an exact-section guarantee.
5. Treat `found` as retrieval, never as support.
6. Treat constrained focus without an answer needle as incomplete even if the current response lacks that dimension.

The proposed dimensioned contract remains future PR-F work. PR-A adds no public fields or aliases.
