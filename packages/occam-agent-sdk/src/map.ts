// @ff-occam/agent-sdk — Map wrapper (Recipe B link discovery)

import { OccamAgentClient, createAgentClient } from "./client.js";

export interface MapOptions {
  url: string;
  source?: "homepage" | "sitemap" | "robots";
  maxLinks?: number;
  sameDomain?: boolean;
  filterNonsense?: boolean;
  focusQuery?: string;
  timeoutMs?: number;
  sessionProfile?: string;
}

export interface MapResult {
  ok: boolean;
  url?: string;
  finalUrl?: string;
  source?: "homepage" | "sitemap" | "robots";
  links?: Array<{
    url: string;
    title?: string;
    path: string;
  }>;
  linkCount?: number;
  filtered?: number;
  focusQuery?: string | null;
  agentHints?: {
    suggestedNext: string;
    maxDigestUrls: number;
    warnings: string[];
  };
  failureCode?: string;
  message?: string;
  statusCode?: number;
}

/**
 * Live same-domain link discovery (≤64 links)
 * 
 * Example:
 * ```typescript
 * const result = await map({
 *   url: "https://nginx.org/en/docs/",
 *   source: "sitemap",
 *   focusQuery: "configuration syntax",
 *   maxLinks: 32
 * });
 * ```
 */
export async function map(options: MapOptions): Promise<MapResult> {
  const client = await createAgentClient();
  
  try {
    const result = await client.map({
      url: options.url,
      source: options.source || "homepage",
      max_links: Math.min(options.maxLinks || 32, 64),
      same_domain: options.sameDomain ?? true,
      filter_nonsense: options.filterNonsense ?? true,
      focus_query: options.focusQuery,
      timeout_ms: options.timeoutMs,
      session_profile: options.sessionProfile
    });
    
    return result as MapResult;
  } finally {
    await client.stop();
  }
}

/**
 * Filter links by path pattern
 */
export function filterLinksByPath(links: MapResult["links"], patterns: string[]): MapResult["links"] {
  if (!links) return [];
  return links.filter(link => 
    patterns.some(pattern => link.path.includes(pattern) || link.url.includes(pattern))
  );
}

/**
 * Rank links by focus query relevance (uses map's built-in BM25 if focusQuery provided)
 */
export function rankLinksByFocus(links: MapResult["links"], focusQuery: string): MapResult["links"] {
  if (!links || !focusQuery) return links || [];
  
  const terms = focusQuery.toLowerCase().split(/\s+/).filter(t => t.length > 1);
  if (terms.length === 0) return links;
  
  return [...links].sort((a, b) => {
    const scoreA = calculateRelevance(a, terms);
    const scoreB = calculateRelevance(b, terms);
    return scoreB - scoreA;
  });
}

function calculateRelevance(link: { url: string; title?: string; path: string }, terms: string[]): number {
  let score = 0;
  const haystack = `${link.url} ${link.title || ""} ${link.path}`.toLowerCase();
  
  for (const term of terms) {
    if (haystack.includes(term)) score += 1;
    if (link.path.toLowerCase().includes(term)) score += 2; // Path match weighted higher
    if (link.title?.toLowerCase().includes(term)) score += 3; // Title match weighted highest
  }
  
  return score;
}

/**
 * Pick top N links for digest (respects agentHints.maxDigestUrls)
 */
export function pickLinksForDigest(mapResult: MapResult, maxUrls = 8): string[] {
  if (!mapResult.links || mapResult.links.length === 0) return [];
  
  const maxDigest = mapResult.agentHints?.maxDigestUrls || 8;
  const limit = Math.min(maxUrls, maxDigest, mapResult.links.length);
  
  return mapResult.links.slice(0, limit).map(l => l.url);
}

/**
 * Map → Digest pipeline (Recipe B)
 */
export async function mapThenDigest(url: string, options: {
  source?: "homepage" | "sitemap" | "robots";
  focusQuery?: string;
  maxLinks?: number;
  maxUrls?: number;
  perUrlMaxTokens?: number;
  backendPolicy?: "http" | "browser" | "http_then_browser";
  sessionProfile?: string;
} = {}) {
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
