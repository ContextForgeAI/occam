# REVIEW_GUIDE.md — start here for a thorough code review

This file orients an external reviewer (human or model) landing cold on **FF-Occam MCP**.
It is a map + invariant list, not a contract — the canonical contracts are linked inline.
Verified against the working tree at the time of writing; if a fact here disagrees with the
code, **the code wins** (and that disagreement is itself a finding worth reporting).

---

## 1. What this project is (60 seconds)

A **Native AOT .NET 10 MCP host** that turns URLs into LLM-ready Markdown. The host owns
routing, token budgeting, post-processing, playbooks, and session state; the actual HTML
extraction is done by **Node.js workers** it spawns.

```
MCP client ──JSON-RPC (stdio / WebSocket / Remote)──► .NET host (src/FFOccamMcp.Core)
                                                          │
                              OccamRouter / TranscodePipeline / post-processors
                                                          │
                        ┌─────────────────────────────────┴───────────────┐
                        ▼                                                   ▼
             workers/http-extract  (domino + Readability + turndown)   workers/browser-extract (Playwright)
```

- **C# namespace is `OccamMcp.Core.*`** — the directory is `src/FFOccamMcp.Core/` but the old
  `FFOccamMcp` namespace must not appear in new code.
- **No cache by design** — every call is a live extraction. There is no on-disk result store to review.

### The tool surface (verified in `Transport/OccamMcpServerRegistration.cs`)

**Always-on core MCP tools:** `occam_client_capabilities`, `occam_transcode`, `occam_probe`, `occam_digest`,
`occam_playbook_resolve`, `occam_map`, `occam_playbook_heal`, `occam_playbook_save`,
`occam_extract_knowledge`, `occam_search`, `occam_verify`, `occam_claim_check`, `occam_attest`,
`occam_playbook_lint`, `occam_dataset_export`.

**Opt-in (off by default, gated by env):**
`occam_batch_submit/status/results` (`OCCAM_BATCH_MCP=1`), `occam_watch` (`OCCAM_WATCH_MCP=1`),
`occam_crosscheck` (`OCCAM_CONSENSUS_MCP=1`), `occam_failure_atlas` (`OCCAM_ATLAS_MCP=1`).

> Source of truth for the catalog is the code (`OccamMcpServerRegistration.OccamToolNames`) +
> `MCP_API_SPEC.md`. Don't spend review budget re-deriving the count.

---

## 2. Where the substance lives (read these first)

| Concern | Files |
|---------|-------|
| Entry / CLI / transport | `src/FFOccamMcp.Core/Program.cs`, `Transport/*McpTransport.cs`, `Transport/OccamMcpServerRegistration.cs` |
| Tool handlers (validate → service → serialize) | `src/FFOccamMcp.Core/Tools/Occam*Tool.cs` |
| Routing & escalation | `Routing/TranscodePipeline.cs`, `Routing/OccamRouter.cs`, `Backends/HttpExtractBackend.cs`, `Backends/BrowserExtractBackend.cs` |
| Trust / quality guards | `PostProcessors/*PostProcessor.cs`, `PostProcessors/ExtractQualityEvaluator.cs` |
| Worker process management | `Workers/HttpExtractRunner.cs`, `Workers/BrowserExtractRunner.cs`, `Workers/BrowserDaemonHost.cs`, `Workers/NodeWorkerProcessSpawner.cs` |
| Token budgeting | `Compile/FitMarkdown.cs`, `Compile/FocusMatcher.cs`, `Compile/TokenBudget.cs` |
| Node extraction (the actual HTML→MD) | `workers/http-extract/lib/http-extract-run.mjs`, `workers/browser-extract/lib/extract-html.mjs`, `workers/browser-extract/lib/browser-session.mjs`, `workers/shared/lib/*.mjs` |
| DI wiring | `Composition/OccamServiceCollectionExtensions.cs` (`AddOccamCore()`) |

Deeper architecture notes: [docs/architecture/semantic-contract.md](docs/architecture/semantic-contract.md).
Contributor rules: `AGENTS.md`, `CLAUDE.md`.

---

## 3. Invariants — review against these, not taste

These are load-bearing. A change that violates one is a bug even if it "works":

1. **`ok:false` = unknown content.** The host must never present model-memory or a partial as if
   it were the page. Typed failures only (`failure.code`). This is the core trust model
   (`docs/failure-codes.md`). The `PostProcessors/` + `ExtractQualityEvaluator` enforce it — a
   thin/challenge/login shell returning `ok:true` is a trust breach.
2. **stdout is pure JSON-RPC.** Banners, logs, diagnostics, CLI help all go to **stderr**. Any
   `Console.Write` to stdout that isn't protocol framing corrupts the transport.
3. **AOT-safe.** All JSON goes through `System.Text.Json` source-generated `JsonSerializerContext`.
   No reflection-based serialization, no runtime codegen — it must trim/AOT-compile clean.
4. **Honest metrics.** No global "works on N% of sites" headline; report per-tier, declare the
   baseline for any reduction %. (`AGENTS.md` §3.5 claims discipline.)
5. **No per-host branches in workers.** New site handling goes through `profiles/playbooks/seeds/`,
   not `if (host === …)` in worker code.
6. **Minimal diff / L0 scope.** No drive-by refactors; the L0 core is deliberately lean.

---

## 4. How to build and verify

```powershell
# Toolchain: npm install + Playwright chromium + dotnet publish
.\scripts\occam-doctor.ps1

# Fast smoke (~30s, HTTP-only 3-URL subset)  → prints  L0_GATE_FAST_OK
.\scripts\run-l0-fast.ps1

# Full integration gate (L0–L9, unit + live) → prints  L0_GATE_OK
dotnet run --project benchmarks\l0-gate

# AOT publish for the current RID (proves trim/AOT health)
dotnet publish src\FFOccamMcp.Core -c Release -r win-x64
```

The gate is a **console app, not a test framework** (`benchmarks/l0-gate/Program.cs`). Each level
is a static `Run(...)`; success is the printed marker `L0_GATE_OK` (and per-level markers like
`L1B_PROBE_OK`). **Trust the marker on the artifact, not any prose summary of the run.**

| Level | Covers |
|-------|--------|
| L0 | Live smoke corpus (`--fast` = 3 URLs) |
| L1a/L1b/L1 | Token economy · probe classifier · full failure taxonomy |
| L2 | Digest, map, sessions, transport, egress, media refs |
| L3 / L4 | Playbook heal→save→verify · genome resolution/merge |
| L5 / L6 / L7 | Batch server · browser pool lifecycle · resource safety |
| L8 | Agent-First (confidence, receipt, auto-recovery, differential) |
| L9 | **Golden set** — frozen-HTML deterministic extraction-fidelity regression |

**L9 is the highest-signal net for a fidelity reviewer:** it serves frozen HTML from a local
fixture server (`benchmarks/l0-gate/fixtures/golden/*.html`, `corpora/l9-golden.jsonl`,
`L9GoldenRunner.cs`) so assertions catch *code* regressions, immune to live-site drift. It pins
`ok`/`failure_code`, char bands, must-contain/must-not-contain, and structured output
(`json_tables`/`json_blocks`/`json_feed`) across both the http and browser backends.

### Known-flaky, not a finding

`L3 heal-pilot-mdn-guide K1 heal capture` occasionally reds on a live MDN SPA and recovers on
re-run (its own K2/K3 pass). All other L3 pilots are stable. A single red there is the live flake,
not a regression.

---

## 5. Doc↔code contract (single source of truth)

Per `CLAUDE.md`/`AGENTS.md`, each topic has exactly one canonical home — check code against these,
not against scattered prose:

| Contract | Canonical |
|----------|-----------|
| Tool params + JSON shapes | `MCP_API_SPEC.md` |
| Tool runtime reference | `docs/tools-reference.md` + `docs/tools/` |
| Environment variables | `docs/configuration.md` |
| Failure codes | `docs/failure-codes.md` |
| Backend policy / timeouts | `docs/concepts.md` |
| Transport | `docs/transports.md` |

A parameter that exists in code but not in `MCP_API_SPEC.md` (or vice-versa) is a doc bug worth
reporting.

---

## 6. Deliberate deferrals — please don't re-report

These are known and intentionally open:

- **`tls_error` = WONTFIX** — the host is deliberately strict; sampled certs were genuinely broken.
  Looser competitors "succeed" on shells. Correct behavior, not a bug.
- **Hard anti-bot (`http_403` on Cloudflare/Akamai/PerimeterX) → managed tier.** The stock-browser
  fingerprint clears *soft* anti-bot honestly; solving CAPTCHA / rotating identity is opt-in
  escalation only. This is a positioning choice, not a coverage bug.
- **Proof-of-extraction (Receipt v1) — SHIPPED**, not a gap: signed receipts on every success (contentHash + block-Merkle root + provenance + signature), signed negative receipts on provable unavailability, `occam_verify` (offline/live/prove/citation/history), and a public offline verifier (CLI + `docs/receipt_verification.md` byte-spec). Distributed receipt exchange across nodes remains future work. Do not flag Receipt v1 as unbuilt.

---

## 7. What changed most recently (scrutinize here)

The recent thread (`Q-016`..`Q-027`, summarized in `CHANGELOG.md` `[Unreleased]`) was a
quality/fidelity push, most of it locked by new L9 golden cases:

- **Q-023** table fidelity — a GFM table rule so markdown stops dropping the first column
  (`workers/shared/lib/turndown-table-rule.mjs`).
- **Q-024** `json_tables`/`json_feed` were dropped on the `ok:true` mapping branch in three paths.
- **Q-025** `json_blocks`/`json_tables` were collected on a Readability-mutated DOM — reordered to
  collect before `Readability.parse()` in both workers.
- **Q-026** content-rich pages false-flagged `captcha_or_challenge` — keyword detection now skips
  above 2000 chars of extracted markdown.
- **Q-027** L9 gained a per-case `backend` override so `json_blocks` is now regression-covered on
  the browser backend too.

Highest-value review targets in that set: the **DOM-mutation ordering** (Q-025) and the
**challenge size-gate threshold** (Q-026) — both are heuristics with boundary conditions.

---

## 8. Reviewer quick checklist

- [ ] Build clean, AOT publish clean, `L0_GATE_OK` prints on a full gate run.
- [ ] No stdout pollution (grep new code for `Console.Write`/`Console.WriteLine` off the stderr path).
- [ ] Every new/changed JSON type is in a source-gen `JsonSerializerContext`.
- [ ] No `ok:true` path can return a thin/challenge/login shell (trust model).
- [ ] Doc matrix (§5) updated for any changed tool/param/env/failure-code.
- [ ] No per-host worker branches; no `FFOccamMcp` namespace in new code.
- [ ] Findings ranked by real user impact, not style.
