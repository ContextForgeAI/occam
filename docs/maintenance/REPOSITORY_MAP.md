# Repository map (public)

Short maintainer map of the **public** tree. For contribution rules see
[AGENTS.md](../../AGENTS.md) and [CONTRIBUTING.md](../../CONTRIBUTING.md).

| Path | Purpose |
|------|---------|
| `src/FFOccamMcp.Core/` | Native AOT MCP host (namespace `OccamMcp.Core.*`) |
| `workers/` | Node extract workers (`http-extract`, `browser-extract`, `css-extract`, `shared`) |
| `profiles/` | Playbook seeds/community + domain tiers |
| `scripts/` | Doctor, install, launch, gates, release, operator CLI |
| `skills/occam/` | Portable agent skill (also mirrored by `@ff-occam/skill`) |
| `benchmarks/l0-gate/` | Primary merge / release gate |
| `benchmarks/l0-ram-stress/` | Maintainer RAM stress harness |
| `benchmarks/rc2-regression/` | Compatibility / RC.2 contract regression |
| `corpora/` | Gate corpora, eval harness, agent prompts |
| `packages/occam-mcp/` | `@ff-occam/mcp` launcher/client |
| `packages/occam-agent-sdk/` | TypeScript agent SDK |
| `packages/occam-skill/` | `@ff-occam/skill` installer |
| `docs/` | User and contributor documentation |
| `docs/architecture/` | Durable architecture notes |
| `docs/maintenance/` | Fixture policy and maintainer maps (this folder) |
| `docs/tools/` | Per-tool pages |
| `.cursor/rules/` | Shared Cursor contributor rules |
| `.codex/config.toml.example` | Example Codex MCP wiring (copy locally; do not commit secrets) |
| `.github/workflows/` | Public GitHub Actions |

Intentionally **not** in the public snapshot (may exist only in private development trees):
experimental editor/WASM packages, Graphify skill trees, archived per-host worker recipes,
RC.2 engineering diaries, private validation packs, and publication-process audit reports.

Semantic contract notes: [semantic-contract.md](../architecture/semantic-contract.md).  
Fixture policy: [FIXTURE_SOURCES.md](FIXTURE_SOURCES.md).
