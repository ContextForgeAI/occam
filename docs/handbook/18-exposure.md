# Chapter 18 — Exposure: 51 entrypoints, profile-scoped tools

**Status:** STABLE · **Prerequisites:** [Chapter 3](03-standing-up-an-install.md)

---

## Mental model

**Product capability ≠ MCP tool count.** Product default is `OCCAM_PROFILE=reader` (8 tools).
"15 tools" is the `OCCAM_PROFILE=full` stdio `tools/list` with opt-in flags off — one exposure
slice of **51 named entrypoints**, not the whole product.

---

## Explanation

### Counting method

Count every distinct **named invocation** a user, agent, or operator can start from shipped code without writing new code:

| Bucket | Count |
|--------|------:|
| Core MCP tools | 15 |
| Opt-in MCP tools | 6 |
| Host offline CLI verbs | 5 |
| Operator `occam <sub>` | 13 |
| Installer / bootstrap | 4 |
| Alternate process modes | 3 |
| Package/API bins | 3 |
| Docker ENTRYPOINT | 1 |
| **Total** | **51** |

Connect's ~15 host adapters are **mechanisms under** `occam connect`, not separate entrypoints.

### Fifteen core MCP tools

Registered in `OccamMcpServerRegistration.OccamToolNames`: client_capabilities, transcode, probe, digest, playbook_resolve, map, playbook_heal, playbook_save, extract_knowledge, search, verify, claim_check, attest, playbook_lint, dataset_export.

An agent on `full` can read pages, probe/map/search, extract knowledge, author playbooks, and run core trust flows — but **cannot** watch/batch/crosscheck/atlas/browser_interact, install/connect hosts, or reach WS/Remote/BatchServer via the canonical launcher without extra flags/modes. The product default `reader` omits heal/save and several trust/authoring tools.

### Profiles change exposure, not semantics

| Profile | Tools exposed |
|---------|--------------:|
| `reader` (**default**) | 8 |
| `researcher` | 9 |
| `auditor` | 12 |
| `full` | 15 |
| invalid value | → `reader` + stderr warning |

Profiles filter **tools/list**, not handler behavior. A `reader` deployment still mints a key, still signs eligible successes, still applies playbook overlays, and may still use a managed provider. Opt-in tools ignore profiles entirely.

**Note (Phase 6):** `reader` now exposes `occam_verify` — earlier produce-without-verify trap is mitigated; still verify pubkey and profile expectations explicitly.

### Transports

| Mode | How to start | Canonical launcher |
|------|--------------|-------------------|
| stdio (default) | `launch-mcp-host.mjs` | **Yes** — forwards no args |
| WebSocket | `--mcp-server` | No — not via launcher |
| Remote WSS+JWT | `--remote` | No |
| BatchServer HTTP | `--batch-server` | No |
| Streamable HTTP | `--mcp-http` / `--streamable-http` (port 5055) | No |

Local WS has no session semaphore; each socket builds a DI container that may kill the shared browser pool. BatchServer has no auth (loopback only). Streamable HTTP is loopback-first. Banner may claim stdio while running WS or Remote.

### Operator surface is first-class

Install, doctor, connect, session, refresh, and host-binary offline verbs (`keys export`, `verify`) are product entrypoints — not "advanced extras." The `occam` wrapper does not route `verify` or `keys`; use the host binary directly.

### npm is a public experimental RC

`ff-occam` is the published primary npm package and wraps the lower-level `@ff-occam/mcp` runtime at the same version. The channel is public but experimental; do not present it as GA. The guarded release install remains the Cosign-policy-aware GitHub Release bootstrap.

---

## CHECK

**LOCAL.** Start the host four times, once per valid `OCCAM_PROFILE` value (`full`, `reader`, `researcher`, `auditor`). Record `tools/list` each time. Start with an invalid profile and observe fallback to `reader` plus stderr warning.

---

## Common misconception

**"Profiles are a security boundary."** Profiles change *exposure*, never handler semantics or signing policy. Opt-in tools are not profile-filtered at all.

---

## Limitations

- 8/51 ≈ 16% of named entrypoints are exposed by the default `reader` profile; the `full` profile exposes 15/51 ≈ 29% before opt-in gates.
- Canonical launcher is stdio-only; WS/Remote/BatchServer require direct binary flags.
- Server instructions may mention tools (e.g. watch) without stating env gates — always check registration code and env.
- `model_id` and `suggestedProfile` from client_capabilities are stored and echoed but not consumed by the host.
- npm remains an experimental RC channel. The Cosign-policy-aware GitHub Release bundle is the guarded public RC path; neither channel becomes GA until `1.0.0` ships.

---

## Links

- [Chapter 3 — Standing up an install](03-standing-up-an-install.md)
- [Chapter 17 — Opt-in surfaces](17-opt-in-surfaces.md)
- [Chapter 19 — Operating an install](19-operating-an-install.md)
- [appendix-status-labels.md](appendix-status-labels.md)
- User docs: [MCP hosts](../mcp-hosts.md) · [Transports](../transports.md) · [Connect](../connect/index.md)
- Audit: `docs-audit/ENTRYPOINT-MODEL.md`
