#!/usr/bin/env node
// Capture proxy — records the EXACT chat/completions request the Hermes gateway sends to the model,
// so a tool-selection A/B can be replayed against the REAL context (full system prompt + the whole
// crowded tools array as the live agent actually sees it), not a hand-built approximation.
//
// The isolated 2-tool A/B over-predicted occam-first (100%/88%) vs the live agent (web_extract first).
// The confound is the full request. This proxy captures one real request; replay-capture.mjs then
// swaps only the occam_transcode description/name and re-asks the model, measuring the true lift.
//
// Use (on the gateway host):
//   node scripts/bench/capture-proxy.mjs            # listens on 127.0.0.1:8799, forwards to OpenRouter
// Then point the gateway's model base_url at http://127.0.0.1:8799/api/v1 for ONE turn, inject a
// "read a page" message, and revert. Captured requests land in /tmp/captured-requests.jsonl.

import http from "node:http";
import { writeFile, appendFile } from "node:fs/promises";

const PORT = Number(process.env.CAPTURE_PORT || 8799);
const UPSTREAM = process.env.CAPTURE_UPSTREAM || "https://openrouter.ai";
const OUT = process.env.CAPTURE_OUT || "/tmp/captured-requests.jsonl";
let captured = 0;

const server = http.createServer(async (req, res) => {
  const chunks = [];
  for await (const c of req) chunks.push(c);
  const bodyBuf = Buffer.concat(chunks);

  // Record only the model calls (chat/completions); pass everything else through untouched.
  if (req.method === "POST" && req.url.includes("/chat/completions") && bodyBuf.length) {
    try {
      const parsed = JSON.parse(bodyBuf.toString("utf8"));
      const toolNames = (parsed.tools || []).map((t) => t.function?.name);
      await appendFile(OUT, JSON.stringify(parsed) + "\n");
      captured++;
      console.error(`[capture] #${captured} model=${parsed.model} tools=${toolNames.length} ` +
        `(occam? ${toolNames.some((n) => n && n.includes("occam"))}, web_extract? ${toolNames.includes("web_extract")}) -> ${OUT}`);
    } catch (e) {
      console.error(`[capture] non-JSON body, forwarding as-is: ${e.message}`);
    }
  }

  // Forward verbatim to the real upstream and stream the response straight back.
  const headers = { ...req.headers };
  delete headers.host;
  delete headers["content-length"];
  let upstream;
  try {
    upstream = await fetch(UPSTREAM + req.url, {
      method: req.method,
      headers,
      body: bodyBuf.length ? bodyBuf : undefined,
    });
  } catch (e) {
    res.writeHead(502, { "content-type": "application/json" });
    res.end(JSON.stringify({ error: `capture-proxy upstream failed: ${e.message}` }));
    return;
  }
  res.writeHead(upstream.status, { "content-type": upstream.headers.get("content-type") || "application/json" });
  if (upstream.body) {
    const reader = upstream.body.getReader();
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      res.write(Buffer.from(value));
    }
  }
  res.end();
});

await writeFile(OUT, "").catch(() => {});
server.listen(PORT, "127.0.0.1", () =>
  console.error(`[capture] listening http://127.0.0.1:${PORT} -> ${UPSTREAM}; requests -> ${OUT}`));
