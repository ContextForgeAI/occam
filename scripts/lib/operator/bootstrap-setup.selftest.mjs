#!/usr/bin/env node
/**
 * Behavioral regression for OCCAM_SETUP bootstrap contract.
 * Default install must never wait on stdin.
 */
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  parseSetupChoice,
  readSetupIntent,
  resolveSetupIntent,
} from "./bootstrap-setup.mjs";
import { promptSetupMode, resolveSetupFromEnv } from "./get-install-welcome.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const welcomeCli = join(here, "get-install-welcome.mjs");

function runWelcome(args, envExtra = {}) {
  const env = { ...process.env, ...envExtra };
  if (!Object.prototype.hasOwnProperty.call(envExtra, "OCCAM_SETUP")) {
    delete env.OCCAM_SETUP;
  }

  const started = Date.now();
  const r = spawnSync(process.execPath, [welcomeCli, ...args], {
    env,
    encoding: "utf8",
    timeout: 5000,
    stdio: ["ignore", "pipe", "pipe"],
  });
  const elapsed = Date.now() - started;
  return { r, elapsed };
}

async function main() {
  assert.equal(readSetupIntent({}), "auto");
  assert.equal(readSetupIntent({ OCCAM_SETUP: "" }), "auto");
  assert.equal(readSetupIntent({ OCCAM_SETUP: "auto" }), "auto");
  assert.equal(readSetupIntent({ OCCAM_SETUP: "1" }), "auto");
  assert.equal(readSetupIntent({ OCCAM_SETUP: "manual" }), "manual");
  assert.equal(readSetupIntent({ OCCAM_SETUP: "2" }), "manual");
  assert.equal(readSetupIntent({ OCCAM_SETUP: "ask" }), "ask");
  assert.throws(() => readSetupIntent({ OCCAM_SETUP: "nope" }));

  assert.equal(resolveSetupIntent({ env: {}, interactive: false }), "auto");
  assert.equal(resolveSetupIntent({ env: { OCCAM_SETUP: "ask" }, interactive: false }), "auto");
  assert.equal(resolveSetupIntent({ env: { OCCAM_SETUP: "ask" }, interactive: true }), "ask");

  assert.equal(parseSetupChoice(""), "auto");
  assert.equal(parseSetupChoice("   "), "auto");
  assert.equal(parseSetupChoice("1"), "auto");
  assert.equal(parseSetupChoice("2"), "manual");
  assert.equal(parseSetupChoice("manual"), "manual");

  assert.equal(resolveSetupFromEnv({}), "auto");
  assert.equal(resolveSetupFromEnv({ OCCAM_SETUP: "ask" }), "auto");
  assert.equal(resolveSetupFromEnv({ OCCAM_SETUP: "manual" }), "manual");

  // E) manual → no menu
  assert.equal(
    await promptSetupMode({
      env: { OCCAM_SETUP: "manual" },
      stdin: /** @type {any} */ ({ isTTY: true }),
      askQuestion: async () => {
        assert.fail("manual must not open a prompt");
      },
    }),
    "manual",
  );

  // ask + interactive mock: Enter → auto; "2" → manual
  assert.equal(
    await promptSetupMode({
      env: { OCCAM_SETUP: "ask" },
      stdin: /** @type {any} */ ({ isTTY: true }),
      askQuestion: async () => "",
    }),
    "auto",
  );
  assert.equal(
    await promptSetupMode({
      env: { OCCAM_SETUP: "ask" },
      stdin: /** @type {any} */ ({ isTTY: true }),
      askQuestion: async (prompt) => {
        assert.match(prompt, /Setup \[1\]/);
        return "2";
      },
    }),
    "manual",
  );

  // unset → auto without prompt; stdin closed; must finish quickly
  {
    const { r, elapsed } = runWelcome(["resolve"]);
    assert.equal(r.status, 0, r.stderr);
    assert.equal(r.stdout.trim().split("\n").at(-1), "auto");
    assert.ok(elapsed < 4000, `resolve hung (${elapsed}ms)`);
  }
  {
    const { r, elapsed } = runWelcome(["prompt"]);
    assert.equal(r.status, 0, r.stderr);
    assert.equal(r.stdout.trim().split("\n").at(-1), "auto");
    assert.ok(elapsed < 4000, `prompt with unset OCCAM_SETUP hung (${elapsed}ms)`);
    assert.doesNotMatch(r.stdout, /Setup \[1\]/);
  }
  {
    const { r, elapsed } = runWelcome(["prompt"], { OCCAM_SETUP: "ask" });
    assert.equal(r.status, 0, r.stderr);
    assert.equal(r.stdout.trim().split("\n").at(-1), "auto");
    assert.ok(elapsed < 4000, `ask + closed stdin hung (${elapsed}ms)`);
    assert.doesNotMatch(r.stdout, /Setup \[1\]/);
  }
  {
    const { r, elapsed } = runWelcome(["prompt"], { OCCAM_SETUP: "manual" });
    assert.equal(r.status, 0, r.stderr);
    assert.equal(r.stdout.trim().split("\n").at(-1), "manual");
    assert.ok(elapsed < 4000, `manual hung (${elapsed}ms)`);
    assert.doesNotMatch(r.stdout, /Setup \[1\]/);
  }

  console.log("bootstrap-setup.selftest: OK");
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
