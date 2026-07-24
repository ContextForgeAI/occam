# AGENTS.md — Cursor entry point

You are working in **FFOccamMCP**, the L0 core of **FF-Occam MCP**. **Read this file first, every session.**

**Language:** English only — user docs, API spec, commits, PRs, comments in committed files.  
**User docs:** [docs/index.md](docs/index.md) — accurate, polished, enjoyable to read.  
**Engineering:** `docs-internal/` — **gitignored**; never commit; never link from user docs.

---

## 1. What exists (document only this)

| Ships | Does not ship |
|-------|---------------|
| MCP stdio host `src/FFOccamMcp.Core` (.NET 10, Native AOT) | 11 legacy `web_*` tools from FFWebMCP |
| **Always-on core MCP tools** (registry: `OccamMcpServerRegistration.OccamToolNames` — do not hand-count): client_capabilities, transcode, probe, digest, playbook_resolve, map, playbook_heal, playbook_save, extract_knowledge, search (`OCCAM_SEARCH_PROVIDER`), verify, claim_check, attest, playbook_lint, dataset_export. **Opt-in** (env-gated, not in the core set): `occam_batch_*` (`OCCAM_BATCH_MCP=1`), `occam_watch` (`OCCAM_WATCH_MCP=1`), `occam_crosscheck` (`OCCAM_CONSENSUS_MCP=1`), `occam_failure_atlas` (`OCCAM_ATLAS_MCP=1`) | `web_probe`, adaptive digest, bundle, publish playbook MCP |
| Params: on `occam_transcode` **only `url` is required** — every other is an off-by-default opt-in (~19, grouped by `[core]`/`[tokens]`/`[structured]`/`[fetch]`/`[watch]`/`[advanced]`); full param tables are code-generated → **canonical in `MCP_API_SPEC.md` + `docs/tools-reference.md`** (don't hand-count here) | `revisit_diff`, legacy `web_map` |
| L1b: probe, domain tiers, agentHints | federation cache, legacy `web_*` |
| Workers: `workers/http-extract/extract.mjs`, `workers/browser-extract/browser-extract.mjs`, **`workers/css-extract/css-extract.mjs`** | gate-unit monolith |
| Browser daemon (host auto-start) | Compare Lab in this repo |
| `scripts/occam-doctor.ps1` | 12-tool public wiki |
| Gate `benchmarks/l0-gate` → `L0_GATE_OK` / `L0_GATE_FAST_OK` / `L1A_TOKEN_OK` / `L1B_PROBE_OK` / `L1_FAILURE_TAXONOMY_OK` / `L2_DIGEST_OK` / `L2_MAP_OK` / **`L2_SESSION_OK`** / **`L2_TRANSPORT_OK`** / **`L2_EGRESS_OK`** / **`L2_MEDIA_REFS_OK`** / **`L3_HEAL_LEARN_OK`** / **`L4_GENOME_OK`** / **`L5_BATCH_OK`** / **`L6_BROWSER_POOL_OK`** / **`L7_RESOURCE_SAFETY_OK`** / **`L8_AGENT_FIRST_OK`** (L9 golden set folds into `L0_GATE_OK`) | Wide Validation, wave2-eval |

**No file cache by design** — every call is live extract (**v1.0.0-rc.2** — fifteen core tools + Receipt v1 verifiable layer + opt-in batch/watch/consensus/atlas; Agent-First AF-1..AF-6; PB2 community + PB3 heal/save + PB4a/b shipped; tier-3 baseline 2026-06-17, L0 core CLOSED).

**PB3 heal-learn (shipped v0.8.4-pb3-heal-learn):** `occam_playbook_heal` + `occam_playbook_save`; gate `L3_HEAL_LEARN_OK` — see `MCP_API_SPEC.md` + `corpora/l3-heal-learn.jsonl`.

**PB4a genome (shipped v0.8.5-pb4a-genome):** resolve extensions (`genome`, `knowledgeSchema`, `pageClass`, `genomeFetch`, `include_lessons`, `fetch_site_genome`, `schema_version`) + transcode `playbook_policy=off|auto` + worker tier overlay. Gate: `L4_GENOME_OK` PB4a subset. See `MCP_API_SPEC.md` · [roadmap.md](docs/roadmap.md).

**PB4b extract (shipped v0.8.6-pb4b-extract):** **eighth tool** — `occam_extract_knowledge` (documented exception like PB3 heal/save). Recipe D: resolve → extract; failure codes §4.3; K7 ≥70%. Gate: `L4_GENOME_OK` **full** + `genome-pilot-k8s-extract`, `genome-neg-extract-no-schema`. Does **not** ship: publish CLI, `occam_playbook_publish`, PB4c gate merge.

**Predecessor (private):** read-only cherry-pick only. **Do not grow Occam core there.**

---

## 2. Task discipline — order of operations

Follow this sequence **on every task**. Do not skip steps.

```
1. UNDERSTAND   Read AGENTS.md + relevant docs/*.md for the area you touch
2. SCOPE        Confirm change is in L0; no drive-by refactors
3. IMPLEMENT    Minimal correct diff in code/workers/scripts
4. VERIFY       Run the right gate (§6) when behavior changed
5. SYNC DOCS    Update every affected doc (§4 matrix) — same session, same PR mindset
6. CLEAN UP     Remove garbage (§5) — no orphans left for the user
7. REPORT       Tell the user what changed: code, docs, commands run, gate result
```

**Definition of done:** code works, docs match code, tree is clean, no stale files, no broken links in `docs/`.

---

## 3. Documentation maintenance — mandatory

Documentation is **part of the deliverable**, not a follow-up ticket.

### 3.1 When you change X, update Y

| You changed… | Update these (all that apply) |
|--------------|-------------------------------|
| `OccamTranscodeTool` params/response | `MCP_API_SPEC.md`, `docs/tools-reference.md` |
| `OccamDigestTool` params/response | `MCP_API_SPEC.md`, `docs/tools-reference.md` |
| Failure codes / post-processors | `MCP_API_SPEC.md`, `docs/failure-codes.md`, `docs/troubleshooting.md` |
| `backend_policy` / router logic | `docs/concepts.md`, `docs/tools-reference.md` |
| Timeouts in backends | `docs/concepts.md`, `MCP_API_SPEC.md` |
| Env vars (`WorkerPaths`, daemon, logger) | `docs/configuration.md`, `MCP_API_SPEC.md`, `docs/getting-started.md` if MCP-related |
| Install / doctor / scripts | `docs/getting-started.md`, `docs/troubleshooting.md`, root `README.md` if quick start shifts |
| Operator onboarding / MCP snippets | `docs/getting-started.md`, `docs/transports.md`, `docs/receipts.md`, `corpora/occam-host-wizard-manifest.json` |
| Smoke corpus `l0-smoke.jsonl` | `docs/tools-reference.md`, `docs/recipes.md` |
| New user-visible capability | `docs/choosing-a-tool.md`, `MCP_API_SPEC.md`, `CHANGELOG.md` |
| Agent / host integration docs | `docs/choosing-a-tool.md`, `docs/getting-started.md`, `docs/recipes.md`, `docs/index.md` agent path |
| Deferred / planning | `CHANGELOG.md` + local `docs-internal/` |
| MCP wiring / Cursor | `docs/getting-started.md` |
| Release-worthy milestone | `CHANGELOG.md` |

### 3.2 Doc audit before finishing

Run this mental (or literal) checklist:

- [ ] Grep docs for the **old** behavior — no stale instructions left
- [ ] `docs/index.md` hub matches files on disk (Agent / Operator / Contributor sections)
- [ ] Cross-links resolve (no links to deleted paths — see docs compaction list)
- [ ] `MCP_API_SPEC.md` and thin tool guides agree on params, codes, timeouts
- [ ] `MCP_API_SPEC.md` out-of-scope table still truthful — nothing documented that does not exist
- [ ] No Russian / mixed-language paragraphs in committed docs
- [ ] No maintainer-only content (gates, RAM stress, AGENT prompts) leaked into `docs/`
- [ ] Root `README.md` still points to `docs/index.md` and `AGENTS.md`

### 3.3 Writing standard

- Short paragraphs, strong headings, tables for comparisons
- Code samples copy-paste ready
- Say what **not** to do (agents hallucinate when docs are vague)
- Prefer updating an existing page over creating duplicate markdown
- New user-facing page → add row to `docs/index.md` immediately

### 3.4 Where to write

| Kind | Path | Git |
|------|------|-----|
| Users + MCP agents | `docs/*.md` | **yes** |
| LLM documentation map | `llms.txt` | **yes** |
| API contract | `MCP_API_SPEC.md` | **yes** |
| Release notes | `CHANGELOG.md` | **yes** |
| Cursor rules | `AGENTS.md` | **yes** |
| Gates, architecture, AGENT prompts, PROJECT_STATE | `docs-internal/` | **no** |

**New feature order:** code → `MCP_API_SPEC.md` → relevant `docs/` pages → `CHANGELOG.md` → `docs/index.md` if new page.

### 3.5 Doc-system principles (the "ideal system")

Docs drifted historically because many agents hand-wrote prose from memory. The fix is a
*system*, not more prose:

- **Two layers, handled differently.** Runtime `tools/list` is the generated source of truth for
  tool availability and input schemas. `MCP_API_SPEC.md` defines response semantics. Narrative
  guides explain workflows and link to the contract instead of duplicating it.
- **One route per audience.** People start at `docs/index.md`; tool-using agents start at
  `llms.txt`; automated installers read `INSTALL.md`; contributors read this file.
- **Executable doc-lint.** `node scripts/check-docs.mjs` validates local links and anchors, H1
  structure, orphan pages, the fifteen-tool registry, `llms.txt`, runtime help routes, stale
  names, and English-only public docs. CI also runs `env-catalog.selftest.mjs` for code↔env drift.
- **Pre-publication = nuke-and-regenerate** from a clean information architecture after a
  claims-vs-code "truth audit" — do **not** patch accumulated drift.
- **Claims discipline (honesty gate).** Forbidden claims: "works on 95% of all websites",
  "20 MB with browser", "bypasses cookie/login walls universally", "AOT-compatible" before CI
  proves it, "token reduction = quality". Report **per-tier** metrics — never a single global
  success rate as the headline; always declare the baseline for any reduction %.

Rationale + reusable assets harvested from the v1 predecessor: `docs-internal/DOC-SYSTEM-AND-HARVEST.md`.

---

## 4. Clean workspace — no garbage

Leave the repo **tidier** than you found it.

### 4.1 Never leave behind

| Garbage | Action |
|---------|--------|
| Temporary scripts you created for one-off debugging | Delete |
| Duplicate doc stubs (“Moved to…”) unless redirect is intentional | Delete stub; fix links |
| Old filenames after rename | Delete old file; grep for references |
| Commented-out dead code blocks | Remove if you introduced them; don't add new ones |
| `artifacts/` outputs from local runs | Do not stage; already gitignored |
| `docs-internal/` content | Do not stage; gitignored |
| Exploratory markdown in repo root | Move to `docs-internal/` or delete |
| Russian (or non-English) in `docs/`, `README.md`, `AGENTS.md`, `MCP_API_SPEC.md` | Rewrite in English |
| FFWebMCP features documented as shipped in FFOccamMCP | Remove or note in `MCP_API_SPEC.md` out-of-scope |
| Unused imports / variables from your edit | Remove |
| TODO comments without issue reference | Either do it or don't add |

### 4.2 After file moves or renames

```text
1. Grep repo for old path/name
2. Update AGENTS.md, docs/index.md, MCP_API_SPEC.md links
3. Delete the old file
4. Confirm docs-internal/README.md if engineering paths moved
```

### 4.3 Scripts and corpora

- New user-facing script → document in `docs/02` or `docs/09`; maintainer-only script → `docs-internal/runbooks/` only
- New smoke URL → `corpora/l0-smoke.jsonl` + `docs/04` + `docs/recipes.md`

---

## 5. Quality bar (code + docs)

1. **Facts only** — verify against `OccamTranscodeTool.cs`, `WorkerPaths.cs`, workers.
2. **Never port** FFWebMCP features not implemented here.
3. **Minimal diff** — L0 stays lean; use existing seams (`AddOccamCore`, `IExtractBackend`, `TranscodePipeline`).
4. **Gate before done** — if extract/workers/host changed: `.\scripts\run-l0-fast.ps1` minimum; full `l0-gate` before release claims.
5. **Never commit** `docs-internal/`, `artifacts/`, secrets, `.cursor/mcp.json` with machine paths.
6. **Commits** — only when the user explicitly asks.
7. **Architecture hardening baseline** — no per-call `new HttpClient` in hot paths; prefer shared/DI clients + centralized env resolver for new host-side runtime settings.

---

## 6. Repository map

```
src/FFOccamMcp.Core/
  Program.cs
  Tools/OccamTranscodeTool.cs
  Tools/OccamDigestTool.cs
  Tools/OccamMapTool.cs
  Services/DigestService.cs
  Services/MapService.cs
  Digest/DigestUrlParser.cs
  Routing/TranscodePipeline.cs, OccamRouter.cs
  Backends/HttpExtractBackend.cs      # 35s timeout
  Backends/BrowserExtractBackend.cs   # 120s timeout
  PostProcessors/ChallengePagePostProcessor, ThinExtractPostProcessor
  Workers/WorkerPaths.cs, BrowserDaemonHost.cs, NodeWorkerProcessRunner.cs

workers/http-extract/extract.mjs
workers/browser-extract/browser-extract.mjs, browser-daemon.mjs
workers/shared/lib/

benchmarks/l0-gate/
corpora/l0-smoke.jsonl
scripts/occam-doctor.ps1
scripts/occam-doctor.sh
scripts/install.ps1
scripts/install.sh
scripts/build-release.ps1
scripts/build-release.sh
scripts/lib/playwright-cache.mjs
scripts/lib/install-preflight.mjs
scripts/lib/build-release.mjs
scripts/lib/release-install.mjs
scripts/lib/resolve-host-binary.mjs
scripts/lib/verify-install.mjs
scripts/lib/print-mcp-snippet.mjs
scripts/lib/print-connection-snippet.mjs
scripts/lib/operator/mcp-snippet.mjs
scripts/occam.mjs
scripts/occam
scripts/occam.ps1
scripts/lib/operator/occam-cli-subcommands.mjs
scripts/lib/operator/control-actions.mjs
scripts/lib/operator/control-loop.mjs
scripts/lib/operator/update-check.mjs
scripts/check-docs.mjs
scripts/get-ff-occam.sh
corpora/occam-host-wizard-manifest.json
docs/getting-started.md
.github/workflows/occam-release.yml
scripts/run-l0-fast.ps1
scripts/run-l0-visual.ps1
scripts/run-visual-matrix.ps1
scripts/run-l0-orphan-audit.ps1
scripts/run-l0-browser-bench.ps1
scripts/run-l0-ram-stress.ps1
scripts/run-browser-daemon.ps1

docs/                    # curated public hub + focused per-tool pages
docs-internal/           # local engineering (gitignored)
```

---

## 7. MCP contract (canonical)

**Fifteen always-on core tools** (registry: `Transport/OccamMcpServerRegistration.cs` → `OccamToolNames`). **Opt-in extras** (env-gated): `occam_batch_submit/status/results` (`OCCAM_BATCH_MCP=1`), `occam_watch` (`OCCAM_WATCH_MCP=1`), `occam_crosscheck` (`OCCAM_CONSENSUS_MCP=1`), `occam_failure_atlas` (`OCCAM_ATLAS_MCP=1`).

**Planned (PB4c — not shipped as MCP):** publish CLI + signed manifest — a CLI, not a tenth MCP tool. Maintainer spec: local `docs-internal/GENOME_EXCHANGE_TEST_PLAN.md`.

```csharp
occam_probe(url)
occam_transcode(url, backend_policy = "http_then_browser")
occam_digest(urls, backend_policy = "http_then_browser", focus_query = "...")
| `occam_playbook_resolve(url)` — read-only; tiers: local → `WT_PLAYBOOKS_PATH` → community → seeds |
occam_map(url, source = "sitemap")
```

Returns JSON string (camelCase): `ok`, `url`, `markdown` | `failure` (transcode) or `classification` / `failureCode` (probe).

Policies: `http` | `browser` | `http_then_browser` (also `http-then-browser`).

Failure codes (representative; **canonical taxonomy → [docs/failure-codes.md](docs/failure-codes.md)**): `invalid_arguments`, `invalid_policy`, `workers_unavailable`, `timeout`, `extraction_failed`, `thin_extract`, `captcha_or_challenge`, `requires_login`, `http_403`, `http_404`, `response_too_large`, `private_url_blocked`, `dns_error`, `tls_error`, `network_error`.

Deep guide: [docs/tools-reference.md](docs/tools-reference.md) · Examples: [docs/recipes.md](docs/recipes.md)

---

## 8. Verification — three tiers

Run the **lowest tier that covers your change**; escalate when behavior touches extract, probe, digest, or token knobs.

| Tier | What | When | Merge-blocking |
|------|------|------|----------------|
| **1. Gate** | `l0-smoke`, `l2-digest`, `l2-map`, failure taxonomy, token/probe corpora | Every behavior change in Core/workers/corpora | **Yes** |
| **2. Visual golden** | 6 cases → `artifacts/l0-runs/` | Worker, compile, FitMarkdown, probe classifier | No |
| **3. Quality audit** | Agent + HTML baseline + rubric 1–5 | Same as tier 2, or after P0/P1 extract fixes | No |

**Tier 1 — always when code changed:**

```powershell
$env:OCCAM_HOME = (Get-Location).Path
.\scripts\occam-doctor.ps1
.\scripts\run-l0-fast.ps1                 # L0_GATE_FAST_OK ~30s
dotnet run --project benchmarks\l0-gate   # L0_GATE_OK
dotnet publish src\FFOccamMcp.Core -c Release -r win-x64
```

**Tier 2 — human glance (not CI substitute):**

```powershell
.\scripts\run-visual-matrix.ps1 -Open
# or: dotnet run --project benchmarks\l0-gate -- --visual --open --smoke-only
```

**Tier 3 — semantic quality (agent, not automated):**

After tier 1 passes, run the **full eight-tool audit** end-to-end. **Do not** substitute gate JSON or desk scripts for MCP fidelity evidence.

```text
@corpora/quality-audit-agent.PROMPT.md @corpora/quality-sprint-wide-cursor-desk.PROMPT.md @AGENTS.md @docs/tools-reference.md @corpora/quality-audit-rotation.jsonl @corpora/l3-heal-learn.jsonl @corpora/l4-genome.jsonl

Full audit: Часть 1 FROZEN+ROTATION → Часть 2 eight-tool 8/8 → Часть 3 Recipe R (K9).
Prerequisites: occam-doctor + Reload MCP · CallMcpTool only · baseline 2026-06-17.
Report drafts: `artifacts/quality-audit/YYYY-MM-DD-….md` (gitignored). Public baseline:
`docs/quality-baseline.md`. Do not treat private report collections as required inputs.

**Expected from a re-run after fixes:** gate still green; golden cases (mdn index, 404, partial digest) stay **Agent-ready**; targeted improvements on nginx noise, probe false positives, digest `focusMatched` honesty — see prompt § Expected outcomes.

---

## 9. Task playbook

| Task | Action |
|------|--------|
| Bad extract on URL | workers, post-processors, corpus, visual artifacts; update docs/04, 05, 09, 11 |
| Quality sprint P0–P2 | `corpora/quality-sprint-p0-p2.PROMPT.md` → implement → `corpora/quality-audit-agent.PROMPT.md` re-audit |
| Post-QA polish | `corpora/quality-sprint-post-qa.PROMPT.md` — verify gates + tier-3 re-audit |
| PB1 playbooks | ✅ done (`0627d45`) — see `corpora/quality-sprint-audit-followup.PROMPT.md` for F3–F5 verify |
| Post-PB1.1 Path A | ✅ done (`dc8780a`) — openai Struct seed + B2 @512 compile verified |
| Post-PB1.1 Path B | ✅ done — agent recipe docs: probe → resolve → transcode in `docs/recipes.md` (0c–0e) |
| Next frozen items only | P3 heal/save — **CUT** (L0 CLOSED); quality micro backlog only |
| PB4a genome engineering | ✅ shipped — `L4_GENOME_OK`; see `MCP_API_SPEC.md` |
| PB4b extract engineering | unfreeze ✅ — [`quality-sprint-pb4b-extract-engineering.PROMPT.md`](corpora/quality-sprint-pb4b-extract-engineering.PROMPT.md) PROPOSED |
| `workers_unavailable` | doctor, `OCCAM_HOME`; update docs/02, 03, 08, 09 if root cause was doc gap |
| StackOverflow / Cloudflare `http_403` with cookies | document TLS bind honesty; prefer `occam-session.mjs export-state` + `backend_policy=browser`; keep UA-only HTTP profile as fallback only |
| SPA blank | Playwright chromium; update docs/02, 05, 09 |
| Mojibake `тАЭ` | UTF-8 chain; update docs/09 if new symptom |
| New MCP tool | **out of L0** unless user expands scope — then full doc pass |
| `OccamProbeTool` params/response | `MCP_API_SPEC.md`, `docs/tools-reference.md`, `docs/06`, `docs/07` |
| `OccamDigestTool` params/response | `MCP_API_SPEC.md`, `docs/tools-reference.md`, `docs/06`, `docs/07` |
| `OccamMapTool` params/response | `MCP_API_SPEC.md`, `docs/tools-reference.md`, `docs/06`, `docs/07` |
| Any MCP tool signature/defaults changed | `docs/tools-reference.md`, `MCP_API_SPEC.md`, tool-specific page(s) |
| P2-4 MCP transport | `docs/transports.md`, `docs/getting-started.md`, `docs/getting-started.md`, `MCP_API_SPEC.md` (if contract changes), `CHANGELOG.md` |
| P2-5a install + system browser | `scripts/install.sh`, `scripts/install.ps1`, `scripts/lib/install-preflight.mjs`, `scripts/lib/verify-install.mjs`, `scripts/lib/print-mcp-snippet.mjs`, `workers/browser-extract/lib/verify-browser-launch.mjs`, `docs/getting-started.md`, `docs/configuration.md`, `CHANGELOG.md` |
| `docs/roadmap.md` → `CHANGELOG.md`
| Hermes CI / MVP gate | `scripts/ci-agent-mvp-gate.*`, `.github/workflows/agent-mvp-gate.yml`, `docs/getting-started.md`, `docs/troubleshooting.md`, `CHANGELOG.md` |
| User asked “docs only” | Still run §3.2 audit across **all** docs, not just one file |
| Any merged change | `CHANGELOG.md` under `[Unreleased]` when user-visible |

---

## 10. Session start checklist

**Current product track:** **v1.0.0-rc.2** — Occam Core 1.0 release candidate (RC1 corpus green 2026-07-20). Next: soak + full live L3–L9 before GA `1.0.0`. Public release identity: `https://github.com/ContextForgeAI/occam`.

1. Read **this file** and [docs/index.md](docs/index.md) for current priorities.
2. Confirm **Cursor rules** active: [.cursor/rules/README.md](.cursor/rules/README.md) (6 `.mdc` files).
3. `git status` — note dirty files; don't pile on unrelated edits.
4. Identify which docs §3.1 matrix rows apply to your task **before** coding.
5. Local `docs-internal/` for engineering depth if present.
6. Execute §2 order through cleanup.

---

## 11. Cursor rules & MCP (links)

| Asset | Path |
|-------|------|
| Always-on L0 rule | [.cursor/rules/occam-l0-core.mdc](.cursor/rules/occam-l0-core.mdc) |
| Doc sync rule | [.cursor/rules/documentation-sync.mdc](.cursor/rules/documentation-sync.mdc) |
| C# / workers / gate rules | [.cursor/rules/csharp-host.mdc](.cursor/rules/csharp-host.mdc), [node-workers.mdc](.cursor/rules/node-workers.mdc), [l0-gate.mdc](.cursor/rules/l0-gate.mdc), [quality-audit.mdc](.cursor/rules/quality-audit.mdc) |
| Rules index | [.cursor/rules/README.md](.cursor/rules/README.md) |
| MCP example config | [.cursor/mcp.json.example](.cursor/mcp.json.example) → copy to `mcp.json`; launcher `scripts/launch-mcp-host.mjs` picks AOT publish by RID |
| Contributor setup guide | [AGENTS.md](AGENTS.md) §12 · local `docs-internal/12-cursor-for-contributors.md` |

---

## 12. Subagent routing

Cursor **Agent mode** can delegate subagents. Request them explicitly when useful:

| Subagent | When to use | Example prompt |
|----------|-------------|----------------|
| `explore` | Find files, trace flow, read specs | "Explore where `thin_extract` is set; respect L0 scope in AGENTS.md" |
| `shell` | Build, gate scripts, git status | "Run doctor + run-l0-fast.ps1 with OCCAM_HOME" |
| `bugbot` | Review diff before merge | "Bugbot: branch changes — MCP/router/workers" |
| `security-review` | User asked security pass | "Security review: uncommitted Core + workers changes" |

Subagents **do not** replace doc sync or cleanup — main agent owns §2 through REPORT.

---

## 13. Anti-patterns

- Ship code without updating the doc matrix rows for that change
- Leave “Moved to…” stubs or duplicate guides (`wiki/`, old Russian filenames)
- Copy all of `WebTranscoderMcp.Core` from FFWebMCP
- Document `web_transcode` — L0 name is **`occam_transcode`**
- Put AGENT_* prompts in `docs/` (use `docs-internal/internal/`)
- Tell end users RAM stress / orphan audit is required (maintainer-only)
- Non-English in committed user-facing markdown
- Create `docs/12-…` without adding it to `docs/index.md`
- “I'll fix docs later” — **later is now**
- Add new per-host `*.prune.mjs` or `if (host === …)` branches in workers — use **`profiles/playbooks/seeds/`** + `playbook-seed.mjs` + gate instead

## Imported Claude Cowork project instructions
