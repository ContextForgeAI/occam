// @ff-occam/mcp — TypeScript entry point and types
// Types track MCP_API_SPEC.md; the runtime client lives in ./client.ts.

export {
  MCP_PROTOCOL_VERSION,
  MCP_PROTOCOL_VERSIONS,
  OccamMcpClient,
  createClient,
} from "./client.js";
export type {
  McpClientOptions,
  McpProtocolVersion,
  McpToolDescriptor,
  McpToolList,
  ToolCallResult,
} from "./client.js";

export interface OccamTranscodeParams {
  url: string;
  backend_policy?: "http" | "browser" | "http_then_browser";
  max_tokens?: number;
  fit_markdown?: boolean;
  focus_query?: string;
  content_selectors?: string;
  session_profile?: string;
  playbook_policy?: "off" | "auto";
  if_none_match?: string;
  semantic_chunking?: boolean;
  capture_screenshot?: boolean;
  json_blocks?: boolean;
  json_tables?: boolean;
  json_feed?: boolean;
  translate_to?: string;
  diff_against?: string;
  prefer_llms_txt?: boolean;
  cache_ttl_s?: number;
  emit_capsule?: boolean;
  rank_blocks?: boolean;
  tag_trust?: boolean;
  delta_only?: boolean;
}

export interface OccamProbeParams {
  url: string;
  timeout_ms?: number;
  include_social_meta?: boolean;
  session_profile?: string;
}

export interface OccamDigestParams {
  /** Preferred native URL array; legacy JSON/CSV/newline string is deprecated but accepted during RC.2. */
  urls?: string | string[];
  backend_policy?: "http" | "browser" | "http_then_browser";
  max_urls?: number;
  per_url_max_tokens?: number;
  focus_query?: string;
  fit_markdown?: boolean;
  include_combined?: boolean;
  session_profile?: string;
  /** AF-5: auto-discover links; when set, urls is ignored. */
  source_url?: string;
  max_links?: number;
  if_none_match?: string;
}

export interface OccamMapParams {
  url: string;
  source?: "homepage" | "sitemap" | "robots";
  max_links?: number;
  same_domain?: boolean;
  filter_nonsense?: boolean;
  focus_query?: string;
  timeout_ms?: number;
  session_profile?: string;
}

export interface OccamPlaybookResolveParams {
  url: string;
  schema_version?: string;
  include_lessons?: boolean;
  fetch_site_genome?: boolean;
}

export interface OccamPlaybookHealParams {
  url: string;
  failure_reason: string;
  session_profile?: string;
  max_skeleton_nodes?: number;
}

export interface OccamPlaybookSaveParams {
  url: string;
  playbook_json: string;
  verify?: boolean;
  verify_url?: string;
  lesson_note?: string;
  failure_reason?: string;
  host_id?: string;
}

export interface OccamExtractKnowledgeParams {
  url: string;
  backend_policy?: "http" | "browser" | "http_then_browser";
  session_profile?: string;
}

// Response types
export interface OccamTranscodeSuccessResponse {
  ok: true;
  url: { url: string; finalUrl: string };
  markdown: string;
  /** Winning extractor identifier, for example node_readability_turndown or browser_playwright. */
  backend: string;
  mediaRefs?: MediaRef[];
  compile?: CompileInfo;
  session?: SessionInfo;
}

export interface OccamTranscodeFailureResponse {
  ok: false;
  url: { url: string; finalUrl: string };
  failure: {
    code: string;
    message: string;
    statusCode?: number;
    retryable?: boolean;
  };
  agentMeta?: {
    decisions: { action: string; reason: string }[];
  };
}

export type OccamTranscodeResponse = OccamTranscodeSuccessResponse | OccamTranscodeFailureResponse;

export interface MediaRef {
  url: string;
  kind: "image" | "video" | "audio" | "pdf" | "download";
  alt?: string;
  contextHeading?: string;
  selectorHint?: string;
}

export interface CompileInfo {
  tokensEstimated: number;
  truncated: boolean;
  truncationStrategy: "head_safe" | "sandwich" | null;
}

export interface SessionInfo {
  profileId: string;
  profileFound: boolean;
  headersApplied: string[];
}

// Probe types
export interface OccamProbeSuccessResponse {
  ok: true;
  url: { requested: string; final: string };
  classification: {
    pageClass: string;
    requiresJavascript: boolean;
    likelyCookieConsent: boolean;
    likelyChallenge: boolean;
    likelyLoginRequired: boolean;
    likelyPaywall: boolean;
    riskFlags: string[];
    domainTier: string;
    httpOnlyRoute: boolean;
  };
  recommendation: {
    backend: "http" | "http_then_browser" | "browser" | "none";
    estimatedLatencyMs: number;
  };
  policy: { privacyMode: string };
  statusCode: number;
  probeLatencyMs: number;
  agentHints: { suggestedNextTool: string; warnings: string[] };
  timestamp: string;
  socialMeta?: {
    title?: string;
    description?: string;
    image?: string;
    type?: string;
    siteName?: string;
    twitterCard?: string;
  };
}

export interface OccamProbeFailureResponse {
  ok: false;
  failureCode: string;
  message: string;
  statusCode?: number;
  policy: { privacyMode: string };
  probeLatencyMs: number;
  redirectChain?: string[];
}

export type OccamProbeResponse = OccamProbeSuccessResponse | OccamProbeFailureResponse;

// Digest types
export interface OccamDigestSuccessResponse {
  ok: true;
  digestId: string;
  items: DigestItem[];
  combined?: string;
  stats: {
    requested: number;
    succeeded: number;
    failed: number;
    totalTokensEstimated: number;
  };
  agentHints: {
    suggestedReadOrder: string;
    warnings: string[];
    decisions: { action: string; reason: string }[];
  };
  timestamp: string;
}

export interface DigestItem {
  url: string;
  ok: boolean;
  title?: string;
  excerpt?: string;
  backend?: string;
  tokensEstimated?: number;
  focusMatched?: boolean;
  mediaRefs?: MediaRef[];
  failure?: {
    code: string;
    message: string;
    statusCode?: number;
  };
}

export interface OccamDigestFailureResponse {
  ok: false;
  failureCode: string;
  message: string;
}

export type OccamDigestResponse = OccamDigestSuccessResponse | OccamDigestFailureResponse;

// Map types
export interface OccamMapSuccessResponse {
  ok: true;
  url: string;
  finalUrl: string;
  source: "homepage" | "sitemap" | "robots";
  links: { url: string; title?: string; path: string }[];
  linkCount: number;
  filtered: number;
  focusQuery: string | null;
  agentHints: {
    suggestedNext: string;
    maxDigestUrls: number;
    warnings: string[];
  };
  timestamp: string;
}

export interface OccamMapFailureResponse {
  ok: false;
  failureCode: string;
  message: string;
  statusCode?: number;
}

export type OccamMapResponse = OccamMapSuccessResponse | OccamMapFailureResponse;

// Playbook types
export interface OccamPlaybookResolveSuccessResponse {
  ok: true;
  url: string;
  matchedHost: string;
  playbookId: string;
  schemaVersion: string;
  provenance: "local" | "user" | "community" | "seed" | "site";
  sourcePath: string;
  contentSelectors: string[];
  preferredBackend: string;
  agentNotes: string;
  genome?: {
    site_type: string;
    page_classes: Record<string, string>;
  };
  knowledgeSchema?: Record<string, { selector: string; attr: string }>;
  pageClass?: string;
  genomeFetch?: {
    ok: boolean;
    wellKnownUrl: string;
    failureCode?: string;
  };
  lessons?: Array<{ host: string; note: string; failureReason?: string }>;
  schemaVersionWarning?: string;
  timestamp: string;
}

export interface OccamPlaybookResolveFailureResponse {
  ok: false;
  failureCode: string;
  message: string;
}

export type OccamPlaybookResolveResponse = OccamPlaybookResolveSuccessResponse | OccamPlaybookResolveFailureResponse;

export interface OccamPlaybookHealSuccessResponse {
  ok: true;
  url: string;
  failureReason: string;
  domSkeleton: {
    root: { tag: string };
    stats: { nodeCount: number; maxDepth: number; interactiveCount: number };
  };
  anchors: {
    landmarks: string[];
    dataTestIds: string[];
    mainCandidates: { selector: string; textAnchor: string; score: number }[];
  };
  agentHints: {
    suggestedNext: string;
    doNot: string[];
    maxVerifyRetries: number;
  };
}

export interface OccamPlaybookHealFailureResponse {
  ok: false;
  failureCode: string;
  message: string;
}

export type OccamPlaybookHealResponse = OccamPlaybookHealSuccessResponse | OccamPlaybookHealFailureResponse;

export interface OccamPlaybookSaveSuccessResponse {
  ok: true;
  playbookId: string;
  writtenPath: string;
  verify: {
    passesGate: boolean;
    score: number;
    noiseLeakage: number;
  };
  lessonAppended: boolean;
}

export interface OccamPlaybookSaveFailureResponse {
  ok: false;
  failureCode: string;
  message: string;
}

export type OccamPlaybookSaveResponse = OccamPlaybookSaveSuccessResponse | OccamPlaybookSaveFailureResponse;

export interface OccamExtractKnowledgeSuccessResponse {
  ok: true;
  url: string;
  playbookId: string;
  pageClass: string;
  facts: { name: string; value: string; selector: string }[];
  meta: { koId: string };
  latencyMs: number;
  backend: string;
}

export interface OccamExtractKnowledgeFailureResponse {
  ok: false;
  failureCode: string;
  message: string;
}

export type OccamExtractKnowledgeResponse = OccamExtractKnowledgeSuccessResponse | OccamExtractKnowledgeFailureResponse;
