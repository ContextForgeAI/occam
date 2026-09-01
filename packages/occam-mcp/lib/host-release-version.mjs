/**
 * GitHub Release version used to download the AOT host + Level B tarball for npx.
 * npm package semver may move ahead for wrapper-only fixes while the published
 * host release stays on the last GitHub cut — bump this when vX.Y.Z ships on GitHub.
 */
export const HOST_RELEASE_VERSION = "1.0.0";

/**
 * @param {string | undefined} envValue OCCAM_HOST_RELEASE_VERSION
 * @returns {string}
 */
export function resolveHostReleaseVersion(envValue = process.env.OCCAM_HOST_RELEASE_VERSION) {
  const override = envValue?.trim();
  return override || HOST_RELEASE_VERSION;
}
