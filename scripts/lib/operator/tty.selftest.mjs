#!/usr/bin/env node
import assert from "node:assert/strict";
import { canPromptInteractively, canOpenControllingTty } from "./tty.mjs";

function main() {
  assert.equal(
    canPromptInteractively({
      stdin: { isTTY: true },
      stdout: { isTTY: true },
      env: {},
    }),
    true,
  );
  assert.equal(
    canPromptInteractively({
      stdin: { isTTY: false },
      stdout: { isTTY: true },
      platform: "win32",
      env: {},
    }),
    false,
  );
  assert.equal(
    canPromptInteractively({
      stdin: { isTTY: false },
      stdout: { isTTY: false },
      platform: "linux",
      env: { CI: "1" },
    }),
    false,
  );
  // Existence check should not throw.
  assert.equal(typeof canOpenControllingTty(), "boolean");
  console.log("tty.selftest: OK");
}

main();
