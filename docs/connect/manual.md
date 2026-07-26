# Manual / generic MCP setup

Use this only when:

- your client is not covered by connect, or  
- connect printed paste guidance (assisted hosts), or  
- you are wiring a custom automation harness  

## Generic stdio registration

| Field | Value |
|-------|-------|
| Server name | `ff-occam` (compatibility id) |
| Command | `node` |
| Args | `["$OCCAM_HOME/scripts/launch-mcp-host.mjs"]` |
| Env | `OCCAM_HOME=<install root>` |

Do **not** put LLM provider API keys in Occam’s environment.

Print a paste-ready snippet:

```bash
occam snippet
```

## Cursor / Claude Desktop examples

Prefer `occam connect` first. Manual JSON is documented under [Getting started](../getting-started.md#advanced-manual-mcp-wiring) for generic clients and contributor checkouts.

## Next

- [Supported hosts](../mcp-hosts.md)
- [Troubleshooting](troubleshooting.md)
