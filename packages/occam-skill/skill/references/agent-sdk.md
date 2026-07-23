# Agent SDK (no MCP tool bridge)

When the harness cannot call MCP tools directly, use `@ff-occam/agent-sdk` over a local MCP connection or embed calls in your own pipeline.

---

## Install

```bash
npm install @ff-occam/agent-sdk @ff-occam/mcp
```

Set `OCCAM_HOME` if using a git clone / tarball (not required for pure npx path when release assets resolve).

---

## Quick start

```typescript
import { createClient, transcode, research } from "@ff-occam/agent-sdk";

const client = await createClient();

const page = await transcode(client, {
  url: "https://example.com",
  fit_markdown: true,
  focusQuery: "pricing",
});

// Multi-step research helper
const report = await research(client, {
  query: "nginx reverse proxy setup",
  focusQuery: "configuration steps",
});
```

---

## Recipe mapping

| Skill recipe | SDK surface |
|--------------|-------------|
| Read one page | `transcode()` |
| Probe first | `client.probe()` or probe via MCP |
| Multi-URL digest | `digest()` |
| Map → digest | `map()` then `digest()` |
| Structured extract | `resolve()` + `extractKnowledge()` |
| Full research | `research()` |

See package README: `packages/occam-agent-sdk/README.md`.

---

## When to prefer MCP vs SDK

| Use MCP (via skill) | Use SDK |
|---------------------|---------|
| Cursor, Claude Desktop, Hermes, IDE agents | Custom Node services, CI scripts, backends |
| Harness already has tool calling | You own the orchestration loop |
| User wired `ff-occam` in mcp.json | Headless automation without IDE |

The skill's trust rules apply to both: `ok: false` → content unknown.
