import { writeFileSync } from "node:fs";
import { createInterface } from "node:readline";

const markerPath = process.argv[2];
const mode = process.argv[3] || "normal";

if (mode === "exit-immediately") {
  process.exit(7);
}

let initialized = false;
let requestedProtocolVersion = null;
const input = createInterface({ input: process.stdin, crlfDelay: Infinity });

function respond(id, result) {
  process.stdout.write(JSON.stringify({ jsonrpc: "2.0", id, result }) + "\n");
}

for await (const line of input) {
  if (!line.trim()) continue;
  const message = JSON.parse(line);

  if (message.method === "initialize") {
    requestedProtocolVersion = message.params.protocolVersion;
    respond(message.id, {
      protocolVersion:
        mode === "legacy-protocol"
          ? "2024-11-05"
          : mode === "unsupported-protocol"
            ? "2099-01-01"
            : message.params.protocolVersion,
      capabilities: { tools: { listChanged: false } },
      serverInfo: { name: "fake-occam", version: "1.0.0" },
    });
    continue;
  }

  if (message.method === "notifications/initialized") {
    initialized = true;
    continue;
  }

  if (message.method === "tools/list") {
    respond(message.id, {
      tools: [{ name: "occam_probe", description: "Test probe", inputSchema: {} }],
    });
    continue;
  }

  if (message.method === "tools/call") {
    if (mode === "ignore-tool") continue;
    respond(message.id, {
      content: [
        {
          type: "text",
          text: JSON.stringify({
            ok: true,
            tool: message.params.name,
            arguments: message.params.arguments,
            initialized,
            requestedProtocolVersion,
          }),
        },
      ],
      isError: false,
    });
  }
}

if (markerPath) writeFileSync(markerPath, "closed", "utf8");
