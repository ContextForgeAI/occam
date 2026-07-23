# @ff-occam/skill

Portable **agent skill** for FF-Occam MCP — lazy orchestration layer any harness can load (Cursor, Claude Code, Hermes, Codex, Copilot, Kiro, Pi, Devin).

> **Registry status:** `npx` commands apply after owner publication or in a configured private
> registry. From a checkout, use `occam skill install`.

## Why skill + MCP?

MCP exposes `occam_*` tools. The skill keeps tool schemas and recipes **out of the system prompt** until the agent needs web extraction — two-stage loading like the pattern described in modern harness docs.

## Install the skill

```bash
npx @ff-occam/skill install --platform all
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

From a git clone: `occam skill install` (same flags).

## Wire MCP (required)

The skill does not replace the MCP host:

```bash
npx @ff-occam/mcp
```

See installed `references/install.md` for Cursor, Claude Desktop, Hermes, and generic stdio wiring.

## Contents

- `SKILL.md` — short card + trust model + fast tool picker
- `references/` — install, recipes, failure codes, MCP tool list, agent SDK

## License

AGPL-3.0-or-later — same as FF-Occam MCP.
