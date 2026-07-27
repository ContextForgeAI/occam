/**
 * Model runtime detectors (Tier D) — classify only; never register Occam into these.
 *
 * Official contract (docs/mcp-hosts.md): Ollama, llama.cpp, LM Studio, MLX are
 * reported when found so operators know Occam saw them. They serve models — they
 * do not consume MCP tools — so they never appear in "Occam can connect to N apps".
 */
import { existsSync } from "node:fs";
import { homedir, platform } from "node:os";
import { join } from "node:path";
import { which } from "./process.mjs";

/**
 * @typedef {{ id: string, name: string, kind: 'MODEL_RUNTIME', detected: boolean, confidence: 'high'|'medium'|'low', signals: string[] }} RuntimeDetection
 */

/**
 * macOS / Windows application-bundle signals for the official Ollama desktop app.
 * The product is branded "Ollama"; some users colloquially call the stack
 * "ollama.cpp" because the inference backend is llama.cpp — that is still this app,
 * not a separate MCP host.
 *
 * @param {{
 *   existsSync?: (p: string) => boolean,
 *   homedir?: () => string,
 *   platform?: string,
 *   env?: NodeJS.ProcessEnv,
 * }} [opts]
 * @returns {string[]}
 */
export function ollamaAppSignals(opts = {}) {
  const exists = opts.existsSync ?? existsSync;
  const home = (opts.homedir ?? homedir)();
  const plat = opts.platform ?? platform();
  const env = opts.env ?? process.env;
  /** @type {string[]} */
  const signals = [];

  if (plat === "darwin") {
    const candidates = [
      "/Applications/Ollama.app",
      join(home, "Applications", "Ollama.app"),
      // Rare / unofficial renames — still the Ollama desktop product family.
      "/Applications/ollama.cpp.app",
      "/Applications/Ollama.cpp.app",
      join(home, "Applications", "ollama.cpp.app"),
      join(home, "Applications", "Ollama.cpp.app"),
    ];
    for (const app of candidates) {
      if (exists(app)) signals.push(`app:${app}`);
    }
    const cliInApp = "/Applications/Ollama.app/Contents/Resources/ollama";
    if (exists(cliInApp)) signals.push(`executable:${cliInApp}`);
    const support = join(home, "Library", "Application Support", "Ollama");
    if (exists(support)) signals.push(`dir:${support}`);
  } else if (plat === "win32") {
    const local = env.LOCALAPPDATA || join(home, "AppData", "Local");
    const localOllama = join(local, "Programs", "Ollama");
    if (exists(localOllama)) signals.push(`dir:${localOllama}`);
    const exe = join(localOllama, "ollama.exe");
    if (exists(exe)) signals.push(`executable:${exe}`);
  }
  return signals;
}

/**
 * @param {{
 *   existsSync?: (p: string) => boolean,
 *   which?: (name: string) => string | null,
 *   homedir?: () => string,
 *   platform?: string,
 *   env?: NodeJS.ProcessEnv,
 * }} [opts]
 * @returns {RuntimeDetection}
 */
export function detectOllama(opts = {}) {
  const exists = opts.existsSync ?? existsSync;
  const whichFn = opts.which ?? which;
  const homeFn = opts.homedir ?? homedir;
  const signals = [];

  const bin = whichFn("ollama");
  if (bin) signals.push(`executable:${bin}`);

  const home = join(homeFn(), ".ollama");
  if (exists(home)) signals.push(`dir:${home}`);

  for (const s of ollamaAppSignals(opts)) {
    if (!signals.includes(s)) signals.push(s);
  }

  const hasApp = signals.some((s) => s.startsWith("app:"));
  const hasBin = signals.some((s) => s.startsWith("executable:"));
  return {
    id: "ollama",
    name: "Ollama",
    kind: "MODEL_RUNTIME",
    detected: signals.length > 0,
    confidence: hasBin || hasApp ? "high" : signals.length ? "medium" : "low",
    signals,
  };
}

/**
 * @param {{ which?: (name: string) => string | null }} [opts]
 * @returns {RuntimeDetection}
 */
export function detectLlamaCpp(opts = {}) {
  const whichFn = opts.which ?? which;
  const signals = [];
  /** @type {string | null} */
  let primaryName = null;
  // PATH tools only — not the Ollama.app bundle (that is detectOllama).
  for (const name of ["llama-server", "llama-cli", "llama.cpp", "ollama.cpp"]) {
    const bin = whichFn(name);
    if (bin) {
      signals.push(`executable:${bin}`);
      if (!primaryName) primaryName = name;
    }
  }
  return {
    id: "llamacpp",
    name: primaryName === "ollama.cpp" ? "ollama.cpp" : "llama.cpp",
    kind: "MODEL_RUNTIME",
    detected: signals.length > 0,
    confidence: signals.length ? "medium" : "low",
    signals,
  };
}

/** @returns {RuntimeDetection} */
export function detectLmStudio() {
  const signals = [];
  const home = join(homedir(), ".lmstudio");
  if (existsSync(home)) signals.push(`dir:${home}`);
  if (platform() === "win32") {
    const local = join(
      process.env.LOCALAPPDATA || join(homedir(), "AppData", "Local"),
      "LM-Studio",
    );
    if (existsSync(local)) signals.push(`dir:${local}`);
  } else if (platform() === "darwin") {
    const app = "/Applications/LM Studio.app";
    if (existsSync(app)) signals.push(`app:${app}`);
  }
  return {
    id: "lmstudio",
    name: "LM Studio",
    kind: "MODEL_RUNTIME",
    detected: signals.length > 0,
    confidence: signals.length ? "medium" : "low",
    signals,
  };
}

/** @returns {RuntimeDetection} */
export function detectMlx() {
  const signals = [];
  if (platform() !== "darwin") {
    return {
      id: "mlx",
      name: "MLX / MLX-LM",
      kind: "MODEL_RUNTIME",
      detected: false,
      confidence: "low",
      signals,
    };
  }
  const bin = which("mlx_lm") || which("mlx-lm");
  if (bin) signals.push(`executable:${bin}`);
  return {
    id: "mlx",
    name: "MLX / MLX-LM",
    kind: "MODEL_RUNTIME",
    detected: signals.length > 0,
    confidence: signals.length ? "medium" : "low",
    signals,
  };
}

/**
 * @param {{
 *   existsSync?: (p: string) => boolean,
 *   which?: (name: string) => string | null,
 *   homedir?: () => string,
 *   platform?: string,
 *   env?: NodeJS.ProcessEnv,
 * }} [opts]
 */
export function detectAllRuntimes(opts = {}) {
  return [
    detectOllama(opts),
    detectLlamaCpp(opts),
    detectLmStudio(),
    detectMlx(),
  ].filter((r) => r.detected);
}
