# What happened after install?

A successful install usually looks like this:

1. Release archive downloaded and hash-verified  
2. Doctor installed worker dependencies / browser bits  
3. Smoke found the required tools for the active profile (default `reader` = **8**; `full` = **15**)
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

## PATH tip

Add `$OCCAM_HOME/scripts` to your `PATH` so `occam` works in a new terminal. Default install root: `~/.local/share/ff-occam`.

## Next

- [Quick Start](../quick-start.md) step 4 — first web read  
- [Supported hosts](../mcp-hosts.md)
