# Host onboarding

Install once, connect the AI application you actually use, then run two
prompts: a tiny connection test, then the recorded documentation job.

```bash
occam connect            # live-validated hosts
occam connect --only cursor
```

Support tiers and what connect will not do: [MCP hosts](../mcp-hosts.md).
Status meanings: [After install](../connect/after-install.md).

## Live-validated hosts

These hosts are configured by `occam connect` when detected.

| Host | Connect | After connect | Where you type |
|------|---------|---------------|----------------|
| Cursor | `occam connect --only cursor` | Restart or reload if asked | **New** Cursor chat |
| Claude Desktop | `occam connect` | Restart the app | New Desktop conversation |
| Claude Code | `occam connect` | New CLI session; host lists Occam | New `claude` conversation |
| Codex CLI | `occam connect` | New CLI session; host lists Occam | New `codex` session |
| Gemini CLI | `occam connect` | New CLI session; host lists Occam | New `gemini` session |
| Hermes Agent | `occam connect --only hermes` | Continue only if Ready | New Hermes conversation |
| OpenClaw | `occam connect` | Host lists Occam | New OpenClaw conversation |

File-backed hosts (Cursor, Claude Desktop) can only confirm the file Occam
wrote. Restart is required before tools appear. CLI hosts can be checked with
the host’s own MCP list command.

## Prompts

**1. Connection test** — same on every host:

```text
Use Occam to read https://example.com/ and summarize the page. Include the
source URL.
```

Success is an `occam_*` tool call (usually `occam_transcode`) plus live
`Example Domain` text. A plausible answer with no tool call is not success.
Published-build smoke for this URL: [current proof](../examples/current-proof/).

**2. First useful job** — after the test works, open another new conversation
and use the [gallery](../examples/gallery.md) prompt (MDN Functions, scope and
closures, omissions). That capture is stamped
`ff-occam/1.1.1` on GitHub Release **v1.1.1**. The ledger's `publicBuild`
identity remains `ff-occam/1.0.0`.

## Host notes

**Cursor.** A config write does not reload tools in an already-open chat.
Restart or reload, then start a new chat. If tools are missing: `occam smoke`,
then `occam connect --only cursor`.

**Claude Desktop.** Restart the app after connect. Occam confirms
`claude_desktop_config.json` was re-read; the app loads it on the next start.

**Claude Code / Codex CLI / Gemini CLI.** Connect uses the host’s `mcp add`
path and checks that the host lists Occam. Type the prompts in a new host
session, not in the terminal that ran `occam connect`.

**Hermes Agent.** A leftover config directory is not enough. Continue only when
connect verification says Ready (or gives a concrete restart step).
**Not connected** means do not test Hermes as if Occam were available.

**OpenClaw.** Same as other CLI hosts: trust the host’s own tool list, then a
new conversation.

## Other hosts

- **Config validated** (VS Code, Cline, Roo, Windsurf, Zed, OpenCode) —
  `occam connect --only <id>`. Not written automatically until a live
  end-to-end run is recorded. [Explicit --only](../connect/explicit-only.md).
- **Assisted** (Goose, Junie) — detect + paste. [Manual connect](../connect/manual.md).
- **Model runtimes** (Ollama, llama.cpp, LM Studio, MLX) are not MCP hosts.
  Experimental local chat: [Quick Start — Ollama](../quick-start.md#path-d-local-ollama-model-experimental).

## If Occam was not used

1. `occam smoke` — local host healthy?
2. `occam connect` — follow the named host’s restart or trust step.
3. Open a **new** conversation (old chats keep a stale tool list).
4. [Connection troubleshooting](../connect/troubleshooting.md).

Missed a command or citation in a recorded job? [Feedback template](../examples/feedback.md).
