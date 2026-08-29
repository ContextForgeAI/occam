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
| One opaque “read” | Ladder: HTTP → browser → optional managed; probe / map / search / digest |
| Silent length = “bad page” | `thin_extract` ≠ `short_quality` success |

Default one-page call: **`occam_transcode` with only `url`**. Everything else is opt-in.

---

## Advantages (product)

1. **Honesty contract** — typed failures; `thin_extract` is bad extract; short complete pages stay `ok:true`.  
2. **Token contract (K2)** — ambient budget via `occam_client_capabilities` (~20% of context) or explicit `max_tokens`; drops reported in `compile.omitted`.  
3. **Acquisition ladder** — cheap HTTP first, browser when needed, managed only as last rung on opted-in hosts.  
4. **Materialization** — focus, structured sidecars, differential re-reads — same extract, different shapes.  
5. **Receipts & evidence** — signed extract integrity; claim_check / attest for citation workflows (heuristic — not crypto truth).  
6. **Playbooks** — per-site recipes (resolve / heal / lint / save) without rewriting the host.  
7. **Local-first** — host runs with you; SSRF/private URL blocks; Cosign on *releases* ≠ page truth.  
8. **Discovery before spend** — probe extractability, map links, search (when configured), then digest many URLs once.

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
| `compact_links` / `compact_block_links` / `include_media_refs` | transcode | Strip link destinations / media noise |

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
| `occam_search` | Needs `OCCAM_SEARCH_PROVIDER` (incl. optional local Donsetch) |
| Managed / archive / PDF OCR | Operator env — see [configuration](configuration.md) · [experimental](experimental.md) |

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
| Site recipe | `occam_playbook_*` (authoring only) |
| Opt-in extras | watch / batch / crosscheck / atlas / browser_interact — [experimental](experimental.md) |

Default profile **`reader`** exposes a subset; `full` exposes the fifteen-core set. Runtime `tools/list` is authoritative.

---

## Do not overclaim

- Receipts = integrity vs a key — **not** truth, identity, origin authenticity, or trusted time.  
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
