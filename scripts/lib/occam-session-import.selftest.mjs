import assert from "node:assert/strict";
import { mkdtempSync, writeFileSync, existsSync, readdirSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const here = dirname(fileURLToPath(import.meta.url));
const script = join(here, "..", "occam-session.mjs");

function runImport(sessionsRoot, extraArgs) {
  const cookies = join(sessionsRoot, "source-cookies.txt");
  // Minimal Netscape cookies.txt
  writeFileSync(
    cookies,
    [
      "# Netscape HTTP Cookie File",
      ".example.com\tTRUE\t/\tFALSE\t2147483647\tsession\tsecret-value",
      "",
    ].join("\n"),
    "utf8",
  );

  const result = spawnSync(
    process.execPath,
    [script, "import", "--from", cookies, "--host", "example.com", "--id", "example.com.test", ...extraArgs],
    {
      env: { ...process.env, OCCAM_SESSIONS_ROOT: sessionsRoot },
      encoding: "utf8",
    },
  );
  return { result, cookies };
}

const rootDefault = mkdtempSync(join(tmpdir(), "occam-session-import-default-"));
try {
  const { result } = runImport(rootDefault, []);
  assert.equal(result.status, 0, result.stderr || result.stdout);
  const importsDir = join(rootDefault, "_imports");
  const retained = existsSync(importsDir) ? readdirSync(importsDir) : [];
  assert.equal(retained.length, 0, `default import must not retain _imports/ copies, got: ${retained.join(",")}`);
  assert.ok(existsSync(join(rootDefault, "example.com.test.json")), "profile must still be written");
} finally {
  rmSync(rootDefault, { recursive: true, force: true });
}

const rootKeep = mkdtempSync(join(tmpdir(), "occam-session-import-keep-"));
try {
  const { result } = runImport(rootKeep, ["--keep-import"]);
  assert.equal(result.status, 0, result.stderr || result.stdout);
  const retained = readdirSync(join(rootKeep, "_imports"));
  assert.ok(
    retained.includes("source-cookies.txt"),
    `--keep-import should retain raw file, got: ${retained.join(",")}`,
  );
} finally {
  rmSync(rootKeep, { recursive: true, force: true });
}

console.log("occam-session-import.selftest: OK");
