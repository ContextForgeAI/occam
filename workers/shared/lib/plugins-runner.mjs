import { pathToFileURL, fileURLToPath } from "node:url";
import { resolve, dirname } from "node:path";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

/**
 * Runs active plugins on the extraction result.
 * Plugins are resolved based on the OCCAM_FEATURES environment variable.
 * 
 * @param {Object} result Extraction result object
 * @returns {Promise<Object>} Processed result object
 */
export async function runPlugins(result, features) {
  const featuresStr = features ?? process.env.OCCAM_FEATURES ?? "";
  const activeFeatures = featuresStr
    .split(",")
    .map((f) => f.trim().toLowerCase())
    .filter(Boolean);

  if (activeFeatures.length === 0) {
    return result;
  }

  // Resolve plugins folder relative to this file: ../plugins/
  // Path: c:\PROJECTS\FFOccamMCP\workers\shared\plugins\
  const sharedDir = resolve(__dirname, "..");

  for (const feature of activeFeatures) {
    if (feature === "semantic_chunking") {
      try {
        const pluginPath = resolve(sharedDir, "plugins", "chunking.mjs");
        // Windows needs file:// URLs for dynamic imports
        const fileUrl = pathToFileURL(pluginPath).href;
        const { default: plugin } = await import(fileUrl);
        if (plugin && typeof plugin.run === "function") {
          result = plugin.run(result);
        }
      } catch (err) {
        console.error(`Failed to run plugin for feature '${feature}':`, err);
      }
    }
  }

  return result;
}
