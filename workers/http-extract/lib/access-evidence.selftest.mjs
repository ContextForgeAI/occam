import assert from "node:assert/strict";
import { JSDOM } from "jsdom";
import { collectAccessEvidence } from "../../shared/lib/access-evidence.mjs";

function collect(html, requestedUrl = "https://example.test/start", finalUrl = requestedUrl) {
  const dom = new JSDOM(html, { url: finalUrl });
  return collectAccessEvidence(dom.window.document, { requestedUrl, finalUrl });
}

const prose = collect("<main><h1>OAuth</h1><p>Authentication required is protocol terminology.</p></main>");
assert.equal(prose.authentication_terminology, true);
assert.equal(prose.password_field, false);
assert.equal(prose.login_form_action, false);

const wall = collect('<main><h1>Sign in</h1><form><input type="email"><input type="password"><button>Log in</button></form></main>');
assert.equal(wall.password_field, true);
assert.equal(wall.identity_field, true);
assert.equal(wall.login_form_action, true);
assert.equal(wall.login_heading, true);
assert.equal(wall.has_usable_content, false);

const widget = collect(`<main><article>${"Public article text ".repeat(50)}</article><form><input type="password"><button>Log in</button></form></main>`);
assert.equal(widget.has_usable_content, true);

const redirect = collect("<main>Sign in</main>", "https://example.test/docs", "https://example.test/account/sign-in");
assert.equal(redirect.redirected_to_login, true);

console.log("access-evidence: OK");
