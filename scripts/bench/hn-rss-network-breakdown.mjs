#!/usr/bin/env node
/** Break down network_ms for HN RSS: DNS pin vs TTFB vs body. */
import { createRequire } from "node:module";
import { performance } from "node:perf_hooks";
import { resolve, dirname } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const REPO = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
const httpRequire = createRequire(resolve(REPO, "workers/http-extract/package.json"));
const {
  resolveAndValidateHost,
  createPinnedDispatcher,
} = await import(pathToFileURL(resolve(REPO, "workers/shared/lib/private-ip.mjs")).href);
const { egressFetch } = await import(pathToFileURL(resolve(REPO, "workers/shared/lib/egress-proxy.mjs")).href);

const url = "https://hnrss.org/frontpage";
const host = new URL(url).hostname;

async function once(label) {
  const t0 = performance.now();
  const records = await resolveAndValidateHost(host, { allowPrivate: false });
  const dnsMs = performance.now() - t0;
  const t1 = performance.now();
  const dispatcher = await createPinnedDispatcher(host, records, { allowPrivate: false });
  const pinMs = performance.now() - t1;
  const t2 = performance.now();
  const res = await egressFetch(url, {
    headers: { "user-agent": "FF-Occam-perf/1.0", accept: "application/rss+xml,*/*" },
    signal: AbortSignal.timeout(60_000),
    dispatcher,
  });
  const headersMs = performance.now() - t2;
  const t3 = performance.now();
  const buf = Buffer.from(await res.arrayBuffer());
  const bodyMs = performance.now() - t3;
  await dispatcher.destroy().catch(() => {});
  return {
    label,
    dnsMs: Math.round(dnsMs),
    pinMs: Math.round(pinMs),
    headersMs: Math.round(headersMs),
    bodyMs: Math.round(bodyMs),
    totalMs: Math.round(performance.now() - t0),
    status: res.status,
    bytes: buf.length,
    records: records.map((r) => `${r.family}:${r.address}`),
  };
}

const a = await once("cold");
const b = await once("warm");
console.log(JSON.stringify({ a, b }, null, 2));
