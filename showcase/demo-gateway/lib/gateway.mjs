import { readFile } from "node:fs/promises";
import { createServer } from "node:http";
import { extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";
import { DemoUrlError, validateDemoUrl } from "./address-policy.mjs";
import { createDemoRateLimiters, takeAll } from "./rate-limit.mjs";

const publicRoot = fileURLToPath(new URL("../public/", import.meta.url));
const contentTypes = new Map([
  [".css", "text/css; charset=utf-8"],
  [".html", "text/html; charset=utf-8"],
  [".js", "text/javascript; charset=utf-8"],
]);

function intOption(value, fallback, min, max) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= min && parsed <= max ? parsed : fallback;
}

function clientKey(request, trustProxy) {
  if (trustProxy) {
    const forwarded = String(request.headers["x-forwarded-for"] ?? "").split(",")[0].trim();
    if (forwarded) return forwarded;
  }
  return request.socket.remoteAddress ?? "unknown";
}

function responseHeaders(allowedOrigin, requestOrigin) {
  const headers = {
    "Cache-Control": "no-store",
    "Content-Security-Policy": "default-src 'none'; frame-ancestors 'none'",
    "Referrer-Policy": "no-referrer",
    "X-Content-Type-Options": "nosniff",
  };
  if (allowedOrigin && requestOrigin === allowedOrigin) {
    headers["Access-Control-Allow-Origin"] = allowedOrigin;
    headers.Vary = "Origin";
  }
  return headers;
}

function sendJson(response, status, payload, headers = {}) {
  const body = Buffer.from(`${JSON.stringify(payload)}\n`, "utf8");
  response.writeHead(status, {
    ...headers,
    "Content-Length": body.length,
    "Content-Type": "application/json; charset=utf-8",
  });
  response.end(body);
}

async function readJson(request, maxBytes) {
  let total = 0;
  const chunks = [];
  for await (const chunk of request) {
    total += chunk.length;
    if (total > maxBytes) {
      throw Object.assign(new Error("request body too large"), { status: 413, code: "request_too_large" });
    }
    chunks.push(chunk);
  }
  try {
    return JSON.parse(Buffer.concat(chunks).toString("utf8"));
  } catch {
    throw Object.assign(new Error("invalid JSON"), { status: 400, code: "invalid_json" });
  }
}

function projectResult(result, maxMarkdownChars) {
  if (result?.ok !== true) {
    return {
      ok: false,
      url: result?.url ?? null,
      failure: {
        code: result?.failure?.code ?? "transcode_failed",
        message: result?.failure?.message ?? "Occam could not read this page.",
        statusCode: result?.failure?.statusCode ?? null,
        retryable: result?.failure?.retryable ?? null,
      },
    };
  }
  const markdown = typeof result.markdown === "string" ? result.markdown : "";
  const truncated = markdown.length > maxMarkdownChars;
  return {
    ok: true,
    url: result.url ?? null,
    markdown: truncated ? markdown.slice(0, maxMarkdownChars) : markdown,
    backend: result.backend ?? "http",
    contentHash: result.contentHash ?? null,
    truncated,
  };
}

async function serveStatic(request, response) {
  const requested = new URL(request.url, "http://demo.invalid").pathname;
  const relative = requested === "/" ? "index.html" : requested.slice(1);
  const safe = normalize(relative).replace(/^(\.\.(\\|\/|$))+/, "");
  if (!new Set(["index.html", "app.js", "styles.css"]).has(safe)) return false;
  const bytes = await readFile(join(publicRoot, safe));
  response.writeHead(200, {
    "Cache-Control": "no-store",
    "Content-Length": bytes.length,
    "Content-Security-Policy": "default-src 'self'; connect-src 'self'; form-action 'none'; frame-ancestors 'none'; base-uri 'none'",
    "Content-Type": contentTypes.get(extname(safe)) ?? "application/octet-stream",
    "Referrer-Policy": "no-referrer",
    "X-Content-Type-Options": "nosniff",
  });
  response.end(bytes);
  return true;
}

export function createDemoGateway(options) {
  const runtime = options.runtime;
  const enabled = options.enabled !== false;
  const validateUrl = options.validateUrl ?? validateDemoUrl;
  const allowedOrigin = options.allowedOrigin ?? "";
  const trustProxy = options.trustProxy === true;
  const maxBodyBytes = intOption(options.maxBodyBytes, 2048, 256, 16_384);
  const maxMarkdownChars = intOption(options.maxMarkdownChars, 12_000, 512, 50_000);
  const maxConcurrency = intOption(options.maxConcurrency, 2, 1, 16);
  const limiters = options.limiters ?? createDemoRateLimiters({
    perMinute: intOption(options.perMinute, 3, 1, 120),
    perDay: intOption(options.perDay, 30, 1, 10_000),
  });
  let active = 0;

  const server = createServer(async (request, response) => {
    const requestOrigin = String(request.headers.origin ?? "");
    const apiHeaders = responseHeaders(allowedOrigin, requestOrigin);
    try {
      if (request.method === "GET" && request.url === "/healthz") {
        sendJson(response, 200, { ok: true, service: "occam-demo-gateway", status: "prototype" }, apiHeaders);
        return;
      }
      if (request.method === "GET" && await serveStatic(request, response)) return;

      if (request.method === "OPTIONS" && request.url === "/v1/transcode") {
        if (allowedOrigin && requestOrigin !== allowedOrigin) {
          sendJson(response, 403, { ok: false, failure: { code: "origin_blocked", message: "origin is not allowed" } }, apiHeaders);
          return;
        }
        response.writeHead(204, {
          ...apiHeaders,
          "Access-Control-Allow-Headers": "content-type",
          "Access-Control-Allow-Methods": "POST, OPTIONS",
        });
        response.end();
        return;
      }

      if (request.method !== "POST" || request.url !== "/v1/transcode") {
        sendJson(response, 404, { ok: false, failure: { code: "not_found", message: "route not found" } }, apiHeaders);
        return;
      }
      if (!enabled) {
        sendJson(response, 503, { ok: false, failure: { code: "demo_disabled", message: "public demo is disabled" } }, apiHeaders);
        return;
      }
      if (allowedOrigin && requestOrigin && requestOrigin !== allowedOrigin) {
        sendJson(response, 403, { ok: false, failure: { code: "origin_blocked", message: "origin is not allowed" } }, apiHeaders);
        return;
      }
      if (!String(request.headers["content-type"] ?? "").toLowerCase().startsWith("application/json")) {
        sendJson(response, 415, { ok: false, failure: { code: "unsupported_media_type", message: "use application/json" } }, apiHeaders);
        return;
      }

      const rate = takeAll(limiters, clientKey(request, trustProxy));
      if (!rate.allowed) {
        sendJson(response, 429, { ok: false, failure: { code: "demo_rate_limited", message: "public demo rate limit reached", retryAfterSeconds: rate.retryAfterSeconds } }, {
          ...apiHeaders,
          "Retry-After": String(rate.retryAfterSeconds),
        });
        return;
      }
      if (active >= maxConcurrency) {
        sendJson(response, 503, { ok: false, failure: { code: "demo_busy", message: "public demo is at its concurrency limit" } }, apiHeaders);
        return;
      }
      active += 1;
      try {
        const body = await readJson(request, maxBodyBytes);
        if (!body || typeof body !== "object" || Array.isArray(body) || Object.keys(body).some((key) => key !== "url")) {
          sendJson(response, 400, { ok: false, failure: { code: "invalid_arguments", message: "request must contain only the url field" } }, apiHeaders);
          return;
        }

        const url = await validateUrl(body.url);
        const result = await runtime.transcode(url);
        sendJson(response, result?.ok === true ? 200 : 422, projectResult(result, maxMarkdownChars), apiHeaders);
      } finally {
        active -= 1;
      }
    } catch (error) {
      if (error instanceof DemoUrlError) {
        sendJson(response, 400, { ok: false, failure: { code: error.code, message: error.message } }, apiHeaders);
        return;
      }
      const safeRequestErrors = new Set(["invalid_json", "request_too_large"]);
      const safeRequestError = Number.isInteger(error?.status) && safeRequestErrors.has(error?.code);
      const status = safeRequestError ? error.status : 503;
      const code = safeRequestError ? error.code : "demo_runtime_unavailable";
      sendJson(response, status, { ok: false, failure: { code, message: status === 503 ? "public demo is temporarily unavailable" : error.message } }, apiHeaders);
    }
  });
  server.headersTimeout = 5_000;
  server.requestTimeout = 45_000;
  server.keepAliveTimeout = 5_000;
  return server;
}
