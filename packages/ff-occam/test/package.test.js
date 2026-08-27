import test from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");

test("ff-occam bin exists", () => {
  assert.ok(existsSync(join(root, "bin", "ff-occam.js")));
});

test("package.json declares ff-occam and occam bins", async () => {
  const pkg = JSON.parse(
    await import("node:fs/promises").then((fs) =>
      fs.readFile(join(root, "package.json"), "utf8"),
    ),
  );
  assert.equal(pkg.name, "ff-occam");
  assert.equal(pkg.bin["ff-occam"], "bin/ff-occam.js");
  assert.equal(pkg.bin.occam, "bin/ff-occam.js");
  assert.ok(pkg.dependencies["@ff-occam/mcp"]);
});
