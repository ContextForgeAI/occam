/** Strip site chrome before Readability — browser path only (iter 004). */

import { CONSENT_CONTAINER_SELECTORS } from "./consent.mjs";

export const PROMO_BANNER_SELECTORS = [
  ".banner",
  ".banner-content",
  '[class*="promo-banner"]',
  '[class*="site-banner"]',
  '[id*="site-banner"]',
];

export const CHROME_SELECTORS = [  "nav",
  "footer",
  '[role="navigation"]',
  '[role="contentinfo"]',
  "aside",
  ".sidebar",
  ".site-footer",
  ".site-header",
  ".page-footer",
  ".page-header",
  ".global-header",
  ".global-footer",
  "#onetrust-consent-sdk",
  "#onetrust-banner-sdk",
  "#CybotCookiebotDialog",
  ".qc-cmp2-container",
  ".fc-consent-root",
  '[class*="cookie-banner"]',
  '[class*="cookie-consent"]',
  '[id*="cookie-notice"]',
];

export function stripChrome(document) {
  for (const selector of CHROME_SELECTORS) {
    document.querySelectorAll(selector).forEach((el) => el.remove());
  }
}

/** F5/nginx-style promo strips that Readability mistakes for article body. */
export function stripPromoBanners(document) {
  for (const selector of PROMO_BANNER_SELECTORS) {
    document.querySelectorAll(selector).forEach((el) => el.remove());
  }
}

/**
 * Heuristic for CMP *dialogs* that vendor id lists miss (custom React portals,
 * Base UI modals, etc.). Require consent-like copy so ordinary UI dialogs stay.
 */
export const CMP_DIALOG_TEXT_RE =
  /cookie|consent|traceur|personnaliser|accepter|accept all|allow all|alle akzeptieren|privacy policy|politique de confidentialit|gestion des cookies|continuer sans accepter/i;

/**
 * @param {Element} el
 * @returns {boolean}
 */
export function looksLikeConsentDialog(el) {
  const text = el?.textContent ?? "";
  if (text.length < 80 || text.length > 12_000) return false;
  return CMP_DIALOG_TEXT_RE.test(text);
}

/** CMP containers only — safer than full chrome strip. */
export function stripConsentOnly(document) {
  for (const selector of CONSENT_CONTAINER_SELECTORS) {
    document.querySelectorAll(selector).forEach((el) => el.remove());
  }
  for (const el of [...document.querySelectorAll('[role="dialog"], [aria-modal="true"], dialog')]) {
    if (!looksLikeConsentDialog(el)) continue;
    const parent = el.parentElement;
    // Thin absolute/fixed portal wrapper (common Base UI / headless modal pattern).
    if (
      parent &&
      parent !== document.body &&
      parent.children.length <= 2 &&
      /modal|overlay|portal|fixed|absolute/i.test(`${parent.className || ""} ${parent.id || ""}`)
    ) {
      parent.remove();
    } else {
      el.remove();
    }
  }
}