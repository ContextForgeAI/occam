/**
 * Controlling-terminal helpers for pipe-to-shell installers (`curl | bash`).
 * Under that contract process.stdin is the script pipe — not the user's keyboard.
 */
import { createReadStream, createWriteStream, openSync, closeSync, existsSync } from "node:fs";

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
 * Open /dev/tty for readline prompts. Caller must close() when done with the pair
 * if they want to release the fd early; streams autoClose by default.
 * @returns {{ input: import('node:fs').ReadStream, output: import('node:fs').WriteStream, close: () => void } | null}
 */
export function openControllingTty() {
  if (process.platform === "win32") return null;
  try {
    const fd = openSync("/dev/tty", "r+");
    const input = createReadStream("", { fd, autoClose: false });
    const output = createWriteStream("", { fd, autoClose: false });
    let closed = false;
    const close = () => {
      if (closed) return;
      closed = true;
      try {
        input.destroy();
      } catch {
        /* ignore */
      }
      try {
        output.destroy();
      } catch {
        /* ignore */
      }
      try {
        closeSync(fd);
      } catch {
        /* ignore */
      }
    };
    return { input, output, close };
  } catch {
    return null;
  }
}
