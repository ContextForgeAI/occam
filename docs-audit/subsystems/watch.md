# S3-02 — Watch / change monitoring (`occam_watch` + `Watch/*`)

**Wave:** 3
**CAP range:** CAP-830 … CAP-849 (allocated CAP-830 … CAP-848; 849 reserved)
**SoT:** current executable code only. Docs untrusted (verified against code below; no drift found).

---

## AUDIT TARGET

`occam_watch` MCP tool (opt-in, `OCCAM_WATCH_MCP=1`) end-to-end: tool surface → `WatchService` →
`WatchEvaluator` (pure verdict) → `WatchStore` (JSON-file persistence) → `WatchHistoryChain` (SI-05
signed change-history) → `occam_verify mode=history` (consumer edge). Not just the tool wrapper —
the full stateful subsystem underneath.

## FILES INSPECTED

- `src/FFOccamMcp.Core/Tools/OccamWatchTool.cs`
- `src/FFOccamMcp.Core/Watch/WatchModels.cs`
- `src/FFOccamMcp.Core/Watch/WatchService.cs`
- `src/FFOccamMcp.Core/Watch/WatchStore.cs`
- `src/FFOccamMcp.Core/Watch/WatchHistory.cs`
- `src/FFOccamMcp.Core/Tools/OccamVerifyTool.cs` (mode=`history` consumer edge, lines 42-47, 83-127)
- `src/FFOccamMcp.Core/Transport/OccamMcpServerRegistration.cs` (lines 133-139 — opt-in registration)
- `src/FFOccamMcp.Core/Tools/BlockDiff.cs` (block-diff hash codec, shared)
- `src/FFOccamMcp.Core/Compile/ContentHashToken.cs` (whole-doc hash codec, shared)
- `src/FFOccamMcp.Core/Receipts/ReceiptCanonicalizer.cs`, `ReceiptsPolicy.cs`, `ReceiptSigner.cs` (referenced, not re-audited — Wave 1 S19)
- `benchmarks/l0-gate/ReceiptUnitTests.cs` (lines 247-277 — SI-05 chain test evidence, incl. `occam_verify` history mode)
- `docs/tools/occam_watch.md`, `docs/recipes.md` (§ Watch a page for changes), `docs/receipts.md`, `docs/receipt_verification.md`, `docs/tools-reference.md`, `docs/configuration.md`, `docs/choosing-a-tool.md`, `docs/tools/index.md` — doc-vs-code diff pass

## ENTRYPOINTS

1. MCP tool `occam_watch` (present in `tools/list` only when `OCCAM_WATCH_MCP=1`; unaffected by `OCCAM_PROFILE` — reuses CAP-011 orthogonality).
2. Consumer edge: `occam_verify(mode="history")` — reachable and useful even when `OCCAM_WATCH_MCP` is **off** on the verifying host, since the caller just needs the `history[]` JSON, not a live watch call (Wave 2 FLOW: "verify history without watch MCP").

## RUNTIME PATH

```
occam_watch(url, backend_policy, focus_query?, session_profile?, playbook_policy, include_diff, reset, include_history)
  → OccamTranscodeOptionsParser.TryBuild(max_tokens:null, fit_markdown:(focus_query!=null), ...)
  → WatchService.WatchAsync
      → options with { JsonBlocks = true }   (forced — CAP-830)
      → TranscodePipeline.TranscodeAsync     (SAME entrypoint as occam_transcode — CAP-848)
      → on failure: return failure verbatim, store untouched            (CAP-842)
      → on success:
          contentHash = ContentHashToken.BareHex(markdown)
          prior = reset ? null : store.Get(url)
          verdict = WatchEvaluator.Evaluate(prior, contentHash, blocks, includeDiff)
              → always computes block delta for contentDeltaTokens, even if includeDiff=false (CAP-834)
          history = AppendHistory(priorHistory, verdict, contentHash, blocks, now)
              → appends only on first_seen | changed (CAP-836); caps at 64 entries (CAP-838)
              → WatchHistoryChain.Append signs iff ReceiptsPolicy.Enabled() (CAP-844)
          store.Upsert(WatchRecord{...})       → WatchStore.Persist() rewrites whole watch.json (CAP-846)
      → OccamWatchSuccessResponse serialized

occam_verify(receipt=historyJson, mode="history")
  → WatchHistoryChain.Verify(entries, publicKeyPem) → verdict history_verified | history_invalid
```

---

## CAPABILITIES

### CAP-830 — `occam_watch` forces block collection unconditionally
- **Impl:** `WatchService.WatchAsync` line 90: `options with { JsonBlocks = true }` — overrides whatever the built options would otherwise carry; the caller has no `json_blocks` parameter on `occam_watch` at all (it isn't in the tool's parameter list).
- **Reach:** every watch call, regardless of `include_diff`.
- **Confidence:** PROVEN

### CAP-831 — Watch parameter surface is a strict, curated subset of transcode
- **Impl:** `OccamWatchTool.Watch` — 8 params total: `url`, `backend_policy`, `focus_query`, `session_profile`, `playbook_policy`, `include_diff`, `reset`, `include_history`. No `max_tokens`, `content_selectors`, `if_none_match` (forced `null` at line 49 of the tool), `json_tables`, `json_feed`, `capture_screenshot`, `translate_to`, `semantic_chunking`, `emit_capsule`. `fit_markdown` is not exposed directly — it is implicitly turned on only when `focus_query` is non-empty (`fit_markdown: !string.IsNullOrWhiteSpace(focus_query)`).
- **Confidence:** PROVEN

### CAP-832 — WatchStore corrupt/unreadable file silently resets to empty
- **Impl:** `WatchStore.EnsureInit` — any exception (parse failure, I/O error) during initial load hits a bare `catch { _records.Clear(); }`; no error is logged to stderr, no failure surfaces to the caller. The tool call proceeds as if this were the very first watch ever recorded for every URL.
- **Implication:** a corrupted `watch.json` (e.g. crash mid-write before this audit's findings, or a manual edit) silently discards **all** watch history for **all** URLs with zero diagnostic trail.
- **Confidence:** PROVEN

### CAP-833 — Store key has no URL normalization
- **Impl:** `WatchStore.Key(url) => url.Trim()` — only whitespace-trims. No scheme/host lowercasing, no trailing-slash collapse, no query-param canonicalization/sorting.
- **Implication:** `https://x.com/a` and `https://x.com/a/` (or the same URL with query params in a different order) are tracked as two unrelated baselines — same behavior class as `TranscodeCacheKey`/digest keys elsewhere in the product, but Wave 1/2 did not audit this specific store.
- **Confidence:** PROVEN

### CAP-834 — Full block-diff is always computed server-side, `include_diff` only gates the response
- **Impl:** `WatchEvaluator.Evaluate` — on any `changed` verdict, `BlockDiff.Compute(blocks, prior.BlockHashes)` runs unconditionally to derive `contentDeltaTokens`; `WatchVerdict.Diff` is populated only when `includeDiff` is true, but the compute cost (and the transient in-memory added/removed block text) already happened.
- **Same shape as:** CAP-083 (`diff_against` forces `blocks[]` collection even when not requested) — a recurring pattern in this codebase: "off" flags hide the response, not the computation.
- **Confidence:** PROVEN

### CAP-835 — `reset=true` is destructive and non-archival
- **Impl:** `WatchService.WatchAsync` — `reset` forces `prior = null` and `priorHistory = []`; the discarded prior `WatchRecord` (including its entire signed history chain) is overwritten by `store.Upsert` with no backup, no archive file, no "previous chain" pointer. The new chain restarts at `seq 0` with a fresh, unrelated genesis.
- **Response shape:** `firstSeen:true`-equivalent semantics (`changed:false`) even though the URL was previously watched — the response gives no signal that this was a reset vs. a genuinely new URL, other than the caller's own memory of having passed `reset:true`.
- **Confidence:** PROVEN

### CAP-836 — SI-05 append-only-on-event policy
- **Impl:** `WatchService.AppendHistory` returns `(priorHistory, null)` unchanged when `!verdict.FirstSeen && !verdict.Changed` — an "unchanged" watch call is a no-op on the chain (no new entry, `LatestEntry: null` in the response) even though `store.Upsert` still runs (updates `LastSeenAt`, still rewrites the whole file — CAP-846).
- **Confidence:** PROVEN

### CAP-837 — SI-05 canonical entry encoding pins the signature into the next link
- **Impl:** `WatchHistoryCanonicalizer.CanonicalBytes(e, includeSig)` — hand-written fixed-field-order writer (AOT-safe, no reflection/property-order drift, same discipline as `ReceiptCanonicalizer`, reused CAP-254 family). The **signature itself** is signed over `includeSig:false` bytes, but `WatchHistoryChain.EntryHash` (used as the next entry's `prevEntryHash`) hashes the **`includeSig:true`** bytes — so tampering with a stored signature (not just the body) breaks the next link, not only the signature check on that entry.
- **Confidence:** PROVEN (test evidence: `ReceiptUnitTests.cs:258` "history tampered sig fails")

### CAP-838 — History is a bounded, silently-truncated window (64 entries)
- **Impl:** `WatchService.MaxHistoryEntries = 64`; `AppendHistory` keeps `appended[^MaxHistoryEntries..]` when the array grows past the cap. Pruned entries are **not** archived anywhere (no export, no secondary log) — they are gone from `watch.json` forever once evicted.
- **Verification semantics:** `WatchHistoryChain.Verify` is window-aware by design — it only enforces the seq-0/null-prevEntryHash genesis rule when the **first retained** entry's `Seq == 0`; a pruned-prefix window (head `Seq > 0`) verifies its own internal consecutive links but says nothing about the discarded prefix's integrity (test: `ReceiptUnitTests.cs:262` "windowed chain (pruned prefix) verifies").
- **Confidence:** PROVEN

### CAP-839 — Two independent hash families coexist per watch cycle, uncorrelated
- **Impl:** Change **detection** uses `BlockDiff.Hash` (16 hex chars of SHA-256 over `type+text`, `Tools/BlockDiff.cs`) stored as `WatchRecord.BlockHashes`. The signed history entry's `BlockMerkleRoot` instead uses `MerkleTree.Root` (full Receipt-v1 Merkle tree over `(text, sourceSelector)` pairs, `Receipts/MerkleTree.cs`, reused CAP-252). Both are computed from the same `blocks` list in the same call but via unrelated codecs with no cross-check — a bug in one would not be caught by the other.
- **Confidence:** PROVEN

### CAP-840 — `IWatchStore.Remove` is dead from every live surface
- **Impl:** `IWatchStore.Remove(string url)` is implemented in `WatchStore.cs` (removes + persists) but **no caller exists anywhere in `src/` or `scripts/`** — not the MCP tool, not any CLI verb, not any operator script. Confirmed via repo-wide grep: the only reference to `.Remove(` matching this signature is the definition itself.
- **Implication:** there is no product-level way to stop tracking / forget a URL. `reset=true` does not delete the record — it re-baselines it (CAP-835). The only way to actually remove an entry is out-of-band manual editing of `watch.json` on disk.
- **Confidence:** PROVEN (repo-wide grep, static)

### CAP-841 — No scheduling, daemon, or TTL anywhere in Watch
- **Impl:** `occam_watch` never self-invokes; `OCCAM_WATCH_MCP=1` (CAP-013, reused) only gates **tool registration** in `OccamMcpServerRegistration.cs` — it does not start any timer, cron, or hosted service (contrast `Batch.BatchJobProcessor`, which **is** an `AddHostedService` for the batch subsystem). `WatchRecord` has no expiry field; nothing ever calls `Remove` (CAP-840) or prunes stale URLs from the store. A URL watched once and never revisited stays in `watch.json` indefinitely.
- **Confidence:** PROVEN

### CAP-842 — A failing watch call never touches persisted state
- **Impl:** `WatchService.WatchAsync` returns early with the raw `outcome.FailureCode`/`outcome.Message` on any pipeline failure (`!outcome.Ok || markdown empty`) — `store.Get`/`store.Upsert` are never reached on that path. There is also no failure event type in `WatchHistoryEntry.Event` (only `first_seen`/`changed`) — a failed extraction leaves **zero** trace in a URL's signed history.
- **Confidence:** PROVEN

### CAP-843 — Response exposes the serving backend for escalation visibility
- **Impl:** `OccamWatchSuccessResponse.Backend` ← `outcome.Backend` — lets a caller notice that watch #1 was served by `http` and watch #2 escalated to `browser` (or vice versa on a policy/site change), independent of `changed`.
- **Confidence:** PROVEN

### CAP-844 — Receipt-signing gate reconfirmed compliant (Watch is on the "good list")
- **Impl:** `WatchService.EffectiveSigner() => ReceiptsPolicy.Enabled() ? signer : null` — consistent with the 5 compliant call sites already named in **EF-005** (`OccamTranscodeTool`, `ClaimCheckService`, `DatasetExportService`, `DigestService`, `WatchService`), unlike `PlaybookSaveService` (EF-005 bug). With `OCCAM_RECEIPTS=off`, `WatchHistoryChain.Append` still produces a **chained** entry (seq + prevEntryHash) but with `keyId:""`, `alg:""`, `sig:null`; `WatchHistoryChain.Verify` explicitly tolerates `Sig is null` entries (chain-only integrity, no authenticity) per `ReceiptUnitTests.cs:264-267`.
- **Confidence:** PROVEN

### CAP-845 — Multi-process race beyond what CAP-387 documents
- **Impl:** `WatchStore` serializes access with a single in-CLR `lock (object) _sync` scoped to **one `WatchStore` instance** (one DI singleton, one process). `Persist()` does a plain `File.WriteAllText` with **no OS-level file lock, no advisory lock, no atomic rename**. CAP-387 (Wave 1, `docs-audit/subsystems/config-env.md`) already documents the single-file/whole-rewrite design as "intentional given the watch set is expected to stay small" — but that framing only covers single-process correctness. It does **not** cover **two separate host processes** (e.g. two Cursor windows/MCP hosts, or a stdio host + a `--mcp-server` WS host on the same machine) both configured with the same `OCCAM_WATCH_DB_PATH`: concurrent `Upsert` calls from different processes are a last-write-wins race with a real risk of interleaved partial writes corrupting `watch.json` (which CAP-832 shows silently resets the WHOLE store to empty on next load, not just the racing record).
- **Confidence:** PROVEN in code (lock scope + plain `WriteAllText`); not reproduced live this wave (see UNCERTAINTIES). **→ EF-019.**

### CAP-846 — Store growth is O(records) per call, no per-store cap
- **Impl:** `WatchStore.Persist()` serializes **all** `_records.Values` and rewrites the entire file on every single `Upsert` — including "unchanged" calls (CAP-836 shows the chain doesn't grow, but `LastSeenAt` still updates and still triggers a full-file rewrite). While per-URL history is capped at 64 entries (CAP-838), the **number of distinct watched URLs** the store will hold is uncapped — there is no `OCCAM_WATCH_MAX_URLS`-style setting, no LRU eviction, unlike the batch subsystem's `OCCAM_BATCH_MAX_URLS` (CAP-386, reused for contrast).
- **Confidence:** PROVEN

### CAP-847 — `content_delta_tokens` is a distinct tokenizer from the response budget system
- **Impl:** `WatchEvaluator.Evaluate` sums `FocusMatcher.Tokenize(b.Text).Count` over newly-added blocks — this is the same tokenizer `FocusMatcher` uses elsewhere for focus-query matching, not the token-budget/`max_tokens` accounting used by `TokenBudget`/`ComputeOutputBudget` (CAP reused). It is a pure "how much new text appeared" signal, not a cost estimate of the watch response payload itself.
- **Confidence:** PROVEN

### CAP-848 — Watch inherits FULL session support via the canonical pipeline (not the headers-only path)
- **Impl:** `WatchService` calls `TranscodePipeline.TranscodeAsync` directly — the **same** entrypoint `OccamTranscodeTool` uses — rather than a lighter extraction path. Wave 2's EF-017 flagged `session_profile` as often **headers-only** (storageState dropped) specifically for `probe`/`map`/`heal`/`extract_knowledge`'s browser-fallback path; `occam_watch` is not in that list because it never leaves the canonical pipeline, so it should get the same storageState + header support as `occam_transcode` itself.
- **Confidence:** STRONGLY INFERRED (call-site equivalence proven; did not re-run Wave 1's transcode-level storageState test live this wave — see UNCERTAINTIES)

---

## ADVANCED / HIDDEN

- `include_diff:false` still computes the full diff server-side (CAP-834) — only the response is thinner.
- `reset:true` destroys the prior signed history irreversibly; the response cannot be distinguished from a true first-ever watch (CAP-835).
- There is **no way** to stop watching a URL through any live tool/CLI surface (CAP-840) — `IWatchStore.Remove` is unreachable dead code.
- `OCCAM_WATCH_MCP=1` gates registration only — there is no timer/daemon; nothing ever expires a record (CAP-841).
- A failed watch call is invisible to the signed history — no failure event type exists (CAP-842).
- Corrupt `watch.json` silently wipes the entire store with no diagnostic (CAP-832) — combined with CAP-845's multi-process race, a second host process is a plausible corruption vector nobody would immediately suspect.

## CONFIG / ENV (this subsystem)

`OCCAM_WATCH_MCP` (opt-in gate, reused CAP-013), `OCCAM_WATCH_DB_PATH` (store path, reused CAP-387), `OCCAM_RECEIPTS` (signing gate, reused CAP-844/EF-005 family). No watch-specific size/TTL/eviction env var exists (CAP-846, CAP-841).

## FALLBACKS

- Missing/invalid `OCCAM_WATCH_DB_PATH` → default `~/.occam/watch/watch.json` (reused CAP-387).
- Corrupt store file → empty in-memory store, no error (CAP-832).
- `OCCAM_RECEIPTS=off` → history still chains, just unsigned (CAP-844).

## FAILURES

`invalid_arguments` (empty `url`, bad `backend_policy`, bad transcode options) plus the **full transcode failure taxonomy** passed through verbatim from the underlying `TranscodePipeline` call (`timeout`, `http_*`, `thin_extract`, `captcha_or_challenge`, `requires_login`, etc.) — Watch adds no new failure codes and, per CAP-842, never persists on any failure path.

## SECURITY / TRUST

- Watch reuses Receipt v1 signing infra correctly (CAP-844, EF-005-compliant).
- SI-05 chain provides tamper-evidence for the **retained window** only (CAP-838) — a pruned prefix's integrity is out of scope for `occam_verify mode=history`.
- Store-file integrity has no protection beyond OS filesystem permissions — no encryption, no HMAC over the whole file (only per-entry signatures inside it); a local attacker with file write access could delete/replace records outside the signed-chain fields (e.g. rewrite `LastSeenAt`) without invalidating the chain, since only entries inside `history[]` are covered by signatures — `ContentHash`/`BlockHashes`/`FirstSeenAt`/`LastSeenAt` on the outer `WatchRecord` are **not** independently signed (only the *history entries* are).

## TEST EVIDENCE

`benchmarks/l0-gate/ReceiptUnitTests.cs:247-277` — thorough SI-05 chain coverage: genesis rule, linking, tamper detection (body/sig/reorder/broken-link), windowed-chain verification, unsigned-chain behavior, and the `occam_verify mode=history` tool wiring (including the `{history:[...]}` wrapper form). **No dedicated unit/live test file exists for `WatchService`/`WatchStore`/`WatchEvaluator` themselves** (no `L*WatchUnitTests.cs` in `benchmarks/l0-gate/`) — only the downstream chain-verification logic is gate-tested; the stateful store/eviction/reset/multi-call-sequence behavior described in this report (CAP-832…CAP-847) is proven by static code reading only, not exercised by the gate.

## DOC GAPS

None found — `docs/tools/occam_watch.md`, `docs/recipes.md`, `docs/receipts.md`, `docs/receipt_verification.md`, `docs/tools-reference.md`, `docs/configuration.md`, `docs/choosing-a-tool.md`, and `docs/tools/index.md` all match the code's parameter list, defaults, and history-chain framing. Gaps that **do** exist are omissions rather than inaccuracies, and are the kind of internal-mechanics detail public docs would not normally carry: no doc mentions the 64-entry history cap (CAP-838), the dead `Remove` path (CAP-840), the unbounded store growth (CAP-846), or the multi-process race (CAP-845) — reasonable for user docs, but worth the operator/security audience knowing (see RECOMMENDED DOC CHANGES).

## INVISIBLE PRODUCT (what an MCP-only user never sees)

An agent calling `occam_watch` sees a clean "did it change" API with an optional signed audit trail. What it cannot see or control:

1. **It cannot ever stop watching a URL.** There is no unwatch/delete tool. `reset` re-baselines, it does not forget.
2. **Every call — even a no-op "unchanged" check — rewrites the entire watch database file on disk**, and that cost grows forever as more distinct URLs get watched (no eviction).
3. **Its own signed history is a sliding window, not a full audit log** — after 64 events for one URL, the oldest signed entries are gone with no export, even though the response kept advertising "historyLength".
4. **A failed check leaves no trace** — an agent cannot later prove via `occam_verify mode=history` that a watch attempt failed at a given time; only successes are chained.
5. **If two Occam host processes point at the same store path, they can silently stomp each other's data** with no error surfaced to either agent.
6. **`include_diff:false` does not save any server-side work** — it only trims what comes back over MCP; the full diff is computed either way.

## ENGINEERING FINDINGS (candidates, appended to ENGINEERING-FINDINGS.md)

- **EF-019** | OBSERVATION/BUG-CANDIDATE | CAP-845, CAP-387, CAP-832 | `WatchStore` serializes access with an in-process `lock` only (plain `File.WriteAllText`, no OS file lock, no atomic rename) — two separate host processes sharing `OCCAM_WATCH_DB_PATH` can race on `watch.json`; combined with CAP-832's silent-empty-on-parse-failure fallback, a torn write from that race would silently wipe the *entire* store (all URLs, not just the racing one) on next load, with zero diagnostic. | PROVEN in code (lock scope + write call) | Yes — repro needs two processes concurrently watching under one `OCCAM_WATCH_DB_PATH` | No | OPEN
- **EF-020** | OBSERVATION | CAP-840, CAP-841 | `IWatchStore.Remove` and any expiry/eviction mechanism are absent from every live product surface — an agent or operator has no supported way to un-watch a URL or bound the store's growth other than manually editing/deleting `watch.json` on disk. | PROVEN (repo-wide grep, static) | No | No | OPEN

## UNCERTAINTIES

- CAP-845 (multi-process race) is proven by static code reading, not reproduced live this wave (no gate test exists for it — see TEST EVIDENCE).
- CAP-848 (full session/storageState support) is proven by call-site equivalence to `occam_transcode`, not re-verified live with an actual storageState-bearing session profile through `occam_watch` specifically this wave.
- Whether an operator/runbook doc (outside `docs/`, i.e. `docs-internal/`) already tracks the 64-entry cap or store growth was not checked (out of scope — `docs-internal/` is gitignored per repo convention and not part of this audit's SoT).

## RECOMMENDED DOC CHANGES (later, not made this wave)

Per AGENTS.md, no docs were edited this wave. If/when addressed: `docs/tools/occam_watch.md` could gain a short "Limits" note (64-entry history window; no per-store URL cap; no unwatch command — delete `watch.json` manually) so operators running Watch at scale aren't surprised. This is additive, not a correction — current docs are not wrong, just silent on internal limits.

## CAPABILITY GRAPH EDGES

```
TOOL:occam_watch --USES--> CAP-013            (opt-in gate, reused, Wave1 S0)
TOOL:occam_watch --USES--> CAP-326            (stateful wrapper, reused, Wave1 S20)
TOOL:occam_watch --USES--> CAP-387            (store path env, reused, Wave1 config-env)
TOOL:occam_watch --USES--> CAP-830..CAP-848   (this wave, new)
TOOL:occam_watch --ROUTES_TO--> TranscodePipeline           (same as occam_transcode, CAP-848)
TranscodePipeline --ROUTES_TO--> OccamRouter                (reused)
CAP-830 --CONSUMES--> OccamTranscodeOptions.JsonBlocks       (reused)
CAP-834 --CONSUMES--> BlockDiff.Compute / BlockDiff.Hash     (reused CAP-325 family)
CAP-839 --CONSUMES--> MerkleTree.Root                        (reused CAP-252)
CAP-836/CAP-837/CAP-838 --PRODUCES--> WatchHistoryEntry[]    (reused CAP-284)
CAP-837 --CONSUMES--> ReceiptSigner.SignDetached             (reused CAP-257)
CAP-844 --CONSUMES--> ReceiptsPolicy.Enabled                 (reused, Wave1/2 EF-005 family)
CAP-840 --PRODUCES--> DEAD:IWatchStore.Remove                (new, unreachable)
PARAM:reset --ENABLES--> CAP-835
PARAM:include_diff --ENABLES--> response-only exposure of CAP-834's precomputed diff
PARAM:include_history --ENABLES--> full history[] disclosure (bounded by CAP-838)
PARAM:focus_query --ENABLES--> implicit fit_markdown (CAP-831)
TOOL:occam_verify --USES--> CAP-273           (mode=history consumer, reused, Wave1 S19)
CAP-273 --CONSUMES--> WatchHistoryChain.Verify               (reused CAP-284)
TOOL:occam_watch --PRODUCES--> ART-025        (Watch history chain, reused, Wave2 ARTIFACT-MAP)
TOOL:occam_watch --PRODUCES--> ART-007        (Receipt v1 positive, reused — history entries are Receipt-v1-family signatures)
ART-025 --CONSUMED_BY--> TOOL:occam_verify    (mode=history)
```

## COMPLETENESS VERDICT

**COMPLETE.** All four files under `Watch/` plus the tool wrapper and the `occam_verify mode=history`
consumer edge were read in full. Persistence, diff, scheduling (absent — documented as absent, CAP-841),
cleanup (absent — documented as absent, CAP-840/846), receipts (signing gate reconfirmed, CAP-844), and
the history chain (structure, capping, verification, test coverage) are all covered. Two new
engineering-findings candidates (EF-019, EF-020) proven in code and appended to the ledger. Remaining
uncertainty is limited to two items explicitly called out in UNCERTAINTIES (live repro of the
multi-process race; live storageState re-verification through `occam_watch` specifically) — neither
blocks this report's conclusions, both are static-proven.
