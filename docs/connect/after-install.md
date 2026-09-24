# What happened after install?

A successful install usually looks like this:

1. Release archive downloaded and hash-verified  
2. Doctor installed worker dependencies / browser bits  
3. Smoke found the required tools for the active profile (default `reader` = **11**; `full` = **18**)
4. `occam connect` detected hosts and configured live-validated ones  
5. You were told to **restart** or complete an **action** on a named app  

## Reading the connect report

| Status | Interpretation |
|--------|----------------|
| Ready | You can ask the agent to use Occam now |
| Almost ready | Restart the named application once |
| Action required | Registration is in place; the host wants trust/approval/paste |
| Not ready | Fix Occam itself first (`occam doctor`, [troubleshooting](../troubleshooting.md)) |

**Action required ≠ broken install.**

## Installed but not Ready (multiple AI apps)

Install exit code can still be **0** when Occam itself is fine but Connect did not write host configs yet.

| Situation | What to do |
|-----------|------------|
| Exactly one live-validated host | Auto-connect during install (no prompt) |
| Several live-validated hosts (Cursor + Claude Desktop, …) | Confirm in a terminal, **or** `OCCAM_CONNECT_ONLY=cursor` (one host), **or** `OCCAM_CONNECT_ALL=1` (all Tier-A), **or** `occam connect --only cursor` |
| Pipe install on Windows (`irm \| iex`) with several hosts | Non-TTY — no silent multi-host write; set `OCCAM_CONNECT_ONLY=<id>` / `OCCAM_CONNECT_ALL=1`, or run `occam connect` interactively |
| Config-validated / Assisted hosts | Not auto; use `occam connect --only <id>` or paste guidance — see [MCP hosts](../mcp-hosts.md) |

`OCCAM_HOST` only steers legacy snippets — it is **not** a silent pre-selected connect target. Use `OCCAM_CONNECT_ONLY` for that.

## PATH tip

Add `$OCCAM_HOME/scripts` to your `PATH` so `occam` works in a new terminal. Default install root: `~/.local/share/ff-occam`.

## Next

- [Host onboarding](../onboarding/index.md) — connect command, where to type, first prompts
- [Workflow gallery](../examples/gallery.md) — first useful job after `example.com`
- [Quick Start](../quick-start.md) — path cards for Cursor, Hermes, Ollama
- [Supported hosts](../mcp-hosts.md)
