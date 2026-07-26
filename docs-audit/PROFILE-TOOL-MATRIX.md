# PROFILE-TOOL-MATRIX (code-derived)

**SoT:** `Transport/OccamToolProfile.cs`, `Transport/OccamMcpServerRegistration.cs`  
**Docs untrusted.**

## Profiles

| Profile | Valid? | Default? | Resolution |
|---------|--------|----------|------------|
| `full` | yes | **yes** (empty/unset) | Entire `OccamToolNames` (15) |
| `reader` | yes | no | 7 tools |
| `researcher` | yes | no | reader + 2 |
| `auditor` | yes | no | researcher + 3 |
| anything else | invalid | falls back to **`full`** + stderr warning | |

**Registration vs behavior:** profile gates **which tools are registered** (`WithTools` / `tools/list`). It does **not** change handler semantics of a registered tool.

## Core tools per profile

| Tool | full | reader | researcher | auditor |
|------|:----:|:------:|:----------:|:-------:|
| `occam_client_capabilities` | ✓ | ✓ | ✓ | ✓ |
| `occam_transcode` | ✓ | ✓ | ✓ | ✓ |
| `occam_probe` | ✓ | ✓ | ✓ | ✓ |
| `occam_digest` | ✓ | ✓ | ✓ | ✓ |
| `occam_map` | ✓ | ✓ | ✓ | ✓ |
| `occam_search` | ✓ | ✓ | ✓ | ✓ |
| `occam_extract_knowledge` | ✓ | ✓ | ✓ | ✓ |
| `occam_claim_check` | ✓ | — | ✓ | ✓ |
| `occam_verify` | ✓ | — | ✓ | ✓ |
| `occam_attest` | ✓ | — | — | ✓ |
| `occam_dataset_export` | ✓ | — | — | ✓ |
| `occam_playbook_lint` | ✓ | — | — | ✓ |
| `occam_playbook_resolve` | ✓ | — | — | — |
| `occam_playbook_heal` | ✓ | — | — | — |
| `occam_playbook_save` | ✓ | — | — | — |

Counts: full=15, reader=7, researcher=9, auditor=12.

## Opt-in tools (orthogonal to profile)

Registered **only** when env flag is set; **not** filtered by `OccamToolProfile.IsExposed`:

| Flag (default off) | Tools |
|--------------------|-------|
| `OCCAM_BATCH_MCP=1` | `occam_batch_submit`, `occam_batch_status`, `occam_batch_results` |
| `OCCAM_WATCH_MCP=1` | `occam_watch` |
| `OCCAM_CONSENSUS_MCP=1` | `occam_crosscheck` |
| `OCCAM_ATLAS_MCP=1` | `occam_failure_atlas` |

**Implication:** `OCCAM_PROFILE=reader` + `OCCAM_BATCH_MCP=1` → reader tools **plus** batch tools on `tools/list`.

## Also not “15 tools”

| Surface | How exposed |
|---------|-------------|
| Offline CLI | `keys export`, `verify`, `install-browser`, `lifecycle`, `version-surface` (pre-MCP) |
| Transports | stdio (default), `--mcp-server` WS, `--remote` WSS+JWT, `--batch-server` |
| MCP `instructions` | Profile-specific text via `OccamServerInstructions.TextFor` |

## Config

- `OCCAM_PROFILE` — sole profile control
- Invalid value → warn → `full`
- Server instructions follow the same profile so narrow surfaces do not advertise hidden tools

## Honest product statement

> Occam’s **default** MCP surface is **15 core tools** (`OCCAM_PROFILE=full`, opt-ins off).  
> Runtime `tools/list` can be **smaller** (profiles) or **larger** (opt-in flags).  
> Saying only “Occam has 15 tools” is incomplete.
