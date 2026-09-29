# Chapter 18 — Exposure: entrypoints and profile-scoped tools

**Status:** STABLE · **Prerequisites:** [Chapter 3](03-standing-up-an-install.md)  
**Verified counts:** 2026-09-29 against `OccamToolNames` / `OccamToolProfile` (doc rescue Phase 6)

---

## Mental model

**Product capability ≠ MCP tool count.** Product default is `OCCAM_PROFILE=reader` (**11** tools).
"**18** tools" is the `OCCAM_PROFILE=full` stdio `tools/list` with opt-in flags off — one MCP exposure
slice of a larger product surface (CLI, install, transports), not the whole product.

Runtime `tools/list` always wins for what a given host exposes.

---

## Explanation

### MCP tool counts (authoritative)

| Bucket | Count |
|--------|------:|
| Core MCP tools (`OccamToolNames`) | **18** |
| Opt-in MCP tools (env-gated) | **8** (batch×3, watch, crosscheck, atlas, browser_interact, exam_submit) |

**Eighteen core MCP tools:** `occam_client_capabilities`, `occam`, `occam_transcode`, `occam_probe`,
`occam_digest`, `occam_playbook_resolve`, `occam_map`, `occam_playbook_heal`, `occam_playbook_save`,
`occam_extract_knowledge`, `occam_search`, `occam_verify`, `occam_claim_check`, `occam_attest`,
`occam_playbook_lint`, `occam_dataset_export`, `occam_canary_issue`, `occam_canary_verify`.

An agent on `full` can read pages, probe/map/search, extract knowledge, author playbooks, and run
core trust flows — but **cannot** use watch/batch/crosscheck/atlas/browser_interact/exam_submit
without the matching `OCCAM_*_MCP` flags. The product default `reader` omits heal/save/resolve and
several authoring/attest tools.

### Profiles change exposure, not semantics

| Profile | Tools exposed |
|---------|--------------:|
| `minimal` | 1 |
| `basic` | 3 |
| `reader` (**default**) | **11** |
| `researcher` | **12** |
| `auditor` | **15** |
| `full` | **18** |
| invalid value | → `reader` + stderr warning |

Source: `Transport/OccamToolProfile.cs`. Profiles filter **tools/list**, not handler behavior.
Opt-in tools ignore profiles entirely when their env gate is on.

`reader` includes `occam_verify` and canary tools — verify pubkey and profile expectations explicitly.

### Transports

| Mode | How to start | Canonical launcher |
|------|--------------|-------------------|
| stdio (default) | `launch-mcp-host.mjs` | **Yes** — forwards no args |
| WebSocket | `--mcp-server` | No — not via launcher |
| Remote WSS+JWT | `--remote` | No |
| BatchServer HTTP | `--batch-server` | No |
| Streamable HTTP | `--mcp-http` / `--streamable-http` | No |

Local WS has no session semaphore; each socket builds a DI container that may kill the shared browser
pool. BatchServer has no auth (loopback only). Streamable HTTP is loopback-first.

### Operator surface is first-class

Install, doctor, connect, session, refresh, and host-binary offline verbs (`keys export`, `verify`)
are product entrypoints — not "advanced extras."

### npm is public experimental

`ff-occam` wraps `@ff-occam/mcp` at the same version. Experimental — not the guarded GA install path
([INSTALL.md](../install.md)).

---

## CHECK

**LOCAL.** Start the host for each of `minimal`, `basic`, `reader`, `researcher`, `auditor`, `full`.
Record `tools/list` each time. Start with an invalid profile and observe fallback to `reader` plus
stderr warning.

---

## Common misconception

**"Profiles are a security boundary."** Profiles change *exposure*, never handler semantics or
signing policy. Opt-in tools are not profile-filtered at all.

---

## Limitations

- Default `reader` is **11/18** of the core catalog (~61%); `full` is all **18** before opt-in gates.
- Canonical launcher is stdio-only; WS/Remote/BatchServer require direct binary flags.
- Server instructions may mention tools without stating env gates — check registration + env.
- `model_id` / `suggestedProfile` from client_capabilities are advisory echoes unless the operator acts.
- Prefer [tools/index](../tools/index.md) over older “51 entrypoints” inventories when counting MCP tools.

---

## Links

- [Tools index](../tools/index.md) · [Configuration](../configuration.md) · [MCP map](../MCP_MAP.md)
- [Why Occam](../why-occam.md) · [Experimental](../experimental.md)
