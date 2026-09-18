/**
 * Regression for real-world install failures (clean VM / agent mis-path).
 *
 * Catalog: corpora/install-failures.jsonl
 *   node scripts/lib/clean-install-regression.selftest.mjs
 *
 * Marker: CLEAN_INSTALL_REGRESSION_OK
 */
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, "..", "..");

function read(rel) {
  return readFileSync(join(root, rel), "utf8");
}

// --- Catalog present ---
{
  const catalog = read("corpora/install-failures.jsonl")
    .trim()
    .split("\n")
    .filter(Boolean)
    .map((line) => JSON.parse(line));
  assert.ok(catalog.some((r) => r.id === "IF-01"));
  assert.ok(catalog.some((r) => r.id === "IF-02"));
  assert.ok(catalog.some((r) => r.id === "IF-03"));
}

// --- IF-01 / IF-02: docs + bootstrap honesty ---
{
  const install = read("INSTALL.md");
  assert.match(
    install,
    /If you are an automated agent:\*\* run \*\*only\*\* the canonical command/i,
  );
  assert.match(install, /Do not build from source/i);
  assert.match(install, /get-ff-occam\.sh/);
  assert.match(install, /get-ff-occam\.ps1/);
  assert.match(install, /No \.NET SDK on the install machine/i);
  assert.doesNotMatch(
    install.slice(0, 800),
    /dotnet publish|TargetFramework.*net8/i,
    "INSTALL.md agent lead must not push SDK build",
  );

  const agents = read("AGENTS.md");
  assert.match(agents, /End-user \/ friend|install Occam/i);
  assert.match(agents, /INSTALL\.md/);
  assert.match(agents, /no \.NET SDK/i);
  assert.match(agents, /Never build from source unless/i);
  assert.match(agents, /dotnet build/i);

  const skillInstall = read("skills/occam/references/install.md");
  assert.match(skillInstall, /DO NOT build from source/i);
  assert.match(skillInstall, /dotnet publish/i);
  assert.match(skillInstall, /If the release tarball is missing/i);

  const skill = read("skills/occam/SKILL.md");
  assert.match(skill, /release tarball only/i);
  assert.match(skill, /Source builds are \*\*not\*\* supported for agents/i);

  const readme = read("README.md");
  assert.match(readme, /Install from the release tarball\. Do not build from source/i);

  const gettingStarted = read("docs/getting-started.md");
  assert.match(gettingStarted, /Install from the release tarball\. Do not build from source/i);

  const sh = read("scripts/get-ff-occam.sh");
  assert.match(sh, /NO git, NO \.NET SDK/i);
  assert.match(sh, /Node\.js \$\{MIN_NODE_MAJOR\}\+ is required to install Occam/);
  assert.match(sh, /No \.NET SDK is required for this release install path/);
  assert.match(sh, /Do NOT build from source/);

  const ps1 = read("scripts/get-ff-occam.ps1");
  assert.match(ps1, /Node\.js \$MinNodeMajor\+ is required to install Occam/);
  assert.match(ps1, /No \.NET SDK is required for this release install path/);
  assert.match(ps1, /Do NOT build from source/);
}

// --- IF-01 live: spawn bootstrap without node on PATH ---
{
  const bash = process.platform === "win32"
    ? ["C:\\Program Files\\Git\\bin\\bash.exe", "C:\\Program Files (x86)\\Git\\bin\\bash.exe"].find(
        (p) => existsSync(p),
      )
    : "bash";

  if (!bash) {
    console.error("[clean-install-regression] SKIP no-node spawn (bash not found on Windows)");
  } else {
    const curl = spawnSync(bash, ["-lc", "command -v curl"], { encoding: "utf8" });
    const tar = spawnSync(bash, ["-lc", "command -v tar"], { encoding: "utf8" });
    const curlPath = (curl.stdout || "").trim();
    const tarPath = (tar.stdout || "").trim();
    assert.ok(curlPath, "curl required for no-node repro");
    assert.ok(tarPath, "tar required for no-node repro");

    const pathDirs = [dirname(curlPath), dirname(tarPath)].join(":");
    const script = join(root, "scripts", "get-ff-occam.sh").replace(/\\/g, "/");
    const result = spawnSync(
      bash,
      [
        "-lc",
        `export PATH='${pathDirs}'; export OCCAM_BOOTSTRAP_STRICT_PATH=1; command -v node >/dev/null && exit 99; bash '${script}'`,
      ],
      {
        encoding: "utf8",
        env: {
          ...process.env,
          PATH: pathDirs,
          OCCAM_BOOTSTRAP_STRICT_PATH: "1",
        },
      },
    );
    assert.notEqual(result.status, 99, "node must be absent from scrubbed PATH");
    assert.notEqual(result.status, 0, "bootstrap must fail without node");
    const err = `${result.stderr || ""}\n${result.stdout || ""}`;
    assert.match(err, /Node\.js 20\+ is required to install Occam/i);
    assert.match(err, /No \.NET SDK is required/i);
    assert.doesNotMatch(err, /^error: required command not found: node$/m);
  }
}

console.log("CLEAN_INSTALL_REGRESSION_OK");
