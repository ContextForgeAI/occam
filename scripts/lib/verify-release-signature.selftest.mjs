#!/usr/bin/env node
/**
 * LOCAL CONTRACT TESTs for release authenticity policy.
 * These do not exercise live GitHub OIDC; workflow DAG remains the live proof.
 */
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { pathToFileURL } from "node:url";
import {
  GITHUB_ACTIONS_OIDC_ISSUER,
  SIGNATURE_POLICY_REQUIRED_COSIGN_V1,
  SIGNATURE_POLICY_SHA256_ONLY,
  enforceReleaseAuthenticity,
  expectedReleaseCertificateIdentity,
  resolveSignaturePolicy,
  verifyRequiredCosignBundle,
} from "./verify-release-signature.mjs";

function tempRoot() {
  return fs.mkdtempSync(path.join(os.tmpdir(), "occam-sig-policy-"));
}

function writePair(root) {
  const archivePath = path.join(root, "ff-occam-1.0.0-rc.3-win-x64.tar.gz");
  const bundlePath = `${archivePath}.bundle`;
  fs.writeFileSync(archivePath, "archive-bytes");
  fs.writeFileSync(bundlePath, `${JSON.stringify({ fake: true })}\n`);
  return { archivePath, bundlePath };
}

function testPolicyResolution() {
  assert.equal(resolveSignaturePolicy({ version: "1.0.0-rc.2" }), SIGNATURE_POLICY_SHA256_ONLY);
  assert.equal(
    resolveSignaturePolicy({ signaturePolicy: SIGNATURE_POLICY_SHA256_ONLY }),
    SIGNATURE_POLICY_SHA256_ONLY,
  );
  assert.equal(
    resolveSignaturePolicy({ signaturePolicy: SIGNATURE_POLICY_REQUIRED_COSIGN_V1 }),
    SIGNATURE_POLICY_REQUIRED_COSIGN_V1,
  );
  assert.throws(
    () => resolveSignaturePolicy({ signaturePolicy: "maybe" }),
    /unsupported release signaturePolicy/,
  );
}

function testIdentityStrings() {
  assert.equal(
    expectedReleaseCertificateIdentity("1.0.0-rc.3"),
    "https://github.com/ContextForgeAI/occam/.github/workflows/occam-release.yml@refs/tags/v1.0.0-rc.3",
  );
  assert.equal(GITHUB_ACTIONS_OIDC_ISSUER, "https://token.actions.githubusercontent.com");
}

function testPositiveAndNegatives() {
  const root = tempRoot();
  try {
    const { archivePath, bundlePath } = writePair(root);
    const expectedIdentity = expectedReleaseCertificateIdentity("1.0.0-rc.3");

    // VALID archive + expected identity → PASS
    verifyRequiredCosignBundle({
      archivePath,
      bundlePath,
      version: "1.0.0-rc.3",
      runner: (args) => {
        assert.ok(args.includes("--certificate-identity"));
        assert.equal(args[args.indexOf("--certificate-identity") + 1], expectedIdentity);
        assert.equal(args[args.indexOf("--certificate-oidc-issuer") + 1], GITHUB_ACTIONS_OIDC_ISSUER);
        return { status: 0, stdout: "ok", stderr: "" };
      },
    });

    // TAMPERED / verifier reject → FAIL
    assert.throws(
      () =>
        verifyRequiredCosignBundle({
          archivePath,
          bundlePath,
          version: "1.0.0-rc.3",
          runner: () => ({ status: 1, stdout: "", stderr: "tampered" }),
        }),
      /cosign verify-blob failed/,
    );

    // VALID archive + wrong signer identity → FAIL (contract: caller supplies wrong identity)
    assert.throws(
      () =>
        verifyRequiredCosignBundle({
          archivePath,
          bundlePath,
          version: "1.0.0-rc.3",
          certificateIdentity:
            "https://github.com/evil/repo/.github/workflows/occam-release.yml@refs/tags/v1.0.0-rc.3",
          runner: (args) => {
            const identity = args[args.indexOf("--certificate-identity") + 1];
            assert.match(identity, /evil/);
            return { status: 1, stdout: "", stderr: "identity mismatch" };
          },
        }),
      /cosign verify-blob failed/,
    );

    // VALID archive + wrong issuer → FAIL
    assert.throws(
      () =>
        verifyRequiredCosignBundle({
          archivePath,
          bundlePath,
          version: "1.0.0-rc.3",
          certificateOidcIssuer: "https://evil.example/oidc",
          runner: (args) => {
            assert.equal(args[args.indexOf("--certificate-oidc-issuer") + 1], "https://evil.example/oidc");
            return { status: 1, stdout: "", stderr: "issuer mismatch" };
          },
        }),
      /cosign verify-blob failed/,
    );

    // MISSING bundle when required → FAIL
    fs.rmSync(bundlePath);
    assert.throws(
      () =>
        enforceReleaseAuthenticity({
          manifest: { signaturePolicy: SIGNATURE_POLICY_REQUIRED_COSIGN_V1 },
          archivePath,
          version: "1.0.0-rc.3",
          runner: () => ({ status: 0, stdout: "", stderr: "" }),
        }),
      /required Cosign bundle missing/,
    );

    // MALFORMED bundle → FAIL
    fs.writeFileSync(bundlePath, "not-json");
    assert.throws(
      () =>
        verifyRequiredCosignBundle({
          archivePath,
          bundlePath,
          version: "1.0.0-rc.3",
          runner: () => ({ status: 0, stdout: "", stderr: "" }),
        }),
      /malformed Cosign bundle/,
    );

    // sha256-only does not require bundle
    assert.deepEqual(
      enforceReleaseAuthenticity({
        manifest: { signaturePolicy: SIGNATURE_POLICY_SHA256_ONLY },
        archivePath,
        version: "1.0.0-rc.3",
      }),
      { policy: SIGNATURE_POLICY_SHA256_ONLY, verified: false, skipped: true },
    );

    // status=null must not count as PASS
    fs.writeFileSync(bundlePath, `${JSON.stringify({ fake: true })}\n`);
    assert.throws(
      () =>
        verifyRequiredCosignBundle({
          archivePath,
          bundlePath,
          version: "1.0.0-rc.3",
          runner: () => ({ status: null, stdout: "", stderr: "spawn failed" }),
        }),
      /failed to start|status=null/,
    );
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
}

function main() {
  testPolicyResolution();
  testIdentityStrings();
  testPositiveAndNegatives();
  console.log("verify-release-signature.selftest: OK (local contract)");
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
