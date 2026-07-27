#!/usr/bin/env node
/**
 * Experimental live friend-value harness for `occam chat` + local Ollama.
 *
 * Tunnel / endpoint status is ALWAYS printed separately from chat PASS/FAIL.
 * Reuses an existing verified 127.0.0.1:11434 endpoint; never starts a duplicate
 * ssh -L when the port is already bound and healthy.
 *
 * Usage:
 *   node scripts/lib/experimental/local-chat/live-friend-test.mjs
 *   node scripts/lib/experimental/local-chat/live-friend-test.mjs --model qwen2.5:7b
 *   node scripts/lib/experimental/local-chat/live-friend-test.mjs --ssh some-host
 *
 * Exit 0 only when chat cases pass. Tunnel issues alone do not imply chat regression
 * when a reused endpoint still served successful chat turns.
 */
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import {
  ensureLocalOllamaEndpoint,
  formatTunnelStatusReport,
} from "./ollama-endpoint.mjs";
import { listModelsWithCapabilities } from "./ollama-api.mjs";
import { selectToolModel } from "./model-select.mjs";
import {
  startLocalChatSession,
  closeLocalChatSession,
  formatToolBanner,
} from "./session.mjs";
import { runChatTurn } from "./chat-loop.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "../../../..");

/**
 * @param {string[]} argv
 */
function parseArgs(argv) {
  /** @type {{ model: string|null, sshHost: string|null, baseUrl: string|null, json: boolean }} */
  const out = { model: null, sshHost: null, baseUrl: null, json: false };
  const args = [...argv];
  while (args.length) {
    const a = args.shift();
    if (a === "--model" || a === "-m") out.model = args.shift() ?? null;
    else if (a?.startsWith("--model=")) out.model = a.slice("--model=".length);
    else if (a === "--ssh") out.sshHost = args.shift() ?? null;
    else if (a?.startsWith("--ssh=")) out.sshHost = a.slice("--ssh=".length);
    else if (a === "--host") out.baseUrl = args.shift() ?? null;
    else if (a?.startsWith("--host=")) out.baseUrl = a.slice("--host=".length);
    else if (a === "--json") out.json = true;
    else if (a === "-h" || a === "--help") out.help = true;
    else throw new Error(`Unknown flag: ${a}`);
  }
  return out;
}

const CASES = [
  {
    id: "NO_WEB",
    prompt: "What is 17 * 19? Reply with only the number.",
    /** @param {{ finalContent: string, metrics: { toolCalls: number } }} r */
    check(r) {
      const n = r.finalContent.trim();
      const answerOk = n === "323" || /\b323\b/.test(n);
      const toolsOk = r.metrics.toolCalls === 0;
      return {
        pass: answerOk && toolsOk,
        detail: `answer=${JSON.stringify(n)} occamCalls=${r.metrics.toolCalls}`,
      };
    },
  },
  {
    id: "DIRECT_URL",
    prompt:
      "Read: https://nodejs.org/api/permissions.html Explain the current Node.js permission model. Use web tools if needed and do not invent content.",
    /** @param {{ finalContent: string, metrics: { toolCalls: number, occamTools: string[] }, occamCalls: object[] }} r */
    check(r) {
      const calls = r.metrics.toolCalls;
      const chose = calls > 0;
      const bounded = calls <= 3;
      const grounded =
        /permission/i.test(r.finalContent) &&
        r.finalContent.trim().length > 80 &&
        !/i don't have access|cannot browse|invent/i.test(r.finalContent);
      return {
        pass: chose && bounded && grounded,
        detail: `occamCalls=${calls} tools=${r.metrics.occamTools.join(",") || "(none)"} grounded=${grounded}`,
      };
    },
  },
  {
    id: "MULTI_PAGE",
    prompt:
      "Compare these two pages and summarize how they relate: https://nodejs.org/api/permissions.html and https://nodejs.org/api/process.html — use web tools; do not invent.",
    /** @param {{ finalContent: string, metrics: { toolCalls: number, occamTools: string[], rounds: number } }} r */
    check(r) {
      const chose = r.metrics.toolCalls > 0;
      const usedDigestOrMulti =
        r.metrics.occamTools.includes("occam_digest") || r.metrics.toolCalls >= 2;
      return {
        pass: chose,
        detail: `occamCalls=${r.metrics.toolCalls} tools=${r.metrics.occamTools.join(",") || "(none)"} rounds=${r.metrics.rounds} digestOrMulti=${usedDigestOrMulti}`,
      };
    },
  },
];

async function main() {
  const args = parseArgs(process.argv.slice(2));
  if (args.help) {
    console.log(`Usage: node live-friend-test.mjs [--model NAME] [--ssh HOST] [--host URL] [--json]`);
    process.exit(0);
  }

  process.env.OCCAM_HOME = process.env.OCCAM_HOME?.trim() || repoRoot;
  process.env.OCCAM_BANNER = "0";
  process.env.WT_OCCAM_BANNER = "0";

  /** @type {import('./session.mjs').LocalChatSession | null} */
  let session = null;
  const cleanup = async () => {
    await closeLocalChatSession(session);
    session = null;
  };
  process.once("SIGINT", () => {
    void cleanup().finally(() => process.exit(130));
  });

  const endpoint = await ensureLocalOllamaEndpoint({
    baseUrl: args.baseUrl || undefined,
    startTunnel: Boolean(args.sshHost),
    sshSpec: args.sshHost ? { host: args.sshHost } : undefined,
  });

  // ALWAYS separate from chat results.
  console.error("");
  console.error(formatTunnelStatusReport(endpoint));
  console.error("");

  if (!endpoint.ok) {
    console.error("CHAT HARNESS: SKIPPED (no usable Ollama endpoint)");
    console.error("Tunnel/endpoint failure is NOT automatically an Occam chat regression.");
    process.exitCode = 2;
    return;
  }

  const models = await listModelsWithCapabilities(endpoint.baseUrl);
  const selection = await selectToolModel(models, {
    modelFlag: args.model,
    interactive: false,
  });
  if (!selection.ok) {
    console.error(selection.message);
    process.exitCode = 1;
    await cleanup();
    return;
  }

  session = await startLocalChatSession(process.env.OCCAM_HOME);
  console.error(`Model: ${selection.model}`);
  console.error(`Web tools: ${formatToolBanner(session)}`);
  console.error("");

  /** @type {Array<{ id: string, pass: boolean, detail: string, metrics: object, answerPreview: string }>} */
  const results = [];

  try {
    for (const c of CASES) {
      console.error(`── CASE ${c.id} ──`);
      const turn = await runChatTurn({
        baseUrl: endpoint.baseUrl,
        model: selection.model,
        messages: [{ role: "user", content: c.prompt }],
        session,
        onStatus: (line) => console.error(`  ${line}`),
      });
      const verdict = c.check(turn);
      results.push({
        id: c.id,
        pass: verdict.pass,
        detail: verdict.detail,
        metrics: turn.metrics,
        answerPreview: turn.finalContent.slice(0, 240).replace(/\s+/g, " "),
      });
      console.error(`${verdict.pass ? "PASS" : "FAIL"} ${c.id}: ${verdict.detail}`);
      console.error("");
    }
  } finally {
    await cleanup();
  }

  const chatPass = results.every((r) => r.pass);
  const report = {
    tunnel: {
      ok: endpoint.ok,
      reused: endpoint.reused,
      startedTunnel: endpoint.startedTunnel,
      ownership: endpoint.ownership,
      baseUrl: endpoint.baseUrl,
      ollamaVersion: endpoint.ollamaVersion,
      message: endpoint.message,
    },
    model: selection.model,
    chatPass,
    cases: results,
  };

  console.error("══════════════════════════════════════");
  console.error("CHAT RESULT (independent of tunnel block above)");
  console.error(`OVERALL: ${chatPass ? "PASS" : "FAIL"}`);
  for (const r of results) {
    console.error(`  ${r.pass ? "PASS" : "FAIL"} ${r.id} — ${r.detail}`);
  }
  console.error("══════════════════════════════════════");

  if (args.json) {
    console.log(JSON.stringify(report, null, 2));
  }

  process.exitCode = chatPass ? 0 : 1;
}

const isMain =
  Boolean(process.argv[1]) && import.meta.url === pathToFileURL(resolve(process.argv[1])).href;
if (isMain) {
  main().catch(async (err) => {
    console.error(`harness error: ${err instanceof Error ? err.message : err}`);
    process.exitCode = 1;
  });
}

export { main as runLiveFriendTest, CASES };
