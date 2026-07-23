import { STEP_DEFS } from "../onboard-steps.mjs";
import { SUPPORTED_ONBOARD_SCHEMA, UI_CONTRACT_VERSION } from "../onboard-schema.mjs";

/** @param {ReturnType<import("../help-catalog.mjs").buildHelpViewModel>} vm */
export function renderHelpJson(vm) {
  return JSON.stringify(vm, null, 2);
}

/** @param {ReturnType<import("../help-catalog.mjs").buildCommandDetail>} detail */
export function renderCommandDetailJson(detail) {
  return JSON.stringify(detail ?? { error: "not_found" }, null, 2);
}

/** @type {Record<string, string>} */
const STEP_TYPES = {
  occamHome: "path",
  hostTarget: "select",
  browser: "select",
  proxy: "select",
  profile: "select",
};

/** @param {ReturnType<import("../onboard-flow.mjs").buildOnboardResult>} result */
export function renderOnboardJson(result, mcpConfig) {
  const steps = STEP_DEFS.map((step) => ({
    id: step.id,
    type: STEP_TYPES[step.id] ?? "text",
    label: step.label,
  }));

  return JSON.stringify(
    {
      ui_contract_version: UI_CONTRACT_VERSION,
      schema_version: result.schema_version ?? SUPPORTED_ONBOARD_SCHEMA,
      steps,
      result: {
        profile: result.profile,
        hostTarget: result.hostTarget,
        occamHome: result.occamHome,
        configPath: result.configPath,
        env: result.env,
        verify: result.verify,
        mcpConfig,
      },
    },
    null,
    2,
  );
}
