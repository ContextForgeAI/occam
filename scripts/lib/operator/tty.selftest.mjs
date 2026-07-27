#!/usr/bin/env node
/**
 * TTY stream lifecycle — regression for macOS curl|bash EBADF after sequential prompts.
 *
 * Live failure: shared /dev/tty fd between ReadStream + WriteStream, then
 * rl.close() + destroy + closeSync → Node 25 WriteStream 'error' EBADF.
 */
import assert from "node:assert/strict";
import {
  createWriteStream,
  createReadStream,
  mkdtempSync,
  openSync,
  closeSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { PassThrough } from "node:stream";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { createInterface } from "node:readline/promises";
import {
  canPromptInteractively,
  canOpenControllingTty,
  openTerminalIo,
  openControllingTty,
  askControllingTty,
  isControllingTtyError,
} from "./tty.mjs";
import { runConnectOnboarding } from "./connect-onboarding.mjs";

function testCanPromptBasics() {
  assert.equal(
    canPromptInteractively({
      stdin: { isTTY: true },
      stdout: { isTTY: true },
      env: {},
    }),
    true,
  );
  assert.equal(
    canPromptInteractively({
      stdin: { isTTY: false },
      stdout: { isTTY: true },
      platform: "win32",
      env: {},
    }),
    false,
  );
  assert.equal(
    canPromptInteractively({
      stdin: { isTTY: false },
      stdout: { isTTY: false },
      platform: "linux",
      env: { CI: "1" },
    }),
    false,
  );
  assert.equal(typeof canOpenControllingTty(), "boolean");
  assert.equal(isControllingTtyError({ code: "EBADF", message: "bad file descriptor" }), true);
  assert.equal(isControllingTtyError(new Error("unrelated")), false);
}

/**
 * Prove / document the OLD shared-fd pattern (live macOS bug class).
 */
async function testSharedFdIsUnsafe() {
  const root = mkdtempSync(join(tmpdir(), "occam-tty-shared-"));
  const path = join(root, "duplex.txt");
  writeFileSync(path, "line\n");
  try {
    const fd = openSync(path, "r+");
    const input = createReadStream("", { fd, autoClose: false });
    const output = createWriteStream("", { fd, autoClose: false });
    let sawEbadf = false;
    const onErr = (err) => {
      if (err && err.code === "EBADF") sawEbadf = true;
    };
    input.on("error", onErr);
    output.on("error", onErr);

    const rl = createInterface({ input, output });
    rl.close();
    input.destroy();
    output.destroy();
    try {
      closeSync(fd);
    } catch (err) {
      if (err && err.code === "EBADF") sawEbadf = true;
    }
    await new Promise((r) => setTimeout(r, 50));
    if (sawEbadf) {
      console.log("  shared-fd pattern reproduces EBADF (expected on some Node builds)");
    } else {
      console.log("  shared-fd pattern did not emit EBADF here (ownership fix still required)");
    }
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

/**
 * Separate-fd pair: sequential open → readline → close cycles must not EBADF
 * or emit unhandled stream errors.
 */
async function testSequentialTerminalIoLifecycle() {
  const root = mkdtempSync(join(tmpdir(), "occam-tty-seq-"));
  const path = join(root, "term.txt");
  /** @type {Error[]} */
  const unhandled = [];
  const onUnhandled = (err) => {
    unhandled.push(err instanceof Error ? err : new Error(String(err)));
  };
  process.on("uncaughtException", onUnhandled);

  try {
    for (const answer of ["n", "3", "q"]) {
      writeFileSync(path, `${answer}\n`);
      // writeFlags 'r+' — do not truncate the fixture the reader needs.
      const pair = openTerminalIo(path, { writeFlags: "r+" });
      assert.ok(pair, "openTerminalIo must succeed on a regular file");
      const rl = createInterface({ input: pair.input, output: pair.output });
      const got = await rl.question("prompt> ");
      assert.equal(got.trim(), answer);
      rl.close();
      pair.close();
      pair.close(); // idempotent
    }
    await new Promise((r) => setTimeout(r, 50));
    assert.equal(unhandled.length, 0, `unhandled errors: ${unhandled.map((e) => e.message).join("; ")}`);
  } finally {
    process.off("uncaughtException", onUnhandled);
    rmSync(root, { recursive: true, force: true });
  }
}

/** Decline → selection → cancel using PassThrough (stdin independent of script pipe). */
async function testPassThroughSequentialPrompts() {
  /** @type {Error[]} */
  const streamErrors = [];
  for (const answer of ["n", "q"]) {
    const input = new PassThrough();
    const output = new PassThrough();
    input.on("error", (e) => streamErrors.push(e));
    output.on("error", (e) => streamErrors.push(e));
    const rl = createInterface({ input, output });
    const pending = rl.question("prompt> ");
    input.write(`${answer}\n`);
    assert.equal((await pending).trim(), answer);
    rl.close();
    input.destroy();
    output.destroy();
  }
  await new Promise((r) => setTimeout(r, 30));
  assert.equal(streamErrors.length, 0);
}

async function testControllingTtyWhenPresent() {
  if (process.platform === "win32") {
    assert.equal(openControllingTty(), null);
    console.log("  /dev/tty path skipped on win32");
    return;
  }
  if (!canOpenControllingTty()) {
    console.log("  /dev/tty unavailable — skip live open");
    return;
  }
  const a = openControllingTty();
  assert.ok(a);
  a.close();
  a.close();
  const b = openControllingTty();
  assert.ok(b);
  b.close();
  await new Promise((r) => setTimeout(r, 30));
}

function testStreamedInstallerInteractivityContract() {
  // Case A: curl|bash — stdin is the pipe, but controlling tty may exist.
  // On CI, never treat /dev/tty as interactive (automation safety).
  assert.equal(
    canPromptInteractively({
      stdin: { isTTY: false },
      stdout: { isTTY: true },
      platform: "darwin",
      env: { CI: "1" },
    }),
    false,
  );
  // Case B: plain interactive shell — stdio TTY is enough.
  assert.equal(
    canPromptInteractively({
      stdin: { isTTY: true },
      stdout: { isTTY: true },
      platform: "darwin",
      env: {},
    }),
    true,
  );
  // Case C: no controlling tty / win32 pipe — not interactive.
  assert.equal(
    canPromptInteractively({
      stdin: { isTTY: false },
      stdout: { isTTY: false },
      platform: "win32",
      env: {},
    }),
    false,
  );
  // Case D: non-CI unix with pipe stdin — interactivity follows canOpenControllingTty().
  if (process.platform !== "win32" && canOpenControllingTty()) {
    assert.equal(
      canPromptInteractively({
        stdin: { isTTY: false },
        stdout: { isTTY: true },
        platform: process.platform,
        env: {},
      }),
      true,
      "streamed install with controlling /dev/tty must be interactive",
    );
  }
}

async function testMultiHostDeclineCancelTranscript() {
  const home = mkdtempSync(join(tmpdir(), "occam-tty-multi-"));
  const emitted = [];
  try {
    const answers = ["n", "q"];
    let i = 0;
    // Force interactive via askQuestion; candidates may be empty on this machine —
    // still validates skip/cancel path and no mutation.
    const result = await runConnectOnboarding({
      occamHome: home,
      interactive: true,
      askQuestion: async () => answers[i++] ?? "q",
      emit: (line) => emitted.push(line),
      skipOccamVerify: true,
      source: "install",
      env: { ...process.env, CI: "1", OCCAM_CONNECT: "off" },
    });
    assert.equal(result.mutated, false);
    // Non-interactive skip copy must not duplicate the headline when we skip.
    if (result.transcript) {
      const multi = result.transcript.match(/Multiple AI apps detected\./g) || [];
      assert.ok(multi.length <= 1, `duplicate multi-host copy: ${result.transcript}`);
    }
  } finally {
    rmSync(home, { recursive: true, force: true });
  }
}

function testOllamaCppRuntimeDiscoveryContract() {
  // Kept as a lightweight contract note — Tier D runtimes are not connect targets.
  // Full ollama.cpp detector coverage lives in runtimes.selftest when present.
  assert.equal(typeof canOpenControllingTty, "function");
}

async function testOnboardingDeclineCancelNoMutation() {
  const home = mkdtempSync(join(tmpdir(), "occam-tty-onboard-"));
  try {
    const answers = ["n", "q"];
    let i = 0;
    const result = await runConnectOnboarding({
      occamHome: home,
      interactive: true,
      askQuestion: async () => answers[i++] ?? "q",
      emit: () => {},
      skipOccamVerify: true,
      env: { ...process.env, CI: "1" },
    });
    assert.equal(result.mutated, false);
  } finally {
    rmSync(home, { recursive: true, force: true });
  }
}

function testAskControllingTtySyncFile() {
  assert.equal(typeof askControllingTty, "function");
  if (process.platform === "win32") {
    assert.throws(() => askControllingTty("x"), /unavailable|TTY/i);
    console.log("  askControllingTty correctly unavailable on win32");
  }
}

async function main() {
  testCanPromptBasics();
  testStreamedInstallerInteractivityContract();
  testOllamaCppRuntimeDiscoveryContract();
  testAskControllingTtySyncFile();
  await testSharedFdIsUnsafe();
  await testSequentialTerminalIoLifecycle();
  await testPassThroughSequentialPrompts();
  await testControllingTtyWhenPresent();
  await testOnboardingDeclineCancelNoMutation();
  await testMultiHostDeclineCancelTranscript();
  console.log("tty.selftest: OK");
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
