#!/usr/bin/env node
/**
 * Discoverability gate — PUBLIC_CORE / PUBLIC_ADVANCED families must be findable
 * through maintainable doc path globs (see scripts/lib/docs-discoverability-catalog.mjs).
 *
 * Run standalone: node scripts/check-docs-discoverability.mjs
 * Also invoked from scripts/check-docs.mjs
 */
import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import { dirname, join, relative, resolve, sep } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import {
  DISCOVERABILITY_FAMILIES,
  DO_NOT_FEATURE_FAMILIES,
  OPT_IN_ENV_GATES,
} from "./lib/docs-discoverability-catalog.mjs";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

function repoPath(path) {
  return relative(repoRoot, path).split(sep).join("/");
}

function expandGlobs(patterns) {
  const out = [];
  for (const pattern of patterns) {
    if (!pattern.includes("*")) {
      out.push(resolve(repoRoot, pattern));
      continue;
    }
    const normalized = pattern.replace(/\\/g, "/");
    const starIdx = normalized.indexOf("*");
    const base = normalized.slice(0, normalized.lastIndexOf("/", starIdx));
    const suffix = normalized.slice(normalized.indexOf("*") + 1);
    walk(resolve(repoRoot, base), suffix, out);
  }
  return out;
}

function walk(dir, suffix, out) {
  if (!existsSync(dir)) return;
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) walk(full, suffix, out);
    else if (entry.name.endsWith(suffix.replace(/^\//, ""))) out.push(full);
  }
}

function fileMatchesMarkers(filePath, slug, markers = []) {
  if (!existsSync(filePath)) return false;
  const text = readFileSync(filePath, "utf8").toLowerCase();
  if (text.includes(slug.toLowerCase())) return true;
  for (const marker of markers) {
    if (text.includes(marker.toLowerCase())) return true;
  }
  return false;
}

function groupMatches(group, slug) {
  if (!group) return false;
  const files = expandGlobs(group.globs);
  const markers = group.markers ?? [];
  return files.some((file) => fileMatchesMarkers(file, slug, markers));
}

/**
 * @param {string} [root]
 * @returns {{ errors: string[], warnings: string[] }}
 */
export function checkDiscoverability(root = repoRoot) {
  const errors = [];
  const warnings = [];
  const llmsPath = join(root, "llms.txt");
  const llmsText = existsSync(llmsPath) ? readFileSync(llmsPath, "utf8") : "";

  for (const family of DISCOVERABILITY_FAMILIES) {
    const { slug, exposureClass } = family;

    // R4: slug verbatim in llms.txt
    if (!llmsText.includes(slug)) {
      errors.push(`DISC-R4: llms.txt missing family slug "${slug}" (${exposureClass})`);
    }
    for (const marker of family.llmsMarkers ?? []) {
      if (!llmsText.toLowerCase().includes(marker.toLowerCase())) {
        warnings.push(`DISC-R4-warn: llms.txt missing marker "${marker}" for ${slug}`);
      }
    }

    const hasTask =
      groupMatches(family.task, slug) ||
      groupMatches(family.quickstart, slug);
    const hasReference = groupMatches(family.reference, slug);
    const hasHandbook = groupMatches(family.handbook, slug);
    const hasLlmsDoc = groupMatches(family.llmsDoc, slug);

    if (exposureClass === "PUBLIC_CORE") {
      if (!hasTask) {
        errors.push(
          `DISC-CORE: ${slug} missing TASK/QUICKSTART discovery path (see catalog task globs)`,
        );
      }
      if (!llmsText.includes(slug)) {
        // already reported
      } else if (!hasReference) {
        errors.push(
          `DISC-CORE: ${slug} missing REFERENCE/CAPABILITY discovery path (see catalog reference globs)`,
        );
      }
    } else if (exposureClass === "PUBLIC_ADVANCED") {
      if (!hasReference) {
        errors.push(
          `DISC-ADV: ${slug} missing REFERENCE/CAPABILITY discovery path (see catalog reference globs)`,
        );
      }
      if (!hasTask && !hasHandbook && !llmsText.includes(slug)) {
        errors.push(
          `DISC-ADV: ${slug} missing second discovery path (TASK, HANDBOOK, or LLMS)`,
        );
      }
    }
  }

  // R2: opt-in env gates co-located with tool names
  for (const gate of OPT_IN_ENV_GATES) {
    let found = false;
    for (const pattern of gate.docGlobs) {
      const files = expandGlobs([pattern]);
      for (const file of files) {
        if (!existsSync(file)) continue;
        const text = readFileSync(file, "utf8");
        if (!text.includes(gate.env)) continue;
        if (gate.tools.every((tool) => text.includes(tool))) {
          found = true;
          break;
        }
      }
      if (found) break;
    }
    if (!found) {
      errors.push(
        `DISC-R2: ${gate.env} not co-located with tools [${gate.tools.join(", ")}] in ${gate.docGlobs.join(" or ")}`,
      );
    }
  }

  // R9: DO_NOT families as positive headlines in llms feature sections
  const llmsFeatureSection = llmsText.split(/## Trust limits/i)[0] ?? llmsText;
  const readmePath = join(root, "README.md");
  const readmeText = existsSync(readmePath) ? readFileSync(readmePath, "utf8") : "";
  for (const item of DO_NOT_FEATURE_FAMILIES) {
    for (const phrase of item.forbiddenHeadlineMarkers) {
      const bulletRe = new RegExp(`^\\s*[-*].*${escapeRe(phrase)}`, "im");
      const negatedBulletRe = new RegExp(
        `^\\s*[-*].*(?:not|never|forbidden|do not).{0,40}${escapeRe(phrase)}`,
        "im",
      );
      for (const line of llmsFeatureSection.split(/\r?\n/)) {
        if (!bulletRe.test(line)) continue;
        if (negatedBulletRe.test(line)) continue;
        if (/forbidden readings?/i.test(line)) continue;
        errors.push(`DISC-R9: llms.txt feature bullet overclaims DO_NOT family: "${phrase}"`);
        break;
      }
      if (bulletRe.test(readmeText.split(/## What Occam does/i)[0] ?? "")) {
        warnings.push(`DISC-R9-warn: README may overclaim DO_NOT phrase: "${phrase}"`);
      }
    }
  }

  return { errors, warnings };
}

function escapeRe(s) {
  return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function main() {
  const { errors, warnings } = checkDiscoverability();
  for (const w of warnings) console.warn(`  warn: ${w}`);
  if (errors.length) {
    console.error(`docs-discoverability: FAILED (${errors.length})`);
    for (const e of errors) console.error(`  - ${e}`);
    process.exit(1);
  }
  console.log(
    `docs-discoverability: OK — ${DISCOVERABILITY_FAMILIES.length} families, ${OPT_IN_ENV_GATES.length} env gates`,
  );
}

const isMain =
  process.argv[1] &&
  import.meta.url === pathToFileURL(resolve(process.argv[1])).href;

if (isMain) main();
