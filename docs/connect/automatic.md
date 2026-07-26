# Automatic connection

```bash
occam connect
```

For **live-validated** hosts that are detected with sufficient confidence, connect:

1. Builds the Occam launch spec (`node` + `launch-mcp-host.mjs` + `OCCAM_HOME`)  
2. Registers the managed server name `ff-occam`  
3. Verifies what it can (CLI list tools, or re-read config file)  
4. Prints the status for each host  

The installer runs this for you. Re-run after installing a new AI tool.

## Safety reminders

- Unmanaged existing `ff-occam` entries are left alone  
- Backup + atomic write  
- CI / build agents do not mutate desktop configs by default  

Details: [Supported hosts](../mcp-hosts.md) · [Installation safety](../trust/installation-safety.md)

## Next

- [Explicit `--only` hosts](explicit-only.md)
- [After install](after-install.md)
