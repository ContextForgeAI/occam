# @ff-occam/skill

Portable **agent skill** for FF-Occam MCP — lazy orchestration layer any harness can load (Cursor, Claude Code, Hermes, Codex, Copilot, Kiro, Pi, Devin).

> **MCP host first:** install Occam with root [INSTALL.md](../../INSTALL.md) (canonical one-liner), then install this skill.
> Registry `npx @ff-occam/skill` applies after owner publication or in a private registry. From a checkout: `occam skill install`.

## Why skill + MCP?

MCP exposes `occam_*` tools. The skill keeps tool schemas and recipes **out of the system prompt** until the agent needs web extraction — two-stage loading like the pattern described in modern harness docs.

## Install the skill

```bash
# After Level B / clone install (OCCAM_HOME set):
occam skill install --platform all
```

| Platform | Destination (global) |
|----------|----------------------|
| `cursor` | `~/.cursor/skills/occam/` |
| `claude` | `~/.claude/skills/occam/` |
| `hermes` | `~/.hermes/skills/occam/` |
| `copilot` | `~/.copilot/skills/occam/` |
| `pi` | `~/.pi/agent/skills/occam/` |
| `devin` | `~/.config/devin/skills/occam/` |

Project scope: add `--project` (e.g. `.cursor/skills/occam` in repo).

Future registry: `npx @ff-occam/skill install --platform all` (not part of `1.0.0-rc.2`).

## Wire MCP (required)

The skill does not replace the MCP host. Use the Level B launcher from [INSTALL.md](../../INSTALL.md) / `references/install.md` — **not** `npx @ff-occam/mcp` for this RC.

See installed `references/install.md` for Cursor, Claude Desktop, Hermes, and generic stdio wiring.

## Contents

- `SKILL.md` — short card + trust model + fast tool picker
- `references/` — install, recipes, failure codes, MCP tool list, agent SDK

## License

AGPL-3.0-or-later — same as FF-Occam MCP.
