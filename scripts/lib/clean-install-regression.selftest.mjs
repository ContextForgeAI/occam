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
import { tmpdir } from "node:os";
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
  const bash =
    process.platform === "win32"
      ? ["C:\\Program Files\\Git\\bin\\bash.exe", "C:\\Program Files (x86)\\Git\\bin\\bash.exe"].find((p) =>
          existsSync(p),
        )
      : "bash";

  if (!bash) {
    console.error("[clean-install-regression] SKIP no-node spawn (bash not found on Windows)");
  } else {
    // On Unix resolve bash to an absolute path before scrubbing PATH.
    // On Windows keep the Git bash.exe path — `command -v bash` returns /usr/bin/bash
    // which Node cannot spawn from Win32.
    const bashAbs =
      process.platform === "win32"
        ? bash
        : (spawnSync(bash, ["-c", "command -v bash"], { encoding: "utf8" }).stdout || "").trim() ||
          bash;
    assert.ok(bashAbs, "bash absolute path required");

    // Git Bash on Windows often has empty `command -v curl` even when curl.exe is on PATH.
    // Prefer well-known absolute paths, then fall back to bash lookup.
    function resolveTool(name) {
      if (process.platform === "win32") {
        const candidates = [
          `C:\\Windows\\System32\\${name}.exe`,
          `C:\\Program Files\\Git\\usr\\bin\\${name}.exe`,
          `C:\\Program Files\\Git\\mingw64\\bin\\${name}.exe`,
          `C:\\Program Files\\Git\\mingw64\\bin\\${name}`,
        ];
        for (const p of candidates) {
          if (existsSync(p)) return p;
        }
        const where = spawnSync("where.exe", [name], { encoding: "utf8" });
        const first = (where.stdout || "").trim().split(/\r?\n/).find(Boolean);
        if (first && existsSync(first)) return first;
      }
      const r = spawnSync(bashAbs, ["-c", `command -v ${name}`], { encoding: "utf8" });
      return (r.stdout || "").trim();
    }

    const curlPath = resolveTool("curl");
    const tarPath = resolveTool("tar");
    assert.ok(curlPath, "curl required for no-node repro");
    assert.ok(tarPath, "tar required for no-node repro");

    const pathDirs = [
      ...new Set([dirname(curlPath), dirname(tarPath), dirname(bashAbs)]),
    ].join(process.platform === "win32" ? ";" : ":");
    const script = join(root, "scripts", "get-ff-occam.sh");
    const scrubEnv = {
      PATH: pathDirs,
      OCCAM_BOOTSTRAP_STRICT_PATH: "1",
      HOME: join(tmpdir(), "occam-bootstrap-no-node-home"),
      OCCAM_HOME: "",
      OCCAM_VERSION: "1.2.0",
    };

    const whichNode = spawnSync(bashAbs, ["-c", "command -v node || true"], {
      encoding: "utf8",
      env: scrubEnv,
    });
    assert.equal(
      (whichNode.stdout || "").trim(),
      "",
      `node must be absent from scrubbed PATH (got ${(whichNode.stdout || "").trim()})`,
    );

    const result = spawnSync(bashAbs, [script], {
      encoding: "utf8",
      env: scrubEnv,
    });
    const err = `${result.stderr || ""}\n${result.stdout || ""}\n${result.error?.message || ""}`;
    assert.notEqual(result.status, 0, `bootstrap must fail without node; output:\n${err}`);
    assert.match(err, /Node\.js 20\+ is required to install Occam/i, err);
    assert.match(err, /No \.NET SDK is required/i, err);
    assert.doesNotMatch(err, /^error: required command not found: node$/m);
  }
}

console.log("CLEAN_INSTALL_REGRESSION_OK");
