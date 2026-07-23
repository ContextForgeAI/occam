#!/usr/bin/env node
/**
 * PB3 desk — Recipe E stdio MCP smoke (heal hint chain).
 * Usage: node scripts/desk-recipe-e-heal.mjs [--out artifacts/l3-heal-desk/2026-06-16]
 */
import { spawn } from "node:child_process";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const root = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");
const outArg = process.argv.find((a) => a.startsWith("--out="));
const outDir =
  outArg?.slice("--out=".length) ||
  join(root, "artifacts", "l3-heal-desk", new Date().toISOString().slice(0, 10));

const REQUEST_TIMEOUT_MS = 180_000;

class McpStdioClient {
  #proc;
  #buffer = "";
  #pending = new Map();
  #id = 1;

  constructor(proc) {
    this.#proc = proc;
    proc.stdout.on("data", (chunk) => this.#onData(chunk.toString()));
    proc.stderr.on("data", () => {});
  }

  #sendLine(obj) {
    this.#proc.stdin.write(`${JSON.stringify(obj)}\n`);
  }

  #onData(chunk) {
    this.#buffer += chunk;
    for (;;) {
      const nl = this.#buffer.indexOf("\n");
      if (nl === -1) break;
      const line = this.#buffer.slice(0, nl).trim();
      this.#buffer = this.#buffer.slice(nl + 1);
      if (!line) continue;
      let msg;
      try {
        msg = JSON.parse(line);
      } catch {
        continue;
      }
      if (msg.id != null && this.#pending.has(msg.id)) {
        const { resolve, reject } = this.#pending.get(msg.id);
        this.#pending.delete(msg.id);
        if (msg.error) reject(new Error(JSON.stringify(msg.error)));
        else resolve(msg.result);
      }
    }
  }

  notify(method, params = {}) {
    this.#sendLine({ jsonrpc: "2.0", method, params });
  }

  request(method, params = {}) {
    const id = this.#id++;
    this.#sendLine({ jsonrpc: "2.0", id, method, params });
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        if (this.#pending.has(id)) {
          this.#pending.delete(id);
          reject(new Error(`MCP timeout ${REQUEST_TIMEOUT_MS}ms: ${method}`));
        }
      }, REQUEST_TIMEOUT_MS);
      this.#pending.set(id, {
        resolve: (v) => {
          clearTimeout(timer);
          resolve(v);
        },
        reject: (e) => {
          clearTimeout(timer);
          reject(e);
        },
      });
    });
  }

  close() {
    this.#proc.stdin.end();
  }
}

function parseToolJson(result) {
  const text = result?.content?.find((c) => c.type === "text")?.text;
  if (!text) return { raw: result, parsed: null };
  try {
    return { raw: text, parsed: JSON.parse(text), isError: result?.isError === true };
  } catch {
    return { raw: text, parsed: null, isError: result?.isError === true };
  }
}

function hasHealHint(parsed) {
  return parsed?.agentHints?.suggestedNext === "occam_playbook_heal";
}

async function main() {
  mkdirSync(outDir, { recursive: true });
  const session = { startedAt: new Date().toISOString(), steps: [], k: {} };

  const launcher = join(root, "scripts", "launch-mcp-host.mjs");
  const proc = spawn(process.execPath, [launcher], {
    cwd: root,
    env: { ...process.env, OCCAM_HOME: root },
    stdio: ["pipe", "pipe", "pipe"],
  });

  const client = new McpStdioClient(proc);
  const timeout = setTimeout(() => proc.kill(), 600_000);

  try {
    await client.request("initialize", {
      protocolVersion: "2024-11-05",
      capabilities: {},
      clientInfo: { name: "desk-recipe-e-heal", version: "1.0" },
    });
    client.notify("notifications/initialized");

    const tools = await client.request("tools/list", {});
    const toolNames = tools?.tools?.map((t) => t.name) ?? [];
    session.k.toolCount = toolNames.length;
    session.k.eightTools = toolNames.length === 9;
    session.k.hasHeal = toolNames.includes("occam_playbook_heal");
    session.k.hasSave = toolNames.includes("occam_playbook_save");

    const nginxLeaf = "https://nginx.org/en/docs/http/ngx_http_core_module.html";
    console.log("[desk-e] transcode nginx leaf + bad selector…");
    const failRes = parseToolJson(
      await client.request("tools/call", {
        name: "occam_transcode",
        arguments: {
          url: nginxLeaf,
          backend_policy: "http",
          content_selectors: "#sidebar",
        },
      }),
    );
    session.steps.push({ tool: "occam_transcode", case: "nginx-leaf-bad-selector", ...failRes });
    session.k.nginxHealHint = hasHealHint(failRes.parsed);
    session.k.nginxFailed = failRes.parsed?.ok === false;

    const mdn404 = "https://developer.mozilla.org/en-US/docs/OccamGateMissingPage404";
    console.log("[desk-e] transcode MDN 404 neg…");
    const neg404 = parseToolJson(
      await client.request("tools/call", {
        name: "occam_transcode",
        arguments: { url: mdn404, backend_policy: "http" },
      }),
    );
    session.steps.push({ tool: "occam_transcode", case: "mdn-404", ...neg404 });
    session.k.mdn404NoHeal = neg404.parsed?.ok === false && !hasHealHint(neg404.parsed);

    if (session.k.nginxHealHint) {
      console.log("[desk-e] occam_playbook_heal (eligible)…");
      const healRes = parseToolJson(
        await client.request("tools/call", {
          name: "occam_playbook_heal",
          arguments: {
            url: nginxLeaf,
            failure_reason: failRes.parsed?.failure?.code ?? "content_selectors_miss",
          },
        }),
      );
      session.steps.push({ tool: "occam_playbook_heal", ...healRes });
      session.k.healCapture = healRes.parsed?.ok === true;
    }

    session.k.challengeUrlGateNote =
      "heal-neg-challenge-url covered by L3_HEAL_LEARN_OK unit policy (js_challenge finalUrl)";

    writeFileSync(join(outDir, "desk-recipe-e-heal.json"), JSON.stringify(session, null, 2), "utf8");

    const pass =
      session.k.eightTools &&
      session.k.hasHeal &&
      session.k.hasSave &&
      session.k.nginxFailed &&
      session.k.nginxHealHint &&
      session.k.mdn404NoHeal;

    console.log(`[desk-e] toolCount=${session.k.toolCount} nginxHealHint=${session.k.nginxHealHint} mdn404NoHeal=${session.k.mdn404NoHeal}`);
    console.log(pass ? "DESK_RECIPE_E_HEAL_OK" : "DESK_RECIPE_E_HEAL_FAIL");
    process.exitCode = pass ? 0 : 1;
  } finally {
    clearTimeout(timeout);
    client.close();
    proc.kill();
  }
}

main().catch((err) => {
  console.error(err);
  process.exitCode = 1;
});
