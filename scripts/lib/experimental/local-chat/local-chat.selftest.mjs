#!/usr/bin/env node
/**
 * Deterministic mocked selftests for experimental local chat (Ollama bridge).
 * No live Ollama / Occam required.
 */
import assert from "node:assert/strict";
import { EventEmitter } from "node:events";
import {
  normalizeOllamaBase,
  DEFAULT_OLLAMA_BASE,
  modelSupportsTools,
  pickDefaultToolModel,
  probeOllama,
  listModelsWithCapabilities,
  ollamaChat,
} from "./ollama-api.mjs";
import {
  allowedToolNames,
  filterListedTools,
  toOllamaTools,
  isSearchConfigured,
  MAX_TOOL_ROUNDS,
  CORE_CHAT_TOOLS,
} from "./tool-surface.mjs";
import { selectToolModel, formatNoToolsList } from "./model-select.mjs";
import {
  normalizeAssistantMessage,
  truncateToolResult,
  runChatTurn,
  detectTextualToolEnvelope,
  redactTextualToolLeakage,
} from "./chat-loop.mjs";
import { callOccamTool, closeLocalChatSession } from "./session.mjs";
import {
  ensureLocalOllamaEndpoint,
  formatTunnelStatusReport,
  parseOllamaListenTarget,
} from "./ollama-endpoint.mjs";
import { parseChatArgs } from "../../../occam-chat.mjs";
import { McpStdioClient } from "../../mcp-stdio-client.mjs";
import { chatModelTier } from "./model-select.mjs";
import { SYSTEM_PROMPT, CHAT_OLLAMA_TOOL_SPECS } from "./tool-surface.mjs";

/** @type {Array<() => Promise<void>|void>} */
const tests = [];
function test(name, fn) {
  tests.push(async () => {
    await fn();
    console.log(`ok: ${name}`);
  });
}

test("normalizeOllamaBase defaults", () => {
  assert.equal(normalizeOllamaBase(), DEFAULT_OLLAMA_BASE);
  assert.equal(normalizeOllamaBase("http://127.0.0.1:11434/"), "http://127.0.0.1:11434");
});

test("Ollama unavailable", async () => {
  const prev = globalThis.fetch;
  globalThis.fetch = async () => {
    throw Object.assign(new Error("fetch failed"), { cause: { code: "ECONNREFUSED" } });
  };
  try {
    const r = await probeOllama("http://127.0.0.1:9");
    assert.equal(r.ok, false);
    assert.match(r.error, /unreachable/i);
  } finally {
    globalThis.fetch = prev;
  }
});

test("version/tags listing with capabilities", async () => {
  const prev = globalThis.fetch;
  globalThis.fetch = async (url, init) => {
    const u = String(url);
    if (u.endsWith("/api/version")) {
      return new Response(JSON.stringify({ version: "0.30.7" }), { status: 200 });
    }
    if (u.endsWith("/api/tags")) {
      return new Response(
        JSON.stringify({
          models: [{ name: "qwen2.5:7b" }, { name: "gemma3:12b" }, { name: "llama3.1:8b" }],
        }),
        { status: 200 },
      );
    }
    if (u.endsWith("/api/show")) {
      const body = JSON.parse(String(init?.body || "{}"));
      const caps =
        body.model === "gemma3:12b"
          ? ["completion", "vision"]
          : ["completion", "tools"];
      return new Response(JSON.stringify({ capabilities: caps }), { status: 200 });
    }
    throw new Error(`unexpected ${u}`);
  };
  try {
    const probe = await probeOllama("http://127.0.0.1:11434");
    assert.equal(probe.ok, true);
    assert.equal(probe.version, "0.30.7");
    const models = await listModelsWithCapabilities("http://127.0.0.1:11434");
    assert.equal(models.length, 3);
    assert.equal(models.find((m) => m.name === "qwen2.5:7b")?.tools, true);
    assert.equal(models.find((m) => m.name === "gemma3:12b")?.tools, false);
    assert.equal(models.find((m) => m.name === "llama3.1:8b")?.tools, true);
  } finally {
    globalThis.fetch = prev;
  }
});

test("one tool-capable model auto-selects", async () => {
  const models = [
    { name: "gemma3:12b", capabilities: ["completion"], tools: false, raw: {} },
    { name: "qwen2.5:7b", capabilities: ["completion", "tools"], tools: true, raw: {} },
  ];
  const sel = await selectToolModel(models, { interactive: false });
  assert.equal(sel.ok, true);
  assert.equal(sel.model, "qwen2.5:7b");
  assert.equal(sel.auto, true);
});

test("multiple tool-capable models picks deterministic default", async () => {
  const models = [
    { name: "llama3.2:3b", capabilities: ["tools"], tools: true, raw: {} },
    { name: "llama3.1:8b", capabilities: ["tools"], tools: true, raw: {} },
    { name: "qwen2.5:7b", capabilities: ["tools"], tools: true, raw: {} },
  ];
  const picked = pickDefaultToolModel(models);
  assert.equal(picked?.name, "qwen2.5:7b");
  const sel = await selectToolModel(models, { interactive: false });
  assert.equal(sel.ok, true);
  assert.equal(sel.model, "qwen2.5:7b");
});

test("no tool-capable models honest message", async () => {
  const models = [{ name: "gemma3:12b", capabilities: ["vision"], tools: false, raw: {} }];
  const sel = await selectToolModel(models, { interactive: false });
  assert.equal(sel.ok, false);
  assert.equal(sel.code, "no_tool_models");
  assert.match(sel.message, /none of the installed models report tool support/i);
  assert.match(formatNoToolsList(models), /gemma3:12b — no tools/);
});

test("explicit --model honored / rejects no-tools", async () => {
  const models = [
    { name: "qwen2.5:7b", capabilities: ["tools"], tools: true, raw: {} },
    { name: "gemma3:12b", capabilities: [], tools: false, raw: {} },
  ];
  const ok = await selectToolModel(models, { modelFlag: "qwen2.5:7b" });
  assert.equal(ok.ok, true);
  assert.equal(ok.model, "qwen2.5:7b");
  const bad = await selectToolModel(models, { modelFlag: "gemma3:12b" });
  assert.equal(bad.ok, false);
  assert.equal(bad.code, "model_no_tools");
  const missing = await selectToolModel(models, { modelFlag: "nope" });
  assert.equal(missing.ok, false);
  assert.equal(missing.code, "model_not_found");
});

test("MCP tools/list filtering + search hidden when unavailable", () => {
  assert.equal(isSearchConfigured({}), false);
  assert.deepEqual(allowedToolNames({}), [...CORE_CHAT_TOOLS]);
  assert.ok(isSearchConfigured({ OCCAM_SEARCH_PROVIDER: "brave", OCCAM_SEARCH_API_KEY: "k" }));
  assert.equal(isSearchConfigured({ OCCAM_SEARCH_PROVIDER: "brave" }), false);
  assert.ok(isSearchConfigured({ OCCAM_SEARCH_PROVIDER: "searxng", OCCAM_SEARCH_URL: "http://x" }));

  const listed = [
    { name: "occam_transcode", description: "t", inputSchema: { type: "object" } },
    { name: "occam_probe", description: "p", inputSchema: { type: "object" } },
    { name: "occam_digest", description: "d", inputSchema: { type: "object" } },
    { name: "occam_search", description: "s", inputSchema: { type: "object" } },
    { name: "occam_map", description: "m", inputSchema: { type: "object" } },
    { name: "occam_client_capabilities", description: "c", inputSchema: { type: "object" } },
  ];
  const filtered = filterListedTools(listed, allowedToolNames({}));
  assert.deepEqual(
    filtered.map((t) => t.name),
    ["occam_transcode", "occam_probe", "occam_digest"],
  );
  const withSearch = filterListedTools(
    listed,
    allowedToolNames({ OCCAM_SEARCH_PROVIDER: "tavily", OCCAM_SEARCH_API_KEY: "k" }),
  );
  assert.ok(withSearch.some((t) => t.name === "occam_search"));
  assert.ok(!withSearch.some((t) => t.name === "occam_map"));

  const ollamaTools = toOllamaTools(filtered);
  assert.equal(ollamaTools.length, 3);
  assert.equal(ollamaTools[0].type, "function");
  assert.equal(ollamaTools[0].function.name, "occam_transcode");
  assert.equal(ollamaTools[1].function.name, "occam_probe");
  assert.equal(ollamaTools[2].function.name, "occam_digest");
  // Slim chat schemas — never ship the full MCP inputSchema blob to Ollama.
  assert.deepEqual(ollamaTools[0].function.parameters.required, ["url"]);
  assert.ok(Object.keys(ollamaTools[0].function.parameters.properties).length <= 2);
  assert.match(ollamaTools[0].function.description, /specific URL/i);
  const rawSize = JSON.stringify(ollamaTools).length;
  assert.ok(rawSize < 2500, `slim tools should stay compact, got ${rawSize}`);
});

test("parseChatArgs --model --verbose --once", () => {
  const a = parseChatArgs(["--model", "qwen2.5:7b", "--verbose", "--once", "hi"]);
  assert.equal(a.model, "qwen2.5:7b");
  assert.equal(a.verbose, true);
  assert.equal(a.once, "hi");
});

test("normalizeAssistantMessage + malformed tool name/args", async () => {
  const norm = normalizeAssistantMessage({
    role: "assistant",
    content: "",
    tool_calls: [
      {
        id: "1",
        function: { name: "occam_transcode", arguments: '{"url":"https://example.com"}' },
      },
    ],
  });
  assert.equal(norm.tool_calls?.[0].function.name, "occam_transcode");
  assert.equal(norm.tool_calls?.[0].function.arguments.url, "https://example.com");

  const session = {
    toolNames: ["occam_transcode", "occam_probe", "occam_digest"],
    client: { request: async () => ({ content: [{ type: "text", text: '{"ok":true}' }] }) },
    closed: false,
  };
  const badName = await callOccamTool(session, "occam_map", { url: "https://x" });
  assert.equal(badName.ok, false);
  assert.match(badName.text, /tool_not_allowed/);

  const badArgs = await callOccamTool(session, "occam_transcode", "not-json{");
  assert.equal(badArgs.ok, false);
  assert.match(badArgs.text, /invalid_arguments/);

  const leak = detectTextualToolEnvelope(
    'I\'ll use a tool.\n\n{"name":"occam_transcode","parameters":{"url":"https://nodejs.org/api/permissions.html"}}',
  );
  assert.equal(leak.detected, true);
  assert.equal(leak.name, "occam_transcode");
  assert.equal(detectTextualToolEnvelope("Hello!").detected, false);
  assert.equal(detectTextualToolEnvelope("{}").detected, true);
  const redacted = redactTextualToolLeakage(
    'Intro\n{"name":"occam_transcode","parameters":{"url":"https://x"}}',
  );
  assert.match(redacted, /Intro/);
  assert.doesNotMatch(redacted, /occam_transcode/);
  assert.match(SYSTEM_PROMPT, /Most turns need no tools/i);
  assert.ok(CHAT_OLLAMA_TOOL_SPECS.occam_transcode.parameters.required.includes("url"));
  assert.equal(chatModelTier("qwen2.5:7b"), "supported");
  assert.equal(chatModelTier("llama3.1:8b"), "supported");
  assert.equal(chatModelTier("llama3.2:3b"), "degraded");
});

test("no-URL tool calls are gated (not executed via MCP)", async () => {
  const prev = globalThis.fetch;
  /** @type {string[]} */
  const called = [];
  let chatN = 0;
  globalThis.fetch = async (url) => {
    if (!String(url).endsWith("/api/chat")) throw new Error(`unexpected ${url}`);
    chatN += 1;
    if (chatN === 1) {
      return new Response(
        JSON.stringify({
          message: {
            role: "assistant",
            content: "",
            tool_calls: [
              { function: { name: "occam_transcode", arguments: { url: "https://www.wolframalpha.com/x" } } },
            ],
          },
        }),
        { status: 200 },
      );
    }
    return new Response(JSON.stringify({ message: { role: "assistant", content: "323" } }), {
      status: 200,
    });
  };
  const session = {
    toolNames: ["occam_transcode", "occam_probe", "occam_digest"],
    ollamaTools: toOllamaTools([
      { name: "occam_transcode", description: "t", inputSchema: { type: "object" } },
      { name: "occam_probe", description: "p", inputSchema: { type: "object" } },
      { name: "occam_digest", description: "d", inputSchema: { type: "object" } },
    ]),
    client: {
      request: async (_m, params) => {
        called.push(params.name);
        return { content: [{ type: "text", text: '{"ok":true}' }] };
      },
    },
    closed: false,
  };
  try {
    const turn = await runChatTurn({
      baseUrl: "http://127.0.0.1:11434",
      model: "llama3.1:8b",
      messages: [{ role: "user", content: "What is 17 × 19?" }],
      session,
    });
    assert.deepEqual(called, []);
    assert.equal(turn.metrics.toolCalls, 0);
    assert.equal(turn.finalContent.trim(), "323");
  } finally {
    globalThis.fetch = prev;
  }
});

test("tool error returns controlled JSON", async () => {
  const session = {
    toolNames: ["occam_transcode"],
    client: {
      request: async () => {
        throw new Error("boom stack should not leak fully if truncated");
      },
    },
    closed: false,
  };
  const r = await callOccamTool(session, "occam_transcode", { url: "https://example.com" });
  assert.equal(r.ok, false);
  const j = JSON.parse(r.text);
  assert.equal(j.failure.code, "host_invocation_failed");
});

test("direct URL tool call + no-web + multi-round + round cap", async () => {
  const prev = globalThis.fetch;
  let chatN = 0;
  /** @type {string[]} */
  const called = [];

  globalThis.fetch = async (url, init) => {
    if (!String(url).endsWith("/api/chat")) throw new Error(`unexpected ${url}`);
    chatN += 1;
    const body = JSON.parse(String(init?.body || "{}"));
    const last = body.messages[body.messages.length - 1];

    // Turn A: direct URL → one tool then answer
    if (body.messages.some((m) => m.role === "user" && /nodejs\.org/.test(m.content))) {
      if (chatN === 1) {
        return new Response(
          JSON.stringify({
            message: {
              role: "assistant",
              content: "",
              tool_calls: [
                {
                  function: {
                    name: "occam_transcode",
                    arguments: { url: "https://nodejs.org/api/permissions.html" },
                  },
                },
              ],
            },
          }),
          { status: 200 },
        );
      }
      return new Response(
        JSON.stringify({
          message: { role: "assistant", content: "Node permission model grounded." },
        }),
        { status: 200 },
      );
    }

    // Turn B: no-web arithmetic
    if (body.messages.some((m) => m.role === "user" && /17/.test(m.content))) {
      return new Response(
        JSON.stringify({ message: { role: "assistant", content: "323" } }),
        { status: 200 },
      );
    }

    // Turn C: multi-round then cap
    if (body.messages.some((m) => m.role === "user" && /ROUNDCAP/.test(m.content))) {
      return new Response(
        JSON.stringify({
          message: {
            role: "assistant",
            content: "",
            tool_calls: [
              {
                function: {
                  name: "occam_probe",
                  arguments: { url: `https://example.com/r${chatN}` },
                },
              },
            ],
          },
        }),
        { status: 200 },
      );
    }

    void last;
    return new Response(JSON.stringify({ message: { role: "assistant", content: "ok" } }), {
      status: 200,
    });
  };

  const session = {
    toolNames: ["occam_transcode", "occam_probe", "occam_digest"],
    ollamaTools: toOllamaTools([
      { name: "occam_transcode", description: "t", inputSchema: { type: "object" } },
      { name: "occam_probe", description: "p", inputSchema: { type: "object" } },
      { name: "occam_digest", description: "d", inputSchema: { type: "object" } },
    ]),
    client: {
      request: async (_method, params) => {
        called.push(params.name);
        return {
          content: [{ type: "text", text: JSON.stringify({ ok: true, markdown: "body", tokens: { used: 12 } }) }],
        };
      },
    },
    closed: false,
  };

  try {
    chatN = 0;
    called.length = 0;
    const urlTurn = await runChatTurn({
      baseUrl: "http://127.0.0.1:11434",
      model: "qwen2.5:7b",
      messages: [
        {
          role: "user",
          content: "Read https://nodejs.org/api/permissions.html and explain it.",
        },
      ],
      session,
    });
    assert.match(urlTurn.finalContent, /permission model/i);
    assert.equal(urlTurn.metrics.toolCalls, 1);
    assert.deepEqual(called, ["occam_transcode"]);

    chatN = 0;
    called.length = 0;
    const mathTurn = await runChatTurn({
      baseUrl: "http://127.0.0.1:11434",
      model: "qwen2.5:7b",
      messages: [{ role: "user", content: "What is 17 × 19?" }],
      session,
    });
    assert.equal(mathTurn.finalContent.trim(), "323");
    assert.equal(mathTurn.metrics.toolCalls, 0);
    assert.deepEqual(called, []);

    chatN = 0;
    called.length = 0;
    const capTurn = await runChatTurn({
      baseUrl: "http://127.0.0.1:11434",
      model: "qwen2.5:7b",
      messages: [{ role: "user", content: "ROUNDCAP please keep calling tools" }],
      session,
      maxRounds: 2,
    });
    assert.equal(capTurn.metrics.roundCapHit, true);
    assert.ok(capTurn.metrics.toolCalls <= 2);
    assert.equal(MAX_TOOL_ROUNDS, 6);
  } finally {
    globalThis.fetch = prev;
  }
});

test("plain assistant JSON tool envelope is never executed", async () => {
  const prev = globalThis.fetch;
  /** @type {string[]} */
  const called = [];
  let chatN = 0;
  globalThis.fetch = async (url, init) => {
    if (!String(url).endsWith("/api/chat")) throw new Error(`unexpected ${url}`);
    chatN += 1;
    const body = JSON.parse(String(init?.body || "{}"));
    // First reply: textual tool JSON (the llama3.1 failure mode). Must NOT execute.
    if (chatN === 1) {
      return new Response(
        JSON.stringify({
          message: {
            role: "assistant",
            content:
              'I will use a tool.\n{"name":"occam_transcode","parameters":{"url":"https://nodejs.org/api/permissions.html"}}',
          },
        }),
        { status: 200 },
      );
    }
    // After nudge: still textual → blocked path
    if (body.messages.some((m) => m.role === "user" && /native tool-calling/i.test(m.content))) {
      return new Response(
        JSON.stringify({
          message: {
            role: "assistant",
            content: '{"name":"occam_transcode","parameters":{"url":"https://example.com"}}',
          },
        }),
        { status: 200 },
      );
    }
    return new Response(JSON.stringify({ message: { role: "assistant", content: "should not hit" } }), {
      status: 200,
    });
  };

  const session = {
    toolNames: ["occam_transcode", "occam_probe", "occam_digest"],
    ollamaTools: toOllamaTools([
      { name: "occam_transcode", description: "t", inputSchema: { type: "object" } },
      { name: "occam_probe", description: "p", inputSchema: { type: "object" } },
      { name: "occam_digest", description: "d", inputSchema: { type: "object" } },
    ]),
    client: {
      request: async (_method, params) => {
        called.push(params.name);
        return { content: [{ type: "text", text: '{"ok":true}' }] };
      },
    },
    closed: false,
  };

  try {
    const turn = await runChatTurn({
      baseUrl: "http://127.0.0.1:11434",
      model: "llama3.1:8b",
      messages: [{ role: "user", content: "Read https://nodejs.org/api/permissions.html" }],
      session,
    });
    assert.deepEqual(called, [], "textual JSON must never invoke MCP tools");
    assert.equal(turn.metrics.toolCalls, 0);
    assert.equal(turn.metrics.textualNudgeUsed, true);
    assert.equal(turn.metrics.textualLeakBlocked, true);
    assert.doesNotMatch(turn.finalContent, /"name"\s*:\s*"occam_transcode"/);
  } finally {
    globalThis.fetch = prev;
  }
});

test("process cleanup closes MCP client", async () => {
  let closed = 0;
  const fakeProc = new EventEmitter();
  fakeProc.stdin = { write() {}, end() {}, writable: true };
  fakeProc.stdout = new EventEmitter();
  fakeProc.stderr = new EventEmitter();
  fakeProc.killed = false;
  fakeProc.exitCode = null;
  fakeProc.kill = () => {
    fakeProc.killed = true;
    fakeProc.exitCode = 0;
    fakeProc.emit("exit", 0);
  };
  const client = new McpStdioClient(/** @type {any} */ (fakeProc));
  const session = {
    client: {
      close: async () => {
        closed += 1;
        await client.close({ graceMs: 10 });
      },
    },
    closed: false,
  };
  await closeLocalChatSession(session);
  await closeLocalChatSession(session);
  assert.equal(closed, 1);
  assert.equal(session.closed, true);
  assert.equal(fakeProc.killed, true);
});

test("truncateToolResult bounds payload", () => {
  const big = "x".repeat(30_000);
  const t = truncateToolResult(big, 100);
  assert.ok(t.length < 200);
  assert.match(t, /truncated/);
});

test("modelSupportsTools", () => {
  assert.equal(modelSupportsTools({ capabilities: ["tools"] }), true);
  assert.equal(modelSupportsTools({ capabilities: ["completion"] }), false);
  assert.equal(modelSupportsTools({}), false);
});

test("ollamaChat sets stream false", async () => {
  const prev = globalThis.fetch;
  let seen = null;
  globalThis.fetch = async (_url, init) => {
    seen = JSON.parse(String(init?.body || "{}"));
    return new Response(JSON.stringify({ message: { role: "assistant", content: "hi" } }), {
      status: 200,
    });
  };
  try {
    await ollamaChat("http://127.0.0.1:11434", { model: "x", messages: [] });
    assert.equal(seen.stream, false);
  } finally {
    globalThis.fetch = prev;
  }
});

test("tunnel helper reuses verified endpoint; report separate from chat", async () => {
  assert.deepEqual(parseOllamaListenTarget("http://127.0.0.1:11434/"), {
    host: "127.0.0.1",
    port: 11434,
  });

  const cold = await ensureLocalOllamaEndpoint({
    baseUrl: "http://127.0.0.1:9",
    startTunnel: false,
  });
  assert.equal(cold.ok, false);
  assert.equal(cold.startedTunnel, false);
  assert.equal(cold.reused, false);
  assert.match(formatTunnelStatusReport(cold), /OLLAMA ENDPOINT \/ TUNNEL STATUS/);
  assert.match(formatTunnelStatusReport(cold), /independent of Occam chat/);

  const http = await import("node:http");
  const server = http.createServer((req, res) => {
    if (req.url === "/api/version") {
      res.writeHead(200, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ version: "0.30.7" }));
      return;
    }
    res.writeHead(404);
    res.end();
  });
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  const port = /** @type {import('node:net').AddressInfo} */ (server.address()).port;
  try {
    const reused = await ensureLocalOllamaEndpoint({
      baseUrl: `http://127.0.0.1:${port}`,
      sshSpec: { host: "remote-host" },
      startTunnel: true,
    });
    assert.equal(reused.ok, true);
    assert.equal(reused.reused, true);
    assert.equal(reused.startedTunnel, false);
    assert.equal(reused.ollamaVersion, "0.30.7");
    assert.equal(reused.ownership, "existing-tunnel-or-local");
    assert.match(reused.message, /no new ssh -L/i);
    assert.match(formatTunnelStatusReport(reused), /reused: YES/);
    assert.match(formatTunnelStatusReport(reused), /startedTunnel: NO/);
  } finally {
    await new Promise((resolve) => server.close(resolve));
  }
});

test("bound-but-unhealthy port refuses duplicate ssh -L", async () => {
  const net = await import("node:net");
  const prevFetch = globalThis.fetch;
  const server = net.createServer((s) => s.end());
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  const port = /** @type {import('node:net').AddressInfo} */ (server.address()).port;
  globalThis.fetch = async () => {
    throw new Error("fetch failed");
  };
  try {
    const r = await ensureLocalOllamaEndpoint({
      baseUrl: `http://127.0.0.1:${port}`,
      sshSpec: { host: "remote-host" },
      startTunnel: true,
    });
    assert.equal(r.ok, false);
    assert.equal(r.startedTunnel, false);
    assert.match(r.message, /refusing duplicate forward/i);
  } finally {
    globalThis.fetch = prevFetch;
    await new Promise((resolve) => server.close(resolve));
  }
});

test("live MCP session cleanup kills owned launcher", async () => {
  // Skip when host binary missing (CI without publish).
  const { existsSync } = await import("node:fs");
  const { join } = await import("node:path");
  const { fileURLToPath } = await import("node:url");
  const root = join(fileURLToPath(new URL("../../../..", import.meta.url)));
  const { resolveHostBinary } = await import("../../resolve-host-binary.mjs");
  const hostPath = resolveHostBinary(root);
  if (!hostPath || !existsSync(hostPath)) {
    console.log("skip: live MCP cleanup (host binary not published)");
    return;
  }

  const { startLocalChatSession, closeLocalChatSession } = await import("./session.mjs");
  const session = await startLocalChatSession(root);
  const launcherPid = session.launcherPid;
  assert.ok(launcherPid);
  await closeLocalChatSession(session);
  // Give Windows taskkill a beat.
  await new Promise((r) => setTimeout(r, 800));
  let alive = false;
  try {
    process.kill(launcherPid, 0);
    alive = true;
  } catch {
    alive = false;
  }
  assert.equal(alive, false, `launcher pid ${launcherPid} still alive after close`);
});

let failed = 0;
for (const t of tests) {
  try {
    await t();
  } catch (err) {
    failed += 1;
    console.error(`FAIL: ${err instanceof Error ? err.stack || err.message : err}`);
  }
}

if (failed) {
  console.error(`local-chat.selftest: ${failed} failed`);
  process.exit(1);
}
console.log("local-chat.selftest: OK");
