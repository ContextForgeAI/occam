/**
 * Controlling-terminal helpers for pipe-to-shell installers (`curl | bash`).
 * Under that contract process.stdin is the script pipe — not the user's keyboard.
 *
 * Contract: open `/dev/tty` with **separate** read and write file descriptors.
 * Never share one fd across ReadStream + WriteStream (Node emits EBADF on
 * destroy/close when both ends tear down the same descriptor).
 */
import { createReadStream, createWriteStream, existsSync } from "node:fs";

/**
 * @returns {boolean}
 */
export function canOpenControllingTty() {
  if (process.platform === "win32") return false;
  if (!existsSync("/dev/tty")) return false;
  try {
    // Probe with a short-lived pair — same ownership rules as prompts.
    const pair = openTerminalIo("/dev/tty");
    if (!pair) return false;
    pair.close();
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
 * Open independent read/write streams for a terminal-like path.
 * Each stream owns its own fd (`autoClose: true` default). `close()` is
 * idempotent and must not double-close a shared descriptor.
 *
 * @param {string} path e.g. `/dev/tty` or a test fixture path
 * @param {{ writeFlags?: string }} [opts]
 * @returns {{ input: import('node:fs').ReadStream, output: import('node:fs').WriteStream, close: () => void } | null}
 */
export function openTerminalIo(path, opts = {}) {
  if (!path) return null;
  try {
    // Two opens → two fds. Sharing one r+ fd between ReadStream and WriteStream
    // caused live macOS Node 25: EBADF on WriteStream close after rl.close().
    const input = createReadStream(path);
    const output = createWriteStream(path, {
      // `/dev/tty` is a char device — default 'w' is correct.
      // Regular files in tests must not truncate before the reader consumes input.
      flags: opts.writeFlags ?? (path === "/dev/tty" ? "w" : "a"),
    });
    let closed = false;

    /** @param {NodeJS.ErrnoException} err */
    const onTeardownError = (err) => {
      // Only ignore teardown races after close() began. Unexpected live errors
      // stay audible (uncaught) so we do not hide real I/O failures.
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
 * Open `/dev/tty` for readline prompts. Caller must close() when the prompt ends.
 * Sequential prompts each open a fresh pair — never reuse a closed pair.
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
    /bad file descriptor/i.test(msg) ||
    /\/dev\/tty/i.test(msg)
  );
}
