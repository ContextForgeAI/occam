// @ff-occam/agent-sdk — Main entry point
// High-level TypeScript SDK for FF-Occam MCP

export {
  OccamMcpClient,
  MCP_PROTOCOL_VERSION,
  MCP_PROTOCOL_VERSIONS,
  createClient,
  OccamAgentClient,
  createAgentClient,
  type McpClientOptions,
  type McpProtocolVersion,
  type McpToolDescriptor,
  type McpToolList,
  type ToolCallResult,
} from "./client.js";
export {
  research,
  quickResearch,
  type ResearchOptions,
  type ResearchResult,
} from "./research.js";
export {
  transcode,
  probeAndTranscode,
  transcodeForContext,
  type TranscodeOptions,
  type TranscodeResult,
} from "./transcode.js";
export {
  digest,
  mapAndDigest,
  filterByFocusMatch,
  sortByRelevance,
  getCombinedMarkdown,
  type DigestOptions,
  type DigestResult,
} from "./digest.js";
export {
  map,
  mapThenDigest,
  filterLinksByPath,
  rankLinksByFocus,
  pickLinksForDigest,
  type MapOptions,
  type MapResult,
} from "./map.js";

// Re-export types from @ff-occam/mcp
export type {
  OccamTranscodeParams,
  OccamProbeParams,
  OccamDigestParams,
  OccamMapParams,
  OccamPlaybookResolveParams,
  OccamPlaybookHealParams,
  OccamPlaybookSaveParams,
  OccamExtractKnowledgeParams,
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
} from "@ff-occam/mcp";
