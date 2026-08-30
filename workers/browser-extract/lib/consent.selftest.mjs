import assert from "node:assert/strict";
import { JSDOM } from "jsdom";
import {
  CONSENT_SELECTORS,
  CONSENT_CSS_SELECTORS,
  CONSENT_TEXT_SELECTORS,
  CONSENT_ROLE_NAME_RE,
  CONSENT_SCAN_BUDGET_MS,
} from "./consent.mjs";
import {
  stripConsentOnly,
  looksLikeConsentDialog,
  CMP_DIALOG_TEXT_RE,
} from "./html-preprocess.mjs";

assert.ok(CONSENT_SELECTORS.includes('button:has-text("Accepter")'));
assert.ok(CONSENT_TEXT_SELECTORS.includes('button:has-text("Tout accepter")'));
assert.ok(CONSENT_CSS_SELECTORS.includes("#onetrust-accept-btn-handler"));
assert.ok(CONSENT_SELECTORS.includes('button:has-text("Alle akzeptieren")'));
assert.ok(CONSENT_SELECTORS.includes('button:has-text("Continuer sans accepter")'));
assert.ok(
  !CONSENT_SELECTORS.includes('button:has-text("OK")'),
  "bare OK is a false-positive on non-CMP buttons and must stay out",
);
assert.ok(CONSENT_SCAN_BUDGET_MS <= 3000, "consent scan must stay budgeted for browser p90");
assert.equal(
  CONSENT_SELECTORS[0],
  CONSENT_CSS_SELECTORS[0],
  "CSS vendor selectors must lead the combined list",
);

assert.match("Accepter", CONSENT_ROLE_NAME_RE);
assert.match("Accept all", CONSENT_ROLE_NAME_RE);
assert.match("Alle akzeptieren", CONSENT_ROLE_NAME_RE);
assert.doesNotMatch("Valider votre recherche", CONSENT_ROLE_NAME_RE);
assert.doesNotMatch("OK", CONSENT_ROLE_NAME_RE);

assert.match("Pour leboncoin, votre expérience sur notre site est une priorité. Accepter Continuer sans accepter", CMP_DIALOG_TEXT_RE);

const keepDialogHtml = `<!doctype html><body>
  <main><h1>Listings</h1><p>Déposer une annonce Immobilier Véhicules</p></main>
  <div class="z-modal absolute" id="portal">
    <div role="dialog" class="z-modal" id="cmp">
      <h1>Pour leboncoin, votre expérience sur notre site est une priorité.</h1>
      <p>Nous utilisons des cookies et autres traceurs. Politique de confidentialité.</p>
      <button>Accepter</button>
      <button>Continuer sans accepter</button>
    </div>
  </div>
  <div role="dialog" id="settings"><h2>Account settings</h2><p>Change your password here.</p></div>
</body>`;

const dom = new JSDOM(keepDialogHtml);
assert.equal(looksLikeConsentDialog(dom.window.document.querySelector("#cmp")), true);
assert.equal(looksLikeConsentDialog(dom.window.document.querySelector("#settings")), false);
stripConsentOnly(dom.window.document);
assert.equal(dom.window.document.querySelector("#cmp"), null, "CMP dialog removed");
assert.equal(dom.window.document.querySelector("#portal"), null, "thin modal portal wrapper removed");
assert.ok(dom.window.document.querySelector("#settings"), "non-CMP dialog kept");
assert.ok(dom.window.document.querySelector("main"), "page content kept");

console.log("consent.selftest: OK");
