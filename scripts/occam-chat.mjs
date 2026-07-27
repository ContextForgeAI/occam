#!/usr/bin/env node
/**
 * Experimental local chat — Ollama native /api/chat + Occam MCP stdio tools.
 *
 * Usage:
 *   occam chat
 *   occam chat --model qwen2.5:7b
 *   occam chat --verbose
 *   occam chat --once "What is 17 × 19?"
 *
 * Not a stable 1.0 API. Friend/tester path only.
 */
import { createInterface } from "node:readline/promises";
import { stdin as input, stdout as output, stderr } from "node:process";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import {
  probeOllama,
  listModelsWithCapabilities,
  normalizeOllamaBase,
} from "./lib/experimental/local-chat/ollama-api.mjs";
import { selectToolModel, formatNoToolsList } from "./lib/experimental/local-chat/model-select.mjs";
import {
  startLocalChatSession,
  closeLocalChatSession,
  formatToolBanner,
} from "./lib/experimental/local-chat/session.mjs";
import { runChatTurn, printVerboseMetrics } from "./lib/experimental/local-chat/chat-loop.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const defaultHome = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");
process.env.OCCAM_HOME = defaultHome;

/**
 * @param {string[]} argv
 */
export function parseChatArgs(argv) {
  /** @type {{ model: string|null, verbose: boolean, once: string|null, baseUrl: string|null, help: boolean, json: boolean }} */
  const out = {
    model: null,
    verbose: false,
    once: null,
    baseUrl: null,
    help: false,
    json: false,
  };
  const args = [...argv];
  while (args.length) {
    const a = args.shift();
    if (a === "-h" || a === "--help") out.help = true;
    else if (a === "--verbose" || a === "-v") out.verbose = true;
    else if (a === "--json") out.json = true;
    else if (a === "--model" || a === "-m") out.model = args.shift() ?? null;
    else if (a?.startsWith("--model=")) out.model = a.slice("--model=".length);
    else if (a === "--host" || a === "--ollama-host") out.baseUrl = args.shift() ?? null;
    else if (a?.startsWith("--host=")) out.baseUrl = a.slice("--host=".length);
    else if (a === "--once") out.once = args.shift() ?? null;
    else if (a?.startsWith("--once=")) out.once = a.slice("--once=".length);
    else if (!a.startsWith("-") && !out.once) {
      // Allow: occam chat "prompt" as once-mode convenience
      out.once = a;
      if (args.length) out.once = [out.once, ...args].join(" ");
      break;
    } else {
      throw new Error(`Unknown flag: ${a}`);
    }
  }
  return out;
}

function printHelp() {
  console.log(`Occam chat (experimental)

Usage:
  occam chat
  occam chat --model <name>
  occam chat --verbose
  occam chat --once "prompt"

Uses the local Ollama install (default http://127.0.0.1:11434) and Occam web tools
via an internal MCP stdio session. Not a stable 1.0 API.

Commands in REPL:
  /exit   Quit
  /model  Show current model
`);
}

/**
 * @param {string} line
 */
function writeOut(line) {
  console.log(line);
}

/**
 * @param {string} line
 */
function writeErr(line) {
  stderr.write(`${line}\n`);
}

async function main() {
  let args;
  try {
    args = parseChatArgs(process.argv.slice(2));
  } catch (err) {
    writeErr(`error: ${err instanceof Error ? err.message : String(err)}`);
    process.exitCode = 1;
    return;
  }

  if (args.help) {
    printHelp();
    return;
  }

  const baseUrl = normalizeOllamaBase(args.baseUrl);
  /** @type {import('./lib/experimental/local-chat/session.mjs').LocalChatSession | null} */
  let session = null;
  /** @type {import('node:readline').Interface | null} */
  let rl = null;
  let cleaned = false;

  const cleanup = async () => {
    if (cleaned) return;
    cleaned = true;
    try {
      rl?.close();
    } catch {
      /* ignore */
    }
    await closeLocalChatSession(session);
    session = null;
  };

  const onSignal = () => {
    void cleanup().finally(() => process.exit(130));
  };
  process.once("SIGINT", onSignal);
  process.once("SIGTERM", onSignal);

  try {
    writeOut("");
    writeOut("⌥  ─ O C C A M");
    writeOut("");
    writeOut("Experimental local chat");
    writeOut("");

    const probe = await probeOllama(baseUrl);
    if (!probe.ok) {
      writeErr(`Ollama not detected at ${probe.baseUrl}`);
      writeErr(probe.error);
      writeErr("Start Ollama, then run: occam chat");
      process.exitCode = 1;
      return;
    }
    writeErr(`✓ Ollama detected (${probe.version})`);

    const models = await listModelsWithCapabilities(baseUrl);
    const selection = await selectToolModel(models, {
      modelFlag: args.model,
      stdin: input,
      stdout: output,
      interactive: !args.once && Boolean(input.isTTY),
    });

    if (!selection.ok) {
      writeErr("");
      writeErr(selection.message);
      if (selection.code === "no_tool_models") {
        /* message already includes list */
      } else if (selection.installed?.length) {
        writeErr("");
        writeErr(formatNoToolsList(selection.installed.map((m) => ({ ...m, capabilities: [] }))));
      }
      process.exitCode = 1;
      return;
    }

    writeErr(`✓ ${selection.model} supports tools`);
    if (selection.warning) {
      writeErr(`⚠ ${selection.warning}`);
    }

    session = await startLocalChatSession(defaultHome);
    writeErr(`✓ Occam web tools ready`);
    writeOut("");
    writeOut(`Ollama ${probe.version}`);
    writeOut(`Model: ${selection.model}`);
    writeOut(`Web tools: ${formatToolBanner(session)}`);
    writeOut("");

    /** @type {object[]} */
    let messages = [];

    const runOnce = async (prompt) => {
      messages.push({ role: "user", content: prompt });
      const result = await runChatTurn({
        baseUrl,
        model: selection.model,
        messages,
        session,
        verbose: args.verbose,
        onStatus: (line) => writeErr(line.includes("→") ? `Reading ${line.split("→")[1]?.trim()}…` : `Using ${line}…`),
      });
      messages = result.messages;
      if (result.finalContent) writeOut(result.finalContent);
      else writeOut("(no response)");
      if (args.verbose) printVerboseMetrics(result.metrics, writeErr);
      if (args.json) {
        writeOut(JSON.stringify({ metrics: result.metrics, answer: result.finalContent }, null, 2));
      }
      return result;
    };

    if (args.once) {
      await runOnce(args.once);
      await cleanup();
      return;
    }

    rl = createInterface({ input, output, terminal: Boolean(input.isTTY) });
    let eof = false;
    rl.on("close", () => {
      eof = true;
    });
    for (;;) {
      if (eof) break;
      let line;
      try {
        line = await new Promise((resolve, reject) => {
          if (eof) {
            reject(new Error("EOF"));
            return;
          }
          const onClose = () => reject(new Error("EOF"));
          rl.once("close", onClose);
          rl.question("> ")
            .then((v) => {
              rl.off("close", onClose);
              resolve(v);
            })
            .catch((err) => {
              rl.off("close", onClose);
              reject(err);
            });
        });
      } catch {
        break;
      }
      const trimmed = line.trim();
      if (!trimmed) continue;
      if (trimmed === "/exit" || trimmed === "/quit") break;
      if (trimmed === "/model") {
        writeOut(selection.model);
        continue;
      }
      await runOnce(trimmed);
      writeOut("");
    }
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    writeErr(`error: ${msg}`);
    process.exitCode = 1;
  } finally {
    await cleanup();
  }
}

const isMain =
  Boolean(process.argv[1]) && import.meta.url === pathToFileURL(resolve(process.argv[1])).href;
if (isMain) {
  main()
    .then(() => {
      process.exit(process.exitCode ?? 0);
    })
    .catch((err) => {
      console.error(err instanceof Error ? err.message : err);
      process.exit(1);
    });
}

export { main as runOccamChat };
