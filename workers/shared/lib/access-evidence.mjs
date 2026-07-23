const LOGIN_ROUTE_RE = /\/(?:login|log-in|signin|sign-in|auth|session)(?:\/|$)/i;
const AUTH_TERMS_RE = /\b(?:authentication|authorization|openid|oauth|password|sign in|log in|login)\b/i;

/**
 * Collects bounded boolean/count-free access signals. No form values, text, headers, or selectors are
 * returned, so worker diagnostics cannot leak credentials or page content.
 * @param {Document} document
 * @param {{ requestedUrl?: string, finalUrl?: string }} [urls]
 */
export function collectAccessEvidence(document, urls = {}) {
  const passwordField = Boolean(document.querySelector('input[type="password"]'));
  const identityField = Boolean(document.querySelector(
    'input[type="email"], input[autocomplete="username"], input[name="username" i], input[name="email" i]'));

  let loginFormAction = false;
  for (const form of document.querySelectorAll("form")) {
    if (!form.querySelector('input[type="password"]')) continue;
    const action = form.getAttribute("action") ?? "";
    const buttonText = Array.from(form.querySelectorAll('button, input[type="submit"]'))
      .map((node) => node.textContent || node.getAttribute("value") || "")
      .join(" ");
    if (LOGIN_ROUTE_RE.test(action) || /\b(?:log in|sign in|login)\b/i.test(buttonText)) {
      loginFormAction = true;
      break;
    }
  }

  const loginHeading = Array.from(document.querySelectorAll("h1, h2"))
    .some((node) => /^(?:sign in|log in|login)$/i.test((node.textContent ?? "").trim()));
  const blockingOverlay = passwordField && Boolean(document.querySelector(
    '[aria-modal="true"], [role="dialog"], .login-modal, [class*="login-modal"]'));
  const mainTextLength = (document.querySelector("article, main, [role='main']")?.textContent
    ?? document.body?.textContent
    ?? "").trim().length;
  const terminologySample = (document.body?.innerHTML ?? "").slice(0, 65_536)
    .replace(/<[^>]*>/g, " ");
  const requestedUrl = urls.requestedUrl ?? "";
  const finalUrl = urls.finalUrl ?? requestedUrl;

  return {
    has_authentication_challenge: false,
    redirected_to_login: requestedUrl.length > 0
      && finalUrl.length > 0
      && requestedUrl.toLowerCase() !== finalUrl.toLowerCase()
      && isLoginRoute(finalUrl),
    password_field: passwordField,
    identity_field: identityField,
    login_form_action: loginFormAction,
    login_heading: loginHeading,
    blocking_overlay: blockingOverlay,
    has_usable_content: mainTextLength >= 600,
    authentication_terminology: AUTH_TERMS_RE.test(terminologySample),
  };
}

export function isLoginRoute(url) {
  try {
    return LOGIN_ROUTE_RE.test(new URL(url).pathname);
  } catch {
    return false;
  }
}
