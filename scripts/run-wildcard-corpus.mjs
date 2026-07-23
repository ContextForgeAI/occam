#!/usr/bin/env node
/**
 * Full wildcard corpus — live stdio MCP probe + transcode per jsonl row.
 * Usage: node scripts/run-wildcard-corpus.mjs [--out artifacts/quality-audit/wildcard-full-TIMESTAMP.json]
 */
import { spawn } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const root = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");
const ts = new Date().toISOString().replace(/[:.]/g, "-").slice(0, 19);
const outArg = process.argv.find((a) => a.startsWith("--out="));
const outPath =
  outArg?.slice("--out=".length) ||
  join(root, "artifacts", "quality-audit", `wildcard-full-${ts}.json`);

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
}

function parseToolJson(result) {
  if (result?.isError) {
    const text = result?.content?.find((c) => c.type === "text")?.text ?? "tool error";
    return { raw: text, parsed: null, isError: true };
  }
  const text = result?.content?.find((c) => c.type === "text")?.text;
  if (!text) return { raw: result, parsed: null, isError: false };
  try {
    return { raw: text, parsed: JSON.parse(text), isError: false };
  } catch {
    return { raw: text, parsed: null, isError: false };
  }
}

function loadJsonl(path) {
  return readFileSync(path, "utf8")
    .trim()
    .split(/\n+/)
    .filter(Boolean)
    .map((line) => JSON.parse(line));
}

function verdict(probe, transcode) {
  const pOk = probe?.parsed?.ok === true;
  const tOk = transcode?.parsed?.ok === true;
  if (tOk) return "WORKS";
  const code =
    transcode?.parsed?.failure?.code ||
    transcode?.parsed?.failureCode ||
    probe?.parsed?.failureCode ||
    "unknown";
  if (["http_403", "http_404", "http_429", "timeout", "private_url_blocked", "captcha_or_challenge", "thin_extract"].includes(code)) {
    return "FAIL_HONEST";
  }
  if (pOk && !tOk) return "FAIL_HONEST";
  return "FAIL_BUG";
}

async function callTool(client, name, args) {
  const result = await client.request("tools/call", { name, arguments: args });
  return parseToolJson(result);
}

async function runRow(client, row, layer) {
  const url = row.url;
  const backend = row.backend || "http_then_browser";
  const entry = {
    id: row.id,
    layer,
    priority: row.priority,
    url,
    backend,
    startedAt: new Date().toISOString(),
  };
  try {
    entry.probe = await callTool(client, "occam_probe", { url });
    entry.transcode = await callTool(client, "occam_transcode", {
      url,
      backend_policy: backend,
    });
    const p = entry.probe.parsed;
    const t = entry.transcode.parsed;
    entry.summary = {
      probeOk: p?.ok === true,
      probeCode: p?.failureCode ?? null,
      transcodeOk: t?.ok === true,
      failureCode: t?.failure?.code ?? t?.failureCode ?? null,
      backendUsed: t?.backend ?? null,
      mdLen: typeof t?.markdown === "string" ? t.markdown.length : 0,
      verdict: verdict(entry.probe, entry.transcode),
    };
  } catch (e) {
    entry.error = String(e?.message ?? e);
    entry.summary = { verdict: "ERROR", error: entry.error };
  }
  entry.finishedAt = new Date().toISOString();
  console.log(`[wildcard] ${entry.id}: ${entry.summary?.verdict ?? "ERROR"}`);
  return entry;
}

async function runDigestChain(client, id, urls, focusQuery) {
  const entry = { id, chain: true, urls, focusQuery, startedAt: new Date().toISOString() };
  try {
    entry.digest = await callTool(client, "occam_digest", {
      urls: JSON.stringify(urls),
      backend_policy: "http_then_browser",
      fit_markdown: false,
      focus_query: focusQuery ?? null,
    });
    const d = entry.digest.parsed;
    entry.summary = {
      digestOk: d?.ok === true,
      stats: d?.stats ?? null,
      items: (d?.items ?? []).map((i) => ({
        url: i.url,
        ok: i.ok,
        focusMatched: i.focusMatched,
        failure: i.failure?.code ?? null,
      })),
      verdict: d?.ok === true ? "WORKS" : "FAIL_HONEST",
    };
  } catch (e) {
    entry.error = String(e?.message ?? e);
    entry.summary = { verdict: "ERROR", error: entry.error };
  }
  entry.finishedAt = new Date().toISOString();
  console.log(`[digest-chain] ${id}: ${entry.summary?.verdict ?? "ERROR"}`);
  return entry;
}

async function main() {
  const baseRows = loadJsonl(join(root, "corpora", "quality-audit-wildcard.jsonl"));
  const digestRows = loadJsonl(join(root, "corpora", "quality-audit-wildcard-digest.jsonl"));
  const byId = Object.fromEntries(digestRows.map((r) => [r.id, r]));

  const proc = spawn(process.execPath, [join(root, "scripts", "launch-mcp-host.mjs")], {
    cwd: root,
    env: { ...process.env, OCCAM_HOME: root },
    stdio: ["pipe", "pipe", "pipe"],
  });
  const client = new McpStdioClient(proc);
  const session = {
    startedAt: new Date().toISOString(),
    base: [],
    digest: [],
    chains: [],
  };

  try {
    await client.request("initialize", {
      protocolVersion: "2024-11-05",
      capabilities: {},
      clientInfo: { name: "wildcard-corpus", version: "1.0" },
    });
    client.notify("notifications/initialized");

    for (const row of baseRows) {
      session.base.push(await runRow(client, row, "wildcard"));
    }
    for (const row of digestRows) {
      session.digest.push(await runRow(client, row, "wildcard_digest"));
    }

    session.chains.push(
      await runDigestChain(
        client,
        "D1-morning-tech",
        [
          byId["hn-item-permalink"].url,
          byId["arxiv-abs-llm"].url,
          byId["github-readme"]?.url || byId["github-issue-thread"].url,
        ],
        "summary for daily standup",
      ),
    );
    session.chains.push(
      await runDigestChain(
        client,
        "D2-news-health",
        [
          byId["guardian-tech-article"].url,
          byId["cdc-health-topic"].url,
          byId["wikipedia-mobile-article"].url,
        ],
        null,
      ),
    );
    session.chains.push(
      await runDigestChain(
        client,
        "D3-builder-stack",
        [
          byId["python-docs-tutorial"].url,
          byId["npm-express-package"].url,
          byId["pypi-requests-package"].url,
        ],
        "install usage",
      ),
    );
  } finally {
    proc.kill();
  }

  const all = [...session.base, ...session.digest];
  session.rollup = {
    totalRows: all.length,
    works: all.filter((r) => r.summary?.verdict === "WORKS").length,
    partial: all.filter((r) => r.summary?.verdict === "PARTIAL").length,
    failHonest: all.filter((r) => r.summary?.verdict === "FAIL_HONEST").length,
    failBug: all.filter((r) => r.summary?.verdict === "FAIL_BUG").length,
    error: all.filter((r) => r.summary?.verdict === "ERROR").length,
    chains: session.chains.map((c) => ({ id: c.id, verdict: c.summary?.verdict })),
  };
  session.finishedAt = new Date().toISOString();

  mkdirSync(dirname(outPath), { recursive: true });
  writeFileSync(outPath, JSON.stringify(session, null, 2), "utf8");
  console.log(`[wildcard] wrote ${outPath}`);
  console.log(JSON.stringify(session.rollup, null, 2));
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
