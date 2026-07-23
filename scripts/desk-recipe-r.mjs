#!/usr/bin/env node
/**
 * PB4 desk L6 — Recipe R via stdio MCP (Inspector-equivalent contract smoke + RAG chain).
 * Usage: node scripts/desk-recipe-r.mjs [--out artifacts/l4-genome-desk/2026-06-10]
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
  join(root, "artifacts", "l4-genome-desk", new Date().toISOString().slice(0, 10));

const REQUEST_TIMEOUT_MS = 120_000;
const DIGEST_URL_MAX = 3;

class McpStdioClient {
  #proc;
  #buffer = "";
  #pending = new Map();
  #id = 1;

  constructor(proc) {
    this.#proc = proc;
    proc.stdout.on("data", (chunk) => this.#onData(chunk.toString()));
    proc.stderr.on("data", () => {}); // banner on stderr — ignore
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
  if (result?.isError) {
    const text = result?.content?.find((c) => c.type === "text")?.text ?? "tool error";
    return { raw: text, parsed: null, isError: true };
  }
  const text = result?.content?.find((c) => c.type === "text")?.text;
  if (!text) return { raw: result, parsed: null };
  try {
    return { raw: text, parsed: JSON.parse(text), isError: false };
  } catch {
    return { raw: text, parsed: null, isError: false };
  }
}

function pickDocsLinks(links, max = DIGEST_URL_MAX) {
  const docs = links
    .filter((l) => typeof l.url === "string" && /kubernetes\.io\/docs\//i.test(l.url))
    .filter((l) => !/\/docs\/home\/?$/i.test(l.url))
    .slice(0, max);
  return docs.map((l) => l.url);
}

async function callStep(client, session, outDir, label, fn) {
  console.log(`[desk] ${label}…`);
  const step = await fn();
  session.steps.push(step);
  writeFileSync(join(outDir, "inspector-session.partial.json"), JSON.stringify(session, null, 2), "utf8");
  console.log(`[desk] ${label} done`);
  return step;
}

async function main() {
  mkdirSync(outDir, { recursive: true });
  const session = { startedAt: new Date().toISOString(), steps: [], tools: null, k9: {} };
  const entryUrl = "https://kubernetes.io/docs/concepts/";
  const focusQuery = "kubernetes pod scheduling concepts";

  const launcher = join(root, "scripts", "launch-mcp-host.mjs");
  const proc = spawn(process.execPath, [launcher], {
    cwd: root,
    env: { ...process.env, OCCAM_HOME: root },
    stdio: ["pipe", "pipe", "pipe"],
  });

  const client = new McpStdioClient(proc);
  const timeout = setTimeout(() => {
    proc.kill();
  }, 600_000);

  try {
    await client.request("initialize", {
      protocolVersion: "2024-11-05",
      capabilities: {},
      clientInfo: { name: "desk-recipe-r", version: "1.0" },
    });
    client.notify("notifications/initialized");

    const tools = await client.request("tools/list", {});
    session.tools = tools?.tools?.map((t) => ({
      name: t.name,
      description: t.description,
      inputSchema: t.inputSchema,
    }));
    writeFileSync(join(outDir, "inspector-session.partial.json"), JSON.stringify(session, null, 2), "utf8");
    console.log(`[desk] tools/list → ${session.tools?.length ?? 0} tools`);

    const resolveSchema = session.tools?.find((t) => t.name === "occam_playbook_resolve");
    const transcodeSchema = session.tools?.find((t) => t.name === "occam_transcode");
    session.k9.inspectorResolveParams = Boolean(
      resolveSchema?.inputSchema?.properties?.fetch_site_genome &&
        resolveSchema?.inputSchema?.properties?.include_lessons,
    );
    session.k9.inspectorTranscodePolicy = Boolean(
      transcodeSchema?.inputSchema?.properties?.playbook_policy,
    );
    session.k9.toolCount = session.tools?.length ?? 0;
    const extractSchema = session.tools?.find((t) => t.name === "occam_extract_knowledge");
    session.k9.inspectorExtractKnowledge = Boolean(extractSchema?.inputSchema?.properties?.url);

    const resolveStep = await callStep(client, session, outDir, "resolve", async () => {
      const resolveRes = parseToolJson(
        await client.request("tools/call", {
          name: "occam_playbook_resolve",
          arguments: { url: entryUrl },
        }),
      );
      return { tool: "occam_playbook_resolve", url: entryUrl, ...resolveRes };
    });
    const resolveOk = resolveStep.parsed?.ok === true;
    session.k9.resolveBeforeMap = resolveOk;

    const mapStep = await callStep(client, session, outDir, "map", async () => {
      const mapRes = parseToolJson(
        await client.request("tools/call", {
          name: "occam_map",
          arguments: { url: entryUrl, max_links: 32, source: "homepage" },
        }),
      );
      return { tool: "occam_map", url: entryUrl, ...mapRes };
    });
    const links = mapStep.parsed?.links ?? [];
    const picked = pickDocsLinks(links, DIGEST_URL_MAX);
    if (picked.length === 0) {
      picked.push("https://kubernetes.io/docs/concepts/overview/");
    }
    session.k9.mapFiltered = picked.length > 0 && picked.length <= 8;

    const digestStep = await callStep(client, session, outDir, `digest (${picked.length} urls, http)`, async () => {
      const digestRes = parseToolJson(
        await client.request("tools/call", {
          name: "occam_digest",
          arguments: {
            urls: JSON.stringify(picked),
            focus_query: focusQuery,
            backend_policy: "http",
          },
        }),
      );
      return { tool: "occam_digest", urls: picked, focusQuery, ...digestRes };
    });
    const items = digestStep.parsed?.items ?? [];
    const okItems = items.filter((i) => i.ok === true);
    const failItems = items.filter((i) => i.ok === false);
    session.k9.noInventedOnFail = failItems.every((i) => i.failure?.code);
    session.k9.hasUsableExcerpt = okItems.some(
      (i) => (i.excerpt?.length ?? 0) > 80 || (i.combined?.length ?? 0) > 80,
    );

    const leafUrl = "https://nginx.org/en/docs/";
    const transcodeStep = await callStep(client, session, outDir, "transcode auto (nginx)", async () => {
      const args = { url: leafUrl, playbook_policy: "auto", backend_policy: "http" };
      let transcodeRes = parseToolJson(
        await client.request("tools/call", {
          name: "occam_transcode",
          arguments: args,
        }),
      );
      if (transcodeRes.parsed?.failure?.code === "timeout") {
        transcodeRes = parseToolJson(
          await client.request("tools/call", {
            name: "occam_transcode",
            arguments: args,
          }),
        );
      }
      return { tool: "occam_transcode", url: leafUrl, playbook_policy: "auto", ...transcodeRes };
    });
    session.k9.autoPolicyTranscode = transcodeStep.parsed?.ok === true;
    const mediaRefs = transcodeStep.parsed?.mediaRefs ?? [];
    session.k9.mediaRefsFieldPresent =
      transcodeStep.parsed?.ok !== true || Array.isArray(transcodeStep.parsed?.mediaRefs);
    session.k9.mediaRefsSample = mediaRefs.slice(0, 3).map((m) => ({
      url: m.url,
      kind: m.kind,
      contextHeading: m.contextHeading ?? null,
    }));
    const digestMedia = okItems.some((i) => Array.isArray(i.mediaRefs) && i.mediaRefs.length > 0);
    session.k9.digestMediaRefsOnOkItem =
      okItems.length === 0 || okItems.every((i) => Array.isArray(i.mediaRefs));
    session.k9.digestHasMediaRefs = digestMedia;

    const k8sExtractUrl = "https://kubernetes.io/docs/concepts/overview/";
    let extractStep = null;
    if (extractSchema) {
      extractStep = await callStep(client, session, outDir, "extract_knowledge (k8s)", async () => {
        const extractRes = parseToolJson(
          await client.request("tools/call", {
            name: "occam_extract_knowledge",
            arguments: { url: k8sExtractUrl, backend_policy: "http" },
          }),
        );
        return { tool: "occam_extract_knowledge", url: k8sExtractUrl, ...extractRes };
      });
      const titleFact = extractStep.parsed?.facts?.find((f) => f.name === "title");
      session.k9.extractAfterResolve =
        resolveOk && extractStep.parsed?.ok === true && Boolean(extractStep.parsed?.meta?.koId);
      session.k9.extractTitleNonEmpty = Boolean(titleFact?.value?.length);
    } else {
      session.k9.extractAfterResolve = null;
      session.k9.extractTitleNonEmpty = null;
    }

    session.k9.pass =
      session.k9.toolCount === 9 &&
      session.k9.inspectorResolveParams &&
      session.k9.inspectorTranscodePolicy &&
      session.k9.resolveBeforeMap &&
      session.k9.mapFiltered &&
      session.k9.noInventedOnFail &&
      session.k9.hasUsableExcerpt &&
      session.k9.autoPolicyTranscode &&
      session.k9.mediaRefsFieldPresent &&
      session.k9.digestMediaRefsOnOkItem &&
      (session.k9.extractAfterResolve === null || session.k9.extractAfterResolve === true) &&
      (session.k9.extractTitleNonEmpty === null || session.k9.extractTitleNonEmpty === true) &&
      !session.steps.some((s) => s.isError);

    session.finishedAt = new Date().toISOString();
    writeFileSync(join(outDir, "inspector-session.json"), JSON.stringify(session, null, 2), "utf8");

    const md = [
      "# PB4 desk — Recipe R (stdio MCP)",
      "",
      `**Date:** ${session.startedAt}`,
      `**K9:** ${session.k9.pass ? "PASS" : "FAIL"}`,
      "",
      "## MCP Inspector equivalent (§9.1)",
      `- Tools: **${session.k9.toolCount}** (expect 9)`,
      `- resolve schema: fetch_site_genome + include_lessons → **${session.k9.inspectorResolveParams}**`,
      `- transcode schema: playbook_policy → **${session.k9.inspectorTranscodePolicy}**`,
      `- extract_knowledge listed → **${session.k9.inspectorExtractKnowledge}**`,
      "",
      "## Recipe R chain",
      `1. resolve \`${entryUrl}\` → ok=${resolveOk}, pageClass=${resolveStep.parsed?.pageClass ?? "—"}`,
      `2. map → ${links.length} links, picked ${picked.length} docs URLs`,
      `3. digest (http, ≤${DIGEST_URL_MAX}) → ok items ${okItems.length}/${items.length}`,
      `4. transcode auto \`${leafUrl}\` → ok=${transcodeStep.parsed?.ok}, mediaRefs=${mediaRefs.length}`,
      digestMedia ? `   digest ok items with mediaRefs: yes` : `   digest mediaRefs: none on ok items (field present: ${session.k9.digestMediaRefsOnOkItem})`,
      extractStep
        ? `5. extract_knowledge \`${k8sExtractUrl}\` → ok=${extractStep.parsed?.ok}, koId=${extractStep.parsed?.meta?.koId ?? "—"}`
        : "5. extract_knowledge — tool not listed (PB4a desk)",
      "",
      "## K9 rubric",
      ...Object.entries(session.k9)
        .filter(([k]) => k !== "pass")
        .map(([k, v]) => `- ${k}: ${v}`),
      "",
    ].join("\n");
    writeFileSync(join(outDir, "cursor-rag-transcript.md"), md, "utf8");

    console.log(md);
    console.log(`\nArtifacts: ${outDir}`);
    process.exitCode = session.k9.pass ? 0 : 1;
  } finally {
    clearTimeout(timeout);
    client.close();
    proc.kill();
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
