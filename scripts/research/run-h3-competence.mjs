#!/usr/bin/env node
/**
 * H3 competence-scoring comparison runner.
 *
 * Compares three tier-assignment strategies against mock (or graded) exam outcomes
 * and H2 task success under the assigned surface:
 *   1. self-report
 *   2. static model-name map (via `occam exam grade` fields)
 *   3. behavioural exam score
 *
 * Usage:
 *   node scripts/research/run-h3-competence.mjs
 *   node scripts/research/run-h3-competence.mjs --seed 7 --out docs/research/results/h3-mock.jsonl
 *
 * Marker: H3_PIPELINE_OK when behavioural assignment predicts mock success better than chance
 * ordering (weak < medium < strong) — still NOT a real-model measurement.
 */
import { mkdirSync, writeFileSync, mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";
import {
  CAPABILITIES,
  SURFACES,
  examSubmissionFor,
  mulberry32,
  simulateTaskSuccess,
} from "./mock-agents.mjs";
import { readFileSync } from "node:fs";

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, "../..");
const seedIdx = process.argv.indexOf("--seed");
const seed = seedIdx >= 0 ? Number(process.argv[seedIdx + 1]) || 1 : 1;
const outIdx = process.argv.indexOf("--out");
const outPath =
  outIdx >= 0
    ? process.argv[outIdx + 1]
    : join(root, "docs/research/results/h3-mock.jsonl");

const tasks = readFileSync(join(root, "docs/research/tasks/h2-web-reading.jsonl"), "utf8")
  .split(/\r?\n/)
  .filter(Boolean)
  .map((l) => JSON.parse(l));

const commit = spawnSync("git", ["rev-parse", "HEAD"], { cwd: root, encoding: "utf8" })
  .stdout?.trim() || "unknown";

mkdirSync(dirname(outPath), { recursive: true });
const rows = [];
rows.push({
  type: "meta",
  protocol: "occam-h3-scorecard-v1",
  mode: "mock",
  seed,
  hostCommit: commit,
  collectedAt: new Date().toISOString(),
  note: "Mock agents + offline exam grade. Not a real-model H3 measurement.",
});

const tmp = mkdtempSync(join(tmpdir(), "occam-h3-"));
const rand = mulberry32(seed);

/** Map tier → profile used as surface for H2 simulation. */
const tierToSurface = { weak: "minimal", medium: "basic", strong: "full" };

const strategyScores = {
  self_report: [],
  static_map: [],
  behavioural: [],
};

for (const trueCapability of CAPABILITIES) {
  // Sometimes self-report lies (weak claims strong).
  const selfReport =
    trueCapability === "weak" && rand() < 0.7
      ? "strong"
      : trueCapability === "medium" && rand() < 0.3
        ? "strong"
        : trueCapability;

  const submission = examSubmissionFor(trueCapability, {
    modelHint: `mock-${trueCapability}`,
    selfReportTier: selfReport,
    sessionId: `h3-${trueCapability}-${seed}`,
  });
  const subPath = join(tmp, `${trueCapability}.json`);
  const gradePath = join(tmp, `${trueCapability}-grade.json`);
  writeFileSync(subPath, `${JSON.stringify(submission, null, 2)}\n`);

  const gradeProc = spawnSync(
    "dotnet",
    [
      "run",
      "--project",
      join(root, "src/FFOccamMcp.Core"),
      "-c",
      "Release",
      "--no-build",
      "--",
      "exam",
      "grade",
      "--submission",
      subPath,
      "--out",
      gradePath,
    ],
    { cwd: root, encoding: "utf8" },
  );
  if (gradeProc.status !== 0) {
    console.error(gradeProc.stderr || gradeProc.stdout);
    rmSync(tmp, { recursive: true, force: true });
    process.exit(1);
  }

  const grade = JSON.parse(readFileSync(gradePath, "utf8"));
  const assigned = {
    self_report: selfReport,
    static_map: grade.staticMapTier,
    behavioural: grade.tier,
  };

  rows.push({
    type: "assignment",
    trueCapability,
    selfReport,
    examScore: grade.score,
    examTier: grade.tier,
    staticMapTier: grade.staticMapTier,
    selfReportMatchesExam: grade.selfReportMatchesExam,
    staticMapMatchesExam: grade.staticMapMatchesExam,
  });

  for (const [strategy, tier] of Object.entries(assigned)) {
    const surface = tierToSurface[tier] || "basic";
    let ok = 0;
    for (const task of tasks) {
      // Evaluate under the *assigned* surface, but with the *true* capability's behaviour.
      const sim = simulateTaskSuccess(task, trueCapability, surface, rand);
      if (sim.ok) ok++;
      rows.push({
        type: "trial",
        strategy,
        trueCapability,
        assignedTier: tier,
        surface,
        taskId: task.id,
        ok: sim.ok,
        reason: sim.reason,
      });
    }
    const successRate = ok / tasks.length;
    strategyScores[strategy].push({ trueCapability, tier, surface, successRate });
    rows.push({
      type: "cell",
      strategy,
      trueCapability,
      assignedTier: tier,
      surface,
      successRate,
      n: tasks.length,
    });
  }
}

rmSync(tmp, { recursive: true, force: true });

// Predictive validity proxy: Spearman-ish order — mean success should rise with true capability
// under behavioural assignment more cleanly than under inflated self-report.
function meanByTrueCap(strategy) {
  const m = {};
  for (const c of CAPABILITIES) {
    const cells = strategyScores[strategy].filter((x) => x.trueCapability === c);
    m[c] = cells.reduce((s, x) => s + x.successRate, 0) / (cells.length || 1);
  }
  return m;
}

const behavioural = meanByTrueCap("behavioural");
const selfReport = meanByTrueCap("self_report");
const ordered =
  behavioural.weak <= behavioural.medium && behavioural.medium <= behavioural.strong;
const selfReportGap =
  selfReport.weak > behavioural.weak + 0.05; // lying weak looks better under self-report surfaces

const summary = {
  protocol: "occam-h3-summary-v1",
  mode: "mock",
  seed,
  hostCommit: commit,
  behavioural,
  selfReport,
  staticMap: meanByTrueCap("static_map"),
  behaviouralMonotone: ordered,
  selfReportInflatesWeak: selfReportGap,
  pipelineOk: ordered,
  note: "Pipeline selftest only. Replace mock agents with real models before claiming H3.",
};

writeFileSync(outPath, rows.map((r) => JSON.stringify(r)).join("\n") + "\n");
writeFileSync(
  outPath.replace(/\.jsonl$/, "-summary.json"),
  `${JSON.stringify(summary, null, 2)}\n`,
);

if (summary.pipelineOk) {
  console.log("H3_PIPELINE_OK");
  console.error(`[h3] wrote ${outPath}`);
  process.exit(0);
}

console.error("H3_PIPELINE_FAIL", summary);
process.exit(1);
