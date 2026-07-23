// @ff-occam/agent-sdk — Digest wrapper (Recipe B)

import { OccamAgentClient, createAgentClient } from "./client.js";

export interface DigestOptions {
  /** Optional when sourceUrl is set (ignored if sourceUrl is set). */
  urls?: string[];
  /** AF-5: auto-discover links from this page; when set, urls is ignored. */
  sourceUrl?: string;
  maxLinks?: number;
  backendPolicy?: "http" | "browser" | "http_then_browser";
  maxUrls?: number;
  perUrlMaxTokens?: number;
  focusQuery?: string;
  fitMarkdown?: boolean;
  includeCombined?: boolean;
  sessionProfile?: string;
  ifNoneMatch?: string;
}

export interface DigestResult {
  ok: boolean;
  digestId?: string;
  items?: Array<{
    url: string;
    ok: boolean;
    title?: string;
    excerpt?: string;
    backend?: string;
    tokensEstimated?: number;
    focusMatched?: boolean;
    mediaRefs?: Array<{
      url: string;
      kind: string;
      alt?: string;
      contextHeading?: string;
      selectorHint?: string;
    }>;
    failure?: {
      code: string;
      message: string;
      statusCode?: number;
    };
  }>;
  combined?: string;
  stats?: {
    requested: number;
    succeeded: number;
    failed: number;
    totalTokensEstimated: number;
  };
  agentHints?: {
    suggestedReadOrder: string;
    warnings: string[];
    decisions: { action: string; reason: string }[];
  };
  failureCode?: string;
  message?: string;
}

/**
 * Linear digest of multiple URLs (Recipe B)
 * 
 * Example:
 * ```typescript
 * const result = await digest({
 *   urls: [
 *     "https://nginx.org/en/docs/http/ngx_http_core_module.html",
 *     "https://nginx.org/en/docs/http/ngx_http_upstream_module.html"
 *   ],
 *   focusQuery: "load balancing",
 *   perUrlMaxTokens: 1024
 * });
 * ```
 */
export async function digest(options: DigestOptions): Promise<DigestResult> {
  const client = await createAgentClient();
  
  try {
    const result = await client.digest({
      urls: options.urls,
      source_url: options.sourceUrl,
      max_links: options.maxLinks,
      backend_policy: options.backendPolicy || "http_then_browser",
      max_urls: Math.min(options.maxUrls || 8, 8),
      per_url_max_tokens: options.perUrlMaxTokens,
      focus_query: options.focusQuery,
      fit_markdown: options.fitMarkdown ?? true,
      include_combined: options.includeCombined ?? true,
      session_profile: options.sessionProfile,
      if_none_match: options.ifNoneMatch,
    });
    
    return result as DigestResult;
  } finally {
    await client.stop();
  }
}

/**
 * Map → Digest workflow (Recipe B full)
 * 
 * Example:
 * ```typescript
 * const result = await mapAndDigest("https://nginx.org/en/docs/", {
 *   focusQuery: "configuration syntax",
 *   maxUrls: 8
 * });
 * ```
 */
export async function mapAndDigest(url: string, options: {
  source?: "homepage" | "sitemap" | "robots";
  focusQuery?: string;
  maxLinks?: number;
  maxUrls?: number;
  perUrlMaxTokens?: number;
  backendPolicy?: "http" | "browser" | "http_then_browser";
  sessionProfile?: string;
} = {}): Promise<{
  ok: boolean;
  map: any;
  digest: DigestResult | null;
}> {
  const client = await createAgentClient();
  
  try {
    return await client.mapAndDigest(url, {
      source: options.source,
      focus_query: options.focusQuery,
      max_links: options.maxLinks,
      max_urls: options.maxUrls,
      per_url_max_tokens: options.perUrlMaxTokens,
      backend_policy: options.backendPolicy,
      session_profile: options.sessionProfile,
    });
  } finally {
    await client.stop();
  }
}

/**
 * Filter digest items by focus match (honest relevance)
 */
export function filterByFocusMatch(digestResult: DigestResult): DigestResult["items"] {
  if (!digestResult.items) return [];
  return digestResult.items.filter(item => item.ok && item.focusMatched === true);
}

/**
 * Get digest items sorted by relevance (focus matched first, then by tokens)
 */
export function sortByRelevance(digestResult: DigestResult): DigestResult["items"] {
  if (!digestResult.items) return [];
  return [...digestResult.items].sort((a, b) => {
    // Focus matched first
    if (a.focusMatched && !b.focusMatched) return -1;
    if (!a.focusMatched && b.focusMatched) return 1;
    // Then by tokens (more content = more detail)
    return (b.tokensEstimated || 0) - (a.tokensEstimated || 0);
  });
}

/**
 * Extract combined markdown from digest (with agent hints check)
 */
export function getCombinedMarkdown(digestResult: DigestResult): string | null {
  if (!digestResult.ok || !digestResult.combined) return null;
  
  // Check agent hints for warnings
  const warnings = digestResult.agentHints?.warnings || [];
  if (warnings.some(w => w.includes("weak focus match"))) {
    console.warn("[digest] Weak focus match detected — prefer items[].excerpt over combined");
  }
  
  return digestResult.combined;
}
