# Roadmap — shipped log and direction

> **Product version:** 1.0.0 (GA) · **L0 core:** CLOSED (fifteen core MCP tools; default profile `reader`).
> **North star:** [VISION.md](https://github.com/ContextForgeAI/occam/blob/main/VISION.md) · **Contract:** [MCP API](reference/mcp-api.md) · **Release notes:** [CHANGELOG.md](https://github.com/ContextForgeAI/occam/blob/main/CHANGELOG.md).

This page is the **public shipped log**. Detailed engineering backlog lives in local `docs-internal/` (not committed).

---

## Shipped (high level)

| Milestone | What landed |
|-----------|-------------|
| **L0 core** | Native AOT .NET 10 host, stdio + optional WebSocket, fifteen `occam_*` tools, live extract only |
| **Receipt v1** | Signed extraction receipts + offline `occam_verify` |
| **PB1 playbooks** | Seeds, resolve tiers, community manifest |
| **PB3 heal/save** | `occam_playbook_heal` + `occam_playbook_save`, gate `L3_HEAL_LEARN_OK` |
| **PB4a genome** | Resolve extensions, `playbook_policy=auto`, gate `L4_GENOME_OK` (PB4a) |
| **PB4b extract** | `occam_extract_knowledge` (Recipe D), gate `L4_GENOME_OK` (full) |
| **Agent-First AF-1..AF-6** | Confidence, receipts, auto-recovery, differential — gate `L8_AGENT_FIRST_OK` |
| **Level B install** | Release tarballs, `get-ff-occam.sh`, GitHub release CI, Hermes smoke |
| **Agent skill** | `skills/occam/`, `@ff-occam/skill`, `occam skill install` |
| **Docs compaction** | Twelve-page `docs/` hub derived from code |

---

## Active engineering (maintainer)

| Track | Status |
|-------|--------|
| GitHub release publish (`occam-release.yml`, `GITHUB_TOKEN`) | CI on SemVer tag `v*` — Level B archives + Cosign (`required-cosign-v1` on rc.3+) |
| Product polish (receipt/login heuristics, doc-truth) | In progress |
| npm publish (`ff-occam`, `@ff-occam/mcp`) | **Experimental RC** via `npm-publish.yml` (dist-tag `rc`) — **not** a GA install channel |

---

## Not shipped (out of L0 scope)

| Item | Notes |
|------|-------|
| PB4c publish CLI + signed manifest exchange | CLI only — not a core MCP tool |
| Playbook marketplace GitHub App | Design only |
| WASM edge extractor | Future — not implemented in this tree’s public snapshot |
| Editor marketplace extension | Future |
| npm as GA install channel | RC/GA packages exist on npm; **guarded** install remains GitHub Release bootstrap (npm stays experimental) |
| Wide validation / wave2-eval | Not in this repo |
| Donsetch / managed acquisition / PDF OCR | Partial: keyless DuckDuckGo is the default search provider; optional `OCCAM_SEARCH_PROVIDER=donsetch`, `OCCAM_MANAGED_PROVIDER=archive\|donsetch`, opt-in `OCCAM_PDF_OCR` — no BoringSSL/crawl rewrite; competitor binaries not bundled |
| Resumable crawl MCP (`OCCAM_CRAWL_MCP`) | Deferred — use `occam_map` + `occam_digest` (+ opt-in batch) for multi-URL workflows |

---

## Historical note

Root `STRATEGIC_ROADMAP.md` (score 72→95, P2/Growth backlog) was **removed 2026-07-08** after compaction. Its shipped items are reflected above and in `CHANGELOG.md`; speculative backlog is not duplicated here to avoid doc drift.
