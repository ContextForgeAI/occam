// P6-06 EF-043 runtime probe: css-extract.mjs performs egressFetch WITHOUT the private-IP DNS
// pin and WITHOUT the OCCAM_MAX_RESPONSE_BYTES body cap that http-extract applies.
// This starts a loopback (private/RFC-ish 127.0.0.1) HTTP server, serves an oversize HTML body,
// runs css-extract against it, and checks: (1) no private_url_blocked, (2) body cap ignored.
import { createServer } from "node:http";
import { spawn } from "node:child_process";
import { writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, "..", "..");
const cssExtract = join(repoRoot, "workers", "css-extract", "css-extract.mjs");

// ~2 MiB body, well above the smallest allowed cap (64 KiB) we set below.
const filler = "x".repeat(2 * 1024 * 1024);
const html = `<!doctype html><html><head><title>PRIVATE-LOOPBACK-TITLE</title></head><body><h1 id="h">secret intranet</h1><!-- ${filler} --></body></html>`;

const server = createServer((_req, res) => {
  res.writeHead(200, { "content-type": "text/html; charset=utf-8" });
  res.end(html);
});

const fieldsPath = join(here, "ef043-fields.json");
writeFileSync(fieldsPath, JSON.stringify({ title: { selector: "title", type: "text" }, heading: { selector: "#h", type: "text" } }), "utf8");

await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
const port = server.address().port;
const target = `http://127.0.0.1:${port}/`;

function runCss() {
  return new Promise((resolve) => {
    const env = { ...process.env, OCCAM_MAX_RESPONSE_BYTES: "65536" };
    const child = spawn(process.execPath, [cssExtract, target, fieldsPath], { env });
    let out = "";
    let err = "";
    child.stdout.on("data", (d) => (out += d));
    child.stderr.on("data", (d) => (err += d));
    child.on("close", () => resolve({ out, err }));
  });
}

const { out, err } = await runCss();
server.close();

let parsed;
try {
  parsed = JSON.parse(out.trim().split("\n").pop());
} catch {
  parsed = null;
}

const reachedLoopback = parsed?.ok === true && parsed?.data?.title === "PRIVATE-LOOPBACK-TITLE";
const notBlocked = parsed?.failure !== "private_url_blocked" && parsed?.failure !== "dns_error";
const capIgnored = typeof parsed?.html_length === "number" && parsed.html_length > 65536;

console.log(`CASE EF-043 | css-extract reaches loopback (no SSRF block) | OBSERVED=ok=${parsed?.ok},title=${parsed?.data?.title},failure=${parsed?.failure ?? "none"} | ${reachedLoopback && notBlocked ? "PASS" : "FAIL"}`);
console.log(`CASE EF-043 | OCCAM_MAX_RESPONSE_BYTES (64KiB) ignored by css-extract | OBSERVED=html_length=${parsed?.html_length},cap=65536 | ${capIgnored ? "PASS" : "FAIL"}`);
if (!parsed) {
  console.log(`NOTE EF-043 | raw stdout: ${out.slice(0, 300)}`);
  console.log(`NOTE EF-043 | raw stderr: ${err.slice(0, 300)}`);
}
