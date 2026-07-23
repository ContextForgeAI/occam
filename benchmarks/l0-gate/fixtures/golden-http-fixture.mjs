import http from "node:http";
import { readFileSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join, basename } from "node:path";

const GOLDEN_DIR = join(dirname(fileURLToPath(import.meta.url)), "golden");

/**
 * Serves frozen golden HTML fixtures from ./golden/<name>.html at /<name>.
 * Deterministic — no live network — so the L9 golden gate catches CODE
 * regressions, not live-site drift (the probe-nuxt lesson).
 * @returns {Promise<{ url: string, port: number, close: () => Promise<void> }>}
 */
export async function startGoldenHttpFixture() {
  const server = http.createServer((req, res) => {
    // /<name> → golden/<name>.html ; basename guards against path traversal.
    const name = basename((req.url ?? "/").replace(/^\/+/, "").split("?")[0]);
    const hasExt = name.endsWith(".html") || name.endsWith(".xml");
    const file = join(GOLDEN_DIR, hasExt ? name : `${name}.html`);
    if (name && existsSync(file) && file.startsWith(GOLDEN_DIR)) {
      const body = readFileSync(file);
      const contentType = file.endsWith(".xml")
        ? "application/rss+xml; charset=utf-8"
        : "text/html; charset=utf-8";
      res.writeHead(200, {
        "Content-Type": contentType,
        "Content-Length": body.length,
      });
      res.end(body);
      return;
    }
    res.writeHead(404, { "Content-Type": "text/plain" });
    res.end("not found");
  });

  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  const address = server.address();
  if (!address || typeof address === "string") {
    throw new Error("golden fixture bind failed");
  }
  return {
    url: `http://127.0.0.1:${address.port}`,
    port: address.port,
    close: () => new Promise((resolve, reject) => {
      server.close((error) => (error ? reject(error) : resolve()));
    }),
  };
}
