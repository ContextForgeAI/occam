import assert from "node:assert/strict";
import { applyErrorShellAccess, looksLikeErrorShell } from "./error-shell.mjs";

const tinyShell = "## This page couldn't load\n\nReload to try again, or go back.";
assert.equal(looksLikeErrorShell(tinyShell), true);

const curly = "## This page couldn’t load\n\nReload to try again, or go back.";
assert.equal(looksLikeErrorShell(curly), true);

const awSnap = "# Aw Snap\n\nERR_INTERNET_DISCONNECTED";
assert.equal(looksLikeErrorShell(awSnap), true);

const connection = "ERR_CONNECTION_RESET";
assert.equal(looksLikeErrorShell(connection), true);

assert.equal(looksLikeErrorShell("try again"), false);
assert.equal(looksLikeErrorShell("something went wrong"), false);
assert.equal(looksLikeErrorShell("err_custom_debug"), false);

const longArticle = `${"When a tab shows This page couldn’t load, operators quote Reload to try again. ".repeat(20)}`;
assert.ok(longArticle.length >= 800);
assert.equal(looksLikeErrorShell(longArticle), false);

const access = applyErrorShellAccess({ has_usable_content: true }, tinyShell);
assert.equal(access.has_usable_content, false);
assert.equal(access.error_shell, true);

const legit = applyErrorShellAccess(
  { has_usable_content: false },
  "# Example Domain\n\nThis domain is for use in illustrative examples in documents.",
);
assert.equal(legit.error_shell, undefined);
assert.equal(legit.has_usable_content, false);

console.log("error-shell: OK");
