# Why Occam — advantages and knobs at a glance

**Audience:** humans *and* agents. Read this **before** treating Occam as “just another web fetch.”

**Status:** STABLE summary of shipped surfaces · deep links only — not a second API contract  
**Contract:** runtime `tools/list` + [MCP API](reference/mcp-api.md) win on schemas.

---

## Why not a generic fetch / model memory?

| Generic fetch / memory | Occam |
|------------------------|--------|
| Empty shell, HTML chrome, or invented text | Live acquire → compact Markdown **or** typed `ok:false` |
| Agent guesses missing pages from training | **`ok:false` = content unknown** — never substitute memory |
| Raw HTML burns the context window | Token budget + focus prune (**not** a LLM summarizer) |
| No integrity of what was returned | Optional **Receipt v1** → `occam_verify` (integrity vs a key — not truth) |
| One opaque “read” | Ladder: HTTP → browser; probe / map / search / digest |
| Silent length = “bad page” | `thin_extract` ≠ `short_quality` success |

Default one-page call: **`occam_transcode` with only `url`**. Everything else is opt-in.

---

## Skills (product)

Tools matter as finished abilities, not as an eighteen-card catalog.

1. **Read under a budget** — honesty contract (`ok:false` = unknown; `thin_extract` ≠ `short_quality`) + token contract (`occam_client_capabilities` ~20% of context, or explicit `max_tokens` / focus) + acquisition ladder (HTTP → browser) + discovery (probe / map / search) + many URLs (`occam_digest`) + cheap re-reads (`if_none_match` / `diff_against`). One-call facade: `occam`. Full knobs: `occam_transcode`.  
2. **Learn a site recipe** — when default extract fails: heal → lint → save (`OCCAM_PROFILE=full`), then reuse via resolve / transcode / `occam_extract_knowledge`. `agent_notes` guide the agent; they are not automatic arithmetic.  
3. **Bind evidence** — Receipt v1 + `occam_verify` (integrity vs a key — not truth); `occam_claim_check` / `occam_attest` (heuristic citation aids); canary proof-of-read; `occam_dataset_export`.  
4. **Size the tool surface** (opt-in) — `OCCAM_EXAM_MCP=1` grades a **submitted behaviour record** and may change `tools/list`; not an autonomous exam.  

Also: **local-first** defaults (SSRF / private URL blocks); Cosign on *releases* ≠ page truth. Opt-in batch / watch / crosscheck / atlas / browser actions stay behind env flags — [experimental](experimental.md).

---

## Knobs agents actually use (not a “compression codec” param)

There is **no** public MCP parameter to pick a knowledge codec (`compact-markdown` / `knowledge-json` exist in-process for benches/extensions; live path is **markdown passthrough**). Token economy is these knobs:

### Budget and focus

| Knob | Tool | Effect |
|------|------|--------|
| `occam_client_capabilities(context_tokens=…)` | once / session | Ambient output budget |
| `max_tokens` / `per_url_max_tokens` | transcode / digest | Hard whole-response / per-URL cap |
| `fit_markdown` + `focus_query` | transcode (off by default) / digest (**on** by default) | BM25 paragraph prune |
| `toc` / `section` / `must_contain` | transcode | Structural / needle focus |
| `compact_links` / `compact_block_links` / `include_media_refs` | transcode | Strip link destinations; set `include_media_refs:true` for image/video URLs |

### Structure and change

| Knob | Effect |
|------|--------|
| `json_blocks` / `json_tables` / `json_feed` / semantic chunks | Sidecars sharing the same budget |
| `rank_blocks` | Salience vs `focus_query` on blocks |
| `if_none_match` | Cheap `unchanged:true` vs prior `contentHash` |
| `diff_against` / `delta_only` | Block-level delta |
| `cache_ttl_s > 0` | Opt-in **local** replay (not a CDN) |

### Reach and recipes

| Surface | When |
|---------|------|
| `backend_policy` | Force `http` / `browser` / `http_then_browser` |
| `session_profile` | Operator cookies for login walls (no CAPTCHA solve) |
| `playbook_policy` / playbook tools | Site recipe overlay / authoring |
| `occam_search` | Default keyless DuckDuckGo; `OCCAM_SEARCH_PROVIDERS=keyless` → duckduckgo+mojeek fan-out |
| PDF OCR | Operator env `OCCAM_PDF_OCR*` — not bundled; else PDF via browser extract |
| Soft anti-bot | Probe `try_browser` → `occam_transcode`; then optional `session_profile` (no CAPTCHA solve) |
| Crawl (opt-in) | `OCCAM_CRAWL_MCP=1` → `occam_crawl` (map→transcode, resume_token) |

---

## Occam vs generic scrapers (honesty)

Occam wins on **signed receipts**, **probe gating**, **playbooks**, **token-aware compile**. It is not a residential unlocker. Change detection is caller-held (`if_none_match` / `diff_against`); opt-in `occam_watch` for stateful watch. Install for agents: **release tarball**, not .NET SDK. Default **`reader` = 11 tools**; playbook authoring needs `OCCAM_PROFILE=full`. `occam_digest` merges per-URL extracts — not cross-page LLM synthesis.

---

## Tool map (one line each)

| Goal | Tool |
|------|------|
| Size later reads | `occam_client_capabilities` |
| Worth fetching? | `occam_probe` |
| One page | `occam_transcode` |
| Many URLs | `occam_digest` |
| Site links | `occam_map` |
| Open web URLs | `occam_search` |
| Typed fields | `occam_extract_knowledge` |
| Integrity / cite | `occam_verify` · `occam_claim_check` · `occam_attest` |
| Corpus export | `occam_dataset_export` |
| Site recipe | `occam_playbook_*` (authoring — needs `OCCAM_PROFILE=full`) |
| Opt-in extras | watch / batch / crosscheck / atlas / browser_interact / exam_submit — [experimental](experimental.md) |

Default profile **`reader`** exposes **11** day-to-day tools; `full` exposes all **18** core tools. Runtime `tools/list` is authoritative.

---

## Do not overclaim

- Receipts = integrity vs a key — **not** truth, identity, origin authenticity, or trusted time.  
- Attest / claim_check = heuristic retrieval aids — **not** general NLI truth.  
- Exam MCP = grade of a submitted record — **not** the host running the four tasks for the agent.  
- Crosscheck = comparison — **not** consensus proof.  
- npm = experimental RC — **not** GA install.  
- Cosign = release authenticity under policy — **not** page-content truth.  
- No public live codec selector yet.

---

## Paste this to an agent (git / docs)

```text
Read docs/why-occam.md (or https://contextforgeai.github.io/occam/why-occam/) then llms.txt.
Do not treat Occam as generic fetch. Use honesty + token knobs + receipts as listed.
Default: occam_transcode(url). Several URLs: occam_digest. On ok:false never invent page text.
```

## Next

- Agents: [`llms.txt`](https://raw.githubusercontent.com/ContextForgeAI/occam/main/llms.txt) · [Ask AI](ask-ai.md) · [Task router](choosing-a-tool.md)  
- Humans: [Quick Start](quick-start.md) · [What is Occam?](what-is-occam.md) · [Materialization](materialization.md)  
- Depth: [Handbook honesty](handbook/02-honesty-contract.md) · [Token contract](handbook/07-materialization-token-contract.md)
