# MCP hosts — what connects automatically

`occam connect` finds the AI tools installed on your machine and registers Occam
as an MCP server with the ones it can configure safely. It never guesses: a host
is only auto-configured when we have run the whole cycle — detect, write,
reload, and confirm the host reports Occam back.

```bash
occam connect            # detect and connect live-validated hosts
occam connect --only zed # configure one config-validated host explicitly
```

Everything below reflects what has been tested, not what is theoretically
possible.

## Live validated (automatic)

These hosts are configured by `occam connect` with no flags.

| Host | How Occam is registered | Confirmation |
|------|------------------------|--------------|
| Hermes Agent | its own CLI | host lists Occam and its tools |
| OpenClaw | its own CLI | host lists Occam and its tools |
| Claude Code | `claude mcp add` | host lists Occam and its tools |
| Codex CLI | `codex mcp add` | host lists Occam and its tools |
| Gemini CLI | `gemini mcp add` | host lists Occam and its tools |
| Cursor | user `mcp.json` | file re-read after write; Cursor restart activates it |
| Claude Desktop | `claude_desktop_config.json` | file re-read after write; app observed loading Occam after restart |

Hosts registered through their own CLI can be checked by asking the host to list
its servers, so `occam connect` reports what the host itself says. For
configuration-file hosts, Occam can only confirm the file it wrote — the host
loads it on the next start.

## Config validated (explicit `--only`)

| Host | Config file | Root key |
|------|------------|----------|
| VS Code / Copilot | user `mcp.json` | `servers` |
| Cline | extension `cline_mcp_settings.json` | `mcpServers` |
| Roo Code | extension `mcp_settings.json` | `mcpServers` |
| Windsurf | `~/.codeium/windsurf/mcp_config.json` | `mcpServers` |
| Zed | `settings.json` | `context_servers` |
| OpenCode | `opencode.json` | `mcp` |

These are implemented against each vendor's published configuration reference
and covered by tests, but no end-to-end run against a live install has been
recorded yet. Until one has, they are not written automatically:

```bash
occam connect --only vscode
```

## Assisted (manual paste)

**Goose** stores extensions in YAML, and **Junie** has no configuration path we
can write safely. `occam connect` detects both, then prints the launch command
for you to paste — Goose through `goose configure → Add Extension (stdio)`,
Junie through its MCP settings.

## Model runtimes are not MCP hosts

Ollama, llama.cpp, LM Studio and MLX are reported when found so you know Occam
saw them, but they serve models — they do not consume MCP tools, so they never
receive a registration. An install that finds only a runtime is still a
successful install.

## What connect will not do to your config

- **Never overwrites someone else's entry.** An existing `ff-occam` entry that
  Occam did not create is left alone and reported; `--force` is required to
  replace it.
- **Backs up before writing** and writes atomically, so an interrupted write
  cannot leave a half-file.
- **Refuses unreadable or commented configs.** A file with `//` comments (VS
  Code and Zed allow them) is never rewritten, because strict JSON output would
  delete the comments. Connect explains this and asks you to add the entry.
- **Undoes a broken registration.** If a host ends up with an entry it cannot
  use, Occam removes it — and if the host rewrote the file meanwhile, only our
  entry is removed.
- **Keeps registrations that only need you.** A host that is configured but
  waiting on a restart or a trust prompt keeps its entry instead of being rolled
  back.
- **Does not touch desktop configs in CI.** On a build server connect reports
  what it found and changes nothing, unless `OCCAM_CONNECT_FORCE=1` is set.
- **Makes no network calls.** Connect only reads and writes local files and
  starts the local Occam server to verify it responds.

## Reading the result

| Status | Meaning |
|--------|---------|
| `Ready` | Every host is configured and confirmed |
| `Almost ready` | Configured; restart the named host once |
| `Action required` | Valid registration blocked by the host — trust a folder, choose a config, or paste an entry |
| `Not ready` | Occam itself could not start; nothing was written |

`Action required` is not a failure of the install: the registration is in place,
and the host needs one action from you.

## Adding a host

Adapters are host profiles over a shared engine — a JSON config-file host needs
a path, a root key and, when the entry shape differs, a small codec. See
[AGENTS.md](https://github.com/ContextForgeAI/occam/blob/main/AGENTS.md) for repository conventions and `scripts/lib/operator/connect/`
for the existing profiles.
