# Explicit `--only` hosts

Some hosts are **implemented** against the vendor’s published MCP config shape and covered by tests, but not yet proven end-to-end on a live desktop install. Connect will **not** write them automatically.

Name them explicitly:

```bash
occam connect --only vscode
occam connect --only zed
```

Current config-validated examples include VS Code / Copilot, Cline, Roo Code, Windsurf, Zed, and OpenCode. Always prefer the live table in [Supported hosts](../mcp-hosts.md).

## Next

- [Manual / generic MCP](manual.md)
- [Connection troubleshooting](troubleshooting.md)
