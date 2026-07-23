# Choosing an occam_* tool

Always-on core MCP tools. Opt-in tools need env flags (see bottom).

---

## By intent

| I want to… | Call | Notes |
|------------|------|-------|
| Read one page as Markdown | `occam_transcode` | Only `url` required |
| Check if URL is worth fetching | `occam_probe` | Cheap; extractability + backend hint |
| Research several pages | `occam_digest` | Up to 8 URLs; `focus_query` |
| Find URLs on a site | `occam_map` → `occam_digest` | Map discovers; digest reads |
| Search the web | `occam_search` → transcode/digest | Needs `OCCAM_SEARCH_PROVIDER` |
| Structured fields | resolve → `occam_extract_knowledge` | Needs `knowledge_schema` in playbook |
| Tuned site extract | resolve → `occam_transcode` | `playbook_policy=auto` |
| Draft/fix playbook | transcode fail → heal → lint → save | Local only |
| Prove one sentence | `occam_claim_check` | Citation proof, not truth judgment |
| Check report citations | `occam_attest` | Batch `{claim, sourceUrl}` |
| Verify signed extraction | `occam_verify` | Offline or live drift |
| Auditable URL corpus | `occam_dataset_export` | 1–20 URLs + manifest |

---

## When NOT to call

| Situation | Do instead |
|-----------|------------|
| `ok: false` on transcode | Read `failure.code`; do not invent content |
| `captcha_or_challenge` | Stop — no CAPTCHA solver |
| `http_404` | Fix or drop URL |
| No `knowledge_schema` | `occam_transcode`, not extract_knowledge |

---

## Defaults that matter

| Tool | Default |
|------|---------|
| `occam_transcode` | `backend_policy=http_then_browser`; only `url` required |
| `occam_digest` | `fit_markdown=true`; max 8 URLs |
| `occam_map` | `source=homepage`; HTTP-only, up to 64 links |
| `occam_playbook_save` | `verify=true` dry-runs before write |

---

## Opt-in tools (env-gated)

| Tool(s) | Env |
|---------|-----|
| `occam_batch_submit/status/results` | `OCCAM_BATCH_MCP=1` |
| `occam_watch` | `OCCAM_WATCH_MCP=1` |
| `occam_crosscheck` | `OCCAM_CONSENSUS_MCP=1` |
| `occam_failure_atlas` | `OCCAM_ATLAS_MCP=1` |
