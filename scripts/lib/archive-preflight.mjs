#!/usr/bin/env node
/**
 * Pre-extraction safety checks for Occam Level-B release archives (.tar.gz).
 *
 * Contract:
 *   untrusted archive → inspect member metadata → reject unsafe members
 *   → extract into isolated staging → validateReleaseRoot → install transaction
 *
 * This module intentionally does not extract file bodies. It reads gzipped
 * ustar/pax headers only, so adversarial path/symlink cases fail closed before
 * any host tar extractor is invoked.
 */
import fs from "node:fs";
import path from "node:path";
import zlib from "node:zlib";
import { pathToFileURL } from "node:url";

export const ARCHIVE_BLOCK_SIZE = 512;

/** @typedef {"file"|"hardlink"|"symlink"|"directory"|"other"} ArchiveMemberType */

/**
 * @typedef {{
 *   name: string,
 *   type: ArchiveMemberType,
 *   linkname: string,
 *   size: number,
 *   mode: number,
 * }} ArchiveMember
 */

/**
 * @param {Buffer} block
 * @returns {boolean}
 */
function isZeroBlock(block) {
  for (let i = 0; i < block.length; i += 1) {
    if (block[i] !== 0) return false;
  }
  return true;
}

/**
 * @param {string} text
 * @returns {number}
 */
function parseOctal(text) {
  const trimmed = String(text || "").replace(/\0/g, "").trim();
  if (!trimmed) return 0;
  return Number.parseInt(trimmed, 8);
}

/**
 * @param {Buffer} block
 * @returns {string}
 */
function readCString(block) {
  const end = block.indexOf(0);
  return block.subarray(0, end === -1 ? block.length : end).toString("utf8");
}

/**
 * @param {number} typeflag
 * @returns {ArchiveMemberType}
 */
function classifyType(typeflag) {
  if (typeflag === 0 || typeflag === 48) return "file"; // \0 or '0'
  if (typeflag === 49) return "hardlink"; // '1'
  if (typeflag === 50) return "symlink"; // '2'
  if (typeflag === 53) return "directory"; // '5'
  return "other";
}

/**
 * Parse a gzipped ustar/pax tarball into member metadata without extracting.
 * Supports ordinary ustar headers and skips pax/gnu extended header payloads.
 *
 * @param {string} archivePath
 * @returns {ArchiveMember[]}
 */
export function listTarGzMembers(archivePath) {
  const compressed = fs.readFileSync(archivePath);
  const raw = zlib.gunzipSync(compressed);
  /** @type {ArchiveMember[]} */
  const members = [];
  let offset = 0;
  let pendingPaxPath = "";
  let pendingPaxLink = "";

  while (offset + ARCHIVE_BLOCK_SIZE <= raw.length) {
    const header = raw.subarray(offset, offset + ARCHIVE_BLOCK_SIZE);
    offset += ARCHIVE_BLOCK_SIZE;
    if (isZeroBlock(header)) {
      if (offset + ARCHIVE_BLOCK_SIZE <= raw.length && isZeroBlock(raw.subarray(offset, offset + ARCHIVE_BLOCK_SIZE))) {
        break;
      }
      continue;
    }

    const checksumField = header.subarray(148, 156).toString("utf8").replace(/\0/g, "").trim();
    const expectedChecksum = Number.parseInt(checksumField, 8);
    if (!Number.isFinite(expectedChecksum)) {
      throw new Error("archive header checksum is not octal");
    }
    let sum = 0;
    for (let i = 0; i < ARCHIVE_BLOCK_SIZE; i += 1) {
      sum += i >= 148 && i < 156 ? 32 : header[i];
    }
    if (sum !== expectedChecksum) {
      throw new Error("archive header checksum mismatch");
    }

    const name = readCString(header.subarray(0, 100));
    const prefix = readCString(header.subarray(345, 500));
    const linkname = readCString(header.subarray(157, 257));
    const size = parseOctal(header.subarray(124, 136).toString("utf8"));
    const mode = parseOctal(header.subarray(100, 108).toString("utf8"));
    const typeflag = header[156];
    const type = classifyType(typeflag);
    const payloadBlocks = Math.ceil(size / ARCHIVE_BLOCK_SIZE);
    const payload = raw.subarray(offset, offset + payloadBlocks * ARCHIVE_BLOCK_SIZE);
    offset += payloadBlocks * ARCHIVE_BLOCK_SIZE;

    // GNU long-name / long-link and PAX extended headers carry metadata only.
    if (typeflag === 76 || typeflag === 75) {
      // 'L' long name, 'K' long link
      const text = payload.subarray(0, size).toString("utf8").replace(/\0/g, "");
      if (typeflag === 76) pendingPaxPath = text;
      else pendingPaxLink = text;
      continue;
    }
    if (typeflag === 120 || typeflag === 88) {
      // 'x' / 'X' pax
      const text = payload.subarray(0, size).toString("utf8");
      for (const line of text.split("\n")) {
        const match = /^(\d+)\s+([^=]+)=(.*)$/.exec(line);
        if (!match) continue;
        if (match[2] === "path") pendingPaxPath = match[3];
        if (match[2] === "linkpath") pendingPaxLink = match[3];
      }
      continue;
    }

    const joined = prefix ? `${prefix}/${name}` : name;
    const memberName = pendingPaxPath || joined;
    const memberLink = pendingPaxLink || linkname;
    pendingPaxPath = "";
    pendingPaxLink = "";
    members.push({
      name: memberName.replace(/\\/g, "/"),
      type,
      linkname: memberLink.replace(/\\/g, "/"),
      size,
      mode,
    });
  }

  return members;
}

/**
 * @param {string} memberPath
 * @returns {string[]}
 */
export function splitArchivePath(memberPath) {
  return String(memberPath || "")
    .replace(/\\/g, "/")
    .split("/")
    .filter((part) => part.length > 0);
}

/**
 * Reject archive member paths that can escape an extract root.
 * @param {string} memberPath
 * @returns {string|null} reason or null when safe
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
  if (normalized.startsWith("//") || /^[\\/]{2}[^\\/]/.test(raw)) {
    return `unc archive member path: ${raw}`;
  }
  if (normalized.includes("\0")) {
    return `nul byte in archive member path: ${raw}`;
  }

  const parts = splitArchivePath(normalized);
  if (parts.includes("..")) {
    return `path traversal in archive member: ${raw}`;
  }

  return null;
}

/**
 * Symlink targets must remain inside the archive root when resolved against the
 * member's directory. Absolute / drive / UNC targets are rejected. Relative
 * `..` segments are allowed only when the resolved path stays under the first
 * archive root component.
 * @param {string} memberName
 * @param {string} linkname
 * @returns {string|null}
 */
export function classifyUnsafeSymlinkTarget(memberName, linkname) {
  const target = String(linkname || "");
  if (!target) return `symlink ${memberName} has empty target`;
  const normalized = target.replace(/\\/g, "/");
  if (normalized.startsWith("/") || normalized.startsWith("~")) {
    return `symlink ${memberName} has absolute target: ${target}`;
  }
  if (/^[A-Za-z]:(\/|$)/.test(normalized)) {
    return `symlink ${memberName} has windows drive target: ${target}`;
  }
  if (normalized.startsWith("//") || /^[\\/]{2}[^\\/]/.test(target)) {
    return `symlink ${memberName} has unc target: ${target}`;
  }
  if (normalized.includes("\0")) {
    return `symlink ${memberName} target contains nul`;
  }

  const memberParts = splitArchivePath(memberName);
  if (memberParts.length === 0) {
    return `symlink ${memberName} has empty member path`;
  }
  const archiveRoot = memberParts[0];
  const memberDir = memberParts.slice(0, -1);
  const targetParts = splitArchivePath(normalized);
  /** @type {string[]} */
  const resolved = [...memberDir];
  for (const part of targetParts) {
    if (part === "." || part === "") continue;
    if (part === "..") {
      if (resolved.length === 0) {
        return `symlink ${memberName} escapes archive root via ${target}`;
      }
      resolved.pop();
      continue;
    }
    resolved.push(part);
  }
  if (resolved.length === 0 || resolved[0] !== archiveRoot) {
    return `symlink ${memberName} escapes archive root via ${target}`;
  }
  return null;
}

/**
 * @param {ArchiveMember[]} members
 * @param {{ expectedRoot?: string }} [options]
 * @returns {string[]} problems
 */
export function validateArchiveMembers(members, options = {}) {
  /** @type {string[]} */
  const problems = [];
  if (!Array.isArray(members) || members.length === 0) {
    return ["archive contains no members"];
  }

  /** @type {Map<string, ArchiveMember>} */
  const seen = new Map();
  for (const member of members) {
    const name = String(member?.name || "");
    const pathReason = classifyUnsafeArchivePath(name);
    if (pathReason) problems.push(pathReason);

    if (member.type === "symlink" || member.type === "hardlink") {
      problems.push(
        `${member.type} archive members are not allowed in Occam release archives: ${name || "(unnamed)"}`,
      );
      const linkReason = classifyUnsafeSymlinkTarget(name, member.linkname);
      if (linkReason) problems.push(linkReason);
    } else if (member.type === "other") {
      problems.push(`unsupported archive member type for ${name || "(unnamed)"}`);
    }

    const prior = seen.get(name);
    if (prior) {
      const conflict =
        prior.type !== member.type ||
        prior.linkname !== member.linkname ||
        prior.size !== member.size;
      problems.push(
        conflict
          ? `conflicting duplicate archive member: ${name}`
          : `duplicate archive member: ${name}`,
      );
    } else {
      seen.set(name, member);
    }
  }

  const expectedRoot = options.expectedRoot ? String(options.expectedRoot) : "";
  if (expectedRoot) {
    const roots = new Set(
      members
        .map((member) => splitArchivePath(member.name)[0] || "")
        .filter(Boolean),
    );
    if (!roots.has(expectedRoot)) {
      problems.push(`missing expected archive root directory: ${expectedRoot}`);
    }
    const unexpected = [...roots].filter((root) => root !== expectedRoot);
    if (unexpected.length > 0) {
      problems.push(`unexpected archive root entries: ${unexpected.join(", ")}`);
    }
  }

  return problems;
}

/**
 * @param {{
 *   archivePath: string,
 *   expectedRoot?: string,
 * }} options
 * @returns {{ members: ArchiveMember[], problems: string[] }}
 */
export function preflightTarGzArchive(options) {
  const archivePath = path.resolve(options.archivePath);
  if (!fs.existsSync(archivePath)) {
    throw new Error(`archive not found: ${archivePath}`);
  }
  const members = listTarGzMembers(archivePath);
  const problems = validateArchiveMembers(members, { expectedRoot: options.expectedRoot });
  return { members, problems };
}

function parseArgs(argv) {
  /** @type {{ archivePath?: string, expectedRoot?: string }} */
  const options = {};
  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    if (arg === "--archive") options.archivePath = argv[++i];
    else if (arg === "--expected-root") options.expectedRoot = argv[++i];
    else if (arg === "-h" || arg === "--help") {
      console.log(
        "usage: node archive-preflight.mjs --archive PATH.tar.gz [--expected-root NAME]",
      );
      process.exit(0);
    } else {
      throw new Error(`unknown argument: ${arg}`);
    }
  }
  if (!options.archivePath) throw new Error("--archive is required");
  return options;
}

function main() {
  try {
    const options = parseArgs(process.argv.slice(2));
    const result = preflightTarGzArchive(options);
    if (result.problems.length > 0) {
      console.error("error: archive preflight failed:");
      for (const problem of result.problems) {
        console.error(`  ${problem}`);
      }
      process.exit(1);
    }
    console.log(`archive-preflight: members=${result.members.length}`);
    console.log("archive-preflight: OK");
  } catch (error) {
    console.error(`error: ${error instanceof Error ? error.message : String(error)}`);
    process.exit(1);
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
