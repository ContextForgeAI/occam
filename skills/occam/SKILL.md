---
name: occam
description: >-
  Live web extraction via FF-Occam MCP — transcode URLs to Markdown, multi-URL
  digest, probe, playbooks, signed receipts. Use when you need real page content
  from public URLs. Requires ff-occam MCP server wired. On ok:false content is
  unknown — never substitute model memory.
license: AGPL-3.0-or-later
metadata:
  version: "0.9.1"
  homepage: https://www.npmjs.com/package/@ff-occam/mcp
  mcp_package: "@ff-occam/mcp"
---

# FF-Occam skill

Portable orchestration layer for the **FF-Occam MCP** host. Any harness that supports skills (Cursor, Claude Code, Hermes, Codex, OpenCode, Copilot, Kiro, Pi, Devin, …) loads only this short card until the task needs web extraction — then read the references below and call MCP tools.

**Install (once):** `occam skill install --platform all` from an FF-Occam install, or copy `skills/occam/` into your harness skills directory. See [references/install.md](references/install.md).

---

## When to activate

Activate this skill when the user or task involves:

- Fetching **live** web page content (not from model memory)
- Converting HTML → Markdown for RAG, research, or citation
- Multi-URL research, sitemap discovery, structured field extraction
- Verifying claims against source pages or signed receipts
- Playbook heal/save for hard sites

**Do not** use for private URLs, CAPTCHA solving, or when MCP is not wired — read [references/install.md](references/install.md) first.

**Install tasks** (Hermes, tarball, doctor, MCP config): read [references/install.md](references/install.md) **before** any shell command. On Hermes **without .NET 10 SDK**, use `get-ff-occam.sh` — not bare `git clone`.

---

## Prerequisites

1. **MCP host installed** — Hermes/prod without .NET 10: `get-ff-occam.sh` tarball; dev with SDK: `occam doctor`. Never edit csproj to net8.0; never run in-repo `occam-mcp.js` on a git clone.
2. **MCP wired** — stdio server with **`OCCAM_HOME`** set (non-empty `env`). Hermes: `scripts/occam-wrapper.sh` + reload MCP.
3. **Smoke check** — `occam smoke`, `tools/list`, or `node scripts/hermes-smoke.mjs` → **14** `occam_*` tools, exit 0.
4. **Call discipline** — use your harness MCP tool interface (`CallMcpTool`, native tool calling, Hermes MCP bridge, etc.). Tool names are always `occam_<verb>`.

If MCP is unavailable, stop and tell the user to follow [references/install.md](references/install.md). Do not guess page content.

---

## Trust model (non-negotiable)

| Signal | Meaning | Agent action |
|--------|---------|--------------|
| `ok: true` | Live extract succeeded | Use `markdown` / structured fields; cite `url.final` |
| `ok: false` | Content **unknown** | Read `failure.code`; follow [references/failure-codes.md](references/failure-codes.md) |
| `receipt.signed` | Locally signed extraction | Optional offline check via `occam_verify` |

Never invent markdown for a failed URL. Never bypass `captcha_or_challenge` or `requires_login` without a configured `session_profile`.

---

## Fast path — pick a tool

| Goal | Tool | Notes |
|------|------|-------|
| Read one page | `occam_transcode` | Only `url` required; add `fit_markdown` + `focus_query` to save tokens |
| Cheap pre-check | `occam_probe` | Extractability score + backend hint |
| Several pages | `occam_digest` | ≤8 URLs; `focus_query` for synthesis |
| Discover URLs | `occam_map` → `occam_digest` | `source=sitemap` then `homepage` fallback |
| Web search → fetch | `occam_search` → transcode/digest | Needs `OCCAM_SEARCH_PROVIDER` |
| Structured fields | `occam_playbook_resolve` → `occam_extract_knowledge` | Schema required in playbook |
| Site-tuned extract | `occam_playbook_resolve` → `occam_transcode` | `playbook_policy=auto` (default) |
| Fix hard site | heal → lint → save | Local playbooks only |
| Cite one sentence | `occam_claim_check` | Proves block in source, not truth |
| Batch citations | `occam_attest` | Report-level `status` counts (gate on `supported`) |
| Offline receipt check | `occam_verify` | No re-fetch required in offline mode |
| Auditable URL set | `occam_dataset_export` | 1–20 URLs + manifest signature |

Full decision guide: [references/tool-picker.md](references/tool-picker.md). Copy-paste flows: [references/recipes.md](references/recipes.md).

---

## Default call patterns

### Read one article

```
occam_probe({ url })                    # optional
occam_transcode({ url, fit_markdown: true, focus_query: "…" })  # tokens optional
```

### Multi-source research

```
occam_map({ url, source: "sitemap", max_links: 8 })
occam_digest({ urls: "[…]", focus_query: "…", fit_markdown: true })
```

### Structured facts (Recipe D)

```
occam_playbook_resolve({ url })
occam_extract_knowledge({ url })        # only if resolve shows knowledge_schema
```

If no schema → use `occam_transcode` for prose.

---

## Without MCP in the harness

Use the TypeScript SDK when the host has no MCP tool bridge:

```bash
npm install @ff-occam/agent-sdk @ff-occam/mcp
```

See [references/agent-sdk.md](references/agent-sdk.md).

---

## Reference index (read on demand)

| File | Load when |
|------|-----------|
| [references/install.md](references/install.md) | Wiring MCP, doctor, host targets |
| [references/tool-picker.md](references/tool-picker.md) | Choosing among core MCP tools |
| [references/recipes.md](references/recipes.md) | Task-oriented flows |
| [references/failure-codes.md](references/failure-codes.md) | `ok: false` handling |
| [references/mcp-tools.md](references/mcp-tools.md) | Tool list + key params |
| [references/agent-sdk.md](references/agent-sdk.md) | Programmatic access without MCP |
| [references/verified-handoff.md](references/verified-handoff.md) | Trusting another agent's read via `occam_verify` (no re-fetch) |

Canonical API contract (normative): `MCP_API_SPEC.md` in the FF-Occam repo / npm package docs.

---

## Maintainer commands

```bash
occam doctor          # workers + Playwright + host binary
occam smoke           # tools/list + probe
occam snippet         # paste-ready MCP JSON for OCCAM_HOME
occam skill install   # copy this skill to harness directories
```
