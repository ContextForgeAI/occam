# MCP tools — core set

Registry: `OccamMcpServerRegistration.OccamToolNames`. All return JSON strings (camelCase).

---

## Core tools

| Tool | Purpose |
|------|---------|
| `occam_probe` | Cheap URL classification, extractability, backend hint |
| `occam_transcode` | URL → Markdown (+ optional structured blocks, tokens, session) |
| `occam_digest` | 1–8 URLs → per-page results + optional combined markdown |
| `occam_playbook_resolve` | Read-only playbook/genome lookup for a host |
| `occam_map` | Discover links (homepage or sitemap) |
| `occam_playbook_heal` | Draft playbook fix from failed extract + DOM skeleton |
| `occam_playbook_save` | Write local playbook after verify gate |
| `occam_extract_knowledge` | Recipe D — structured `facts[]` when schema exists |
| `occam_search` | Web search (provider via `OCCAM_SEARCH_PROVIDER`) |
| `occam_verify` | Offline/live receipt, citation, manifest, history verification |
| `occam_claim_check` | Ground one claim in extracted blocks + Merkle proof |
| `occam_attest` | Batch claim-check for a cited report |
| `occam_playbook_lint` | Static playbook JSON validation (no network) |
| `occam_dataset_export` | 1–20 URLs → signed auditable dataset manifest |

---

## occam_transcode — key opt-in params

Only `url` is required. Common opt-ins:

| Param | Effect |
|-------|--------|
| `backend_policy` | `http` \| `browser` \| `http_then_browser` (default) |
| `fit_markdown` + `focus_query` | Token-budgeted markdown |
| `max_tokens` | Hard cap on output tokens |
| `json_blocks` / `json_tables` / `json_feed` | Structured sidecars |
| `playbook_policy` | `off` \| `auto` |
| `session_profile` | Authenticated browser session |
| `if_none_match` | Skip when content hash unchanged |

Full param tables: `docs/tools-reference.md`, `MCP_API_SPEC.md`.

---

## Opt-in tools (not in default tools/list)

| Tools | Env |
|-------|-----|
| `occam_batch_submit`, `occam_batch_status`, `occam_batch_results` | `OCCAM_BATCH_MCP=1` |
| `occam_watch` | `OCCAM_WATCH_MCP=1` |
| `occam_crosscheck` | `OCCAM_CONSENSUS_MCP=1` |
| `occam_failure_atlas` | `OCCAM_ATLAS_MCP=1` |

---

## Response shapes (abbreviated)

**Success (transcode):**

```json
{
  "ok": true,
  "url": { "requested": "…", "final": "…" },
  "markdown": "…",
  "backend": "http",
  "receipt": { "signed": { "v": 1, "contentHash": "sha256:…", "sig": "…" } }
}
```

**Failure:**

```json
{
  "ok": false,
  "failure": { "code": "http_404", "message": "…" }
}
```
