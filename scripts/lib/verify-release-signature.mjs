#!/usr/bin/env node
/**
 * Release authenticity policy for Occam Level-B installers.
 *
 * LOCAL CONTRACT TEST surface (this module + selftest):
 *   - parse signaturePolicy from release manifests
 *   - enforce fail-closed behavior for required Cosign material
 *   - exact OIDC identity / issuer string construction
 *
 * LIVE GITHUB OIDC PROOF remains in:
 *   .github/workflows/occam-release.yml
 *   .github/workflows/sign-release.yml
 *
 * Compatibility:
 *   - manifests without signaturePolicy → treated as legacy sha256-only
 *     (compatible with published Level B / rc.2 installers)
 *   - signaturePolicy=sha256-only → integrity only
 *   - signaturePolicy=required-cosign-v1 → Cosign bundle required + identity match
 *     (for future signed releases; not required by current public bootstrap)
 */
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { pathToFileURL } from "node:url";

export const SIGNATURE_POLICY_SHA256_ONLY = "sha256-only";
export const SIGNATURE_POLICY_REQUIRED_COSIGN_V1 = "required-cosign-v1";
export const SIGNATURE_POLICIES = Object.freeze([
  SIGNATURE_POLICY_SHA256_ONLY,
  SIGNATURE_POLICY_REQUIRED_COSIGN_V1,
]);

export const GITHUB_ACTIONS_OIDC_ISSUER = "https://token.actions.githubusercontent.com";
export const OCCAM_RELEASE_WORKFLOW_PATH =
  "ContextForgeAI/occam/.github/workflows/occam-release.yml";

/**
 * @param {unknown} manifest
 * @returns {"sha256-only"|"required-cosign-v1"}
 */
export function resolveSignaturePolicy(manifest) {
  if (!manifest || typeof manifest !== "object" || Array.isArray(manifest)) {
    throw new Error("release manifest must be a JSON object");
  }
  if (!Object.prototype.hasOwnProperty.call(manifest, "signaturePolicy")) {
    // Legacy / undeclared: do not invent required Cosign for old RC assets.
    return SIGNATURE_POLICY_SHA256_ONLY;
  }
  const policy = String(manifest.signaturePolicy || "");
  if (!SIGNATURE_POLICIES.includes(policy)) {
    throw new Error(
      `unsupported release signaturePolicy: ${policy || "(empty)"} ` +
        `(supported: ${SIGNATURE_POLICIES.join(", ")})`,
    );
  }
  return /** @type {"sha256-only"|"required-cosign-v1"} */ (policy);
}

/**
 * @param {string} version SemVer without leading v
 */
export function expectedReleaseCertificateIdentity(version) {
  const cleaned = String(version || "").trim();
  if (!cleaned) throw new Error("version is required for certificate identity");
  return `https://github.com/${OCCAM_RELEASE_WORKFLOW_PATH}@refs/tags/v${cleaned}`;
}

/**
 * @param {{
 *   archivePath: string,
 *   bundlePath: string,
 *   version: string,
 *   certificateIdentity?: string,
 *   certificateOidcIssuer?: string,
 *   runner?: (args: string[]) => { status: number|null, stdout: string, stderr: string },
 * }} options
 */
export function verifyRequiredCosignBundle(options) {
  const archivePath = path.resolve(options.archivePath);
  const bundlePath = path.resolve(options.bundlePath);
  if (!fs.existsSync(archivePath)) {
    throw new Error(`archive not found: ${archivePath}`);
  }
  if (!fs.existsSync(bundlePath)) {
    throw new Error(`required Cosign bundle missing: ${bundlePath}`);
  }
  let bundleRaw = "";
  try {
    bundleRaw = fs.readFileSync(bundlePath, "utf8");
    JSON.parse(bundleRaw);
  } catch {
    throw new Error(`malformed Cosign bundle JSON: ${bundlePath}`);
  }

  const identity = options.certificateIdentity || expectedReleaseCertificateIdentity(options.version);
  const issuer = options.certificateOidcIssuer || GITHUB_ACTIONS_OIDC_ISSUER;
  const runner =
    options.runner ||
    ((args) => {
      const result = spawnSync("cosign", args, { encoding: "utf8" });
      return {
        status: result.status,
        stdout: String(result.stdout || ""),
        stderr: String(result.stderr || result.error?.message || ""),
      };
    });

  const args = [
    "verify-blob",
    archivePath,
    "--bundle",
    bundlePath,
    "--certificate-identity",
    identity,
    "--certificate-oidc-issuer",
    issuer,
  ];
  const result = runner(args);
  if (result.status === null) {
    throw new Error(`cosign verify-blob failed to start: ${result.stderr || "status=null"}`);
  }
  if (result.status !== 0) {
    throw new Error(
      `cosign verify-blob failed for required signaturePolicy` +
        `\n  identity: ${identity}` +
        `\n  issuer: ${issuer}` +
        `\n  ${result.stderr || result.stdout || `exit ${result.status}`}`,
    );
  }
  return { identity, issuer, ok: true };
}

/**
 * Enforce authenticity after SHA-256 integrity succeeds.
 * @param {{
 *   manifest: Record<string, unknown>,
 *   archivePath: string,
 *   bundlePath?: string,
 *   version: string,
 *   runner?: (args: string[]) => { status: number|null, stdout: string, stderr: string },
 * }} options
 */
export function enforceReleaseAuthenticity(options) {
  const policy = resolveSignaturePolicy(options.manifest);
  if (policy === SIGNATURE_POLICY_SHA256_ONLY) {
    return { policy, verified: false, skipped: true };
  }
  const bundlePath =
    options.bundlePath || `${path.resolve(options.archivePath)}.bundle`;
  verifyRequiredCosignBundle({
    archivePath: options.archivePath,
    bundlePath,
    version: options.version,
    runner: options.runner,
  });
  return { policy, verified: true, skipped: false };
}

function parseArgs(argv) {
  /** @type {Record<string, string>} */
  const options = {};
  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    if (arg === "--manifest") options.manifest = argv[++i];
    else if (arg === "--archive") options.archive = argv[++i];
    else if (arg === "--bundle") options.bundle = argv[++i];
    else if (arg === "--version") options.version = argv[++i];
    else if (arg === "-h" || arg === "--help") {
      console.log(
        "usage: node verify-release-signature.mjs --manifest PATH --archive PATH --version VER [--bundle PATH]",
      );
      process.exit(0);
    } else {
      throw new Error(`unknown argument: ${arg}`);
    }
  }
  if (!options.manifest || !options.archive || !options.version) {
    throw new Error("--manifest, --archive, and --version are required");
  }
  return options;
}

function main() {
  try {
    const options = parseArgs(process.argv.slice(2));
    const manifest = JSON.parse(fs.readFileSync(options.manifest, "utf8"));
    const result = enforceReleaseAuthenticity({
      manifest,
      archivePath: options.archive,
      bundlePath: options.bundle,
      version: options.version,
    });
    console.log(`verify-release-signature: policy=${result.policy}`);
    console.log(
      result.skipped
        ? "verify-release-signature: skipped (sha256-only)"
        : "verify-release-signature: cosign OK",
    );
  } catch (error) {
    console.error(`error: ${error instanceof Error ? error.message : String(error)}`);
    process.exit(1);
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
