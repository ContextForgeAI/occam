# DOCS-V3-NONMCP-COVERAGE (Phase 8E)

**Branch:** `docs/v3-canonical`  
**Date:** 2026-07-26  
**Goal:** Ensure public docs do **not** reduce Occam to “15 MCP tools” only.

**Exposure anchor:** Handbook ch.18 — **51 named entrypoints** (15 core MCP + 6 opt-in MCP + 5 host offline verbs + 13 operator `occam` subcommands + 4 installer/bootstrap + 3 alternate process modes + 3 package bins + 1 Docker entry — connect adapters folded under `occam connect`).

---

## Summary

| Surface | VISIBLE | ACCURATE | STATUS CLEAR | Primary paths |
|---------|---------|----------|--------------|---------------|
| CLI wrapper (`occam`) | YES | PARTIAL | YES | `operators.md`, `getting-started.md`, handbook ch.19 |
| Install / bootstrap | YES | YES | YES | `INSTALL.md`, `install.md`, `quick-start.md` |
| Doctor | YES | YES | YES | INSTALL, quick-start, operators |
| Connect platform | YES | YES | YES | `connect/*`, `mcp-hosts.md` |
| Session lifecycle (CLI) | YES | YES | YES | `sessions.md`, guides/sessions |
| Profiles | YES | PARTIAL | YES | choosing-a-tool, handbook ch.18, configuration |
| Opt-in MCP tools | YES | YES | YES | experimental.md, tools/index |
| Batch HTTP server | YES | YES | YES | transports.md, experimental.md |
| Transports (stdio/WS/remote) | YES | YES | YES | transports.md, handbook ch.18 |
| Managed backends | YES | YES | YES | acquisition.md, experimental.md |
| Packaging / update | YES | YES | YES | operators.md, INSTALL.md, trust/installation-safety |
| Verification CLI | PARTIAL | YES | YES | receipts.md, occam_verify.md, handbook ch.15 |
| Dataset manifest verify | PARTIAL | YES | YES | datasets.md, occam_dataset_export.md |

**Non-MCP gaps (remaining):**

1. **CLI routing asymmetry** — `occam` wrapper does not expose host-binary `verify` / `keys`; documented in handbook ch.18/15 but easy to miss from quick-start alone.
2. **Manifest verify** — CLI-only; MCP agents must be told explicitly (datasets page OK; not in llms.txt agent routing).
3. **Reader profile count** — handbook table said 7, code has 8 — **fixed** in ch.18.
4. **npm / Docker** — correctly marked NOT GA / LIMITED; not hidden.

**Covered surfaces:** 11/11 listed — all visible; 2 partial discoverability (verify CLI path, manifest).

---

## 1. CLI wrapper (`occam`)

| Aspect | Documentation |
|--------|---------------|
| **What ships** | `scripts/occam` / `occam.ps1` → subcommands: connect, session, refresh, control, update-check, etc. |
| **Where** | `docs/operators.md`, `docs/getting-started.md`, handbook ch.19 |
| **Honesty** | Wrapper ≠ full host binary surface; direct `OccamMcp.Core verify` / `keys export` required for some verbs |
| **Gap** | Quick Start emphasizes MCP first — offline verify path one hop away via receipts.md |

---

## 2. Install / bootstrap

| Aspect | Documentation |
|--------|---------------|
| **What ships** | `get-ff-occam.sh/ps1` → tarball + SHA-256 → doctor → connect |
| **Where** | `INSTALL.md`, `docs/install.md`, `docs/quick-start.md`, handbook ch.03 |
| **Honesty** | npm NOT GA; Cosign not enforced; destructive replace/no rollback stated in trust/installation-safety |
| **Gap** | None blocking |

---

## 3. Doctor

| Aspect | Documentation |
|--------|---------------|
| **What ships** | `scripts/occam-doctor.ps1/sh` — workers, Playwright, publish sanity |
| **Where** | INSTALL, quick-start, operators, troubleshooting |
| **Honesty** | Required after install; not optional polish |
| **Gap** | None |

---

## 4. Connect platform

| Aspect | Documentation |
|--------|---------------|
| **What ships** | detect → classify → backup → configure → verify → restart → rollback (tier-limited) |
| **Where** | `docs/connect/index.md`, automatic/explicit-only/manual/troubleshooting, `mcp-hosts.md` |
| **Honesty** | Rollback not universal; onboard.json precedence in after-install |
| **Gap** | None blocking |

---

## 5. Session lifecycle (operator)

| Aspect | Documentation |
|--------|---------------|
| **What ships** | `occam-session` import/export; profiles under `OCCAM_SESSIONS_ROOT`; tier matrix |
| **Where** | `sessions.md`, `guides/sessions.md`, `examples/session-profile.md` |
| **Honesty** | Not same on every tool; `_imports/` retention; no CAPTCHA solve |
| **Gap** | `concepts.md` session section still simplified (link to sessions.md sufficient) |

---

## 6. Profiles (`OCCAM_PROFILE`)

| Aspect | Documentation |
|--------|---------------|
| **What ships** | full=15, reader=8, researcher=9, auditor=12; opt-ins ignore profile |
| **Where** | choosing-a-tool, configuration, handbook ch.18, llms.txt mcp-exposure |
| **Honesty** | Filters tools/list only — signing/playbooks/managed may still run |
| **Gap (fixed)** | Handbook reader count 7→8 |

---

## 7. Opt-in MCP tools

| Aspect | Documentation |
|--------|---------------|
| **What ships** | batch (3), watch, crosscheck, failure_atlas — env gates |
| **Where** | `experimental.md`, opt-in tool pages, mkdocs labeled nav, llms.txt Experimental table |
| **Honesty** | Limits in same breath as tool names |
| **Gap** | None |

---

## 8. Batch HTTP server

| Aspect | Documentation |
|--------|---------------|
| **What ships** | `OccamMcp.Core --batch-server` (loopback); distinct from batch MCP tools |
| **Where** | `transports.md`, experimental.md, occam_batch.md |
| **Honesty** | No auth; not via canonical launcher |
| **Gap** | None |

---

## 9. Transports

| Aspect | Documentation |
|--------|---------------|
| **What ships** | stdio (default), WebSocket, remote WSS+JWT |
| **Where** | `transports.md`, operators, handbook ch.18 |
| **Honesty** | Launcher hardcodes stdio; WS pool disruption noted |
| **Gap** | None |

---

## 10. Managed backends

| Aspect | Documentation |
|--------|---------------|
| **What ships** | Operator-configured providers after local dual-failure |
| **Where** | acquisition.md, experimental.md, configuration, trust/local-first |
| **Honesty** | Not a backend_policy; URL leaves machine; failure never surfaces as result |
| **Gap** | None |

---

## 11. Packaging / update

| Aspect | Documentation |
|--------|---------------|
| **What ships** | Release tarball, source build, npm wrapper (non-GA), Docker (limited) |
| **Where** | operators.md, INSTALL, trust/installation-safety, roadmap (developer) |
| **Honesty** | Channel matrix explicit |
| **Gap** | None |

---

## 12. Verification CLI

| Aspect | Documentation |
|--------|---------------|
| **What ships** | `OccamMcp.Core verify` — offline, manifest; `--pubkey` mandatory on CLI |
| **Where** | receipts.md, occam_verify.md, datasets.md, handbook ch.15 |
| **Honesty** | MCP vs CLI asymmetry; live/prove MCP-only |
| **Gap** | Not in quick-start path — operators must read receipts |

---

## 13. Dataset manifest verify

| Aspect | Documentation |
|--------|---------------|
| **What ships** | `verify --mode manifest --input export.json --pubkey …` |
| **Where** | datasets.md, occam_dataset_export.md, occam_verify MCP vs CLI table |
| **Honesty** | CLI-only; binds row order/leaves not semantics |
| **Gap** | Agent map (`llms.txt`) could add one line under dataset-provenance |

---

## “Not only 15 tools” — doc assets that state the full product

| Asset | Statement |
|-------|-----------|
| `docs/index.md` | “do not treat a fixed 15 as a health check” |
| `llms.txt` | 51 entrypoints via handbook ch.18 link; operator surface section |
| `docs/handbook/18-exposure.md` | Full counting method |
| `docs/operators.md` | First-class operator system |
| `mkdocs.yml` | Separate Operators, Connect, Handbook, Capabilities nav — not tool-only |

---

## Recommended follow-ups (non-blocking)

1. Add manifest-verify pointer to `llms.txt` dataset-provenance row.
2. One sentence in `quick-start.md` after first MCP success: “Offline verify and connect use the operator CLI — see Operators.”
3. Align `docs-audit/PROFILE-TOOL-MATRIX.md` with code (reader=8) on next audit sync.
