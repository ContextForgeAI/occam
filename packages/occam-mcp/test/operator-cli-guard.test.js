import test from "node:test";
import assert from "node:assert/strict";
import {
  formatOperatorCliRefusal,
  matchOperatorCliVerb,
  OPERATOR_CLI_VERBS,
} from "../lib/operator-cli-guard.mjs";

test("matchOperatorCliVerb catches connect and aliases", () => {
  assert.equal(matchOperatorCliVerb(["connect"]), "connect");
  assert.equal(matchOperatorCliVerb(["settings", "--json"]), "settings");
  assert.equal(matchOperatorCliVerb(["--help"]), null);
  assert.equal(matchOperatorCliVerb(["--mcp-server"]), null);
  assert.equal(matchOperatorCliVerb([]), null);
});

test("OPERATOR_CLI_VERBS includes core operator surface", () => {
  for (const verb of ["connect", "doctor", "disconnect", "onboard", "help", "status"]) {
    assert.ok(OPERATOR_CLI_VERBS.has(verb), verb);
  }
});

test("formatOperatorCliRefusal points at get-ff-occam", () => {
  const text = formatOperatorCliRefusal("connect");
  assert.match(text, /get-ff-occam\.sh/);
  assert.match(text, /occam connect/);
  assert.match(text, /INSTALL\.md/);
});
