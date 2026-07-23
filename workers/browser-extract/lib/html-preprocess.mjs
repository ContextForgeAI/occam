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

/** CMP containers only — safer than full chrome strip. */
export function stripConsentOnly(document) {
  for (const selector of CONSENT_CONTAINER_SELECTORS) {
    document.querySelectorAll(selector).forEach((el) => el.remove());
  }
}