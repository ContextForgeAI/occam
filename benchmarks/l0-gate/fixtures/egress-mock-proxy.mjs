import http from "node:http";
import https from "node:https";
import net from "node:net";

/**
 * Minimal forward HTTP/HTTPS proxy for gate L2_EGRESS_OK (127.0.0.1 only).
 * Set MOCK_PROXY_REJECT=1 to return 502 for all requests (unreachable-upstream case).
 * @returns {Promise<{ server: import('node:http').Server, url: string, close: () => Promise<void> }>}
 */
export function startMockForwardProxy() {
  const rejectAll = process.env.MOCK_PROXY_REJECT === "1";

  return new Promise((resolve, reject) => {
    const server = http.createServer((req, res) => {
      if (rejectAll) {
        res.writeHead(502);
        res.end("upstream rejected");
        return;
      }

      let targetUrl;
      try {
        targetUrl = new URL(req.url ?? "");
      } catch {
        res.writeHead(400);
        res.end();
        return;
      }

      const transport = targetUrl.protocol === "https:" ? https : http;
      const proxyReq = transport.request(
        {
          hostname: targetUrl.hostname,
          port: targetUrl.port || (targetUrl.protocol === "https:" ? 443 : 80),
          path: `${targetUrl.pathname}${targetUrl.search}`,
          method: req.method,
          headers: {
            ...req.headers,
            host: targetUrl.host,
          },
        },
        (proxyRes) => {
          res.writeHead(proxyRes.statusCode ?? 502, proxyRes.headers);
          proxyRes.pipe(res);
        },
      );

      proxyReq.on("error", () => {
        if (!res.headersSent) {
          res.writeHead(502);
        }
        res.end();
      });

      req.pipe(proxyReq);
    });

    server.on("connect", (req, clientSocket, head) => {
      if (rejectAll) {
        clientSocket.write("HTTP/1.1 502 Bad Gateway\r\n\r\n");
        clientSocket.end();
        return;
      }

      const [host, portText] = (req.url ?? "").split(":");
      const port = Number(portText) || 443;
      const upstream = net.connect(port, host, () => {
        clientSocket.write("HTTP/1.1 200 Connection Established\r\n\r\n");
        if (head?.length) {
          upstream.write(head);
        }
        upstream.pipe(clientSocket);
        clientSocket.pipe(upstream);
      });

      upstream.on("error", () => {
        clientSocket.end();
      });
      clientSocket.on("error", () => {
        upstream.end();
      });
    });

    server.on("error", reject);
    server.listen(0, "127.0.0.1", () => {
      const address = server.address();
      const port = typeof address === "object" && address ? address.port : 0;
      resolve({
        server,
        url: `http://127.0.0.1:${port}`,
        close: () =>
          new Promise((done, failed) => {
            server.close((error) => (error ? failed(error) : done()));
          }),
      });
    });
  });
}
