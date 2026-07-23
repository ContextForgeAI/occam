// @ff-occam/agent-sdk — Research workflow (Recipe B + D + E)
// High-level: probe → map → digest → extract → heal/save

import { OccamAgentClient, createAgentClient } from "./client.js";

export interface ResearchOptions {
  /** Starting URL or URLs */
  urls: string | string[];
  /** Focus query for relevance filtering */
  focusQuery?: string;
  /** Maximum URLs to process in digest (max 8) */
  maxUrls?: number;
  /** Token budget per URL */
  perUrlMaxTokens?: number;
  /** Backend policy for extraction */
  backendPolicy?: "http" | "browser" | "http_then_browser";
  /** Session profile for authenticated requests */
  sessionProfile?: string;
  /** Whether to attempt playbook heal on failures */
  autoHeal?: boolean;
  /** Custom playbook JSON for heal-save cycle */
  playbookJson?: string;
  /** Lesson note for playbook save */
  lessonNote?: string;
}

export interface ResearchResult {
  ok: boolean;
  /** Original URLs */
  urls: string[];
  /** Probe results */
  probes: Awaited<ReturnType<OccamAgentClient["probe"]>>[];
  /** Map results (if multi-URL) */
  map?: Awaited<ReturnType<OccamAgentClient["map"]>>;
  /** Digest results */
  digest?: Awaited<ReturnType<OccamAgentClient["digest"]>>;
  /** Extracted knowledge (if playbook schema available) */
  knowledge?: Awaited<ReturnType<OccamAgentClient["extractKnowledge"]>>[];
  /** Heal/save results */
  healSave?: Array<{ url: string; heal: any; save: any }>;
  /** Combined markdown from digest */
  combinedMarkdown?: string;
  /** Error if failed */
  error?: string;
}

/**
 * Full research workflow: probe → map → digest → extract
 * 
 * Example:
 * ```typescript
 * const result = await research({
 *   urls: "https://nginx.org/en/docs/",
 *   focusQuery: "configuration syntax",
 *   maxUrls: 8
 * });
 * ```
 */
export async function research(options: ResearchOptions): Promise<ResearchResult> {
  const urls = Array.isArray(options.urls) ? options.urls : [options.urls];
  const client = await createAgentClient();
  
  try {
    // Step 1: Probe first URL for classification
    const firstUrl = urls[0];
    const probe = await client.probe({ 
      url: firstUrl, 
      include_social_meta: true,
      session_profile: options.sessionProfile
    });
    
    if (!probe.ok) {
      return {
        ok: false,
        urls,
        probes: [probe],
        error: `Probe failed: ${probe.failureCode} - ${probe.message}`
      };
    }
    
    const probes = [probe];
    
    // Step 2: Map for link discovery (if multiple URLs or focus query)
    let mapResult;
    let targetUrls = urls;
    
    if (urls.length === 1 && options.focusQuery) {
      mapResult = await client.map({
        url: firstUrl,
        source: "sitemap",
        focus_query: options.focusQuery,
        max_links: Math.min(options.maxUrls || 8, 64),
        same_domain: true,
        filter_nonsense: true,
        session_profile: options.sessionProfile
      });
      
      if (mapResult.ok && mapResult.links && mapResult.links.length > 0) {
        targetUrls = mapResult.links
          .slice(0, options.maxUrls || 8)
          .map(l => l.url);
      }
    }
    
    // Step 3: Digest
    const digest = await client.digest({
      urls: targetUrls,
      backend_policy: options.backendPolicy || "http",
      max_urls: options.maxUrls || 8,
      per_url_max_tokens: options.perUrlMaxTokens || 1024,
      focus_query: options.focusQuery,
      fit_markdown: true,
      include_combined: true,
      session_profile: options.sessionProfile
    });
    
    if (!digest.ok) {
      return {
        ok: false,
        urls,
        probes,
        map: mapResult,
        digest,
        error: `Digest failed: ${digest.failureCode} - ${digest.message}`
      };
    }
    
    // Step 4: Extract knowledge from successful items with playbook schema
    const knowledgeResults = [];
    for (const item of digest.items || []) {
      if (item.ok && item.url) {
        // Check if playbook has knowledge schema
        const resolve = await client.playbookResolve({ url: item.url });
        if (resolve.ok && resolve.knowledgeSchema) {
          const extract = await client.extractKnowledge({
            url: item.url,
            backend_policy: options.backendPolicy || "http_then_browser",
            session_profile: options.sessionProfile
          });
          if (extract.ok) {
            knowledgeResults.push(extract);
          }
        }
      }
    }
    
    // Step 5: Auto-heal on failures (optional)
    const healSaveResults = [];
    if (options.autoHeal && options.playbookJson) {
      for (const item of digest.items || []) {
        if (!item.ok && item.failure) {
          const heal = await client.playbookHeal({
            url: item.url,
            failure_reason: item.failure.code,
            session_profile: options.sessionProfile
          });
          
          if (heal.ok) {
            const save = await client.playbookSave({
              url: item.url,
              playbook_json: options.playbookJson,
              verify: true,
              lesson_note: options.lessonNote,
              failure_reason: item.failure.code
            });
            healSaveResults.push({ url: item.url, heal, save });
          }
        }
      }
    }
    
    return {
      ok: true,
      urls,
      probes,
      map: mapResult,
      digest,
      knowledge: knowledgeResults.length > 0 ? knowledgeResults : undefined,
      healSave: healSaveResults.length > 0 ? healSaveResults : undefined,
      combinedMarkdown: digest.combined
    };
  } catch (error) {
    return {
      ok: false,
      urls,
      probes: [],
      error: error instanceof Error ? error.message : String(error)
    };
  } finally {
    await client.stop();
  }
}

/**
 * Quick research - just probe + transcode (Recipe A)
 */
export async function quickResearch(url: string, options: {
  backendPolicy?: "http" | "browser" | "http_then_browser";
  maxTokens?: number;
  fitMarkdown?: boolean;
  focusQuery?: string;
  sessionProfile?: string;
} = {}) {
  const client = await createAgentClient();
  
  try {
    const result = await client.probeAndTranscode(url, {
      backend_policy: options.backendPolicy,
      max_tokens: options.maxTokens,
      fit_markdown: options.fitMarkdown,
      focus_query: options.focusQuery,
      session_profile: options.sessionProfile,
    });
    return result;
  } finally {
    await client.stop();
  }
}
