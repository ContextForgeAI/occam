#!/usr/bin/env node
import assert from "node:assert/strict";
import { once } from "node:events";
import { SsrfBlockedError } from "../../workers/shared/lib/private-ip.mjs";
import { validateDemoUrl } from "./lib/address-policy.mjs";
import { createDemoGateway } from "./lib/gateway.mjs";
import { FixedWindowRateLimiter } from "./lib/rate-limit.mjs";

async function expectCode(promise, code) {
  await assert.rejects(promise, (error) => error?.code === code);
}

await expectCode(validateDemoUrl("file:///etc/passwd", { resolve: async () => [] }), "invalid_url");
await expectCode(validateDemoUrl("https://user:secret@example.com/", { resolve: async () => [] }), "url_credentials_blocked");
await expectCode(validateDemoUrl("https://example.com:8443/", { resolve: async () => [] }), "port_blocked");
await expectCode(validateDemoUrl("http://example.com:443/", { resolve: async () => [] }), "port_blocked");
await expectCode(validateDemoUrl("http://127.0.0.1/", {
  resolve: async () => { throw new SsrfBlockedError("private_ip_blocked"); },
}), "private_url_blocked");
await expectCode(validateDemoUrl("http://[::1]/", {
  resolve: async (hostname) => {
    assert.equal(hostname, "::1");
    throw new SsrfBlockedError("private_ip_blocked");
  },
}), "private_url_blocked");
assert.equal(await validateDemoUrl("https://example.com/path#fragment", {
  resolve: async () => [{ address: "93.184.216.34", family: 4 }],
}), "https://example.com/path");

let now = 1_000;
const limiter = new FixedWindowRateLimiter({ limit: 2, windowMs: 1_000, now: () => now });
assert.equal(limiter.take("client").allowed, true);
assert.equal(limiter.take("client").allowed, true);
assert.equal(limiter.take("client").allowed, false);
now += 1_001;
assert.equal(limiter.take("client").allowed, true);

let releaseFirst;
const firstBlocked = new Promise((resolve) => { releaseFirst = resolve; });
let calls = 0;
let slowStarted = false;
const runtime = {
  async transcode(url) {
    calls += 1;
    if (url.includes("explode")) {
      const error = new Error("spawn failed at C:\\secret\\path");
      error.code = "EPERM";
      throw error;
    }
    if (url.includes("slow")) {
      slowStarted = true;
      await firstBlocked;
    }
    return {
      ok: true,
      url: { url, finalUrl: url },
      markdown: "# Result\n\nUseful content",
      backend: "http",
      contentHash: "abc123",
    };
  },
};
const server = createDemoGateway({
  runtime,
  validateUrl: async (url) => new URL(url).toString(),
  maxConcurrency: 1,
  allowedOrigin: "https://contextforgeai.github.io",
  perMinute: 20,
  perDay: 20,
  maxMarkdownChars: 512,
});
server.listen(0, "127.0.0.1");
await once(server, "listening");
const address = server.address();
assert.ok(address && typeof address !== "string");
const base = `http://127.0.0.1:${address.port}`;

const health = await fetch(`${base}/healthz`).then((response) => response.json());
assert.equal(health.ok, true);

const valid = await fetch(`${base}/v1/transcode`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ url: "https://example.com/" }),
}).then(async (response) => ({ status: response.status, body: await response.json() }));
assert.equal(valid.status, 200);
assert.equal(valid.body.ok, true);
assert.equal(valid.body.truncated, false);

const extraField = await fetch(`${base}/v1/transcode`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ url: "https://example.com/", headers: { Cookie: "secret" } }),
}).then(async (response) => ({ status: response.status, body: await response.json() }));
assert.equal(extraField.status, 400);
assert.equal(extraField.body.failure.code, "invalid_arguments");

const wrongOrigin = await fetch(`${base}/v1/transcode`, {
  method: "POST",
  headers: { "Content-Type": "application/json", Origin: "https://attacker.invalid" },
  body: JSON.stringify({ url: "https://example.com/" }),
}).then(async (response) => ({ status: response.status, body: await response.json() }));
assert.equal(wrongOrigin.status, 403);
assert.equal(wrongOrigin.body.failure.code, "origin_blocked");

const oversized = await fetch(`${base}/v1/transcode`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ url: `https://example.com/${"a".repeat(2200)}` }),
}).then(async (response) => ({ status: response.status, body: await response.json() }));
assert.equal(oversized.status, 413);
assert.equal(oversized.body.failure.code, "request_too_large");

const runtimeError = await fetch(`${base}/v1/transcode`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ url: "https://explode.example/" }),
}).then(async (response) => ({ status: response.status, body: await response.json() }));
assert.equal(runtimeError.status, 503);
assert.equal(runtimeError.body.failure.code, "demo_runtime_unavailable");
assert.equal(JSON.stringify(runtimeError.body).includes("secret"), false);
assert.equal(JSON.stringify(runtimeError.body).includes("EPERM"), false);

const slow = fetch(`${base}/v1/transcode`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ url: "https://slow.example/" }),
});
while (!slowStarted) await new Promise((resolve) => setTimeout(resolve, 5));
const busy = await fetch(`${base}/v1/transcode`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ url: "https://busy.example/" }),
}).then(async (response) => ({ status: response.status, body: await response.json() }));
assert.equal(busy.status, 503);
assert.equal(busy.body.failure.code, "demo_busy");
releaseFirst();
await slow;

await new Promise((resolve, reject) => server.close((error) => error ? reject(error) : resolve()));

let loadActive = 0;
let loadPeak = 0;
const loadServer = createDemoGateway({
  runtime: {
    async transcode(url) {
      loadActive += 1;
      loadPeak = Math.max(loadPeak, loadActive);
      await new Promise((resolve) => setTimeout(resolve, 40));
      loadActive -= 1;
      return { ok: true, url: { url, finalUrl: url }, markdown: "ok", backend: "http" };
    },
  },
  validateUrl: async (url) => new URL(url).toString(),
  maxConcurrency: 2,
  perMinute: 100,
  perDay: 100,
});
loadServer.listen(0, "127.0.0.1");
await once(loadServer, "listening");
const loadAddress = loadServer.address();
assert.ok(loadAddress && typeof loadAddress !== "string");
const loadBase = `http://127.0.0.1:${loadAddress.port}`;
const loadResponses = await Promise.all(Array.from({ length: 10 }, (_, index) =>
  fetch(`${loadBase}/v1/transcode`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ url: `https://example.com/${index}` }),
  })));
const loadStatuses = loadResponses.map((response) => response.status);
assert.equal(loadStatuses.filter((status) => status === 200).length, 2);
assert.equal(loadStatuses.filter((status) => status === 503).length, 8);
assert.equal(loadPeak, 2);
await new Promise((resolve, reject) => loadServer.close((error) => error ? reject(error) : resolve()));

console.log("DEMO_GATEWAY_SELFTEST_OK");
