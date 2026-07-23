// @ff-occam/mcp — MCP client wrapper
// A thin JSON-RPC-over-stdio client for the FF-Occam host.

import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { resolveHostBinary, resolveRid } from "./resolve-host-binary.mjs";
import type {
  OccamDigestResponse,
  OccamExtractKnowledgeResponse,
  OccamMapResponse,
  OccamPlaybookHealResponse,
  OccamPlaybookResolveResponse,
  OccamPlaybookSaveResponse,
  OccamProbeResponse,
  OccamTranscodeResponse,
} from "./index.js";

export const MCP_PROTOCOL_VERSIONS = [
  "2025-11-25",
  "2025-06-18",
  "2025-03-26",
  "2024-11-05",
] as const;
export type McpProtocolVersion = (typeof MCP_PROTOCOL_VERSIONS)[number];
export const MCP_PROTOCOL_VERSION: McpProtocolVersion = MCP_PROTOCOL_VERSIONS[0];
const DEFAULT_HANDSHAKE_TIMEOUT_MS = 10_000;
const DEFAULT_REQUEST_TIMEOUT_MS = 120_000;
const DEFAULT_SHUTDOWN_TIMEOUT_MS = 3_000;

export interface McpClientOptions {
  /** Explicit host executable. When omitted, OCCAM_HOME or the npm launcher is used. */
  binaryPath?: string;
  args?: string[];
  env?: Record<string, string>;
  handshakeTimeoutMs?: number;
  requestTimeoutMs?: number;
  shutdownTimeoutMs?: number;
}

export interface ToolCallResult<T> {
  ok: boolean;
  data?: T;
  error?: string;
}

export interface McpToolDescriptor {
  name: string;
  description?: string;
  inputSchema?: Record<string, unknown>;
}

export interface McpToolList {
  tools: McpToolDescriptor[];
  nextCursor?: string;
}

interface PendingRequest {
  resolve: (value: unknown) => void;
  reject: (error: Error) => void;
  timer: ReturnType<typeof setTimeout>;
}

interface McpTextContent {
  type: string;
  text?: string;
}

interface McpToolCallEnvelope {
  content?: McpTextContent[];
  structuredContent?: unknown;
  isError?: boolean;
}

interface McpInitializeResult {
  protocolVersion?: unknown;
}

interface LaunchTarget {
  command: string;
  args: string[];
}

export class OccamMcpClient {
  private process: ReturnType<typeof spawn> | null = null;
  private requestId = 0;
  private pendingRequests = new Map<number, PendingRequest>();
  private buffer = "";
  private readonly command: string;
  private readonly args: string[];
  private readonly env: NodeJS.ProcessEnv;
  private readonly handshakeTimeoutMs: number;
  private readonly requestTimeoutMs: number;
  private readonly shutdownTimeoutMs: number;
  private initialized = false;
  private startPromise: Promise<void> | null = null;
  private stopPromise: Promise<void> | null = null;
  private stopping = false;
  private negotiatedVersion: McpProtocolVersion | null = null;

  constructor(options: McpClientOptions = {}) {
    this.env = { ...process.env, ...options.env };
    const target = this.resolveLaunchTarget(options);
    this.command = target.command;
    this.args = target.args;
    this.handshakeTimeoutMs = this.normalizeTimeout(
      options.handshakeTimeoutMs,
      DEFAULT_HANDSHAKE_TIMEOUT_MS,
    );
    this.requestTimeoutMs = this.normalizeTimeout(
      options.requestTimeoutMs,
      DEFAULT_REQUEST_TIMEOUT_MS,
    );
    this.shutdownTimeoutMs = this.normalizeTimeout(
      options.shutdownTimeoutMs,
      DEFAULT_SHUTDOWN_TIMEOUT_MS,
    );
  }

  private normalizeTimeout(value: number | undefined, fallback: number): number {
    return Number.isFinite(value) && Number(value) > 0 ? Number(value) : fallback;
  }

  private resolveLaunchTarget(options: McpClientOptions): LaunchTarget {
    if (options.binaryPath) {
      return { command: options.binaryPath, args: [...(options.args ?? [])] };
    }

    const occamHome = this.env.OCCAM_HOME?.trim();
    if (occamHome) {
      const binary = resolveHostBinary(occamHome, resolveRid());
      if (binary) {
        return { command: binary, args: [...(options.args ?? [])] };
      }
      throw new Error(
        `OccamMcp.Core binary not found under OCCAM_HOME=${occamHome}. Run occam doctor first.`,
      );
    }

    const packageRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
    const launcher = resolve(packageRoot, "bin", "occam-mcp.js");
    if (existsSync(launcher)) {
      return { command: process.execPath, args: [launcher, ...(options.args ?? [])] };
    }

    throw new Error(
      "Occam MCP launcher not found. Set OCCAM_HOME or reinstall @ff-occam/mcp.",
    );
  }

  async start(): Promise<void> {
    if (this.initialized && this.process) return;
    if (this.stopPromise) await this.stopPromise;
    if (this.startPromise) return this.startPromise;

    const operation = this.startInternal();
    this.startPromise = operation;
    try {
      await operation;
    } finally {
      if (this.startPromise === operation) this.startPromise = null;
    }
  }

  private async startInternal(): Promise<void> {
    this.buffer = "";
    const child = spawn(this.command, this.args, {
      stdio: ["pipe", "pipe", "inherit"],
      env: this.env,
      windowsHide: true,
      detached: process.platform !== "win32",
    });
    this.process = child;

    child.stdout?.on("data", (data: Buffer) => {
      if (this.process !== child) return;
      this.buffer += data.toString("utf8");
      this.processBuffer();
    });
    child.once("error", (error) => this.handleProcessEnd(child, error));
    child.once("exit", (code, signal) => {
      const detail = signal ? `signal ${signal}` : `code ${code ?? "unknown"}`;
      this.handleProcessEnd(child, new Error(`MCP host exited with ${detail}`));
    });

    try {
      const initializeResult = (await this.request(
        "initialize",
        {
          protocolVersion: MCP_PROTOCOL_VERSION,
          capabilities: {},
          clientInfo: { name: "@ff-occam/mcp", version: "1.0.0-rc.2" },
        },
        this.handshakeTimeoutMs,
      )) as McpInitializeResult;
      const negotiatedVersion = initializeResult.protocolVersion;
      if (
        typeof negotiatedVersion !== "string" ||
        !MCP_PROTOCOL_VERSIONS.includes(negotiatedVersion as McpProtocolVersion)
      ) {
        throw new Error(
          `Unsupported MCP protocol version negotiated by server: ${String(negotiatedVersion)}`,
        );
      }
      await this.writeMessage({
        jsonrpc: "2.0",
        method: "notifications/initialized",
      });
      if (this.process !== child) throw new Error("MCP host exited during initialization");
      this.negotiatedVersion = negotiatedVersion as McpProtocolVersion;
      this.initialized = true;
    } catch (error) {
      await this.stop();
      throw error;
    }
  }

  private handleProcessEnd(child: ReturnType<typeof spawn>, error: Error): void {
    if (this.process !== child) return;
    this.process = null;
    this.initialized = false;
    this.negotiatedVersion = null;
    this.buffer = "";
    this.rejectPending(error);
  }

  private processBuffer(): void {
    const lines = this.buffer.split(/\r?\n/);
    this.buffer = lines.pop() ?? "";

    for (const line of lines) {
      if (!line.trim()) continue;
      try {
        const message = JSON.parse(line) as {
          id?: number;
          result?: unknown;
          error?: { message?: string };
        };
        if (message.id === undefined) continue;
        const pending = this.pendingRequests.get(message.id);
        if (!pending) continue;
        this.pendingRequests.delete(message.id);
        clearTimeout(pending.timer);
        if (message.error) {
          pending.reject(new Error(message.error.message || "MCP request failed"));
        } else {
          pending.resolve(message.result);
        }
      } catch {
        // Ignore non-JSON stdout lines. The matching request will still time out safely.
      }
    }
  }

  private request(
    method: string,
    params: Record<string, unknown>,
    timeoutMs = this.requestTimeoutMs,
  ): Promise<unknown> {
    const child = this.process;
    if (!child || this.stopping) {
      return Promise.reject(new Error("Client not started. Call start() first."));
    }

    const id = ++this.requestId;
    return new Promise((resolveRequest, rejectRequest) => {
      const timer = setTimeout(() => {
        const pending = this.pendingRequests.get(id);
        if (!pending) return;
        this.pendingRequests.delete(id);
        pending.reject(new Error(`MCP request timeout: ${method}`));
      }, timeoutMs);

      this.pendingRequests.set(id, {
        resolve: resolveRequest,
        reject: rejectRequest,
        timer,
      });

      const payload = JSON.stringify({ jsonrpc: "2.0", id, method, params }) + "\n";
      child.stdin?.write(payload, (error) => {
        if (!error) return;
        const pending = this.pendingRequests.get(id);
        if (!pending) return;
        this.pendingRequests.delete(id);
        clearTimeout(pending.timer);
        pending.reject(error);
      });
    });
  }

  private writeMessage(message: Record<string, unknown>): Promise<void> {
    const child = this.process;
    if (!child || this.stopping || !child.stdin) {
      return Promise.reject(new Error("MCP host stdin is unavailable"));
    }
    return new Promise((resolveWrite, rejectWrite) => {
      child.stdin!.write(JSON.stringify(message) + "\n", (error) => {
        if (error) rejectWrite(error);
        else resolveWrite();
      });
    });
  }

  private rejectPending(error: Error): void {
    for (const pending of this.pendingRequests.values()) {
      clearTimeout(pending.timer);
      pending.reject(error);
    }
    this.pendingRequests.clear();
  }

  get negotiatedProtocolVersion(): McpProtocolVersion | null {
    return this.negotiatedVersion;
  }

  async listTools(): Promise<McpToolList> {
    return (await this.request("tools/list", {})) as McpToolList;
  }

  async callTool<T = unknown>(name: string, params: Record<string, unknown>): Promise<T> {
    const envelope = (await this.request("tools/call", {
      name,
      arguments: params,
    })) as McpToolCallEnvelope;
    return this.unwrapToolResult<T>(name, envelope);
  }

  private unwrapToolResult<T>(name: string, envelope: McpToolCallEnvelope): T {
    if (envelope.structuredContent !== undefined) {
      if (envelope.isError) throw new Error(`MCP tool failed: ${name}`);
      return envelope.structuredContent as T;
    }

    const text = envelope.content
      ?.filter((item) => item.type === "text" && typeof item.text === "string")
      .map((item) => item.text)
      .join("\n");
    if (!text) {
      throw new Error(`MCP tool ${name} returned no text or structured content`);
    }

    let decoded: unknown = text;
    try {
      decoded = JSON.parse(text);
    } catch {
      // Preserve plain-text tool errors below; successful Occam tools return JSON.
    }
    if (envelope.isError) {
      const message =
        typeof decoded === "object" && decoded !== null && "message" in decoded
          ? String((decoded as { message: unknown }).message)
          : text;
      throw new Error(`MCP tool ${name} failed: ${message}`);
    }
    return decoded as T;
  }

  async transcode(params: {
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
  }): Promise<OccamTranscodeResponse> {
    return this.callTool("occam_transcode", params);
  }

  async probe(params: {
    url: string;
    timeout_ms?: number;
    include_social_meta?: boolean;
    session_profile?: string;
  }): Promise<OccamProbeResponse> {
    return this.callTool("occam_probe", params);
  }

  async digest(params: {
    urls?: string | string[];
    backend_policy?: "http" | "browser" | "http_then_browser";
    max_urls?: number;
    per_url_max_tokens?: number;
    focus_query?: string;
    fit_markdown?: boolean;
    include_combined?: boolean;
    session_profile?: string;
    source_url?: string;
    max_links?: number;
    if_none_match?: string;
  }): Promise<OccamDigestResponse> {
    return this.callTool("occam_digest", params);
  }

  async map(params: {
    url: string;
    source?: "homepage" | "sitemap" | "robots";
    max_links?: number;
    same_domain?: boolean;
    filter_nonsense?: boolean;
    focus_query?: string;
    timeout_ms?: number;
    session_profile?: string;
  }): Promise<OccamMapResponse> {
    return this.callTool("occam_map", params);
  }

  async playbookResolve(params: {
    url: string;
    schema_version?: string;
    include_lessons?: boolean;
    fetch_site_genome?: boolean;
  }): Promise<OccamPlaybookResolveResponse> {
    return this.callTool("occam_playbook_resolve", params);
  }

  async playbookHeal(params: {
    url: string;
    failure_reason: string;
    session_profile?: string;
    max_skeleton_nodes?: number;
  }): Promise<OccamPlaybookHealResponse> {
    return this.callTool("occam_playbook_heal", params);
  }

  async playbookSave(params: {
    url: string;
    playbook_json: string;
    verify?: boolean;
    verify_url?: string;
    lesson_note?: string;
    failure_reason?: string;
    host_id?: string;
  }): Promise<OccamPlaybookSaveResponse> {
    return this.callTool("occam_playbook_save", params);
  }

  async extractKnowledge(params: {
    url: string;
    backend_policy?: "http" | "browser" | "http_then_browser";
    session_profile?: string;
  }): Promise<OccamExtractKnowledgeResponse> {
    return this.callTool("occam_extract_knowledge", params);
  }

  async stop(): Promise<void> {
    if (this.stopPromise) return this.stopPromise;
    const operation = this.stopInternal();
    this.stopPromise = operation;
    try {
      await operation;
    } finally {
      if (this.stopPromise === operation) this.stopPromise = null;
    }
  }

  private async stopInternal(): Promise<void> {
    this.stopping = true;
    this.initialized = false;
    this.negotiatedVersion = null;
    this.rejectPending(new Error("MCP client stopped"));
    const child = this.process;
    if (!child) {
      this.stopping = false;
      return;
    }

    try {
      child.stdin?.end();
      if (!(await this.waitForExit(child, this.shutdownTimeoutMs))) {
        await this.terminateProcessTree(child);
        await this.waitForExit(child, 1_000);
      }
    } finally {
      if (this.process === child) this.process = null;
      this.buffer = "";
      this.stopping = false;
    }
  }

  private waitForExit(child: ReturnType<typeof spawn>, timeoutMs: number): Promise<boolean> {
    if (child.exitCode !== null || child.signalCode !== null) return Promise.resolve(true);
    return new Promise((resolveWait) => {
      const onExit = () => {
        clearTimeout(timer);
        resolveWait(true);
      };
      const timer = setTimeout(() => {
        child.off("exit", onExit);
        resolveWait(false);
      }, timeoutMs);
      child.once("exit", onExit);
    });
  }

  private async terminateProcessTree(child: ReturnType<typeof spawn>): Promise<void> {
    const pid = child.pid;
    if (!pid) {
      child.kill();
      return;
    }

    if (process.platform !== "win32") {
      try {
        process.kill(-pid, "SIGKILL");
      } catch {
        child.kill("SIGKILL");
      }
      return;
    }

    await new Promise<void>((resolveKill) => {
      const killer = spawn("taskkill", ["/PID", String(pid), "/T", "/F"], {
        stdio: "ignore",
        windowsHide: true,
      });
      killer.once("error", () => {
        child.kill();
        resolveKill();
      });
      killer.once("exit", () => resolveKill());
    });
  }
}

export async function createClient(options?: McpClientOptions): Promise<OccamMcpClient> {
  const client = new OccamMcpClient(options);
  await client.start();
  return client;
}
