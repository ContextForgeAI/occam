#!/usr/bin/env node
/**
 * Bootstrap release install contract — select legacy Level B vs self-contained-v1
 * from the download manifest (not from version strings).
 */
import { pathToFileURL } from "node:url";
import {
  resolveSignaturePolicy,
  SIGNATURE_POLICY_REQUIRED_COSIGN_V1,
  SIGNATURE_POLICY_SHA256_ONLY,
} from "./verify-release-signature.mjs";

export const RUNTIME_LAYOUT_SELF_CONTAINED_V1 = "self-contained-v1";
export const INSTALL_CONTRACT_LEGACY = "legacy";
export const INSTALL_CONTRACT_SELF_CONTAINED_V1 = "self-contained-v1";

/** Canonical public install default while rc.3 is unpublished. */
export const PUBLIC_DEFAULT_RELEASE_VERSION = "1.0.0-rc.2";

/**
 * @param {unknown} manifest
 * @returns {"legacy"|"self-contained-v1"}
 */
export function resolveInstallContract(manifest) {
  if (!manifest || typeof manifest !== "object" || Array.isArray(manifest)) {
    throw new Error("release manifest must be a JSON object");
  }
  if (!Object.prototype.hasOwnProperty.call(manifest, "runtimeLayout")) {
    return INSTALL_CONTRACT_LEGACY;
  }
  const layout = String(/** @type {{ runtimeLayout?: unknown }} */ (manifest).runtimeLayout || "");
  if (layout === RUNTIME_LAYOUT_SELF_CONTAINED_V1) {
    return INSTALL_CONTRACT_SELF_CONTAINED_V1;
  }
  if (!layout) {
    throw new Error("unsupported release runtimeLayout: (empty)");
  }
  throw new Error(`unsupported release runtimeLayout: ${layout}`);
}

/**
 * Whether executable helper overlays from mutable main are allowed.
 * Self-contained contracts must never fetch executable overlays.
 * @param {"legacy"|"self-contained-v1"} contract
 */
export function allowsExecutableOverlay(contract) {
  return contract === INSTALL_CONTRACT_LEGACY;
}

/**
 * @param {unknown} manifest
 */
export function resolveBootstrapSignaturePolicy(manifest) {
  return resolveSignaturePolicy(manifest);
}

export {
  SIGNATURE_POLICY_REQUIRED_COSIGN_V1,
  SIGNATURE_POLICY_SHA256_ONLY,
};

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  console.log(`public_default=${PUBLIC_DEFAULT_RELEASE_VERSION}`);
}
