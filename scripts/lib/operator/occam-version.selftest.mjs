#!/usr/bin/env node
/**
 * occam --version / -V prints the installed version and exits 0.
 *
 * Marker: OCCAM_VERSION_CLI_OK
 */
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, "..", "..", "..");
const cli = join(root, "scripts", "occam.mjs");
const expected = readFileSync(join(root, "VERSION"), "utf8").trim().replace(/^v/i, "");

function run(args) {
  return spawnSync(process.execPath, [cli, ...args], {
    cwd: root,
    env: { ...process.env, OCCAM_HOME: root, CI: "1" },
    encoding: "utf8",
  });
}

const plain = run(["--version"]);
assert.equal(plain.status, 0, plain.stderr);
assert.equal(plain.stdout.trim(), expected);

const short = run(["-V"]);
assert.equal(short.status, 0, short.stderr);
assert.equal(short.stdout.trim(), expected);

const json = run(["--json", "--version"]);
assert.equal(json.status, 0, json.stderr);
assert.deepEqual(JSON.parse(json.stdout), { version: expected });

const reversed = run(["--version", "--json"]);
assert.equal(reversed.status, 0, reversed.stderr);
assert.deepEqual(JSON.parse(reversed.stdout), { version: expected });

console.log("OCCAM_VERSION_CLI_OK");
