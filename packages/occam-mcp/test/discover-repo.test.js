import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  discoverRepoRoot,
  findInstallRoots,
  isOccamRepoRoot,
} from "../lib/discover-repo.mjs";

const packageRoot = join(dirname(fileURLToPath(import.meta.url)), "..");
const repoRoot = join(packageRoot, "..", "..");

describe("discover-repo", () => {
  it("recognizes the monorepo root from packages/occam-mcp", () => {
    assert.equal(isOccamRepoRoot(repoRoot), true);
    assert.equal(discoverRepoRoot(packageRoot), repoRoot);
  });

  it("discovers from repo cwd when npm wrapper runs with cwd at install root", () => {
    assert.equal(discoverRepoRoot(repoRoot), repoRoot);
    assert.deepEqual(findInstallRoots([repoRoot, "/tmp"]), [repoRoot]);
  });

  it("discovers from script path when cwd is unrelated", () => {
    const bin = join(packageRoot, "bin", "occam-mcp.js");
    assert.deepEqual(findInstallRoots(["/tmp", bin]), [repoRoot]);
  });

  it("returns null outside the tree", () => {
    assert.equal(discoverRepoRoot("/tmp"), null);
    assert.deepEqual(findInstallRoots(["/tmp", "/var"]), []);
  });
});
