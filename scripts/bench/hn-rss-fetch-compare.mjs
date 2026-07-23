#!/usr/bin/env node
import { performance } from "node:perf_hooks";
import { resolve, dirname } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const REPO = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
const {
  resolveAndValidateHost,
  createPinnedDispatcher,
} = await import(pathToFileURL(resolve(REPO, "workers/shared/lib/private-ip.mjs")).href);
const { egressFetch } = await import(pathToFileURL(resolve(REPO, "workers/shared/lib/egress-proxy.mjs")).href);

const url = "https://hnrss.org/frontpage";
const host = new URL(url).hostname;
const headers = { "user-agent": "FF-Occam-perf/1.0", accept: "application/rss+xml,*/*" };

async function timed(name, fn) {
  const t0 = performance.now();
  const r = await fn();
  return { name, ms: Math.round(performance.now() - t0), ...r };
}

const results = [];
results.push(await timed("global_fetch", async () => {
  const res = await fetch(url, { headers, signal: AbortSignal.timeout(60_000) });
  const buf = Buffer.from(await res.arrayBuffer());
  return { status: res.status, bytes: buf.length };
}));
results.push(await timed("global_fetch_warm", async () => {
  const res = await fetch(url, { headers, signal: AbortSignal.timeout(60_000) });
  const buf = Buffer.from(await res.arrayBuffer());
  return { status: res.status, bytes: buf.length };
}));
results.push(await timed("egress_no_pin", async () => {
  const res = await egressFetch(url, { headers, signal: AbortSignal.timeout(60_000) });
  const buf = Buffer.from(await res.arrayBuffer());
  return { status: res.status, bytes: buf.length };
}));
const records = await resolveAndValidateHost(host, { allowPrivate: false });
const dispatcher = await createPinnedDispatcher(host, records, { allowPrivate: false });
results.push(await timed("egress_pinned", async () => {
  const res = await egressFetch(url, { headers, signal: AbortSignal.timeout(60_000), dispatcher });
  const buf = Buffer.from(await res.arrayBuffer());
  return { status: res.status, bytes: buf.length };
}));
results.push(await timed("egress_pinned_warm", async () => {
  const res = await egressFetch(url, { headers, signal: AbortSignal.timeout(60_000), dispatcher });
  const buf = Buffer.from(await res.arrayBuffer());
  return { status: res.status, bytes: buf.length };
}));
await dispatcher.destroy().catch(() => {});
console.log(JSON.stringify(results, null, 2));
