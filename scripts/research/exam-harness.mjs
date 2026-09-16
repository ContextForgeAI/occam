#!/usr/bin/env node
/**
 * Thin wrapper: emit exam tasks, grade a submission file, print recommended OCCAM_PROFILE.
 *
 *   node scripts/research/exam-harness.mjs tasks [--out tasks.json]
 *   node scripts/research/exam-harness.mjs grade --submission path.json [--out result.json] [--write-env e.env]
 *   node scripts/research/exam-harness.mjs selftest
 */
import { spawnSync } from "node:child_process";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "../..");
const argv = process.argv.slice(2);
const sub = argv[0] || "help";

function runExam(args) {
  const r = spawnSync(
    "dotnet",
    ["run", "--project", join(root, "src/FFOccamMcp.Core"), "-c", "Release", "--no-build", "--", "exam", ...args],
    { cwd: root, encoding: "utf8", stdio: "inherit" },
  );
  process.exit(r.status ?? 1);
}

if (sub === "tasks") {
  runExam(["tasks", ...argv.slice(1)]);
} else if (sub === "grade") {
  runExam(["grade", ...argv.slice(1)]);
} else if (sub === "selftest") {
  runExam(["harness-selftest"]);
} else {
  console.error(`usage:
  node scripts/research/exam-harness.mjs tasks [--out P]
  node scripts/research/exam-harness.mjs grade --submission P [--out R] [--write-env E]
  node scripts/research/exam-harness.mjs selftest`);
  process.exit(2);
}
