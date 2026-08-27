#!/usr/bin/env node
/**
 * Last-resort archive-member listing parser for bootstrap when the Node
 * ustar preflight module is unavailable. Accepts `tar -tvzf` / `tar.exe -tvzf`
 * output (GNU and BSD layouts) and applies the same path/root safety rules
 * as archive-preflight.mjs.
 *
 * Prefer archive-preflight.mjs (direct gzipped-ustar header read) whenever
 * possible — this path only inspects listing text.
 */
import fs from "node:fs";
import { pathToFileURL } from "node:url";

/**
 * @param {string} line
 * @returns {{ type: string, name: string } | null}
 */
export function parseTarListingLine(line) {
  const raw = String(line || "");
  if (!raw.trim()) return null;

  const type = raw[0] || "";
  const arrow = raw.indexOf(" -> ");
  if (arrow !== -1 && (type === "l" || type === "h")) {
    const left = raw.slice(0, arrow).trim().split(/\s+/);
    const name = left.slice(nameStartIndex(left)).join(" ");
    return { type, name };
  }

  const parts = raw.trim().split(/\s+/);
  if (parts.length < 2) return null;
  const start = nameStartIndex(parts);
  if (start < 0 || start >= parts.length) return null;
  const name = parts.slice(start).join(" ");
  if (!name) return null;
  return { type, name };
}

/**
 * GNU: perms owner/group size date time name… (owner token contains '/')
 * BSD: perms links user group size month day time name…
 * @param {string[]} parts
 */
function nameStartIndex(parts) {
  if (parts.length >= 6 && String(parts[1] || "").includes("/")) {
    return 5;
  }
  if (parts.length >= 9 && /^\d+$/.test(String(parts[1] || ""))) {
    return 8;
  }
  // Windows tar.exe often mirrors GNU with fewer leading fields.
  if (parts.length >= 6) return 5;
  return -1;
}

/**
 * @param {string} memberPath
 * @returns {string|null}
 */
export function classifyUnsafeArchivePath(memberPath) {
  const raw = String(memberPath || "");
  if (!raw) return "empty archive member path";
  const normalized = raw.replace(/\\/g, "/");
  if (normalized.startsWith("/") || normalized.startsWith("~")) {
    return `absolute archive member path: ${raw}`;
  }
  if (/^[A-Za-z]:(\/|$)/.test(normalized)) {
    return `windows drive archive member path: ${raw}`;
  }
  if (normalized.startsWith("//")) {
    return `unc archive member path: ${raw}`;
  }
  if (normalized.split("/").includes("..")) {
    return `path traversal in archive member: ${raw}`;
  }
  return null;
}

/**
 * @param {string} listingText
 * @param {string} expectedRoot
 */
export function validateTarListingText(listingText, expectedRoot) {
  const lines = String(listingText || "").split(/\r?\n/).filter(Boolean);
  /** @type {string[]} */
  const names = [];
  for (const line of lines) {
    const parsed = parseTarListingLine(line);
    if (!parsed || !parsed.name) {
      throw new Error(`unable to parse archive member listing line: ${line}`);
    }
    if (parsed.type === "l" || parsed.type === "h") {
      throw new Error(
        `${parsed.type === "l" ? "symlink" : "hardlink"} archive members are not allowed: ${parsed.name}`,
      );
    }
    names.push(parsed.name.replace(/\\/g, "/"));
  }

  const roots = new Set();
  for (const name of names) {
    const reason = classifyUnsafeArchivePath(name);
    if (reason) throw new Error(reason);
    const root = name.split("/").filter(Boolean)[0];
    if (root) roots.add(root);
  }
  if (!roots.has(expectedRoot)) {
    throw new Error(`missing expected archive root directory: ${expectedRoot}`);
  }
  for (const root of roots) {
    if (root !== expectedRoot) {
      throw new Error(`unexpected archive root entries: ${root}`);
    }
  }
  return { members: names.length, roots: [...roots] };
}

function main() {
  const [listingPath, expectedRoot] = process.argv.slice(2);
  if (!listingPath || !expectedRoot) {
    console.error("usage: node archive-preflight-listing.mjs LISTING.txt EXPECTED_ROOT");
    process.exit(2);
  }
  try {
    const text = fs.readFileSync(listingPath, "utf8");
    const result = validateTarListingText(text, expectedRoot);
    console.log(`archive-preflight-listing: members=${result.members}`);
    console.log("archive-preflight-listing: OK");
  } catch (error) {
    console.error(`error: ${error instanceof Error ? error.message : String(error)}`);
    process.exit(1);
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
