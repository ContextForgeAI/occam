import assert from "node:assert/strict";
import { createServer } from "node:http";
import { spawn } from "node:child_process";
import { mkdtempSync, writeFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  extractStructuredData,
  NuxtAttrDisabledError,
} from "./lib/css-schema-extract.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const worker = join(here, "css-extract.mjs");

// --- EF-013: nuxt attr never evals page JS ---
const hostileHtml = `<!doctype html><html><body>
<script>window.__NUXT__={}; require('fs').writeFileSync('SHOULD_NOT_EXIST','pwn')</script>
</body></html>`;

assert.throws(
  () =>
    extractStructuredData(hostileHtml, "https://example.com/", {
      x: { attr: "nuxt", selector: "data.a" },
    }),
  (err) => err instanceof NuxtAttrDisabledError && err.code === "nuxt_attr_disabled",
);

// Benign-looking payload also refused (no eval path at all).
assert.throws(
  () =>
    extractStructuredData(
      `<script>window.__NUXT__={ data: { a: 1 } }</script>`,
      "https://example.com/",
      { x: { attr: "nuxt", selector: "data.a" } },
    ),
  NuxtAttrDisabledError,
);

// Non-nuxt fields still work.
const okData = extractStructuredData(
  `<!doctype html><html><head><title>T</title></head><body><h1 id="h">Hi</h1></body></html>`,
  "https://example.com/",
  { title: { selector: "title" }, heading: { selector: "#h" } },
);
assert.equal(okData.title, "T");
assert.equal(okData.heading, "Hi");

// --- EF-043: private URL fail-closed + body cap ---
function runWorker(args, env = {}) {
  return new Promise((resolve) => {
    const child = spawn(process.execPath, [worker, ...args], {
      env: { ...process.env, ...env },
      cwd: here,
    });
    let out = "";
    let err = "";
    child.stdout.on("data", (d) => (out += d));
    child.stderr.on("data", (d) => (err += d));
    child.on("close", () => {
      let parsed = null;
      try {
        parsed = JSON.parse(out.trim().split("\n").pop());
      } catch {
        // leave null
      }
      resolve({ out, err, parsed });
    });
  });
}

const tmp = mkdtempSync(join(tmpdir(), "css-extract-selftest-"));
const fieldsPath = join(tmp, "fields.json");
writeFileSync(
  fieldsPath,
  JSON.stringify({ title: { selector: "title" }, heading: { selector: "#h" } }),
  "utf8",
);

const prevAllow = process.env.OCCAM_ALLOW_PRIVATE_URLS;
delete process.env.OCCAM_ALLOW_PRIVATE_URLS;

const filler = "x".repeat(2 * 1024 * 1024);
const html = `<!doctype html><html><head><title>PRIVATE-LOOPBACK-TITLE</title></head><body><h1 id="h">secret intranet</h1><!-- ${filler} --></body></html>`;

const server = createServer((_req, res) => {
  res.writeHead(200, { "content-type": "text/html; charset=utf-8" });
  res.end(html);
});

await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
const port = server.address().port;
const target = `http://127.0.0.1:${port}/`;

try {
  const blocked = await runWorker([target, fieldsPath], {
    OCCAM_MAX_RESPONSE_BYTES: "65536",
    OCCAM_ALLOW_PRIVATE_URLS: "",
  });
  assert.equal(blocked.parsed?.ok, false, `expected fail-closed, got ${JSON.stringify(blocked.parsed)}`);
  assert.ok(
    blocked.parsed?.failure === "private_ip_blocked" ||
      blocked.parsed?.failure === "private_url_blocked" ||
      blocked.parsed?.failure === "dns_resolution_failed",
    `unexpected failure: ${blocked.parsed?.failure}`,
  );

  // With private allowed, body cap must still reject oversize (default fail mode).
  const capped = await runWorker([target, fieldsPath], {
    OCCAM_MAX_RESPONSE_BYTES: "65536",
    OCCAM_ALLOW_PRIVATE_URLS: "1",
    OCCAM_HTTP_OVERSIZE_MODE: "fail",
  });
  assert.equal(capped.parsed?.ok, false, `expected body cap fail, got ${JSON.stringify(capped.parsed)}`);
  assert.equal(capped.parsed?.failure, "response_too_large");
} finally {
  server.close();
  rmSync(tmp, { recursive: true, force: true });
  if (prevAllow === undefined) {
    delete process.env.OCCAM_ALLOW_PRIVATE_URLS;
  } else {
    process.env.OCCAM_ALLOW_PRIVATE_URLS = prevAllow;
  }
}

console.log("css-extract.selftest: OK");
