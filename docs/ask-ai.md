# Ask AI

## For AI assistants

Start with the compact map — do **not** ingest the whole documentation site:

- Repository: [`llms.txt`](https://raw.githubusercontent.com/ContextForgeAI/occam/main/llms.txt)  
- Docs site entry: [Ask AI](ask-ai.md) (this page) + task router below

Then:

1. Use the [task router](choosing-a-tool.md)  
2. Open **one** focused [tool page](tools/index.md)  
3. Open [failure codes](failure-codes.md) only after `ok: false`  
4. Open [configuration](configuration.md) only when setup is required  
5. Use [MCP API](reference/mcp-api.md) only for normative response semantics  

Runtime `tools/list` wins for tool availability and input schemas.

## For humans using an AI assistant

Paste a prompt like:

> Read https://raw.githubusercontent.com/ContextForgeAI/occam/main/llms.txt and help me use Occam to research these URLs: …

Or point the assistant at the docs site:

> https://contextforgeai.github.io/occam/

## Future: docs-aware assistant

A hosted “Ask AI” search over this documentation may arrive later. **Not shipped today.** This navigation slot is reserved so we can add it without reorganizing the site.

## Next

- [Quick Start](quick-start.md)
- [Task router](choosing-a-tool.md)
- [Examples](examples/index.md)
