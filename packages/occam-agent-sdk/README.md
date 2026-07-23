# @ff-occam/agent-sdk

**TypeScript SDK for FF-Occam MCP** — High-level research, transcode, digest, map, and extract workflows.

> **Package status:** `@ff-occam/agent-sdk` is **not** part of the `1.0.0-rc.2` tarball-only RC.
> Install commands below apply after a future npm publication or in a configured private registry.
> Source development uses this workspace; prefer GitHub release tarballs for the host.

## Installation

```bash
# Not part of 1.0.0-rc.2 — future / private registry only
npm install @ff-occam/agent-sdk @ff-occam/mcp
```

Requires `@ff-occam/mcp` as peer dependency (provides the MCP server binary).

## Quick Start

```typescript
import { transcode, digest, map, research, createAgentClient } from "@ff-occam/agent-sdk";

// Simple transcode (Recipe A: probe → transcode)
const result = await transcode({
  url: "https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide",
  backendPolicy: "http",
  fitMarkdown: true,
  focusQuery: "closures",
  maxTokens: 2048
});

console.log(result.markdown);
```

## Recipes

### Recipe A: Probe → Transcode
```typescript
import { probeAndTranscode } from "@ff-occam/agent-sdk";

const { probe, transcode } = await probeAndTranscode(
  "https://nginx.org/en/docs/http/ngx_http_core_module.html",
  { fitMarkdown: true, focusQuery: "directives" }
);
```

### Recipe B: Map → Digest
```typescript
import { mapAndDigest, mapThenDigest } from "@ff-occam/agent-sdk";

// Full pipeline
const { map, digest } = await mapAndDigest("https://nginx.org/en/docs/", {
  focusQuery: "load balancing",
  maxUrls: 8,
  perUrlMaxTokens: 1024
});

// Or single call
const result = await mapThenDigest("https://kubernetes.io/docs/concepts/", {
  focusQuery: "pod scheduling",
  maxUrls: 8
});
```

### Recipe D: Resolve → Extract Knowledge
```typescript
import { createAgentClient } from "@ff-occam/agent-sdk";

const client = await createAgentClient();
const { resolve, extract } = await client.resolveAndExtract(
  "https://kubernetes.io/docs/concepts/overview/"
);
await client.stop();
```

### Recipe E: Heal → Save (Interactive)
```typescript
const { heal, save } = await client.healAndSave(
  "https://example.com/failed-page",
  "thin_extract",  // failure reason from transcode
  { 
    playbookJson: `{"schema_version":"1.0","id":"example.com","hosts":["example.com"],"extract":{"selectors":["main"]}}`,
    lessonNote: "Added main selector for thin extract fix"
  }
);
```

## Full Research Workflow

```typescript
import { research } from "@ff-occam/agent-sdk";

const result = await research({
  urls: "https://nginx.org/en/docs/",
  focusQuery: "configuration syntax",
  maxUrls: 8,
  perUrlMaxTokens: 1024,
  backendPolicy: "http",
  autoHeal: true,
  playbookJson: customPlaybookJson,  // optional
  lessonNote: "Fixed config page extraction"
});

// Results include:
// - probes: classification & recommendation
// - map: discovered links (if applicable)
// - digest: multi-URL extraction with combined markdown
// - knowledge: structured facts from playbook schemas
// - healSave: playbook heal/save cycles
```

## API Reference

### `transcode(options)`
Single URL to Markdown with token economy (K2).

```typescript
transcode({
  url: string;
  backendPolicy?: "http" | "browser" | "http_then_browser";
  maxTokens?: number;        // Token budget (min 128)
  fitMarkdown?: boolean;     // BM25 prune (default: false)
  focusQuery?: string;       // Focus keywords for pruning
  contentSelectors?: string; // JSON array or comma-separated
  sessionProfile?: string;   // Session headers profile
  playbookPolicy?: "off" | "auto";  // Auto-apply playbook (default: auto)
})
```

### `digest(options)`
Linear multi-URL digest (≤8 URLs).

```typescript
digest({
  urls: string[];
  backendPolicy?: "http" | "browser" | "http_then_browser";
  maxUrls?: number;           // Cap 8
  perUrlMaxTokens?: number;   // Min 128
  focusQuery?: string;        // Focus for BM25 + focusMatched
  fitMarkdown?: boolean;      // Default true
  includeCombined?: boolean;  // Default true
  sessionProfile?: string;
})
```

### `map(options)`
Live same-domain link discovery (≤64 links).

```typescript
map({
  url: string;
  source?: "homepage" | "sitemap" | "robots";
  maxLinks?: number;          // Cap 64
  sameDomain?: boolean;       // Default true
  filterNonsense?: boolean;   // Default true
  focusQuery?: string;        // BM25 rank
  timeoutMs?: number;         // 3000-30000
  sessionProfile?: string;
})
```

### `research(options)`
Full research pipeline: probe → map → digest → extract → heal/save.

```typescript
research({
  urls: string | string[];
  focusQuery?: string;
  maxUrls?: number;           // Max 8
  perUrlMaxTokens?: number;
  backendPolicy?: "http" | "browser" | "http_then_browser";
  sessionProfile?: string;
  autoHeal?: boolean;         // Attempt heal on failures
  playbookJson?: string;      // For heal-save cycle
  lessonNote?: string;        // For playbook lessons
})
```

## TypeScript Types

All types are exported and match the MCP API spec exactly:

```typescript
import type {
  OccamTranscodeResponse,
  OccamProbeResponse,
  OccamDigestResponse,
  OccamMapResponse,
  OccamPlaybookResolveResponse,
  OccamPlaybookHealResponse,
  OccamPlaybookSaveResponse,
  OccamExtractKnowledgeResponse,
  MediaRef,
  CompileInfo,
  SessionInfo
} from "@ff-occam/agent-sdk";
```

## MCP Server Management

The SDK auto-starts the MCP server via `@ff-occam/mcp`. For long-running processes:

```typescript
import { createAgentClient, OccamAgentClient } from "@ff-occam/agent-sdk";

// Reuse single client for multiple calls
const client = await createAgentClient();
try {
  const tools = await client.listTools();
  const result1 = await client.transcode({ url: "https://a.example.com" });
  const result2 = await client.digest({ urls: ["https://b.example.com", "https://c.example.com"] });
} finally {
  await client.stop();  // Graceful, idempotent shutdown
}
```

Client creation completes the MCP initialize handshake, validates the server-selected revision, and
exposes it as `client.negotiatedProtocolVersion`. Typed methods return decoded Occam JSON;
`callTool<T>(name, arguments)` remains available for opt-in and newly added tools. For a clone,
set `OCCAM_HOME` after running `occam doctor` so the RID-specific AOT publish is discovered.

## Environment Variables

| Variable | Purpose |
|----------|---------|
| `OCCAM_HOME` | Use local build instead of downloading |
| `OCCAM_RELEASE_BASE_URL` | Forge release base (default matches `get-ff-occam.sh`) |

## Documentation

- **MCP API Spec**: https://github.com/ContextForgeAI/occam/blob/main/MCP_API_SPEC.md
- **Choosing a tool**: https://github.com/ContextForgeAI/occam/tree/main/docs/choosing-a-tool.md
- **Recipes**: https://github.com/ContextForgeAI/occam/tree/main/docs/recipes.md
- **Failure codes**: https://github.com/ContextForgeAI/occam/blob/main/docs/failure-codes.md

## License

AGPL-3.0-or-later.
