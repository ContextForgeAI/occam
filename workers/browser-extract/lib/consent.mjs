/** Generic CMP / cookie-banner dismiss — site-agnostic selectors only. */

/** Stable CSS / testid selectors (querySelector-friendly, tried first). */
export const CONSENT_CSS_SELECTORS = [
  "#onetrust-accept-btn-handler",
  "#CybotCookiebotDialogBodyLevelButtonLevelOptinAllowAll",
  "#truste-consent-button",
  'button[data-testid="accept-all"]',
  '[data-testid="accept-all"]',
  'button[id*="accept-all" i]',
  'button[id*="accept_all" i]',
  ".fc-cta-consent",
  'button.fc-button.fc-cta-consent.fc-primary-button',
  'button[aria-label*="accept" i]',
  'button[aria-label*="agree" i]',
  'button[class*="accept" i]',
];

/** Playwright text/role selectors — slower; scanned under a time budget. */
export const CONSENT_TEXT_SELECTORS = [
  // English
  'a[role="button"]:has-text("Accept")',
  'button:has-text("Accept all")',
  'button:has-text("Accept All")',
  'button:has-text("Accept cookies")',
  'button:has-text("Accept and close")',
  'button:has-text("Allow all")',
  'button:has-text("I agree")',
  'button:has-text("Agree")',
  'button:has-text("Got it")',
  // French / EU CMPs (Accepter before any vague OK — "OK" false-positives leave the wall up)
  'button:has-text("Tout accepter")',
  'button:has-text("Accepter et fermer")',
  'button:has-text("Accepter tout")',
  'button:has-text("J\'accepte")',
  'button:has-text("Accepter")',
  'button:has-text("Continuer sans accepter")',
  // German / Dutch / Spanish / Italian / Portuguese
  'button:has-text("Alle akzeptieren")',
  'button:has-text("Alles akzeptieren")',
  'button:has-text("Akzeptieren")',
  'button:has-text("Alles accepteren")',
  'button:has-text("Aceptar todo")',
  'button:has-text("Aceptar")',
  'button:has-text("Accetta tutto")',
  'button:has-text("Accetta")',
  'button:has-text("Aceitar tudo")',
  'button:has-text("Aceitar")',
];

/** Full ordered list (CSS first) — kept for recipes / docs / selftests. */
export const CONSENT_SELECTORS = [...CONSENT_CSS_SELECTORS, ...CONSENT_TEXT_SELECTORS];

/** Role-name patterns for getByRole — keep in sync with CONSENT_TEXT_SELECTORS languages. */
export const CONSENT_ROLE_NAME_RE =
  /accept|agree|allow all|got it|accepter|akzeptieren|accepteren|aceptar|accetta|aceitar/i;

/** Hard cap for a consent scan — stripConsentOnly still removes CMP dialogs in HTML. */
export const CONSENT_SCAN_BUDGET_MS = 2_500;

const CONSENT_FRAME_HINTS = ["consent", "cookie", "gdpr", "privacy", "sp_message"];

/** Containers to hide when click fails — layered approach (SnapRender / DEV CMP rules 2025–2026). */
export const CONSENT_CONTAINER_SELECTORS = [
  "#onetrust-consent-sdk",
  "#onetrust-banner-sdk",
  "#CybotCookiebotDialog",
  ".qc-cmp2-container",
  "#didomi-host",
  ".fc-consent-root",
  ".sp_message_container",
  '[class*="cookie-banner"]',
  '[class*="cookie-consent"]',
  '[id*="cookie-notice"]',
  '[class*="consent-banner"]',
];

const OVERLAY_HIDE_CSS = [
  ...CONSENT_CONTAINER_SELECTORS.map((s) => `${s} { display: none !important; visibility: hidden !important; }`),
  "body, html { overflow: auto !important; position: static !important; }",
].join("\n");

export async function tryDismissConsent(page, { aggressive = false, recipe = null } = {}) {
  // Brief settle only — long sleeps were burning WRB browser p90 on CMP-heavy pages.
  if (aggressive) {
    await page.waitForTimeout(350);
  }

  const extra = recipe?.consentSelectors ?? [];
  const clicked = await clickConsentInScopes([page, ...page.frames()], extra, CONSENT_SCAN_BUDGET_MS);
  if (clicked) {
    await page.waitForTimeout(aggressive ? 450 : 250);
    return clicked;
  }

  return null;
}

/**
 * @param {import('playwright').Page[]} scopes
 * @param {string[]} extraSelectors
 * @param {number} budgetMs
 */
async function clickConsentInScopes(scopes, extraSelectors = [], budgetMs = CONSENT_SCAN_BUDGET_MS) {
  const deadline = Date.now() + budgetMs;
  const ordered = prioritizeFrames(scopes);
  const cssSelectors = [...extraSelectors.filter((s) => !/:has-text\(|text=/i.test(s)), ...CONSENT_CSS_SELECTORS];
  const textSelectors = [
    ...extraSelectors.filter((s) => /:has-text\(|text=/i.test(s)),
    ...CONSENT_TEXT_SELECTORS,
  ];

  for (const scope of ordered) {
    if (Date.now() > deadline) return null;

    // Prefer open dialogs / modals (custom React CMP portals).
    const clickScopes = [scope];
    try {
      const dialogCount = await scope
        .locator('[role="dialog"], [role="alertdialog"], dialog, [aria-modal="true"]')
        .count();
      for (let i = 0; i < Math.min(dialogCount, 3); i++) {
        clickScopes.push(
          scope.locator('[role="dialog"], [role="alertdialog"], dialog, [aria-modal="true"]').nth(i),
        );
      }
    } catch {
      // keep page/frame only
    }

    for (const clickScope of clickScopes) {
      if (Date.now() > deadline) return null;

      // Instant CSS hit via evaluate when the scope is the page/frame (not a locator).
      if (typeof clickScope.evaluate === "function" && cssSelectors.length > 0) {
        try {
          const cssHit = await clickScope.evaluate((selectors) => {
            for (const sel of selectors) {
              try {
                const el = document.querySelector(sel);
                if (!el) continue;
                const style = window.getComputedStyle(el);
                if (style.display === "none" || style.visibility === "hidden") continue;
                if (el.offsetParent === null && style.position !== "fixed") continue;
                return sel;
              } catch {
                // invalid selector in this document
              }
            }
            return null;
          }, cssSelectors);
          if (cssHit) {
            await clickScope.locator(cssHit).first().click({ timeout: 1500, force: true });
            return cssHit;
          }
        } catch {
          // fall through to locator path
        }
      }

      try {
        const roleBtn = clickScope.getByRole("button", { name: CONSENT_ROLE_NAME_RE }).first();
        if (await roleBtn.isVisible({ timeout: 120 })) {
          await roleBtn.click({ timeout: 1500, force: true });
          return "role:accept-button";
        }
      } catch {
        // continue
      }

      for (const selector of textSelectors) {
        if (Date.now() > deadline) return null;
        try {
          const locator = clickScope.locator(selector).first();
          if (await locator.isVisible({ timeout: 80 })) {
            await locator.click({ timeout: 1500, force: true });
            return selector;
          }
        } catch {
          // try next
        }
      }
    }
  }

  return null;
}

/** CSS layer: hide CMP chrome if click did not fully dismiss (production pattern ~80–95% coverage). */
export async function hideConsentOverlays(page) {
  try {
    await page.addStyleTag({ content: OVERLAY_HIDE_CSS });
  } catch {
    // non-fatal
  }
}

/** Wait until known CMP root is detached or hidden after accept click. */
export async function waitForConsentOverlayHidden(page, { timeoutMs = 4000 } = {}) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const visible = await page
      .evaluate((selectors) => {
        for (const sel of selectors) {
          const el = document.querySelector(sel);
          if (!el) continue;
          const style = window.getComputedStyle(el);
          if (style.display !== "none" && style.visibility !== "hidden" && el.offsetParent !== null) {
            return true;
          }
        }
        return false;
      }, CONSENT_CONTAINER_SELECTORS)
      .catch(() => false);
    if (!visible) return true;
    await page.waitForTimeout(250);
  }
  return false;
}

function prioritizeFrames(scopes) {
  const page = scopes[0];
  const frames = scopes.slice(1);
  const hinted = frames.filter((f) => {
    const name = (f.url() || "").toLowerCase();
    return CONSENT_FRAME_HINTS.some((h) => name.includes(h));
  });
  const rest = frames.filter((f) => !hinted.includes(f));
  return [page, ...hinted, ...rest];
}
