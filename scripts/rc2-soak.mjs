#!/usr/bin/env node
/**
 * RC.2 bounded local soak (PR-H).
 *
 * Exercises representative repository-supported surfaces without unbounded
 * network load, paid services, kill-by-name, or destructive actions.
 *
 * Usage (from repo root):
 *   node scripts/rc2-soak.mjs
 *   node scripts/rc2-soak.mjs --iterations=3
 *   node scripts/rc2-soak.mjs --host=artifacts/rc2/win-x64/OccamMcp.Core.exe
 *
 * Outputs JSON summary to stdout and writes artifacts/rc2/soak-report.json.
 */
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { performance } from 'node:perf_hooks';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, '..');
const outDir = path.join(root, 'artifacts', 'rc2');
fs.mkdirSync(outDir, { recursive: true });

function argValue(name, fallback) {
  const prefix = `--${name}=`;
  const hit = process.argv.find((a) => a.startsWith(prefix));
  return hit ? hit.slice(prefix.length) : fallback;
}

const iterations = Math.max(1, Math.min(10, Number(argValue('iterations', '3')) || 3));
const hostArg = argValue('host', '');
const occamHome = process.env.OCCAM_HOME || root;

function resolveHost() {
  if (hostArg) return path.resolve(hostArg);
  if (process.env.OCCAM_RC2_HOST) return path.resolve(process.env.OCCAM_RC2_HOST);
  const candidates = [
    path.join(root, 'artifacts', 'rc2', 'win-x64', 'OccamMcp.Core.exe'),
    path.join(root, 'artifacts', 'rc2', 'win-x64', 'OccamMcp.Core'),
    path.join(root, 'artifacts', 'rc2', 'linux-x64', 'OccamMcp.Core'),
    path.join(root, 'artifacts', 'rc2', 'osx-arm64', 'OccamMcp.Core'),
    path.join(root, 'OccamMcp.Core.exe'),
    path.join(root, 'OccamMcp.Core'),
  ];
  for (const c of candidates) {
    if (fs.existsSync(c)) return c;
  }
  throw new Error('No Occam host binary found. Publish AOT or set --host / OCCAM_RC2_HOST.');
}

function run(cmd, args, opts = {}) {
  return new Promise((resolve) => {
    const child = spawn(cmd, args, {
      cwd: root,
      env: { ...process.env, OCCAM_HOME: occamHome, OCCAM_BANNER: '0', ...(opts.env || {}) },
      stdio: ['ignore', 'pipe', 'pipe'],
      windowsHide: true,
    });
    let stdout = '';
    let stderr = '';
    child.stdout.on('data', (d) => { stdout += d.toString('utf8'); });
    child.stderr.on('data', (d) => { stderr += d.toString('utf8'); });
    child.on('close', (code) => resolve({ code: code ?? 1, stdout, stderr }));
    child.on('error', (err) => resolve({ code: 1, stdout, stderr: String(err) }));
  });
}

async function countOccamRelated() {
  if (process.platform === 'win32') {
    const ps = `
$procs = Get-CimInstance Win32_Process | Where-Object {
  $_.Name -match 'OccamMcp|node' -and (
    $_.CommandLine -match 'occam|FFOccam|browser-daemon|http-extract|browser-extract|css-extract'
  )
}
@($procs).Count
`;
    const r = await run('powershell', ['-NoProfile', '-Command', ps]);
    const n = Number((r.stdout || '').trim().split(/\r?\n/).filter(Boolean).pop());
    return Number.isFinite(n) ? n : null;
  }
  const r = await run('bash', ['-lc', `ps -eo pid,comm,args | grep -E 'OccamMcp|browser-daemon|http-extract|browser-extract' | grep -v grep | wc -l`]);
  const n = Number((r.stdout || '').trim());
  return Number.isFinite(n) ? n : null;
}

async function sampleMemoryMb() {
  if (process.platform === 'win32') {
    const ps = `
$p = Get-Process -Name OccamMcp.Core -ErrorAction SilentlyContinue |
  Measure-Object WorkingSet64 -Maximum
if ($null -eq $p.Maximum) { 'null' } else { [math]::Round($p.Maximum / 1MB, 1) }
`;
    const r = await run('powershell', ['-NoProfile', '-Command', ps]);
    const line = (r.stdout || '').trim().split(/\r?\n/).filter(Boolean).pop();
    if (!line || line === 'null') return null;
    const n = Number(line);
    return Number.isFinite(n) ? n : null;
  }
  return null;
}

function createMcpSession(hostPath) {
  const child = spawn(hostPath, [], {
    cwd: root,
    env: {
      ...process.env,
      OCCAM_HOME: occamHome,
      OCCAM_BANNER: '0',
      OCCAM_RUNTIME_ID: `soak-${Date.now().toString(36)}`,
      OCCAM_SESSION_ID: `soak-sess-${Date.now().toString(36)}`,
      OCCAM_PARENT_PID: String(process.pid),
      OCCAM_PARENT_LABEL: 'rc2-soak',
    },
    stdio: ['pipe', 'pipe', 'pipe'],
    windowsHide: true,
  });
  const rl = createInterface({ input: child.stdout });
  let id = 0;
  const pending = new Map();
  rl.on('line', (line) => {
    try {
      const msg = JSON.parse(line);
      if (msg.id != null && pending.has(msg.id)) pending.get(msg.id)(msg);
    } catch {
      // ignore non-JSON stderr-mirrored noise on stdout
    }
  });
  const send = (method, params) =>
    new Promise((resolve, reject) => {
      const reqId = ++id;
      const timer = setTimeout(() => {
        pending.delete(reqId);
        reject(new Error(`timeout:${method}`));
      }, 120_000);
      pending.set(reqId, (msg) => {
        clearTimeout(timer);
        resolve(msg);
      });
      child.stdin.write(JSON.stringify({ jsonrpc: '2.0', id: reqId, method, params }) + '\n');
    });
  const notify = (method, params) => {
    child.stdin.write(JSON.stringify({ jsonrpc: '2.0', method, params }) + '\n');
  };
  const close = () =>
    new Promise((resolve) => {
      const done = () => resolve();
      child.once('close', done);
      try {
        child.kill('SIGTERM');
      } catch {
        // ignore
      }
      setTimeout(() => {
        try { child.kill('SIGKILL'); } catch { /* ignore */ }
      }, 5_000);
    });
  return { send, notify, close, pid: child.pid };
}

function parseToolText(msg) {
  const text = msg?.result?.content?.[0]?.text;
  if (!text) return { raw: msg, parsed: null };
  try {
    return { raw: text, parsed: JSON.parse(text) };
  } catch {
    return { raw: text, parsed: null };
  }
}

async function main() {
  const started = new Date().toISOString();
  const t0 = performance.now();
  const host = resolveHost();
  const failures = [];
  const iterationsLog = [];
  let maxMemoryMb = null;

  const beforeCount = await countOccamRelated();
  const command = `node scripts/rc2-soak.mjs --iterations=${iterations} --host=${host}`;

  // Offline cumulative regression repetitions (deterministic, no network).
  for (let i = 1; i <= iterations; i++) {
    const iter = { index: i, steps: [] };
    const reg = await run('dotnet', [
      'run',
      '--project',
      'benchmarks/rc2-regression',
      '-c',
      'Release',
      '--no-build',
      '--',
      '--pr-g',
    ], { env: { OCCAM_RC2_HOST: host } });
    const pass = /"failed":0/.test(reg.stdout) && reg.code === 0;
    iter.steps.push({
      name: 'offline-pr-g',
      ok: pass,
      exit: reg.code,
      observation: (reg.stdout.match(/"suite":"[^"]+","total":\d+,"passed":\d+,"failed":\d+/) || [''])[0],
    });
    if (!pass) failures.push({ iteration: i, step: 'offline-pr-g', exit: reg.code });

    // Lifecycle CLI (read-only).
    const self = await run(host, ['lifecycle', 'self']);
    let selfOk = self.code === 0;
    try {
      const j = JSON.parse(self.stdout.trim().split(/\r?\n/).filter(Boolean).pop() || '{}');
      selfOk = selfOk && j.ok === true && !!j.identity?.runtimeId;
      iter.steps.push({
        name: 'lifecycle-self',
        ok: selfOk,
        runtimeId: j.identity?.runtimeId ?? null,
        pid: j.identity?.pid ?? null,
      });
    } catch (e) {
      selfOk = false;
      iter.steps.push({ name: 'lifecycle-self', ok: false, error: String(e) });
    }
    if (!selfOk) failures.push({ iteration: i, step: 'lifecycle-self' });

    const diagnose = await run(host, ['lifecycle', 'diagnose']);
    const diagOk = diagnose.code === 0 && /"ok":true/.test(diagnose.stdout);
    iter.steps.push({ name: 'lifecycle-diagnose', ok: diagOk, exit: diagnose.code });
    if (!diagOk) failures.push({ iteration: i, step: 'lifecycle-diagnose' });

    // Bounded live MCP session against a single public page (HTTP only).
    const session = createMcpSession(host);
    try {
      await session.send('initialize', {
        protocolVersion: '2024-11-05',
        capabilities: {},
        clientInfo: { name: 'rc2-soak', version: '1' },
      });
      session.notify('notifications/initialized', {});
      const listed = await session.send('tools/list', {});
      const toolNames = (listed?.result?.tools || []).map((t) => t.name);
      const hasCore = ['occam_probe', 'occam_transcode', 'occam_digest'].every((n) => toolNames.includes(n));
      iter.steps.push({ name: 'tools-list', ok: hasCore, toolCount: toolNames.length });
      if (!hasCore) failures.push({ iteration: i, step: 'tools-list' });

      const probe = parseToolText(
        await session.send('tools/call', {
          name: 'occam_probe',
          arguments: { url: 'https://example.com/' },
        }),
      );
      const probeOk = probe.parsed && (probe.parsed.ok === true || probe.parsed.classification != null);
      iter.steps.push({
        name: 'probe-example',
        ok: !!probeOk,
        access: probe.parsed?.access ?? null,
        likelyLogin: probe.parsed?.likelyLoginRequired ?? probe.parsed?.classification?.likelyLoginRequired ?? null,
      });
      if (!probeOk) failures.push({ iteration: i, step: 'probe-example' });

      const tx = parseToolText(
        await session.send('tools/call', {
          name: 'occam_transcode',
          arguments: {
            url: 'https://example.com/',
            backend_policy: 'http',
            max_tokens: 400,
            focus_query: 'Example Domain',
          },
        }),
      );
      const txOk = !!tx.parsed && typeof tx.parsed.ok === 'boolean';
      const semantic =
        tx.parsed &&
        (tx.parsed.focus != null ||
          tx.parsed.completeness != null ||
          tx.parsed.transportOk != null ||
          tx.parsed.usable != null);
      iter.steps.push({
        name: 'transcode-focus-budget',
        ok: txOk,
        okField: tx.parsed?.ok ?? null,
        focus: tx.parsed?.focus ?? null,
        completeness: tx.parsed?.completeness ?? null,
        transportOk: tx.parsed?.transportOk ?? null,
        usable: tx.parsed?.usable ?? null,
        semanticEnvelopePresent: !!semantic,
        markdownChars: (tx.parsed?.markdown || '').length,
      });
      if (!txOk) failures.push({ iteration: i, step: 'transcode-focus-budget' });

      const digest = parseToolText(
        await session.send('tools/call', {
          name: 'occam_digest',
          arguments: {
            urls: ['https://example.com/'],
            backend_policy: 'http',
            max_tokens: 300,
          },
        }),
      );
      const digestOk = !!digest.parsed && typeof digest.parsed.ok === 'boolean';
      iter.steps.push({
        name: 'digest-native-array',
        ok: digestOk,
        okField: digest.parsed?.ok ?? null,
        failureCode: digest.parsed?.failureCode ?? digest.parsed?.failure?.code ?? null,
      });
      if (!digestOk) failures.push({ iteration: i, step: 'digest-native-array' });

      const mem = await sampleMemoryMb();
      if (mem != null) maxMemoryMb = maxMemoryMb == null ? mem : Math.max(maxMemoryMb, mem);
    } catch (e) {
      failures.push({ iteration: i, step: 'mcp-session', error: String(e) });
      iter.steps.push({ name: 'mcp-session', ok: false, error: String(e) });
    } finally {
      await session.close();
    }

    iterationsLog.push(iter);
  }

  // Allow child teardown.
  await new Promise((r) => setTimeout(r, 1500));
  const afterCount = await countOccamRelated();
  const elapsedMs = Math.round(performance.now() - t0);
  const ended = new Date().toISOString();

  const report = {
    suite: 'rc2-soak',
    startedAt: started,
    endedAt: ended,
    elapsedMs,
    iterations,
    failures: failures.length,
    failureDetails: failures,
    processCountBefore: beforeCount,
    processCountAfter: afterCount,
    orphanedHostCount:
      beforeCount == null || afterCount == null
        ? null
        : Math.max(0, afterCount - beforeCount),
    maximumObservedMemoryMb: maxMemoryMb,
    host,
    command,
    commit: process.env.GITHUB_SHA || null,
    platform: process.platform,
    arch: process.arch,
    iterationsLog,
  };

  const json = JSON.stringify(report, null, 2);
  const reportPath = path.join(outDir, 'soak-report.json');
  fs.writeFileSync(reportPath, json);
  const artifactHash = createHash('sha256').update(json).digest('hex');
  report.artifactHash = artifactHash;
  report.artifactPath = reportPath;
  const finalJson = JSON.stringify(report, null, 2);
  fs.writeFileSync(reportPath, finalJson);

  console.log(finalJson);
  process.exit(failures.length === 0 ? 0 : 1);
}

main().catch((err) => {
  console.error(String(err?.stack || err));
  process.exit(1);
});
