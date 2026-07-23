import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
function key() {
  if (process.env.OPENROUTER_API_KEY) return process.env.OPENROUTER_API_KEY;
  const p = path.join(root, ".secrets", "openrouter.env");
  for (const line of fs.readFileSync(p, "utf8").split(/\r?\n/)) {
    const m = line.trim().match(/^OPENROUTER_API_KEY\s*=\s*(.+)$/);
    if (m) return m[1].trim().replace(/^['"]|['"]$/g, "");
  }
  return null;
}
const API_KEY = key();
const withTools = process.argv.includes("--tools");
const models = process.argv.slice(2).filter((a) => a !== "--tools");
if (!models.length) models.push("google/gemma-4-31b-it:free");

const TOOLS = [
  {
    type: "function",
    function: {
      name: "web_extract",
      description: "Extract content from web page URLs. Returns markdown.",
      parameters: { type: "object", properties: { urls: { type: "array", items: { type: "string" } } }, required: ["urls"] },
    },
  },
  {
    type: "function",
    function: {
      name: "occam_transcode",
      description: "Read one web page as clean markdown. Just pass url.",
      parameters: { type: "object", properties: { url: { type: "string" } }, required: ["url"] },
    },
  },
];

for (const model of models) {
  const t0 = Date.now();
  try {
    const res = await fetch("https://openrouter.ai/api/v1/chat/completions", {
      method: "POST",
      headers: { Authorization: `Bearer ${API_KEY}`, "Content-Type": "application/json" },
      body: JSON.stringify({
        model,
        max_tokens: 32,
        temperature: 0,
        messages: [
          { role: "user", content: "Read https://example.com and tell me what it says." },
        ],
        ...(withTools ? { tools: TOOLS, tool_choice: "auto" } : {}),
      }),
    });
    const ms = Date.now() - t0;
    const hdr = {
      remaining: res.headers.get("x-ratelimit-remaining"),
      reset: res.headers.get("x-ratelimit-reset"),
      retryAfter: res.headers.get("retry-after"),
    };
    const text = await res.text();
    let toolCall = null;
    try {
      const j = JSON.parse(text);
      toolCall = j.choices?.[0]?.message?.tool_calls?.[0]?.function?.name ?? null;
    } catch {}
    console.log(`\n=== ${model} ${withTools ? "[tools]" : "[no-tools]"} ===`);
    console.log(`HTTP=${res.status} ms=${ms} firstTool=${toolCall} hdr=${JSON.stringify(hdr)}`);
    console.log(`BODY=${text.slice(0, 350)}`);
  } catch (e) {
    console.log(`\n=== ${model} ===`);
    console.log(`FETCH_ERR=${e.message}`);
  }
}
