/**
 * Small process helpers for native host CLIs.
 */
import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { delimiter, dirname, join } from "node:path";

/**
 * Quote one token for cmd.exe. Inside a quoted token cmd expects a literal
 * double quote to be doubled — backslash escaping is a POSIX habit that breaks
 * quote parity and lets `&` split the command line.
 * @param {string} value
 */
export function windowsCmdQuote(value) {
  const s = String(value);
  if (s === "") return '""';
  if (!/[\s"&|<>^()!,;=]/.test(s)) return s;
  return `"${s.replace(/"/g, '""')}"`;
}

/**
 * `cmd.exe` expands `%VAR%` on the command line and offers no escape that works
 * for a quoted path, so a command path containing a defined variable reference
 * would silently run something else.
 * @param {string} command
 * @param {NodeJS.ProcessEnv} env
 */
function expandableVarInPath(command, env) {
  for (const match of String(command).matchAll(/%([A-Za-z_][A-Za-z0-9_]*)%/g)) {
    if (env[match[1]] != null) return match[0];
  }
  return null;
}

/**
 * @param {string} command
 * @param {string[]} args
 * @param {{ cwd?: string, env?: NodeJS.ProcessEnv, input?: string, timeoutMs?: number }} [opts]
 */
export function runCapture(command, args, opts = {}) {
  const isWinCmd = process.platform === "win32" && /\.(cmd|bat)$/i.test(command);
  /** @type {import('node:child_process').SpawnSyncReturns<string>} */
  let result;
  if (isWinCmd) {
    const env = opts.env ? { ...process.env, ...opts.env } : process.env;
    const expandable = expandableVarInPath(command, env);
    if (expandable) {
      return {
        status: 1,
        signal: null,
        stdout: "",
        stderr: `refusing to launch: path contains ${expandable}, which cmd.exe would expand`,
        error: new Error(`unsafe cmd path: ${command}`),
      };
    }
    // .cmd requires a shell; quote paths with spaces (e.g. Program Files\nodejs\npx.CMD).
    const cmdline = [windowsCmdQuote(command), ...args.map(windowsCmdQuote)].join(" ");
    result = spawnSync(cmdline, {
      cwd: opts.cwd,
      env,
      encoding: "utf8",
      input: opts.input,
      timeout: opts.timeoutMs ?? 120_000,
      windowsHide: true,
      shell: true,
    });
  } else {
    result = spawnSync(command, args, {
      cwd: opts.cwd,
      env: opts.env ? { ...process.env, ...opts.env } : process.env,
      encoding: "utf8",
      input: opts.input,
      timeout: opts.timeoutMs ?? 120_000,
      windowsHide: true,
    });
  }
  return {
    status: result.status ?? 1,
    signal: result.signal,
    stdout: result.stdout?.toString() ?? "",
    stderr: result.stderr?.toString() ?? "",
    error: result.error ?? null,
  };
}

/**
 * @param {string} name
 * @param {NodeJS.ProcessEnv} [env]
 */
export function which(name, env = process.env) {
  const pathEnv = env.PATH || env.Path || "";
  const isWin = process.platform === "win32";
  const exts = isWin
    ? (env.PATHEXT || ".EXE;.CMD;.BAT;.COM").split(";").filter(Boolean)
    : [""];

  for (const dir of pathEnv.split(delimiter)) {
    if (!dir) continue;
    if (isWin) {
      for (const ext of exts) {
        const withExt = join(dir, name + (ext.startsWith(".") ? ext : `.${ext}`));
        if (existsSync(withExt)) return withExt;
      }
      // Avoid extensionless npm shims (npx without .cmd) — Node spawn ENOENT.
      continue;
    }
    const plain = join(dir, name);
    if (existsSync(plain)) return plain;
  }
  return null;
}

/**
 * Deep-ish equality for launch env/args (order-insensitive env).
 * @param {Record<string, string>|undefined} a
 * @param {Record<string, string>|undefined} b
 */
export function envEqual(a = {}, b = {}) {
  const ak = Object.keys(a).sort();
  const bk = Object.keys(b).sort();
  if (ak.length !== bk.length) return false;
  for (let i = 0; i < ak.length; i++) {
    if (ak[i] !== bk[i]) return false;
    // Path-like env (OCCAM_HOME, cwd twins) may mix \ and / across writes.
    if (normalizePathish(String(a[ak[i]])) !== normalizePathish(String(b[bk[i]]))) {
      return false;
    }
  }
  return true;
}

/**
 * @param {string[]} a
 * @param {string[]} b
 */
export function argsEqual(a = [], b = []) {
  if (a.length !== b.length) return false;
  for (let i = 0; i < a.length; i++) {
    if (normalizePathish(a[i]) !== normalizePathish(b[i])) return false;
  }
  return true;
}

/** @param {string} p */
export function normalizePathish(p) {
  return String(p).replace(/\\/g, "/").replace(/\/+$/, "");
}

/**
 * @param {string} filePath
 */
export function dirnameOf(filePath) {
  return dirname(filePath);
}
