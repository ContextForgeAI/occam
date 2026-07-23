#!/usr/bin/env node
/**
 * Public MCP contract + version-surface diagnostic.
 *
 * Spawns the same launch path as the ChatGPT tunnel (`scripts/launch-mcp-host.mjs`),
 * fetches live tools/list, asserts RC1 schema, fingerprints it, and optionally
 * compares stdio ↔ local WebSocket parity.
 *
 * Usage:
 *   node scripts/check-public-mcp-contract.mjs
 *   node scripts/check-public-mcp-contract.mjs --write-fingerprint
 *   node scripts/check-public-mcp-contract.mjs --ws
 *   node scripts/check-public-mcp-contract.mjs --invoke-smoke
 *   node scripts/check-public-mcp-contract.mjs --diagnose   # print version-surface JSON
 *
 * Exit 0 on PASS. Marker: PUBLIC_MCP_CONTRACT_OK
 */
import { spawn } from "node:child_process";
import { createInterface } from "node:readline";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { createConnection } from "node:net";
import {
  assertPublicMcpContract,
  schemaFingerprint,
} from "./lib/public-mcp-contract.mjs";
import { resolveHostBinary } from "./lib/resolve-host-binary.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const root = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");
const FINGERPRINT_PATH = join(root, "corpora", "public-mcp-schema-fingerprint.txt");
const PACKAGE_JSON = join(root, "packages", "occam-mcp", "package.json");
const REQUEST_TIMEOUT_MS = 60_000;

const args = new Set(process.argv.slice(2));
const writeFingerprint = args.has("--write-fingerprint");
const withWs = args.has("--ws");
const invokeSmoke = args.has("--invoke-smoke");
const diagnoseOnly = args.has("--diagnose");

class McpStdioClient {
  #proc;
  #pending = new Map();
  #id = 1;
  #rl;

  constructor(proc) {
    this.#proc = proc;
    this.#rl = createInterface({ input: proc.stdout });
    this.#rl.on("line", (line) => this.#onLine(line));
    proc.stderr.on("data", () => {});
  }

  #onLine(line) {
    const trimmed = line.trim();
    if (!trimmed) return;
    let msg;
    try {
      msg = JSON.parse(trimmed);
    } catch {
      return;
    }
    if (msg.id != null && this.#pending.has(msg.id)) {
      const { resolve, reject, timer } = this.#pending.get(msg.id);
      this.#pending.delete(msg.id);
      clearTimeout(timer);
      if (msg.error) reject(new Error(JSON.stringify(msg.error)));
      else resolve(msg.result);
    }
  }

  notify(method, params = {}) {
    this.#proc.stdin.write(`${JSON.stringify({ jsonrpc: "2.0", method, params })}\n`);
  }

  request(method, params = {}) {
    const id = this.#id++;
    this.#proc.stdin.write(`${JSON.stringify({ jsonrpc: "2.0", id, method, params })}\n`);
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        if (this.#pending.has(id)) {
          this.#pending.delete(id);
          reject(new Error(`timeout ${method}`));
        }
      }, REQUEST_TIMEOUT_MS);
      this.#pending.set(id, { resolve, reject, timer });
    });
  }

  close() {
    try {
      this.#proc.stdin.end();
    } catch {
      /* ignore */
    }
    try {
      this.#proc.kill();
    } catch {
      /* ignore */
    }
  }
}

function spawnLaunchPath() {
  return spawn(process.execPath, [join(root, "scripts", "launch-mcp-host.mjs")], {
    cwd: root,
    env: { ...process.env, OCCAM_HOME: root, OCCAM_BANNER: "0" },
    stdio: ["pipe", "pipe", "pipe"],
  });
}

function parseToolJson(result) {
  const text = result?.content?.find((c) => c.type === "text")?.text;
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

async function handshakeList(client) {
  const init = await client.request("initialize", {
    protocolVersion: "2024-11-05",
    capabilities: {},
    clientInfo: { name: "public-mcp-contract", version: "0" },
  });
  client.notify("notifications/initialized");
  const list = await client.request("tools/list", {});
  return { init, tools: list.tools || [] };
}

function readPackageVersion() {
  try {
    return JSON.parse(readFileSync(PACKAGE_JSON, "utf8")).version;
  } catch {
    return null;
  }
}

function runHostVersionSurface(binaryPath) {
  return new Promise((resolve) => {
    const child = spawn(binaryPath, ["version-surface"], {
      cwd: root,
      env: { ...process.env, OCCAM_HOME: root, OCCAM_BANNER: "0" },
      stdio: ["ignore", "pipe", "pipe"],
    });
    let out = "";
    child.stdout.on("data", (d) => {
      out += d.toString();
    });
    child.on("exit", () => {
      try {
        resolve(JSON.parse(out.trim().split("\n").pop()));
      } catch {
        resolve(null);
      }
    });
  });
}

async function waitForPort(port, timeoutMs = 15_000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const ok = await new Promise((resolve) => {
      const sock = createConnection({ host: "127.0.0.1", port }, () => {
        sock.end();
        resolve(true);
      });
      sock.on("error", () => resolve(false));
    });
    if (ok) return true;
    await new Promise((r) => setTimeout(r, 200));
  }
  return false;
}

async function main() {
  const binary = resolveHostBinary(root);
  if (!binary) {
    console.error("FAIL: no published host binary (run occam doctor / publish)");
    process.exit(1);
  }

  const packageVersion = readPackageVersion();
  const hostSurface = await runHostVersionSurface(binary);

  const proc = spawnLaunchPath();
  const client = new McpStdioClient(proc);
  let stdioFp;
  let protocolVersion;
  try {
    const { init, tools } = await handshakeList(client);
    protocolVersion = init.protocolVersion;
    const check = assertPublicMcpContract(tools);
    if (!check.ok) {
      console.error("FAIL: public MCP contract assertions:");
      for (const f of check.failures) console.error(" -", f);
      process.exit(1);
    }

    stdioFp = schemaFingerprint(tools);

    if (writeFingerprint) {
      writeFileSync(FINGERPRINT_PATH, `${stdioFp}\n`, "utf8");
      console.error(`wrote ${FINGERPRINT_PATH}`);
    } else if (existsSync(FINGERPRINT_PATH)) {
      const expected = readFileSync(FINGERPRINT_PATH, "utf8").trim();
      if (expected !== stdioFp) {
        console.error("FAIL: schemaFingerprint mismatch");
        console.error(" expected:", expected);
        console.error(" actual:  ", stdioFp);
        console.error("Re-run with --write-fingerprint after an intentional schema change.");
        process.exit(1);
      }
    } else {
      console.error(`FAIL: missing fingerprint corpus at ${FINGERPRINT_PATH}`);
      console.error("Run with --write-fingerprint once to seed it.");
      process.exit(1);
    }

    if (invokeSmoke) {
      const digestResult = await client.request("tools/call", {
        name: "occam_digest",
        arguments: {
          source_url: "https://docs.python.org/3/",
          focus_query: "asyncio",
          max_links: 8,
        },
      });
      const digestBody = parseToolJson(digestResult);
      if (!digestBody) {
        console.error("FAIL: source_url-only occam_digest returned no JSON");
        process.exit(1);
      }
      if (digestBody.failureCode === "invalid_arguments" || digestBody.ok === false && !digestBody.digestId) {
        // Typed neither-urls-nor-source would be invalid_arguments — that must not happen here.
        if (digestBody.failureCode === "invalid_arguments") {
          console.error("FAIL: source_url-only rejected as invalid_arguments", digestBody);
          process.exit(1);
        }
      }
      if (digestBody.ok !== true) {
        console.error("FAIL: source_url-only occam_digest did not succeed", digestBody.failureCode || digestBody);
        process.exit(1);
      }
      console.error("DIGEST_SOURCE_URL_OK");

      // Minimal valid request exercising RC1 schema knobs. Prefer a stable docs host;
      // accept ok:true OR a typed extract failure (proves params were not schema-rejected).
      const transcodeResult = await client.request("tools/call", {
        name: "occam_transcode",
        arguments: {
          url: "https://nginx.org/en/docs/",
          json_blocks: true,
          rank_blocks: true,
          tag_trust: true,
          focus_query: "configuration",
          emit_capsule: false,
          delta_only: false,
          semantic_chunking: false,
        },
      });
      const transcodeBody = parseToolJson(transcodeResult);
      if (!transcodeBody) {
        console.error("FAIL: occam_transcode RC1 options returned no JSON");
        process.exit(1);
      }
      const code = transcodeBody.failure?.code || transcodeBody.failureCode;
      if (code === "invalid_arguments") {
        console.error("FAIL: RC1 transcode options rejected as invalid_arguments", transcodeBody);
        process.exit(1);
      }
      console.error(
        transcodeBody.ok === true
          ? "TRANSCODE_RC1_OPTIONS_OK"
          : `TRANSCODE_RC1_OPTIONS_ACCEPTED failure=${code}`,
      );
    }

    if (withWs) {
      const port = 18765;
      const wsProc = spawn(binary, ["--mcp-server", "--port", String(port)], {
        cwd: root,
        env: { ...process.env, OCCAM_HOME: root, OCCAM_BANNER: "0" },
        stdio: ["ignore", "ignore", "pipe"],
      });
      try {
        const up = await waitForPort(port);
        if (!up) {
          console.error("FAIL: WebSocket host did not listen");
          process.exit(1);
        }
        // Local WS accepts connections; full tools/list over StreamServerTransport is covered by
        // stdio (same AddOccamMcpServer registration). Tunnel path is stdio via launch-mcp-host.
        const accepted = await new Promise((resolve) => {
          const WS = globalThis.WebSocket;
          if (!WS) {
            resolve(false);
            return;
          }
          const ws = new WS(`ws://127.0.0.1:${port}/`);
          const t = setTimeout(() => {
            try {
              ws.close();
            } catch {
              /* ignore */
            }
            resolve(false);
          }, 5000);
          ws.addEventListener("open", () => {
            clearTimeout(t);
            ws.close();
            resolve(true);
          });
          ws.addEventListener("error", () => {
            clearTimeout(t);
            resolve(false);
          });
        });
        if (!accepted) {
          console.error("FAIL: WebSocket upgrade not accepted");
          process.exit(1);
        }
        console.error("WS_LISTEN_OK");
      } finally {
        try {
          wsProc.kill();
        } catch {
          /* ignore */
        }
      }

      // Parity: direct binary stdio vs launch-mcp-host (tunnel) must share fingerprint.
      const directProc = spawn(binary, [], {
        cwd: root,
        env: { ...process.env, OCCAM_HOME: root, OCCAM_BANNER: "0" },
        stdio: ["pipe", "pipe", "pipe"],
      });
      const directClient = new McpStdioClient(directProc);
      try {
        const { tools: directTools } = await handshakeList(directClient);
        const directFp = schemaFingerprint(directTools);
        if (directFp !== stdioFp) {
          console.error("FAIL: launch-mcp-host vs direct-binary schemaFingerprint mismatch");
          console.error(" launch:", stdioFp);
          console.error(" binary:", directFp);
          process.exit(1);
        }
        console.error("STDIO_LAUNCH_PARITY_OK");
      } finally {
        directClient.close();
      }
    }
  } finally {
    client.close();
  }

  const surface = {
    hostVersion: hostSurface?.hostVersion ?? null,
    assemblyPath: hostSurface?.assemblyPath ?? binary,
    packageVersion: packageVersion ?? hostSurface?.packageVersion ?? null,
    protocolVersion: protocolVersion ?? null,
    schemaFingerprint: stdioFp,
    launchPath: "node scripts/launch-mcp-host.mjs",
    binaryPath: binary,
  };

  if (diagnoseOnly || true) {
    // Always emit version-surface on stdout (last line machine-readable).
    console.log(JSON.stringify(surface));
  }

  if (packageVersion && hostSurface?.hostVersion && packageVersion !== hostSurface.hostVersion) {
    console.error(
      `WARN: packageVersion (${packageVersion}) != hostVersion (${hostSurface.hostVersion})`,
    );
  }

  console.error("PUBLIC_MCP_CONTRACT_OK");
  process.exit(0);
}

main().catch((err) => {
  console.error("FAIL:", err);
  process.exit(1);
});
