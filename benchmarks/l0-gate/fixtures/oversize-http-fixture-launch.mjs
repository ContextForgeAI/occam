import { startOversizeHttpFixture } from "./oversize-http-fixture.mjs";

const bodyBytes = Number.parseInt(process.env.OVERSIZE_FIXTURE_BYTES ?? "2097152", 10);
const fixture = await startOversizeHttpFixture({ bodyBytes });
console.log(JSON.stringify({ url: fixture.url, bodyBytes }));

const shutdown = async () => {
  await fixture.close();
  process.exit(0);
};

process.on("SIGINT", shutdown);
process.on("SIGTERM", shutdown);

setInterval(() => {}, 1 << 30);
