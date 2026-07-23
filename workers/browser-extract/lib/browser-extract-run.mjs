import { createBrowserSession, renderAndExtract, parseExtractVariant } from "./browser-session.mjs";
import { getRecipe, hasRecipe } from "./recipes/registry.mjs";
import { readBrowserPlanFile } from "./interaction-steps.mjs";
import { runPlugins } from "../../shared/lib/plugins-runner.mjs";
import { wasOverlayApplied } from "../../shared/lib/playbook-seed.mjs";

/**
 * @param {string} url
 * @param {{
 *   leanAssets?: boolean,
 *   consentAggressive?: boolean,
 *   headersFile?: string | null,
 *   storageStateFile?: string | null,
 *   browserPlanFile?: string | null,
 *   extractVariant?: string,
 *   cookieInject?: boolean,
 * }} [options]
 */
export async function runBrowserExtract(url, options = {}) {
  if (options.cookieInject) {
    process.env.WT_COOKIE_INJECT = "1";
  }

  const leanAssets = options.leanAssets !== false;
  const blockStylesheets = leanAssets || hasRecipe(url);
  const browserPlan = readBrowserPlanFile(options.browserPlanFile ?? null);
  const recipe = await getRecipe(url);
  const extractVariant = parseExtractVariant(
    options.extractVariant ?? recipe?.extractVariant ?? process.env.WT_BROWSER_EXTRACT_VARIANT,
  );

  const session = await createBrowserSession({
    blockStylesheets,
    headersFile: options.headersFile ?? undefined,
    storageStateFile: options.storageStateFile ?? undefined,
  });

  try {
    const result = await renderAndExtract(session.context, url, {
      consentAggressive: options.consentAggressive === true,
      extractVariant,
      browserPlan,
      sessionHeaders: session.sessionHeaders,
      features: options.features,
    });
    if (session.browserProvisioned && result && typeof result === "object") {
      result.browser_provisioned = session.browserProvisioned;
    }
    // A3: honest provenance on the one-shot path — argv overlay matched this host (no ALS store here).
    if (result && typeof result === "object") {
      result.overlay_applied = wasOverlayApplied(url);
    }
    return await runPlugins(result, options.features);
  } finally {
    await session.close();
  }
}
