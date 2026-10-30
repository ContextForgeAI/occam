# ADR-0018 — Env-gated crawl MCP (`OCCAM_CRAWL_MCP`)

- **Status:** Accepted — MVP implemented as env-gated `occam_crawl` (`OCCAM_CRAWL_MCP=1`)
- **Date:** 2026-10-29

## Context

Live comparison with scrapers that expose `crawl --mode map|full` (budgets, resume, JSONL) showed Occam’s `occam_map` + `occam_digest` (+ opt-in batch) as a workable but agent-hostile substitute. Roadmap listed resumable crawl as Deferred. Opt-in env-gated tools (`OCCAM_*_MCP`) remain the only expansion path while core tool freeze holds.

## Decision

1. Ship crawl as **`OCCAM_CRAWL_MCP=1`** opt-in tool(s), not as a nineteenth always-on core tool and not in default `reader`.
2. MVP shape (Occam-shaped, not a Firecrawl clone):
   - Seed URL or URL list → sitemap-first discovery (reuse map) then BFS within budget.
   - Budgets: `max_pages`, depth, optional total token budget.
   - Per page: `TranscodePipeline` with passthrough `backend_policy` / `session_profile` / `playbook_policy`.
   - Resume token + JSONL lines compatible with `occam_dataset_export` where practical.
   - Optional per-page receipts; honesty: crawl does not unlock CAPTCHAs.
3. Registration mirrors batch/watch in `OccamMcpServerRegistration`.
4. Opt-in env-gated tools remain the freeze-safe expansion path (`OCCAM_*_MCP`). MVP landed without promoting crawl into the eighteen core tools.

## Consequences

- Agents get a single crawl call under a flag without widening default `tools/list`.
- Docs mark crawl **experimental** ([experimental.md](../experimental.md)).
- Default profiles unchanged; enable with `OCCAM_CRAWL_MCP=1`.

## Alternatives considered

- Promote crawl into core eighteen — rejected (tool freeze + reader surface bloat).
- Only document map→digest recipes — rejected as insufficient vs live competitor UX.
- Bundle residential unlocker into crawl — rejected (product niche / honesty).
