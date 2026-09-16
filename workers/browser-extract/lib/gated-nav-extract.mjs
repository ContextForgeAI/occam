/**
 * P0: HTTP 401/403 on browser navigation is not a content verdict.
 * Continue to DOM extract; only fail if the document is a challenge/thin shell.
 */

/** Visible-text floor aligned with AccessEvidenceAdapters.UsableVisibleTextCharacters. */
export const GATED_CONTENT_FLOOR = 600;

/**
 * @param {number} status
 * @returns {boolean}
 */
export function shouldContinueAfterNavStatus(status) {
  const n = Number(status) || 0;
  if (n < 400) return true;
  return n === 401 || n === 403;
}

/**
 * @param {number} status
 * @returns {boolean}
 */
export function isGatedNavStatus(status) {
  const n = Number(status) || 0;
  return n === 401 || n === 403;
}

/**
 * @param {{
 *   navStatus?: number,
 *   markdown?: string,
 *   isChallengeWall?: boolean,
 *   looksThin?: boolean,
 *   looksErrorShell?: boolean,
 * }} input
 * @returns {{ kind: "normal" } | { kind: "ok", statusCode: number, accessStatus: string } | { kind: "fail", failure: string, statusCode: number }}
 */
export function classifyGatedNavResult(input) {
  const navStatus = Number(input?.navStatus) || 0;
  if (!isGatedNavStatus(navStatus)) {
    return { kind: "normal" };
  }

  if (input?.isChallengeWall) {
    return { kind: "fail", failure: "captcha_or_challenge", statusCode: navStatus };
  }

  const markdown = String(input?.markdown ?? "");
  const tooShort = markdown.trim().length < GATED_CONTENT_FLOOR;
  if (input?.looksErrorShell || input?.looksThin || tooShort) {
    return { kind: "fail", failure: `http_${navStatus}`, statusCode: navStatus };
  }

  return {
    kind: "ok",
    statusCode: navStatus,
    accessStatus: "blocked-but-content-available",
  };
}
