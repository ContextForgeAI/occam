import { startGoldenHttpFixture } from "./golden-http-fixture.mjs";

const fixture = await startGoldenHttpFixture();
console.log(JSON.stringify({ url: fixture.url }));

const shutdown = async () => {
  await fixture.close();
  process.exit(0);
};

process.on("SIGINT", shutdown);
process.on("SIGTERM", shutdown);

setInterval(() => {}, 1 << 30);
