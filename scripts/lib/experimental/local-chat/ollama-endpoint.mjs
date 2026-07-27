/**
 * Local Ollama endpoint / SSH tunnel hygiene for live friend tests.
 *
 * NEVER treat a duplicate ssh -L failure as an Occam chat regression when
 * 127.0.0.1:11434 is already a verified working endpoint.
 *
 * Tunnel ownership/status MUST be reported separately from chat results.
 */
import { spawn } from "node:child_process";
import net from "node:net";
import { normalizeOllamaBase, probeOllama, DEFAULT_OLLAMA_BASE } from "./ollama-api.mjs";

/**
 * @typedef {{
 *   listening: boolean,
 *   host: string,
 *   port: number,
 * }} PortListenStatus
 *
 * @typedef {{
 *   ok: boolean,
 *   reused: boolean,
 *   startedTunnel: boolean,
 *   baseUrl: string,
 *   ollamaVersion: string|null,
 *   ownership: 'existing-local'|'existing-tunnel-or-local'|'started-ssh-forward'|'unavailable',
 *   message: string,
 *   tunnelPid: number|null,
 *   error?: string,
 * }} OllamaEndpointEnsureResult
 */

/**
 * @param {string} [baseUrl]
 * @returns {{ host: string, port: number }}
 */
export function parseOllamaListenTarget(baseUrl) {
  const base = normalizeOllamaBase(baseUrl);
  try {
    const u = new URL(base);
    const host = u.hostname || "127.0.0.1";
    const port = u.port ? Number(u.port) : u.protocol === "https:" ? 443 : 11434;
    return { host, port };
  } catch {
    return { host: "127.0.0.1", port: 11434 };
  }
}

/**
 * TCP listen check only — does not validate Ollama HTTP.
 * @param {string} host
 * @param {number} port
 * @param {number} [timeoutMs]
 * @returns {Promise<boolean>}
 */
export function isTcpPortOpen(host, port, timeoutMs = 800) {
  return new Promise((resolve) => {
    const socket = net.connect({ host, port });
    let done = false;
    const finish = (ok) => {
      if (done) return;
      done = true;
      try {
        socket.destroy();
      } catch {
        /* ignore */
      }
      resolve(ok);
    };
    socket.setTimeout(timeoutMs);
    socket.once("connect", () => finish(true));
    socket.once("timeout", () => finish(false));
    socket.once("error", () => finish(false));
  });
}

/**
 * @param {string} [baseUrl]
 * @returns {Promise<PortListenStatus>}
 */
export async function getLocalOllamaListenStatus(baseUrl) {
  const { host, port } = parseOllamaListenTarget(baseUrl);
  const listening = await isTcpPortOpen(host, port);
  return { listening, host, port };
}

/**
 * Probe whether local base already serves a real Ollama API.
 * @param {string} [baseUrl]
 */
export async function verifyLocalOllamaEndpoint(baseUrl) {
  const listen = await getLocalOllamaListenStatus(baseUrl);
  if (!listen.listening) {
    return {
      ok: false,
      listen,
      version: null,
      error: `nothing listening on ${listen.host}:${listen.port}`,
    };
  }
  const probe = await probeOllama(baseUrl);
  if (!probe.ok) {
    return { ok: false, listen, version: null, error: probe.error };
  }
  return { ok: true, listen, version: probe.version, error: null };
}

/**
 * Ensure a usable local Ollama HTTP endpoint for tests.
 *
 * Policy:
 * 1. If 127.0.0.1:11434 (or configured base) already answers /api/version → reuse, do NOT ssh -L.
 * 2. Only if not listening AND sshSpec provided → start one forward.
 * 3. Never start a duplicate forward when the port is already bound.
 *
 * @param {{
 *   baseUrl?: string,
 *   sshSpec?: { host: string, remoteHost?: string, remotePort?: number, identityFile?: string },
 *   startTunnel?: boolean,
 * }} [opts]
 * @returns {Promise<OllamaEndpointEnsureResult>}
 */
export async function ensureLocalOllamaEndpoint(opts = {}) {
  const baseUrl = normalizeOllamaBase(opts.baseUrl || DEFAULT_OLLAMA_BASE);
  const verified = await verifyLocalOllamaEndpoint(baseUrl);

  if (verified.ok) {
    return {
      ok: true,
      reused: true,
      startedTunnel: false,
      baseUrl,
      ollamaVersion: verified.version,
      ownership: "existing-tunnel-or-local",
      message: `Reusing existing endpoint ${baseUrl} (Ollama ${verified.version}); no new ssh -L`,
      tunnelPid: null,
    };
  }

  const { host, port } = parseOllamaListenTarget(baseUrl);
  if (verified.listen.listening) {
    // Port bound but not a healthy Ollama API — do not pile another forward on top.
    return {
      ok: false,
      reused: false,
      startedTunnel: false,
      baseUrl,
      ollamaVersion: null,
      ownership: "unavailable",
      message: `Port ${host}:${port} is listening but not a healthy Ollama API — refusing duplicate forward`,
      tunnelPid: null,
      error: verified.error || "unhealthy endpoint",
    };
  }

  if (opts.startTunnel === false || !opts.sshSpec?.host) {
    return {
      ok: false,
      reused: false,
      startedTunnel: false,
      baseUrl,
      ollamaVersion: null,
      ownership: "unavailable",
      message: `No Ollama at ${baseUrl}; tunnel not started (missing sshSpec or startTunnel=false)`,
      tunnelPid: null,
      error: verified.error || "not listening",
    };
  }

  const remoteHost = opts.sshSpec.remoteHost || "127.0.0.1";
  const remotePort = opts.sshSpec.remotePort || port;
  const args = [
    "-o",
    "BatchMode=yes",
    "-o",
    "ExitOnForwardFailure=yes",
    "-o",
    "ServerAliveInterval=30",
    "-f",
    "-N",
    "-L",
    `${host}:${port}:${remoteHost}:${remotePort}`,
    opts.sshSpec.host,
  ];
  if (opts.sshSpec.identityFile) {
    args.unshift("-i", opts.sshSpec.identityFile);
  }

  const child = spawn("ssh", args, {
    stdio: "ignore",
    detached: true,
    windowsHide: true,
  });
  child.unref();

  // Brief settle, then re-verify — tunnel start failure must not be blamed on chat.
  await new Promise((r) => setTimeout(r, 800));
  const after = await verifyLocalOllamaEndpoint(baseUrl);
  if (!after.ok) {
    return {
      ok: false,
      reused: false,
      startedTunnel: true,
      baseUrl,
      ollamaVersion: null,
      ownership: "started-ssh-forward",
      message: `Started ssh -L toward ${opts.sshSpec.host} but endpoint still unhealthy`,
      tunnelPid: child.pid ?? null,
      error: after.error || "tunnel verify failed",
    };
  }

  return {
    ok: true,
    reused: false,
    startedTunnel: true,
    baseUrl,
    ollamaVersion: after.version,
    ownership: "started-ssh-forward",
    message: `Started ssh -L ${host}:${port} → ${opts.sshSpec.host}:${remoteHost}:${remotePort}`,
    tunnelPid: child.pid ?? null,
  };
}

/**
 * Format tunnel/endpoint status for tester logs (separate from chat metrics).
 * @param {OllamaEndpointEnsureResult} result
 */
export function formatTunnelStatusReport(result) {
  const lines = [
    "OLLAMA ENDPOINT / TUNNEL STATUS",
    `ok: ${result.ok ? "YES" : "NO"}`,
    `reused: ${result.reused ? "YES" : "NO"}`,
    `startedTunnel: ${result.startedTunnel ? "YES" : "NO"}`,
    `ownership: ${result.ownership}`,
    `baseUrl: ${result.baseUrl}`,
    `ollamaVersion: ${result.ollamaVersion ?? "(none)"}`,
    `tunnelPid: ${result.tunnelPid ?? "(none)"}`,
    `message: ${result.message}`,
  ];
  if (result.error) lines.push(`error: ${result.error}`);
  lines.push("(This block is independent of Occam chat PASS/FAIL.)");
  return lines.join("\n");
}
