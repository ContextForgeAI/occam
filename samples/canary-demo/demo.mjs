#!/usr/bin/env node
// Walks all four proof-of-read verdicts against a live probe server.
//
// Every step is a real HTTP round trip — nothing here is simulated. Two of the verdicts need the
// server configured away from its defaults to be reachable inside a demo:
//
//   READ_STALE      needs a bucket to roll over, so the demo runs 30-second buckets with zero
//                   fresh tolerance instead of the default 300s/±1 (which would take two hours).
//   REPLAY_SUSPECT  needs an authentic sentinel whose issuance record is gone. The demo shrinks the
//                   issuance log to its minimum and then overflows it, which is exactly the
//                   eviction caveat documented in PROBE_PROTOCOL.md §6.2.
//
// Usage: node samples/canary-demo/demo.mjs

import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const PORT = Number(process.env.CANARY_DEMO_PORT ?? 8971);
const BUCKET_SECONDS = 30;
const ISSUE_LOG_CAPACITY = 256; // protocol minimum

const hostDll = join(repoRoot, 'src', 'FFOccamMcp.Core', 'bin', 'Release', 'net10.0', 'OccamMcp.Core.dll');
if (!existsSync(hostDll)) {
  console.error(`host not built: ${hostDll}`);
  console.error('run: dotnet build src/FFOccamMcp.Core -c Release');
  process.exit(2);
}

const serverEnv = {
  ...process.env,
  OCCAM_CANARY_BUCKET_SECONDS: String(BUCKET_SECONDS),
  OCCAM_CANARY_FRESH_TOLERANCE: '0',
  OCCAM_CANARY_STALE_HORIZON: '24',
  OCCAM_CANARY_ISSUE_LOG_CAPACITY: String(ISSUE_LOG_CAPACITY),
  // The demo issues a few hundred sentinels on purpose; the default ceiling of 30 would refuse.
  OCCAM_CANARY_RATE_LIMIT: '10000',
};

const server = spawn('dotnet', [hostDll, 'canary', 'serve', '--port', String(PORT)], {
  env: serverEnv,
  stdio: ['ignore', 'ignore', 'pipe'],
});

const base = `http://127.0.0.1:${PORT}`;
let failures = 0;

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function waitForServer() {
  const listening = new Promise((resolvePromise, rejectPromise) => {
    const timer = setTimeout(() => rejectPromise(new Error('server did not report listening in 30s')), 30_000);
    server.stderr.on('data', (chunk) => {
      if (String(chunk).includes('canary_probe_listening')) {
        clearTimeout(timer);
        resolvePromise();
      }
    });
    server.on('exit', (code) => {
      clearTimeout(timer);
      rejectPromise(new Error(`server exited early with code ${code}`));
    });
  });
  await listening;
  // Kestrel prints before the first accept completes on some platforms.
  await sleep(150);
}

function newSessionId(label) {
  return `${label}-${Math.random().toString(36).slice(2, 10)}`;
}

async function fetchSentinel(sessionId) {
  const response = await fetch(`${base}/probe/canary/${sessionId}`);
  if (!response.ok) {
    throw new Error(`probe returned ${response.status} for ${sessionId}`);
  }
  const html = await response.text();
  const match = html.match(/name="occam-sentinel" content="([^"]+)"/);
  if (!match) {
    throw new Error('probe document carried no sentinel');
  }
  return match[1];
}

async function verify(sessionId, sentinel) {
  const url = `${base}/probe/canary/${sessionId}/verify?sentinel=${encodeURIComponent(sentinel)}`;
  return (await fetch(url)).json();
}

function report(step, role, result, expected) {
  const pass = result.verdict === expected;
  if (!pass) {
    failures++;
  }
  const mark = pass ? ' ' : '!';
  console.log(
    `${mark}[${step}/4] ${role.padEnd(24)} -> ${String(result.verdict).padEnd(16)} ok=${result.ok}` +
    (pass ? '' : `   EXPECTED ${expected}`),
  );
  console.log(`        ${result.detail}`);
}

try {
  await waitForServer();
  console.log(`probe server on ${base} (buckets of ${BUCKET_SECONDS}s, fresh tolerance 0)\n`);

  // 1. An agent that actually read the page and reports the value verbatim.
  const honest = newSessionId('honest');
  const honestSentinel = await fetchSentinel(honest);
  report(1, 'honest reader', await verify(honest, honestSentinel), 'READ_VERIFIED');

  // 2. An agent that never read the page and produces a plausible-looking token.
  const fabricator = newSessionId('fabricator');
  await fetchSentinel(fabricator);
  report(2, 'fabricator', await verify(fabricator, 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'), 'HALLUCINATED');

  // 3. A real read reported after its bucket has rolled over. Wait for the boundary rather than
  //    faking a clock, so this stays an honest HTTP observation.
  const late = newSessionId('late');
  const lateSentinel = await fetchSentinel(late);
  const nowSeconds = Date.now() / 1000;
  const nextBoundary = (Math.floor(nowSeconds / BUCKET_SECONDS) + 1) * BUCKET_SECONDS;
  const waitMs = Math.ceil((nextBoundary - nowSeconds + 1) * 1000);
  console.log(`\n        waiting ${Math.round(waitMs / 1000)}s for the bucket to roll over…`);
  await sleep(waitMs);
  report(3, 'late reader', await verify(late, lateSentinel), 'READ_STALE');

  // 4. A sentinel this host really issued, whose issuance record has since been evicted — the same
  //    observable state as a value that arrived by an unlogged path.
  const relay = newSessionId('relay');
  const relaySentinel = await fetchSentinel(relay);
  console.log(`\n        overflowing the ${ISSUE_LOG_CAPACITY}-record issuance log…`);
  for (let i = 0; i <= ISSUE_LOG_CAPACITY; i++) {
    await fetchSentinel(newSessionId(`filler${i}`));
  }
  report(4, 'out-of-band relay', await verify(relay, relaySentinel), 'REPLAY_SUSPECT');

  console.log(
    failures === 0
      ? '\nAll four verdicts behaved as specified.'
      : `\n${failures} step(s) did not match the specification.`,
  );
} catch (error) {
  console.error(`demo failed: ${error.message}`);
  failures++;
} finally {
  server.kill();
}

process.exit(failures === 0 ? 0 : 1);
