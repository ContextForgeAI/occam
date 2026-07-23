/** Generic CMP / cookie-banner dismiss — site-agnostic selectors only. */

export const CONSENT_SELECTORS = [
  "#onetrust-accept-btn-handler",
  "#CybotCookiebotDialogBodyLevelButtonLevelOptinAllowAll",
  "#truste-consent-button",
  'button[data-testid="accept-all"]',
  'button[id*="accept-all" i]',
  'button[id*="accept_all" i]',
  'button[class*="accept" i]',
  'button[aria-label*="accept" i]',
  'button[aria-label*="agree" i]',
  'a[role="button"]:has-text("Accept")',
  'button:has-text("Accept all")',
  'button:has-text("Accept All")',
  'button:has-text("Accept cookies")',
  'button:has-text("Allow all")',
  'button:has-text("I agree")',
  'button:has-text("Agree")',
  'button:has-text("Got it")',
  'button:has-text("OK")',
  '[data-testid="accept-all"]',
  ".fc-cta-consent",
  'button.fc-button.fc-cta-consent.fc-primary-button',
];

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
  if (aggressive) {
    await page.waitForTimeout(1200);
  }

  const extra = recipe?.consentSelectors ?? [];
  const clicked = await clickConsentInScopes([page, ...page.frames()], page, extra);
  if (clicked) {
    await page.waitForTimeout(aggressive ? 900 : 500);
    return clicked;
  }

  if (aggressive) {
    await page.waitForTimeout(1500);
    return await clickConsentInScopes([page, ...page.frames()], page, extra);
  }

  return null;
}

async function clickConsentInScopes(scopes, page, extraSelectors = []) {
  const ordered = prioritizeFrames(scopes);
  const selectors = [...extraSelectors, ...CONSENT_SELECTORS];

  for (const scope of ordered) {
    for (const selector of selectors) {
      try {
        const locator = scope.locator(selector).first();
        if (await locator.isVisible({ timeout: 500 })) {
          await locator.click({ timeout: 2500, force: true });
          return selector;
        }
      } catch {
        // try next
      }
    }

    try {
      const roleBtn = scope.getByRole("button", { name: /accept|agree|allow all|got it/i }).first();
      if (await roleBtn.isVisible({ timeout: 300 })) {
        await roleBtn.click({ timeout: 2500, force: true });
        return "role:accept-button";
      }
    } catch {
      // continue
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
