import http from "node:http";

/**
 * @param {{ bodyBytes?: number, path?: string }} [options]
 */
export async function startOversizeHttpFixture(options = {}) {
  const bodyBytes = options.bodyBytes ?? 2 * 1024 * 1024;
  const path = options.path ?? "/oversize";
  const chunk = "<p>oversize-fixture</p>\n";
  const repeat = Math.ceil(bodyBytes / Buffer.byteLength(chunk, "utf8"));
  const body = chunk.repeat(repeat).slice(0, bodyBytes);

  const server = http.createServer((req, res) => {
    if (req.url === path || req.url === `${path}/`) {
      res.writeHead(200, {
        "Content-Type": "text/html; charset=utf-8",
        "Content-Length": Buffer.byteLength(body, "utf8"),
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
    throw new Error("oversize fixture bind failed");
  }

  const url = `http://127.0.0.1:${address.port}${path}`;
  return {
    url,
    port: address.port,
  /**
   * @returns {Promise<void>}
   */
    close: () => new Promise((resolve, reject) => {
      server.close((error) => (error ? reject(error) : resolve()));
    }),
  };
}
