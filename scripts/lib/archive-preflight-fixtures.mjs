#!/usr/bin/env node
/**
 * Deterministic ustar+gzip writer for adversarial archive-preflight fixtures.
 * Not used by production installers.
 */
import fs from "node:fs";
import zlib from "node:zlib";
import { ARCHIVE_BLOCK_SIZE } from "./archive-preflight.mjs";

/**
 * @param {string} value
 * @param {number} size
 */
function encodeString(value, size) {
  const out = Buffer.alloc(size, 0);
  Buffer.from(String(value || ""), "utf8").copy(out, 0, 0, size - 1);
  return out;
}

/**
 * @param {number} value
 * @param {number} size
 */
function encodeOctal(value, size) {
  const body = Math.max(0, Number(value) || 0).toString(8);
  const field = body.padStart(size - 1, "0");
  return encodeString(field, size);
}

/**
 * @param {{
 *   name: string,
 *   type?: "file"|"symlink"|"directory"|"hardlink",
 *   linkname?: string,
 *   content?: string|Buffer,
 *   mode?: number,
 * }} entry
 */
function buildUstarHeader(entry) {
  const content = Buffer.isBuffer(entry.content)
    ? entry.content
    : Buffer.from(entry.content || "", "utf8");
  const typeMap = {
    file: 48, // '0'
    hardlink: 49,
    symlink: 50,
    directory: 53,
  };
  const typeflag = typeMap[entry.type || "file"] ?? 48;
  const size = entry.type === "file" ? content.length : 0;

  const header = Buffer.alloc(ARCHIVE_BLOCK_SIZE, 0);
  encodeString(entry.name, 100).copy(header, 0);
  encodeOctal(entry.mode ?? (entry.type === "directory" ? 0o755 : 0o644), 8).copy(header, 100);
  encodeOctal(0, 8).copy(header, 108); // uid
  encodeOctal(0, 8).copy(header, 116); // gid
  encodeOctal(size, 12).copy(header, 124);
  encodeOctal(Math.floor(Date.now() / 1000), 12).copy(header, 136);
  header.fill(0x20, 148, 156); // checksum field = 8 spaces while summing
  header[156] = typeflag;
  encodeString(entry.linkname || "", 100).copy(header, 157);
  Buffer.from("ustar\0", "utf8").copy(header, 257);
  Buffer.from("00", "utf8").copy(header, 263);

  let sum = 0;
  for (let i = 0; i < ARCHIVE_BLOCK_SIZE; i += 1) sum += header[i];
  const checksumText = `${sum.toString(8).padStart(6, "0")}\0 `;
  Buffer.from(checksumText, "utf8").copy(header, 148, 0, 8);
  return { header, content: entry.type === "file" ? content : Buffer.alloc(0) };
}

/**
 * @param {Array<{
 *   name: string,
 *   type?: "file"|"symlink"|"directory"|"hardlink",
 *   linkname?: string,
 *   content?: string|Buffer,
 *   mode?: number,
 * }>} entries
 * @param {string} outPath
 */
export function writeTarGzArchive(entries, outPath) {
  /** @type {Buffer[]} */
  const parts = [];
  for (const entry of entries) {
    const { header, content } = buildUstarHeader(entry);
    parts.push(header);
    if (content.length > 0) {
      parts.push(content);
      const pad = (ARCHIVE_BLOCK_SIZE - (content.length % ARCHIVE_BLOCK_SIZE)) % ARCHIVE_BLOCK_SIZE;
      if (pad > 0) parts.push(Buffer.alloc(pad, 0));
    }
  }
  parts.push(Buffer.alloc(ARCHIVE_BLOCK_SIZE * 2, 0));
  const tar = Buffer.concat(parts);
  fs.writeFileSync(outPath, zlib.gzipSync(tar));
}
