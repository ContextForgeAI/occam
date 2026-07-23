import http from "node:http";
import { BrowserPool } from "./lib/browser-pool.mjs";
import { classifyBrowserLaunchError } from "./lib/browser-launch-options.mjs";

const args = process.argv.slice(2);
const portArg = args.find((a) => a.startsWith("--port="));
const port = Number(portArg?.split("=")[1] ?? process.env.OCCAM_BROWSER_DAEMON_PORT ?? 39_217);

const pool = new BrowserPool();

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

const server = http.createServer(async (req, res) => {
  try {
    if (req.method === "GET" && req.url === "/health") {
      const slotId = process.env.OCCAM_BROWSER_POOL_SLOT_ID;
      sendJson(res, 200, {
        ok: true,
        backend: "browser_daemon",
        ...(slotId != null && slotId !== "" ? { slot_id: Number(slotId) } : {}),
      });
      return;
    }

    if (req.method === "POST" && req.url === "/recycle") {
      await pool.recycle();
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

      const headersFile =
        body.headers_file
        ?? process.env.OCCAM_REQUEST_HEADERS_FILE
        ?? null;
      const storageStateFile = body.storage_state_file ?? null;

      // A3: the resolved genome arrives inline (playbook_overlay_json) — no temp file / shared-fs across
      // the process boundary. Parse leniently: a bad overlay must not fail the extract, just skip it.
      let overlaySeed = null;
      if (typeof body.playbook_overlay_json === "string" && body.playbook_overlay_json.length > 0) {
        try {
          overlaySeed = JSON.parse(body.playbook_overlay_json);
        } catch {
          console.error("[occam-browser-daemon] playbook_overlay_invalid code=parse_failed");
        }
      }

      const result = await pool.extract(url, {
        leanAssets: body.lean_assets !== false,
        consentAggressive: body.consent_aggressive === true,
        headersFile,
        storageStateFile,
        browserPlanFile: body.browser_plan_file ?? null,
        extractVariant: body.extract_variant,
        forceRecycle: body.force_recycle === true,
        features: body.features ?? null,
        timeoutMs: body.timeout_ms,
        overlaySeed,
        overlayStrict: body.playbook_overlay_strict === true,
      });

      sendJson(res, 200, result);
      return;
    }

    if (req.method === "POST" && req.url === "/skeleton") {
      const body = await readJson(req);
      const url = body.url;
      if (!url || typeof url !== "string") {
        sendJson(res, 400, { Ok: false, FailureCode: "missing_url" });
        return;
      }

      const headersFile =
        body.headers_file
        ?? process.env.OCCAM_REQUEST_HEADERS_FILE
        ?? null;

      const result = await pool.captureSkeleton(url, {
        maxNodes: body.max_nodes ?? 600,
        consentAggressive: body.consent_aggressive === true,
        headersFile,
      });

      sendJson(res, 200, result);
      return;
    }

    sendJson(res, 404, { ok: false, failure: "not_found" });
  } catch (error) {
    // A browser-availability failure (missing binary / missing system libs) surfaces here when the
    // pool's launch throws. Attach the same actionable reason + fix the one-shot worker emits, so the
    // typed remedy reaches the client on the pool path too (the default), not just the fallback.
    const provision = classifyBrowserLaunchError(error);
    sendJson(res, 500, {
      ok: false,
      backend: provision ? "browser_playwright" : "browser_daemon",
      failure: provision ? "playwright_missing" : (error?.name ?? "error"),
      ...(provision ? { reason: provision.reason, fix: provision.fix } : {}),
      message: error?.message ?? String(error),
    });
  }
});

// If the port is already taken, another daemon owns this slot — the host probes /health and will use it,
// so stand down cleanly (exit 0) instead of throwing an unhandled 'error' event and crashing loudly. Any
// other listen error is a real fault: report it and exit non-zero. (The C# pool also serialises spawns per
// slot so this rarely fires, but a stale daemon from a prior run can still hold the port.)
server.on("error", (err) => {
  if (err && err.code === "EADDRINUSE") {
    console.error(`[occam-browser-daemon] port ${port} already in use — another daemon owns this slot, standing down`);
    process.exit(0);
  }
  console.error(`[occam-browser-daemon] listen error on ${port}: ${err?.message ?? err}`);
  process.exit(1);
});

server.listen(port, "127.0.0.1", () => {
  console.error(`[occam-browser-daemon] listening http://127.0.0.1:${port}`);
});

async function shutdown() {
  await pool.close();
  server.close();
  process.exit(0);
}

process.on("SIGINT", shutdown);
process.on("SIGTERM", shutdown);
