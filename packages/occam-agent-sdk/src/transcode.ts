// @ff-occam/agent-sdk — Transcode wrapper (Recipe A + K2 token economy)

import { OccamAgentClient, createAgentClient } from "./client.js";

export interface TranscodeOptions {
  url: string;
  backendPolicy?: "http" | "browser" | "http_then_browser";
  maxTokens?: number;
  fitMarkdown?: boolean;
  focusQuery?: string;
  contentSelectors?: string;
  sessionProfile?: string;
  playbookPolicy?: "off" | "auto";
}

export interface TranscodeResult {
  ok: boolean;
  markdown?: string;
  backend?: string;
  tokensEstimated?: number;
  truncated?: boolean;
  truncationStrategy?: "head_safe" | "sandwich" | null;
  mediaRefs?: Array<{
    url: string;
    kind: string;
    alt?: string;
    contextHeading?: string;
    selectorHint?: string;
  }>;
  session?: {
    profileId: string;
    profileFound: boolean;
    headersApplied: string[];
  };
  failure?: {
    code: string;
    message: string;
    statusCode?: number;
    retryable?: boolean;
  };
  agentMeta?: {
    decisions: { action: string; reason: string }[];
  };
}

/**
 * Transcode a single URL to Markdown with token economy (K2)
 * 
 * Example:
 * ```typescript
 * const result = await transcode({
 *   url: "https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide",
 *   backendPolicy: "http",
 *   fitMarkdown: true,
 *   focusQuery: "closures",
 *   maxTokens: 2048
 * });
 * ```
 */
export async function transcode(options: TranscodeOptions): Promise<TranscodeResult> {
  const client = await createAgentClient();
  
  try {
    const result = await client.transcode({
      url: options.url,
      backend_policy: options.backendPolicy || "http_then_browser",
      max_tokens: options.maxTokens,
      fit_markdown: options.fitMarkdown,
      focus_query: options.focusQuery,
      content_selectors: options.contentSelectors,
      session_profile: options.sessionProfile,
      playbook_policy: options.playbookPolicy || "auto"
    });
    
    return result as TranscodeResult;
  } finally {
    await client.stop();
  }
}

/**
 * Probe + Transcode (Recipe A) - uses probe recommendation for backend
 */
export async function probeAndTranscode(url: string, options: Omit<TranscodeOptions, "url"> = {}): Promise<{
  ok: boolean;
  probe: any;
  transcode: TranscodeResult | null;
}> {
  const client = await createAgentClient();
  
  try {
    return await client.probeAndTranscode(url, {
      backend_policy: options.backendPolicy,
      max_tokens: options.maxTokens,
      fit_markdown: options.fitMarkdown,
      focus_query: options.focusQuery,
      content_selectors: options.contentSelectors,
      session_profile: options.sessionProfile,
      playbook_policy: options.playbookPolicy,
    });
  } finally {
    await client.stop();
  }
}

/**
 * Transcode with automatic token budgeting for LLM context
 */
export async function transcodeForContext(url: string, contextWindowTokens: number, options: Omit<TranscodeOptions, "url" | "maxTokens"> = {}): Promise<TranscodeResult> {
  // Reserve ~20% for prompt/response overhead
  const maxTokens = Math.floor(contextWindowTokens * 0.8);
  
  return transcode({
    url,
    maxTokens,
    fitMarkdown: true,
    focusQuery: options.focusQuery,
    backendPolicy: options.backendPolicy,
    sessionProfile: options.sessionProfile,
    playbookPolicy: "auto"
  });
}
