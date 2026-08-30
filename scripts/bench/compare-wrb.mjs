#!/usr/bin/env node
/** Render two WRB result JSON files as a compact, direction-aware scorecard. */
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const paths = process.argv.slice(2);
if (paths.length !== 2) {
  console.error("usage: node scripts/bench/compare-wrb.mjs <occam.json> <competitor.json>");
  process.exit(2);
}

function load(path) {
  const absolute = resolve(path);
  let parsed;
  try {
    parsed = JSON.parse(readFileSync(absolute, "utf8"));
  } catch (error) {
    console.error(`error: cannot read WRB JSON ${absolute}: ${error.message}`);
    process.exit(2);
  }
  return { path: absolute, data: parsed };
}

function score(run, section) {
  return run.data?.[section]?.score ?? null;
}

function percent(value) {
  return Number.isFinite(value) ? `${(value * 100).toFixed(1)}%` : "not measured";
}

function number(value, suffix = "") {
  return Number.isFinite(value) ? `${Math.round(value)}${suffix}` : "not measured";
}

const [left, right] = paths.map(load);
const leftName = left.data.tool || "left";
const rightName = right.data.tool || "right";
const rows = [];

function add(area, metric, leftValue, rightValue, format, direction) {
  const leftText = format(leftValue);
  const rightText = format(rightValue);
  let winner = "not comparable";
  if (Number.isFinite(leftValue) && Number.isFinite(rightValue)) {
    if (leftValue === rightValue) winner = "tie";
    else if (direction === "high") winner = leftValue > rightValue ? leftName : rightName;
    else winner = leftValue < rightValue ? leftName : rightName;
  }
  rows.push([area, metric, leftText, rightText, winner]);
}

const lf = score(left, "fetch");
const rf = score(right, "fetch");
add("Fetch", "retrieval", lf?.retrieval_rate, rf?.retrieval_rate, percent, "high");
for (const tier of [1, 2, 3]) {
  add(
    "Fetch",
    `tier ${tier}`,
    lf?.tier_rates?.[tier],
    rf?.tier_rates?.[tier],
    percent,
    "high",
  );
}
add(
  "Fetch",
  "false positives",
  lf?.honesty?.false_positive_rate,
  rf?.honesty?.false_positive_rate,
  percent,
  "low",
);
add("Fetch", "p50", lf?.speed_median_ms, rf?.speed_median_ms, (v) => number(v, " ms"), "low");
add("Fetch", "p90", lf?.speed_p90_ms, rf?.speed_p90_ms, (v) => number(v, " ms"), "low");
add("Fetch", "median tokens", lf?.token_median, rf?.token_median, number, "low");

const ls = score(left, "search");
const rs = score(right, "search");
add("Search", "domain recall", ls?.recall, rs?.recall, percent, "high");
add("Search", "answer precision", ls?.precision, rs?.precision, percent, "high");
add("Search", "coverage", ls?.coverage, rs?.coverage, percent, "high");
add("Search", "p50", ls?.speed_median_ms, rs?.speed_median_ms, (v) => number(v, " ms"), "low");
add("Search", "median tokens", ls?.token_median, rs?.token_median, number, "low");

const lc = score(left, "crawl");
const rc = score(right, "crawl");
add("Crawl*", "coverage", lc?.coverage, rc?.coverage, percent, "high");
add("Crawl*", "precision", lc?.precision, rc?.precision, percent, "high");
add("Crawl*", "pages", lc?.pages_crawled, rc?.pages_crawled, number, "high");
add("Crawl*", "total time", lc?.speed_total_ms, rc?.speed_total_ms, (v) => number(v, " ms"), "low");
add("Crawl*", "total tokens", lc?.token_total, rc?.token_total, number, "low");

console.log(`# WRB scorecard: ${leftName} vs ${rightName}`);
console.log("");
console.log(`- ${leftName}: ${left.path} (${left.data.timestamp || "timestamp unavailable"})`);
console.log(`- ${rightName}: ${right.path} (${right.data.timestamp || "timestamp unavailable"})`);
console.log("");
console.log(`| Area | Metric | ${leftName} | ${rightName} | Better value |`);
console.log("|---|---|---:|---:|---|");
for (const row of rows) {
  console.log(`| ${row.join(" | ")} |`);
}
console.log("");
console.log("*Occam's current WRB crawl arm uses `occam_map` for URL discovery. It is not a resumable content crawl, so interpret this section as capability-gap evidence, not full feature parity.");
console.log("");
console.log("WRB uses deterministic probes and chars/4 token estimates. It is useful comparative evidence, not an independent product certification or an agent-answer-quality benchmark.");
