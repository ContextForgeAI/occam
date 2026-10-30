# Roadmap — shipped log and direction

> **Product version:** 1.3.0 (GA) · **L0 core:** CLOSED (**18** core MCP tools; default profile `reader` = 11).
> **North star:** [VISION.md](https://github.com/ContextForgeAI/occam/blob/main/VISION.md) · **Contract:** [MCP API](reference/mcp-api.md) · **Release notes:** [CHANGELOG.md](https://github.com/ContextForgeAI/occam/blob/main/CHANGELOG.md).

This page is the **public shipped log**. Detailed engineering backlog lives in local `docs-internal/` (not committed).

---

## Shipped (high level)

| Milestone | What landed |
|-----------|-------------|
| **L0 core** | Native AOT .NET 10 host, stdio + Streamable HTTP / WS, **18** core tools (registry: `OccamToolNames`), live extract by default |
| **Receipt v1** | Signed extraction receipts + offline `occam_verify` |
| **PB1–PB4b playbooks** | Seeds, resolve tiers, heal/save, genome + `occam_extract_knowledge` (Recipe D); gates `L3_HEAL_LEARN_OK` / `L4_GENOME_OK` |
| **Agent-First AF-1..AF-6** | Confidence, receipts, auto-recovery, differential — gate `L8_AGENT_FIRST_OK` |
| **Level B install** | Release tarballs + Cosign `required-cosign-v1`, `get-ff-occam.*`, doctor/workers repair, Hermes smoke |
| **v1.3.0 install ship** | Idempotent `occam update`, clean-install CI (IF-01…03), agent tarball-only discipline, Exam MCP opt-in (`OCCAM_EXAM_MCP`), canary E2E selftest |
| **Tool surface freeze** | No new core MCP tools until install is zero-config; expansion via env-gated opt-ins only |
| **Agent skill** | `skills/occam/`, `@ff-occam/skill`, `occam skill install` |
| **Docs hub** | Curated `docs/` + `INSTALL.md` as the agent/operator install entry |

---

## Active engineering (maintainer)

| Track | Status |
|-------|--------|
| **Zero-config install ship** | In progress — Node/Cosign prerequisites, tag-pinned bootstrap (T4), multi-host connect, npm honesty; unlocks ending the tool freeze |
| GitHub release publish (`occam-release.yml`) | CI on SemVer tag `v*` — Level B archives + Cosign |
| Product polish (receipt/login heuristics, doc-truth) | In progress |
| npm publish (`ff-occam`, `@ff-occam/mcp`) | **Experimental** — **not** the guarded GA install channel |

---

## Not shipped (out of L0 scope)

| Item | Notes |
|------|-------|
| PB4c publish CLI + signed manifest exchange | CLI only — not a core MCP tool |
| Playbook marketplace GitHub App | Design only |
| WASM edge extractor | Future — not implemented in this tree’s public snapshot |
| Editor marketplace extension | Future |
| npm as GA install channel | Packages may exist on npm; **guarded** install remains GitHub Release bootstrap |
| Wide validation / wave2-eval | Not in this repo |
| External CLI search / PDF OCR | Partial: keyless DuckDuckGo default; optional `OCCAM_SEARCH_PROVIDER=external_cli`, opt-in `OCCAM_PDF_OCR` — third-party binaries not bundled |
| Resumable crawl MCP (`OCCAM_CRAWL_MCP`) | **Opt-in experimental** — `OCCAM_CRAWL_MCP=1` → `occam_crawl` ([ADR-0018](adr/0018-crawl-mcp.md)); else map+digest |
| CAPTCHA unlocker / residential proxy | **Rejected** — permanent non-goal; soft challenge → `try_browser`, hard CAPTCHA → stop + operator `session_profile` |
| Default `OCCAM_PROFILE=full` | **Rejected** — product default stays `reader`; runtime `profile=` or boot env for authoring; contract may spawn `full` for fingerprint only |

---

## Historical note

Root `STRATEGIC_ROADMAP.md` (score 72→95, P2/Growth backlog) was **removed 2026-07-08** after compaction. Its shipped items are reflected above and in `CHANGELOG.md`; speculative backlog is not duplicated here to avoid doc drift.
