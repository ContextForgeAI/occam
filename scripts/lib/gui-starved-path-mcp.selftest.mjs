#!/usr/bin/env node
/**
 * Regression: installed-style MCP launch under a GUI-like starved PATH, without
 * manually setting OCCAM_NODE_BIN in the host env. The launcher must stamp Node
 * from process.execPath / runtime/node-bin so workers can run.
 *
 *   node scripts/lib/gui-starved-path-mcp.selftest.mjs
 *
 * Optional: OCCAM_STARVED_PATH_URL (default https://example.com/)
 */
import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { createInterface } from "node:readline";
import { writeInstallNodeBin } from "./resolve-node-runtime.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const root = process.env.OCCAM_HOME?.trim() || join(here, "..", "..");
const url = process.env.OCCAM_STARVED_PATH_URL?.trim() || "https://example.com/";
const launcher = join(root, "scripts", "launch-mcp-host.mjs");

const starvedPath =
  process.platform === "win32"
    ? join(process.env.SystemRoot || "C:\\Windows", "System32")
    : "/usr/bin:/bin";

// Record install-time Node so Core can resolve even if stamp were skipped.
writeInstallNodeBin(root, process.execPath);

/** @type {NodeJS.ProcessEnv} */
const env = {
  HOME: process.env.HOME || process.env.USERPROFILE || "",
  TMPDIR: process.env.TMPDIR || process.env.TEMP || "",
  TEMP: process.env.TEMP || "",
  TMP: process.env.TMP || "",
  SystemRoot: process.env.SystemRoot,
  USERPROFILE: process.env.USERPROFILE,
  APPDATA: process.env.APPDATA,
  LOCALAPPDATA: process.env.LOCALAPPDATA,
  PATH: starvedPath,
  OCCAM_HOME: root,
  OCCAM_BANNER: "0",
  WT_OCCAM_BANNER: "0",
  // Intentionally NO OCCAM_NODE_BIN — acceptance criterion for this regression.
};
delete env.OCCAM_NODE_BIN;

const child = spawn(process.execPath, [launcher], {
  cwd: root,
  env,
  stdio: ["pipe", "pipe", "pipe"],
  windowsHide: true,
});

const rl = createInterface({ input: child.stdout });
let nextId = 1;
/** @type {Map<number, { resolve: (v: any) => void, reject: (e: Error) => void }>} */
const pending = new Map();

rl.on("line", (line) => {
  const trimmed = line.trim();
  if (!trimmed.startsWith("{")) return;
  let obj;
  try {
    obj = JSON.parse(trimmed);
  } catch {
    return;
  }
  if (obj.id == null) return;
  const waiter = pending.get(obj.id);
  if (!waiter) return;
  pending.delete(obj.id);
  waiter.resolve(obj);
});

function req(method, params, timeoutMs = 90_000) {
  const id = nextId++;
  const payload = JSON.stringify({ jsonrpc: "2.0", id, method, params: params || {} });
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      pending.delete(id);
      reject(new Error(`timeout waiting for ${method}`));
    }, timeoutMs);
    pending.set(id, {
      resolve: (v) => {
        clearTimeout(timer);
        resolve(v);
      },
      reject,
    });
    child.stdin.write(`${payload}\n`);
  });
}

function toolBody(resp) {
  const result = resp.result || {};
  assert.notEqual(result.isError, true, `MCP isError for tool: ${JSON.stringify(result).slice(0, 300)}`);
  const text = result.content?.[0]?.text;
  assert.ok(typeof text === "string" && text.startsWith("{"), "expected JSON tool body");
  return JSON.parse(text);
}

let failed = false;
try {
  await req("initialize", {
    protocolVersion: "2024-11-05",
    capabilities: {},
    clientInfo: { name: "gui-starved-path-selftest", version: "0" },
  });
  child.stdin.write(`${JSON.stringify({ jsonrpc: "2.0", method: "notifications/initialized" })}\n`);

  const listed = await req("tools/list");
  const names = (listed.result?.tools || []).map((t) => t.name);
  assert.ok(names.includes("occam_probe"), "occam_probe missing");
  assert.ok(names.includes("occam_transcode"), "occam_transcode missing");

  const probeResp = await req("tools/call", {
    name: "occam_probe",
    arguments: { url },
  });
  const probe = toolBody(probeResp);
  assert.equal(probe.ok, true, `probe failed: ${JSON.stringify(probe.failure || probe)}`);

  const transcodeResp = await req("tools/call", {
    name: "occam_transcode",
    arguments: { url },
  });
  const body = toolBody(transcodeResp);
  assert.equal(body.ok, true, `transcode failed: ${JSON.stringify(body.failure || body)}`);
  assert.ok(String(body.markdown || "").length > 20, "transcode markdown too short");

  console.log(
    JSON.stringify({
      ok: true,
      starvedPath,
      hasManualOccamNodeBin: false,
      url,
      markdownLen: String(body.markdown || "").length,
      recordedNode: process.execPath,
    }),
  );
  console.log("gui-starved-path-mcp.selftest: ok");
} catch (err) {
  failed = true;
  console.error("gui-starved-path-mcp.selftest: FAIL", err instanceof Error ? err.message : err);
  process.exitCode = 1;
} finally {
  try {
    child.kill();
  } catch {
    // ignore
  }
}

if (!failed) process.exit(0);
