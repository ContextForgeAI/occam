# Roadmap — shipped log and direction

> **Product version:** 1.0.0-rc.2 · **L0 core:** CLOSED (fifteen core MCP tools).
> **North star:** [VISION.md](../VISION.md) · **Contract:** [MCP_API_SPEC.md](../MCP_API_SPEC.md) · **Release notes:** [CHANGELOG.md](../CHANGELOG.md).

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
| GitHub release publish (`occam-release.yml`, `GITHUB_TOKEN`) | CI on SemVer tag `v*` — **tarball-only RC** (`linux-x64`, `osx-arm64`) |
| Product polish (receipt/login heuristics, doc-truth) | In progress |
| npm publish (`@ff-occam/mcp`, `@ff-occam/skill`) | **Not part of `1.0.0-rc.2`** — deferred after tarball RC |

---

## Not shipped (out of L0 scope)

| Item | Notes |
|------|-------|
| PB4c publish CLI + signed manifest exchange | CLI only — not a core MCP tool |
| Playbook marketplace GitHub App | Design only |
| WASM edge extractor | Future — not implemented in this tree’s public snapshot |
| Editor marketplace extension | Future — not part of `1.0.0-rc.2` |
| Wide validation / wave2-eval | Not in this repo |

---

## Historical note

Root `STRATEGIC_ROADMAP.md` (score 72→95, P2/Growth backlog) was **removed 2026-07-08** after compaction. Its shipped items are reflected above and in `CHANGELOG.md`; speculative backlog is not duplicated here to avoid doc drift.
