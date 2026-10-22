# Occam

**Live URL → compact Markdown for LLM agents — or a typed `ok:false`.**  
Never invent page text from model memory.

Occam is a **local MCP server** (`ff-occam`). It fetches the real page, strips chrome, fits a token budget, and returns usable context — or a typed refusal. Optional receipts prove **integrity relative to a local key**, not that the page is true.

The product is not a pile of eighteen tools. Tools combine into a few **skills**: get material under a budget, learn a site recipe when the default path fails, attach evidence for citations, and (opt-in) size the tool surface to how an agent actually behaves.

[![Release](https://img.shields.io/github/v/release/ContextForgeAI/occam?label=release)](https://github.com/ContextForgeAI/occam/releases/latest)
[![License: AGPL-3.0](https://img.shields.io/badge/license-AGPL--3.0-blue.svg)](LICENSE)
[![Docs](https://img.shields.io/badge/docs-contextforgeai.github.io%2Foccam-0ea5e9)](https://contextforgeai.github.io/occam/)
[![MCP](https://img.shields.io/badge/MCP-18%20core%20tools-0ea5e9)](docs/tools/index.md)

| Generic fetch / model memory | Occam |
|------------------------------|--------|
| Empty shell or invented text | Live Markdown **or** typed `ok:false` |
| Raw HTML burns the context window | Token budget + focus prune |
| Silent failure looks like “a short page” | `thin_extract` ≠ successful `short_quality` |
| No integrity of what was returned | Optional Receipt v1 → `occam_verify` |

---

## Install

> **Use the release tarball.** Do not build from source (`dotnet build` / `publish` / `run`) unless you are developing Occam itself.  
> Needs **Node 20+**. No .NET SDK on the install machine. Full rules: [INSTALL.md](INSTALL.md).

**Windows (PowerShell):**

```powershell
irm https://raw.githubusercontent.com/ContextForgeAI/occam/v1.3.0/scripts/get-ff-occam.ps1 | iex
```

**Linux x64 / macOS Apple Silicon:**

```bash
curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/v1.3.0/scripts/get-ff-occam.sh | bash
```

Then connect your MCP host — [quick-start](docs/quick-start.md) · [getting-started](docs/getting-started.md) · [site](https://contextforgeai.github.io/occam/).

`npx ff-occam@1.3.0` is **experimental** (MCP host only; not the guarded GA path).

---

## First call

Default profile **`OCCAM_PROFILE=reader`** exposes **11** day-to-day tools. Full core catalog: **18**.

1. Once per session: `occam_client_capabilities(context_tokens=…)` (or set `OCCAM_CLIENT_CONTEXT_TOKENS`).
2. One page: **`occam_transcode`** with only `url` — or the one-call facade **`occam`**.
3. Several URLs: one **`occam_digest`**, not N× transcode.
4. On `ok: false`: read `failure.code` — **do not** fill in the page from memory.

```text
occam_transcode({ "url": "https://example.com" })
→ ok:true + markdown  |  ok:false + failure.code
```

Tool picker: [choosing-a-tool](docs/choosing-a-tool.md) · Per-tool pages: [docs/tools/index.md](docs/tools/index.md) · Flashcard: [why-occam](docs/why-occam.md).

---

## Skills (how the tools work together)

### 1. Get usable material under a budget

**When:** the agent needs page content for a task without burning the context window.  
**Result:** compact Markdown (or a typed unknown), sized to budget, with the source URL.

Typical path the **agent** assembles:

`occam_search` / `occam_map` / `occam_probe` (optional) → `occam_transcode` or `occam` or `occam_digest` → optional cheap re-read (`if_none_match` / `diff_against`).

- Set budget once with `occam_client_capabilities` (applies to later `occam_transcode` / `occam_digest` when `max_tokens` is omitted).
- **`occam`** is the simple one-call ladder (playbook look-up → HTTP → browser → content hash).
- **`occam_transcode`** is the full reader: smarter `http_then_browser` escalation, focus, structured sidecars, sessions, receipts.
- Search results can be passed as handles (`S1` / `H…`) into later reads in the same process. Map → digest is usually agent-assembled; `occam_digest` with `source_url` can discover links itself.

### 2. Learn a site recipe and reuse it

**When:** the default extract is wrong or noisy on a site you will visit again.  
**Result:** a local playbook that later reads (and typed `occam_extract_knowledge`) can reuse.

Loop the **agent** drives (Occam validates on save):

failure → `occam_playbook_heal` → review / `occam_playbook_lint` → `occam_playbook_save` → later resolve / transcode / extract.

Authoring tools (`resolve` / `heal` / `save`) need **`OCCAM_PROFILE=full`**. Default `reader` can still call `occam_extract_knowledge` when a recipe with `knowledge_schema` already exists. Playbook `agent_notes` guide the **agent** (via resolve); they are not an automatic price converter. Numeric field transforms in schema are rare author opt-ins (e.g. integer `divide` on regex fields).

### 3. Bind claims to extracted evidence

**When:** you need citations, exports, or proof of what was returned.  
**Result:** different checks for different questions — keep them separate.

| Question | Tool | Does **not** prove |
|----------|------|--------------------|
| Integrity of returned bytes vs a local key | Receipt + `occam_verify` | Page truth or site identity |
| Which block matches a claim string | `occam_claim_check` | That the page supports the claim |
| Heuristic support / contradiction | `occam_attest` | General truth (rule classifier, fail-closed) |
| Agent actually read a canary page | `occam_canary_issue` / `occam_canary_verify` | That the agent understood it |
| Bundle many sourced rows | `occam_dataset_export` | Quality of those rows |

### 4. Size the tool surface to the agent (opt-in research)

**When:** you want a narrower or fuller tool menu based on observed tool-use behaviour.  
**Result:** graded tier → `minimal` / `basic` / `full` for the session (unless `OCCAM_PROFILE` is pinned).

Enable with `OCCAM_EXAM_MCP=1`, submit a behaviour record via `occam_exam_submit` (or CLI `exam grade`). The host **grades a submitted record**; it does not autonomously run the four tasks for the agent. Canary also works standalone for proof-of-read outside the exam. Details: [experimental](docs/experimental.md) · research pages on the [docs site](https://contextforgeai.github.io/occam/).

### Advanced (env-gated, not day-one)

Batch jobs, page watch + history, multi-view cross-check, failure atlas, and browser click/type-then-read are opt-in (`OCCAM_BATCH_MCP`, `OCCAM_WATCH_MCP`, `OCCAM_CONSENSUS_MCP`, `OCCAM_ATLAS_MCP`, `OCCAM_BROWSER_ACTIONS_MCP`). See [configuration](docs/configuration.md).

---

## One path through a failure

1. `occam_transcode` → `ok:false`, `failure.code` e.g. `thin_extract` (bad extract — not “short but fine”).
2. Do **not** invent the page. Read `agentHints` if present.
3. If the site is worth fixing: set `OCCAM_PROFILE=full`, run heal → lint → save, then read again or `occam_extract_knowledge`.
4. If you only needed a citation later: keep the receipt and use `occam_verify` / `occam_claim_check` as appropriate.

---

## Docs map

| Audience | Start here |
|----------|------------|
| Human (this page) | Install → first call → [docs site](https://contextforgeai.github.io/occam/) · [docs hub](docs/index.md) |
| Operator / install agent | [INSTALL.md](INSTALL.md) |
| Tool-using agent | [`llms.txt`](llms.txt) → [why-occam](docs/why-occam.md) → [choosing-a-tool](docs/choosing-a-tool.md) |
| API contract | [MCP_API_SPEC.md](MCP_API_SPEC.md) |
| Contributor | [AGENTS.md](AGENTS.md) |

Trust limits: [trust-and-safety](docs/trust-and-safety.md). Deep hubs (not the front door): [TRUST](docs/TRUST.md) · [CASCADE](docs/CASCADE.md) · [SEARCH](docs/SEARCH.md) · [receipts](docs/receipts.md).

Runtime `tools/list` and code win when a narrative page disagrees.
