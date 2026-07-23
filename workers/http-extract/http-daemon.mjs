import http from "node:http";
import { runHttpExtract } from "./lib/http-extract-run.mjs";

const args = process.argv.slice(2);
const portArg = args.find((a) => a.startsWith("--port="));
const port = Number(portArg?.split("=")[1] ?? process.env.OCCAM_HTTP_DAEMON_PORT ?? 39_218);

let runsSinceRecycle = 0;
const RECYCLE_AFTER_RUNS = 10;

function readJson(req) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    req.on("data", (c) => chunks.push(c));
    req.on("end", () => {
      try {
        const raw = Buffer.concat(chunks).toString("utf8").trim();
        resolve(raw.length > 0 ? JSON.parse(raw) : {});
      } catch (error) {
        reject(error);
      }
    });
    req.on("error", reject);
  });
}

function sendJson(res, status, payload) {
  const body = JSON.stringify(payload);
  res.writeHead(status, {
    "Content-Type": "application/json; charset=utf-8",
    "Content-Length": Buffer.byteLength(body),
  });
  res.end(body);
}

function noteRunResult(result, forceRecycle) {
  if (forceRecycle || !result?.ok) {
    runsSinceRecycle = 0;
    return;
  }

  runsSinceRecycle += 1;
  if (runsSinceRecycle >= RECYCLE_AFTER_RUNS) {
    runsSinceRecycle = 0;
  }
}

/** Serializes /extract — JSDOM extract is not safe for concurrent runs in one process. */
let extractChain = Promise.resolve();

function enqueueExtract(work) {
  const run = extractChain.then(work, work);
  extractChain = run.catch(() => {});
  return run;
}

const server = http.createServer(async (req, res) => {
  try {
    if (req.method === "GET" && req.url === "/health") {
      sendJson(res, 200, { ok: true, backend: "http_daemon" });
      return;
    }

    if (req.method === "POST" && req.url === "/recycle") {
      runsSinceRecycle = 0;
      sendJson(res, 200, { ok: true, recycled: true });
      return;
    }

    if (req.method === "POST" && req.url === "/extract") {
      const body = await readJson(req);
      const url = body.url;
      if (!url || typeof url !== "string") {
        sendJson(res, 400, { ok: false, failure: "missing_url" });
        return;
      }

      const result = await enqueueExtract(async () => {
        const headersFile =
          body.headers_file
          ?? process.env.OCCAM_REQUEST_HEADERS_FILE
          ?? null;

        return runHttpExtract({
          url,
          headersFile,
          htmlFile: body.html_file ?? null,
          finalUrl: body.final_url ?? null,
          features: body.features ?? null,
        });
      });

      noteRunResult(result, body.force_recycle === true);
      sendJson(res, 200, result);
      return;
    }

    sendJson(res, 404, { ok: false, failure: "not_found" });
  } catch (error) {
    sendJson(res, 500, {
      ok: false,
      backend: "http_daemon",
      failure: error?.name ?? "error",
    });
  }
});

server.listen(port, "127.0.0.1", () => {
  process.stderr.write(`occam-http-daemon listening on 127.0.0.1:${port}\n`);
});
