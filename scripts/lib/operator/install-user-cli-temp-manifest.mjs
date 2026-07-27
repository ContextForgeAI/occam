/**
 * Canonical relative paths for bootstrap temp staging of install-user-cli.
 *
 * get-ff-occam.sh / get-ff-occam.ps1 download these into a temp directory that
 * preserves repo-relative layout so ESM imports resolve:
 *   scripts/lib/operator/install-user-cli.mjs
 *   → ../resolve-node-runtime.mjs
 *
 * When adding a local import to install-user-cli.mjs (or its transitive locals),
 * extend this list and update both bootstrap scripts (or the selftest will fail).
 */
export const INSTALL_USER_CLI_TEMP_RELPATHS = Object.freeze([
  "scripts/lib/operator/install-user-cli.mjs",
  "scripts/lib/resolve-node-runtime.mjs",
]);

/** Entry executed after staging. */
export const INSTALL_USER_CLI_TEMP_ENTRY = "scripts/lib/operator/install-user-cli.mjs";
