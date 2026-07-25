# MCP API

The **normative** MCP response and semantics contract is the repository-root file:

**[MCP_API_SPEC.md](https://github.com/ContextForgeAI/occam/blob/main/MCP_API_SPEC.md)**

Use it for:

- Response shapes and cross-tool semantics  
- Limits and transport contract  
- Failure and receipt semantics at the protocol level  

## How it relates to other docs

| Source | Authority |
|--------|-----------|
| Runtime `tools/list` | Tool availability and **input** JSON Schema |
| `MCP_API_SPEC.md` | Response / semantic contract |
| [Per-tool pages](../tools/index.md) | Usage, recovery, examples |
| [Tools reference](../tools-reference.md) | Compact human reference |

If pages disagree, prefer the higher row and report documentation drift.

## Quick links

- [Choosing a tool](../choosing-a-tool.md)
- [Failure codes](../failure-codes.md)
- [Receipt verification](../receipt_verification.md)
- [Configuration](../configuration.md)
- [Transports](../transports.md)
