// P6-06 repro stand-in for workers/browser-extract/browser-daemon.mjs.
// Answers the pool health probe (GET /health) and records its own PID so the harness can
// observe whether BrowserPoolManager.InstallShared terminated it. No Playwright, no Chromium.
import { createServer } from "node:http";
import { writeFileSync } from "node:fs";

const portArg = process.argv.find((a) => a.startsWith("--port="));
const port = Number(portArg?.slice("--port=".length) ?? 0);
const pidFile = process.env.P6_FAKE_DAEMON_PIDFILE;

const server = createServer((req, res) => {
  if (req.url === "/health") {
    res.writeHead(200, { "content-type": "application/json" });
    res.end(JSON.stringify({ ok: true, pid: process.pid, port }));
    return;
  }
  res.writeHead(404);
  res.end();
});

server.listen(port, "127.0.0.1", () => {
  if (pidFile) {
    writeFileSync(pidFile, String(process.pid), "utf8");
  }
  process.stderr.write(`fake-daemon listening pid=${process.pid} port=${port}\n`);
});
