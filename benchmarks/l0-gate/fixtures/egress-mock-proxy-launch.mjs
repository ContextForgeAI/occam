import { startMockForwardProxy } from "./egress-mock-proxy.mjs";

const reject = process.argv.includes("--reject");
if (reject) {
  process.env.MOCK_PROXY_REJECT = "1";
}

const proxy = await startMockForwardProxy();
console.log(JSON.stringify({ url: proxy.url, reject }));

const shutdown = async () => {
  await proxy.close();
  process.exit(0);
};

process.on("SIGINT", shutdown);
process.on("SIGTERM", shutdown);

setInterval(() => {}, 1 << 30);
