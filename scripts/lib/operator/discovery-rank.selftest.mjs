import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { evaluateTask, rankHits, scoreHit } from "./discovery-rank.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const lines = readFileSync(join(here, "fixtures/discovery/held-out.jsonl"), "utf8")
  .split(/\r?\n/)
  .filter(Boolean)
  .map((line) => JSON.parse(line));

assert.ok(lines.length >= 3, "held-out discovery tasks");

const reports = lines.map((task) => evaluateTask(task, 3));
let improvedPrecision = 0;
let improvedRecall = 0;
let improvedTtu = 0;
for (const report of reports) {
  assert.equal(report.provider, "fixture-serp");
  if (report.ranked.precision >= report.baseline.precision) improvedPrecision += 1;
  if (report.ranked.recall >= report.baseline.recall) improvedRecall += 1;
  if (report.ranked.timeToUseful <= report.baseline.timeToUseful) improvedTtu += 1;
}

const mean = (key, side) =>
  reports.reduce((sum, report) => sum + report[side][key], 0) / reports.length;

const pRanked = mean("precision", "ranked");
const pBase = mean("precision", "baseline");
const rRanked = mean("recall", "ranked");
const rBase = mean("recall", "baseline");
const ttuRanked = mean("timeToUseful", "ranked");
const ttuBase = mean("timeToUseful", "baseline");

assert.ok(pRanked > pBase, `P@3 must improve (${pBase.toFixed(3)} → ${pRanked.toFixed(3)})`);
assert.ok(rRanked > rBase, `R@3 must improve (${rBase.toFixed(3)} → ${rRanked.toFixed(3)})`);
assert.ok(ttuRanked < ttuBase, `time-to-useful must drop (${ttuBase} → ${ttuRanked})`);
assert.equal(improvedPrecision, reports.length);
assert.equal(improvedRecall, reports.length);
assert.equal(improvedTtu, reports.length);

const nginx = rankHits("nginx proxy_read_timeout", lines[0].hits);
assert.ok(
  String(nginx[0].url).includes("ngx_http_proxy_module"),
  "docs page should outrank the homepage for a directive query",
);
assert.ok(scoreHit("nginx proxy_pass", { url: "https://nginx.org/en/docs/http/", title: "proxy" }) >
  scoreHit("nginx proxy_pass", { url: "https://shop.example/", title: "buy" }));

console.log(
  `discovery-rank.selftest: OK  held-out n=${reports.length}  P@3 ${pBase.toFixed(2)}→${pRanked.toFixed(2)}  R@3 ${rBase.toFixed(2)}→${rRanked.toFixed(2)}  TTU ${ttuBase.toFixed(2)}→${ttuRanked.toFixed(2)}`,
);
