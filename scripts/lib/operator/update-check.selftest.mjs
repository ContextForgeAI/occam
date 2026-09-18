import assert from "node:assert/strict";
import {
  compareVersions,
  decideUpdateAction,
  fetchLatestReleaseTag,
  readInstalledVersion,
  releaseBaseToApiUrl,
} from "./update-check.mjs";

assert.equal(compareVersions("0.8.13", "0.8.12"), 1);
assert.equal(compareVersions("0.8.12", "0.8.12"), 0);
assert.equal(compareVersions("0.8.11", "0.8.12"), -1);
assert.equal(compareVersions("v0.8.12", "0.8.12"), 0);

assert.equal(decideUpdateAction({ installed: "1.2.0", latest: "1.2.0" }).action, "noop");
assert.equal(decideUpdateAction({ installed: "1.0.0", latest: "1.2.0" }).action, "upgrade");
assert.equal(decideUpdateAction({ installed: "9.0.0", latest: "1.2.0" }).action, "error");
assert.equal(
  decideUpdateAction({ installed: "1.2.0", latest: "1.2.0", force: true }).action,
  "upgrade",
);

const api = releaseBaseToApiUrl(
  "http://example/releases/download/v0.8.12",
);
assert.equal(api, "http://example/releases");

const installed = readInstalledVersion(process.env.OCCAM_HOME || process.cwd());
assert.ok(typeof installed === "string" && installed.length > 0);

{
  const calls = [];
  const fetchFn = async (url) => {
    calls.push(url);
    return {
      ok: true,
      json: async () => [
        { draft: true, tag_name: "v9.9.9" },
        { draft: false, prerelease: true, tag_name: "v1.0.0-rc.2" },
      ],
    };
  };
  const got = await fetchLatestReleaseTag(
    fetchFn,
    "https://api.github.com/repos/ContextForgeAI/occam/releases/latest",
  );
  assert.equal(got.latest, "1.0.0-rc.2");
  assert.equal(got.error, null);
  assert.match(calls[0], /releases\?per_page=15$/);
}

console.log("update-check.selftest: OK");
