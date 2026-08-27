#!/usr/bin/env node
/**
 * Dual-contract bootstrap selection — legacy Level B vs self-contained-v1.
 */
import assert from "node:assert/strict";
import { pathToFileURL } from "node:url";
import {
  INSTALL_CONTRACT_LEGACY,
  INSTALL_CONTRACT_SELF_CONTAINED_V1,
  PUBLIC_DEFAULT_RELEASE_VERSION,
  allowsExecutableOverlay,
  resolveBootstrapSignaturePolicy,
  resolveInstallContract,
  SIGNATURE_POLICY_REQUIRED_COSIGN_V1,
  SIGNATURE_POLICY_SHA256_ONLY,
} from "./bootstrap-release-contract.mjs";

function main() {
  assert.equal(PUBLIC_DEFAULT_RELEASE_VERSION, "1.0.0-rc.3");

  // LEGACY MANIFEST: runtimeLayout absent → legacy + sha256-only
  assert.equal(resolveInstallContract({ version: "1.0.0-rc.2", rid: "win-x64" }), INSTALL_CONTRACT_LEGACY);
  assert.equal(resolveBootstrapSignaturePolicy({ version: "1.0.0-rc.2" }), SIGNATURE_POLICY_SHA256_ONLY);
  assert.equal(allowsExecutableOverlay(INSTALL_CONTRACT_LEGACY), true);

  // LEGACY EXPLICIT sha256-only
  assert.equal(
    resolveBootstrapSignaturePolicy({ signaturePolicy: SIGNATURE_POLICY_SHA256_ONLY }),
    SIGNATURE_POLICY_SHA256_ONLY,
  );

  // RC3 self-contained + required Cosign
  assert.equal(
    resolveInstallContract({
      runtimeLayout: "self-contained-v1",
      signaturePolicy: SIGNATURE_POLICY_REQUIRED_COSIGN_V1,
    }),
    INSTALL_CONTRACT_SELF_CONTAINED_V1,
  );
  assert.equal(
    resolveBootstrapSignaturePolicy({
      signaturePolicy: SIGNATURE_POLICY_REQUIRED_COSIGN_V1,
    }),
    SIGNATURE_POLICY_REQUIRED_COSIGN_V1,
  );
  assert.equal(allowsExecutableOverlay(INSTALL_CONTRACT_SELF_CONTAINED_V1), false);

  // UNKNOWN runtimeLayout → FAIL CLOSED
  assert.throws(() => resolveInstallContract({ runtimeLayout: "experimental-v9" }), /unsupported release runtimeLayout/);
  assert.throws(() => resolveInstallContract({ runtimeLayout: "" }), /unsupported release runtimeLayout/);

  // UNKNOWN signaturePolicy → FAIL CLOSED
  assert.throws(
    () => resolveBootstrapSignaturePolicy({ signaturePolicy: "maybe" }),
    /unsupported release signaturePolicy/,
  );

  console.log("bootstrap-release-contract.selftest: OK");
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
