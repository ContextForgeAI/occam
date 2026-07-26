#!/usr/bin/env node
/**
 * Install-time connect orchestration — thin wrapper over shared onboarding.
 * Kept for import stability from post-install-ux / selftests.
 */
export {
  allowConnectAll,
  listConnectCandidates as listInstallConnectCandidates,
  listDetectedHosts,
  renderMultiHostConfirmPrompt,
  runConnectOnboarding as runInstallConnectFlow,
} from "./connect-onboarding.mjs";
