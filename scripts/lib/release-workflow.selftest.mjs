#!/usr/bin/env node

import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const releasePath = path.join(repoRoot, ".github", "workflows", "occam-release.yml");
const verifierPath = path.join(repoRoot, ".github", "workflows", "sign-release.yml");
const release = fs.readFileSync(releasePath, "utf8");
const verifier = fs.readFileSync(verifierPath, "utf8");

function parseYaml(paths) {
  const python = String.raw`
import sys, yaml
for file in sys.argv[1:]:
    with open(file, "r", encoding="utf-8") as stream:
        value = yaml.safe_load(stream)
    if not isinstance(value, dict):
        raise SystemExit(f"workflow root is not a mapping: {file}")
`;
  for (const command of ["python", "python3"]) {
    const result = spawnSync(command, ["-c", python, ...paths], { encoding: "utf8" });
    if (!result.error && result.status === 0) return;
    if (!result.error && !/ModuleNotFoundError|No module named/.test(result.stderr ?? "")) {
      throw new Error(`YAML parse failed with ${command}: ${result.stderr || result.stdout}`);
    }
  }

  const ruby = String.raw`
require "yaml"
ARGV.each do |file|
  value = YAML.load_file(file)
  raise "workflow root is not a mapping: #{file}" unless value.is_a?(Hash)
end
`;
  const result = spawnSync("ruby", ["-e", ruby, ...paths], { encoding: "utf8" });
  if (!result.error && result.status === 0) return;
  throw new Error(
    `no working YAML parser (PyYAML or Ruby Psych): ${result.stderr || result.error?.message || "unknown error"}`,
  );
}

function assertExternalActionsPinned(text, file) {
  const uses = [...text.matchAll(/^\s*-?\s*uses:\s*([^\s#]+)(?:\s+#.*)?$/gm)].map(
    (match) => match[1],
  );
  assert.ok(uses.length > 0, `${file} must use at least one pinned action`);
  for (const use of uses) {
    if (use.startsWith("./")) continue;
    assert.match(
      use,
      /^[A-Za-z0-9_.-]+\/[A-Za-z0-9_./-]+@[0-9a-f]{40}$/,
      `${file}: external action is not pinned to a full commit: ${use}`,
    );
  }
}

function jobBlock(text, id) {
  const marker = `  ${id}:\n`;
  const start = text.indexOf(marker);
  assert.notEqual(start, -1, `missing job: ${id}`);
  const next = /^  [a-zA-Z0-9_-]+:\s*$/gm;
  next.lastIndex = start + marker.length;
  const match = next.exec(text);
  return text.slice(start, match?.index ?? text.length);
}

parseYaml([releasePath, verifierPath]);
assertExternalActionsPinned(release, "occam-release.yml");
assertExternalActionsPinned(verifier, "sign-release.yml");

assert.doesNotMatch(release, /softprops\/action-gh-release|@[vV]\d/);
assert.match(release, /^permissions:\n  contents: read$/m);
assert.match(release, /^    branches: \[main\]\n    tags: \["v\*"\]$/m);
assert.match(release, /^  pull_request:\n    branches: \[main\]$/m);
assert.match(release, /^  workflow_dispatch:$/m);

const buildIds = [...release.matchAll(/^  (build-[a-z0-9-]+):$/gm)].map((match) => match[1]);
assert.deepEqual(buildIds, ["build-linux", "build-macos", "build-windows"]);
for (const [id, runner, rid] of [
  ["build-linux", "ubuntu-latest", "linux-x64"],
  ["build-macos", "macos-latest", "osx-arm64"],
  ["build-windows", "windows-latest", "win-x64"],
]) {
  const block = jobBlock(release, id);
  assert.match(block, new RegExp(`runs-on: ${runner}`));
  assert.match(block, /permissions:\n      contents: read/);
  assert.doesNotMatch(block, /contents: write|id-token: write/);
  assert.match(block, /actions\/upload-artifact@[0-9a-f]{40}/);
  assert.match(block, new RegExp(`name: release-${rid}`));
  assert.match(block, new RegExp(`ff-occam-.*-${rid}\\.tar\\.gz`));
  assert.match(block, new RegExp(`ff-occam-.*-${rid}-manifest\\.json`));
}

const publish = jobBlock(release, "publish-release");
assert.match(publish, /needs: \[build-linux, build-macos, build-windows\]/);
assert.match(
  publish,
  /if: github\.event_name == 'push' && startsWith\(github\.ref, 'refs\/tags\/v'\)/,
);
assert.match(publish, /environment: github-release/);
assert.match(publish, /permissions:\n      actions: read\n      contents: write\n      id-token: write/);
assert.match(publish, /ContextForgeAI\/occam\/\.github\/workflows\/occam-release\.yml/);
assert.match(publish, /GITHUB_WORKFLOW_REF/);
assert.match(publish, /git merge-base --is-ancestor/);
assert.match(publish, /release-version\.mjs --assert-alignment/);
assert.match(publish, /--version-file VERSION/);
assert.match(publish, /--changelog CHANGELOG\.md/);
assert.match(publish, /args=\(release create.*--draft/s);
assert.match(publish, /gh "\$\{args\[@\]\}"/);
assert.match(publish, /gh api --method PATCH[\s\S]*-F draft=false/);

const orderedMarkers = [
  "Download linux-x64 workflow artifact",
  "Revalidate exact unsigned artifact set",
  "Sign exactly three platform archives",
  "Verify signer identity and reject tampering",
  "Require exact signed asset set",
  "Fail closed if the release or draft already exists",
  "Create one draft with all nine assets",
  "Verify the draft before publication",
  "Publish the verified draft once",
];
let previous = -1;
for (const marker of orderedMarkers) {
  const index = publish.indexOf(marker);
  assert.ok(index > previous, `publish ordering violation at: ${marker}`);
  previous = index;
}
const publication = publish.indexOf("Publish the verified draft once");
assert.doesNotMatch(publish.slice(publication), /gh release upload|cosign sign-blob/);

for (const rid of ["linux-x64", "osx-arm64", "win-x64"]) {
  assert.match(publish, new RegExp(`ff-occam-\\$\\{VERSION\\}-${rid}\\.tar\\.gz`));
  assert.match(publish, new RegExp(`ff-occam-\\$\\{VERSION\\}-${rid}-manifest\\.json`));
  assert.match(publish, new RegExp(`ff-occam-\\$\\{VERSION\\}-${rid}\\.tar\\.gz\\.bundle`));
}

assert.match(verifier, /^permissions:\n  contents: read$/m);
assert.doesNotMatch(verifier, /contents: write|id-token: write|cosign sign-blob|gh release upload|--method PATCH/);
assert.match(verifier, /gh release download/);
assert.match(verifier, /verify-release-json/);
assert.match(verifier, /verify-directory/);
assert.match(verifier, /certificate-identity/);
assert.match(verifier, /occam-release\.yml@refs\/tags/);

console.log("release-workflow.selftest: YAML + permissions + pins + DAG + exact-set policy OK");
