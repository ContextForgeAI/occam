#!/usr/bin/env node
/**
 * Self-test: GNU + BSD tar listing parsers used by piped-bootstrap fallback.
 */
import assert from "node:assert/strict";
import { pathToFileURL } from "node:url";
import {
  parseTarListingLine,
  validateTarListingText,
} from "./archive-preflight-listing.mjs";

function main() {
  const gnuFile =
    "-rwxr-xr-x 0/0      12345 2026-08-12 16:09 ff-occam-1.0.0-rc.3-linux-x64/OccamMcp.Core";
  const gnuDir =
    "drwxr-xr-x 0/0         0 2026-08-12 16:09 ff-occam-1.0.0-rc.3-linux-x64/";
  const bsdFile =
    "-rw-r--r--  1 user group 12345 Aug 12 16:09 ff-occam-1.0.0-rc.3-osx-arm64/VERSION";

  assert.equal(
    parseTarListingLine(gnuFile)?.name,
    "ff-occam-1.0.0-rc.3-linux-x64/OccamMcp.Core",
  );
  assert.equal(
    parseTarListingLine(gnuDir)?.name,
    "ff-occam-1.0.0-rc.3-linux-x64/",
  );
  assert.equal(
    parseTarListingLine(bsdFile)?.name,
    "ff-occam-1.0.0-rc.3-osx-arm64/VERSION",
  );

  const listing = [gnuDir, gnuFile].join("\n");
  const ok = validateTarListingText(listing, "ff-occam-1.0.0-rc.3-linux-x64");
  assert.equal(ok.members, 2);

  assert.throws(
    () => validateTarListingText(listing, "wrong-root"),
    /missing expected archive root/,
  );

  // Old BSD-only slice(8) would fail on GNU lines — ensure we never regress to empty name.
  assert.ok(parseTarListingLine(gnuFile)?.name);

  console.log("ok: archive-preflight-listing GNU/BSD parsers");
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
