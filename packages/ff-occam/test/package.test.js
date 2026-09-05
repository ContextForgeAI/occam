import test from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const bin = join(root, "bin", "ff-occam.js");

test("ff-occam bin exists", () => {
  assert.ok(existsSync(bin));
});

test("package.json declares ff-occam bin only (not occam)", async () => {
  const pkg = JSON.parse(
    await import("node:fs/promises").then((fs) =>
      fs.readFile(join(root, "package.json"), "utf8"),
    ),
  );
  assert.equal(pkg.name, "ff-occam");
  assert.equal(pkg.bin["ff-occam"], "bin/ff-occam.js");
  assert.equal(pkg.bin.occam, undefined);
  assert.ok(pkg.dependencies["@ff-occam/mcp"]);
});

test("postinstall explains operator CLI is not this package", () => {
  const postinstall = join(root, "scripts", "postinstall.js");
  assert.ok(existsSync(postinstall));
  const result = spawnSync(process.execPath, [postinstall], {
    encoding: "utf8",
    env: { ...process.env, OCCAM_QUIET_POSTINSTALL: "" },
  });
  assert.equal(result.status, 0);
  assert.match(result.stderr, /does not install the `occam` operator CLI/i);
  assert.match(result.stderr, /get-ff-occam\.ps1/);
  assert.match(result.stderr, /npx ff-occam@/);
});

test("ff-occam connect refuses with get-ff-occam pointer", () => {
  const result = spawnSync(process.execPath, [bin, "connect"], {
    encoding: "utf8",
    env: process.env,
  });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /operator CLI command/i);
  assert.match(result.stderr, /get-ff-occam\.sh/);
});
