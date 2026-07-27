/**
 * Controlling-terminal helpers for pipe-to-shell installers (`curl | bash`).
 * Under that contract process.stdin is the script pipe — not the user's keyboard.
 *
 * Prompting uses synchronous open/read/write on `/dev/tty` (single r+ fd).
 * Stream-based helpers remain for tests; do not use them for live install prompts
 * — Node 25 stream destroy on `/dev/tty` can hang after readline.close().
 */
import {
  createReadStream,
  createWriteStream,
  existsSync,
  openSync,
  closeSync,
  readSync,
  writeSync,
} from "node:fs";

/**
 * @returns {boolean}
 */
export function canOpenControllingTty() {
  if (process.platform === "win32") return false;
  if (!existsSync("/dev/tty")) return false;
  try {
    const fd = openSync("/dev/tty", "r+");
    closeSync(fd);
    return true;
  } catch {
    return false;
  }
}

/**
 * True when we can prompt a human: real stdio TTY, or a controlling /dev/tty
 * (needed when the installer itself was streamed on stdin).
 * @param {{ stdin?: { isTTY?: boolean }, stdout?: { isTTY?: boolean }, platform?: string, env?: NodeJS.ProcessEnv }} [opts]
 */
export function canPromptInteractively(opts = {}) {
  const env = opts.env ?? process.env;
  // CI / automation: never block on /dev/tty.
  if (env.CI === "1" || env.CI === "true" || env.GITHUB_ACTIONS) {
    const stdin = opts.stdin ?? process.stdin;
    const stdout = opts.stdout ?? process.stdout;
    return stdin?.isTTY === true && stdout?.isTTY === true;
  }
  const stdin = opts.stdin ?? process.stdin;
  const stdout = opts.stdout ?? process.stdout;
  if (stdin?.isTTY === true && stdout?.isTTY === true) return true;
  const plat = opts.platform ?? process.platform;
  if (plat === "win32") return false;
  return canOpenControllingTty();
}

/**
 * Read one line from the controlling terminal (curl|bash safe).
 * @param {string} prompt
 * @param {{ path?: string }} [opts]
 * @returns {string}
 */
export function askControllingTty(prompt, opts = {}) {
  const path = opts.path || "/dev/tty";
  if (process.platform === "win32") {
    throw Object.assign(new Error("Interactive terminal unavailable. No AI app configurations were changed."), {
      code: "ERR_TTY_UNAVAILABLE",
    });
  }
  let fd;
  try {
    fd = openSync(path, "r+");
  } catch (err) {
    throw Object.assign(new Error("Interactive terminal unavailable. No AI app configurations were changed."), {
      code: "ERR_TTY_UNAVAILABLE",
      cause: err,
    });
  }
  try {
    writeSync(fd, String(prompt ?? ""));
    /** @type {Buffer[]} */
    const parts = [];
    const tmp = Buffer.alloc(1);
    while (true) {
      const n = readSync(fd, tmp, 0, 1, null);
      if (n === 0) break;
      if (tmp[0] === 0x0a) break; // \n
      if (tmp[0] === 0x0d) continue; // \r
      parts.push(Buffer.from(tmp));
    }
    return Buffer.concat(parts).toString("utf8");
  } finally {
    try {
      closeSync(fd);
    } catch {
      /* ignore */
    }
  }
}

/**
 * Open independent read/write streams for a terminal-like path (tests / diagnostics).
 * Prefer askControllingTty() for live install prompts.
 *
 * @param {string} path e.g. `/dev/tty` or a test fixture path
 * @param {{ writeFlags?: string }} [opts]
 * @returns {{ input: import('node:fs').ReadStream, output: import('node:fs').WriteStream, close: () => void } | null}
 */
export function openTerminalIo(path, opts = {}) {
  if (!path) return null;
  try {
    const input = createReadStream(path);
    const output = createWriteStream(path, {
      flags: opts.writeFlags ?? (path === "/dev/tty" ? "w" : "a"),
    });
    let closed = false;

    /** @param {NodeJS.ErrnoException} err */
    const onTeardownError = (err) => {
      if (!closed) return;
      if (err && (err.code === "EBADF" || err.code === "ERR_STREAM_DESTROYED")) return;
    };
    input.on("error", onTeardownError);
    output.on("error", onTeardownError);

    const close = () => {
      if (closed) return;
      closed = true;
      if (!input.destroyed) input.destroy();
      if (!output.destroyed) output.destroy();
    };
    return { input, output, close };
  } catch {
    return null;
  }
}

/**
 * Open `/dev/tty` as stream pair (tests only — live prompts use askControllingTty).
 * @returns {{ input: import('node:fs').ReadStream, output: import('node:fs').WriteStream, close: () => void } | null}
 */
export function openControllingTty() {
  if (process.platform === "win32") return null;
  if (!existsSync("/dev/tty")) return null;
  return openTerminalIo("/dev/tty");
}

/**
 * True when an error is a controlling-TTY open/IO failure (human boundary).
 * @param {unknown} err
 */
export function isControllingTtyError(err) {
  const msg = err instanceof Error ? err.message : String(err);
  const code = err && typeof err === "object" && "code" in err ? String(err.code) : "";
  return (
    code === "EBADF" ||
    code === "EIO" ||
    code === "ENXIO" ||
    code === "ENOENT" ||
    code === "ERR_TTY_UNAVAILABLE" ||
    /bad file descriptor/i.test(msg) ||
    /\/dev\/tty/i.test(msg) ||
    /Interactive terminal unavailable/i.test(msg)
  );
}
