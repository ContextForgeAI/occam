// @ff-occam/agent-sdk — MCP client wrapper
// Thin wrapper around @ff-occam/mcp client with TypeScript types

import {
  OccamMcpClient as BaseClient,
  MCP_PROTOCOL_VERSION,
  MCP_PROTOCOL_VERSIONS,
  createClient as baseCreateClient,
  type McpClientOptions,
  type McpProtocolVersion,
  type McpToolDescriptor,
  type McpToolList,
  type ToolCallResult,
} from "@ff-occam/mcp";

export {
  BaseClient as OccamMcpClient,
  MCP_PROTOCOL_VERSION,
  MCP_PROTOCOL_VERSIONS,
  baseCreateClient as createClient,
};
export type {
  McpClientOptions,
  McpProtocolVersion,
  McpToolDescriptor,
  McpToolList,
  ToolCallResult,
};

type BackendPolicy = "http" | "browser" | "http_then_browser";

function supportedBackend(value: string | undefined, fallback: BackendPolicy): BackendPolicy {
  return value === "http" || value === "browser" || value === "http_then_browser"
    ? value
    : fallback;
}

// Extended client with helper methods
export class OccamAgentClient extends BaseClient {
  // Recipe A: Probe → Transcode
  async probeAndTranscode(url: string, options: {
    backend_policy?: "http" | "browser" | "http_then_browser";
    max_tokens?: number;
    fit_markdown?: boolean;
    focus_query?: string;
    content_selectors?: string;
    session_profile?: string;
    playbook_policy?: "off" | "auto";
  } = {}) {
    const probe = await this.probe({ url, timeout_ms: 10000 });
    
    if (!probe.ok) {
      return { ok: false as const, probe, transcode: null };
    }
    
    const backend =
      probe.recommendation.backend === "none"
        ? options.backend_policy || "http_then_browser"
        : probe.recommendation.backend;
    const transcode = await this.transcode({
      url,
      backend_policy: backend,
      max_tokens: options.max_tokens,
      fit_markdown: options.fit_markdown,
      focus_query: options.focus_query,
      content_selectors: options.content_selectors,
      session_profile: options.session_profile,
      playbook_policy: options.playbook_policy || "auto"
    });
    
    return { ok: true as const, probe, transcode };
  }

  // Recipe B: Map → Digest
  async mapAndDigest(url: string, options: {
    source?: "homepage" | "sitemap" | "robots";
    focus_query?: string;
    max_links?: number;
    max_urls?: number;
    per_url_max_tokens?: number;
    backend_policy?: "http" | "browser" | "http_then_browser";
    session_profile?: string;
  } = {}) {
    const mapResult = await this.map({
      url,
      source: options.source || "sitemap",
      focus_query: options.focus_query,
      max_links: options.max_links || 32,
      same_domain: true,
      filter_nonsense: true,
      session_profile: options.session_profile
    });
    
    if (!mapResult.ok || !mapResult.links?.length) {
      return { ok: false as const, map: mapResult, digest: null };
    }
    
    // Pick top N links
    const urls = mapResult.links
      .slice(0, options.max_urls || 8)
      .map(l => l.url);
    
    const digest = await this.digest({
      urls,
      backend_policy: options.backend_policy || "http",
      max_urls: options.max_urls || 8,
      per_url_max_tokens: options.per_url_max_tokens || 1024,
      focus_query: options.focus_query,
      fit_markdown: true,
      include_combined: true,
      session_profile: options.session_profile
    });
    
    return { ok: true as const, map: mapResult, digest };
  }

  // Recipe D: Resolve → Extract Knowledge
  async resolveAndExtract(url: string, options: {
    backend_policy?: "http" | "browser" | "http_then_browser";
    session_profile?: string;
  } = {}) {
    const resolve = await this.playbookResolve({
      url,
      schema_version: "1.0",
      include_lessons: false,
      fetch_site_genome: false
    });
    
    if (!resolve.ok || !resolve.knowledgeSchema) {
      return { ok: false as const, resolve, extract: null };
    }
    
    const extract = await this.extractKnowledge({
      url,
      backend_policy:
        options.backend_policy || supportedBackend(resolve.preferredBackend, "http_then_browser"),
      session_profile: options.session_profile
    });
    
    return { ok: true as const, resolve, extract };
  }

  // Recipe E: Heal → Save (interactive)
  async healAndSave(url: string, failureReason: string, options: {
    session_profile?: string;
    playbookJson?: string;
    verify?: boolean;
    lessonNote?: string;
  } = {}) {
    const heal = await this.playbookHeal({
      url,
      failure_reason: failureReason,
      session_profile: options.session_profile,
      max_skeleton_nodes: 400
    });
    
    if (!heal.ok || !options.playbookJson) {
      return { ok: false as const, heal, save: null };
    }
    
    const save = await this.playbookSave({
      url,
      playbook_json: options.playbookJson,
      verify: options.verify ?? true,
      lesson_note: options.lessonNote,
      failure_reason: failureReason
    });
    
    return { ok: true as const, heal, save };
  }
}

export async function createAgentClient(options?: McpClientOptions): Promise<OccamAgentClient> {
  const baseClient = await baseCreateClient(options);
  // Cast to our extended class
  return Object.setPrototypeOf(baseClient, OccamAgentClient.prototype);
}
