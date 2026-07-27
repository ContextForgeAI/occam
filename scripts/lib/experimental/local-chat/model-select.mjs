/**
 * Model selection UX + live compatibility policy for experimental local chat.
 *
 * `capabilities.includes("tools")` is necessary but not sufficient for
 * friend-grade reliability. Tiers below are based on live Mac evidence with
 * slim Ollama tool schemas (2026-07-27).
 */
import { pickDefaultToolModel } from "./ollama-api.mjs";

/** @typedef {'supported'|'degraded'|'unknown'} ChatModelTier */

/**
 * Known live tiers. Prefer supported models for auto-select.
 * Degraded models may still be chosen explicitly, with a warning.
 */
export const CHAT_MODEL_COMPAT = Object.freeze({
  "qwen2.5:7b": "supported",
  "qwen2.5": "supported",
  "llama3.1:8b": "supported",
  "llama3.1": "supported",
  "llama3.2:3b": "degraded",
  "llama3.2": "degraded",
});

/**
 * @param {string} name
 * @returns {ChatModelTier}
 */
export function chatModelTier(name) {
  const n = String(name || "").trim().toLowerCase();
  if (!n) return "unknown";
  if (CHAT_MODEL_COMPAT[n]) return CHAT_MODEL_COMPAT[n];
  for (const [key, tier] of Object.entries(CHAT_MODEL_COMPAT)) {
    if (n === key || n.startsWith(`${key}:`) || n.startsWith(`${key}-`)) return /** @type {ChatModelTier} */ (tier);
  }
  return "unknown";
}

/**
 * @param {ChatModelTier} tier
 */
export function formatTierLabel(tier) {
  if (tier === "supported") return "supported";
  if (tier === "degraded") return "degraded — may leak tool JSON / over-call tools";
  return "unverified";
}

/**
 * @param {import('./ollama-api.mjs').OllamaModelInfo[]} models
 * @param {{ modelFlag?: string|null, stdin?: NodeJS.ReadableStream|null, stdout?: NodeJS.WritableStream|null, interactive?: boolean }} [opts]
 * @returns {Promise<{ ok: true, model: string, auto: boolean, tier: ChatModelTier, warning?: string } | { ok: false, code: string, message: string, installed?: object[] }>}
 */
export async function selectToolModel(models, opts = {}) {
  const toolModels = models.filter((m) => m.tools);
  const flag = opts.modelFlag?.trim() || null;

  if (flag) {
    const exact = models.find((m) => m.name === flag);
    if (!exact) {
      return {
        ok: false,
        code: "model_not_found",
        message: `Model not installed: ${flag}`,
        installed: models.map((m) => ({ name: m.name, tools: m.tools, tier: chatModelTier(m.name) })),
      };
    }
    if (!exact.tools) {
      return {
        ok: false,
        code: "model_no_tools",
        message: `Model ${flag} does not report tool support.`,
        installed: models.map((m) => ({ name: m.name, tools: m.tools, tier: chatModelTier(m.name) })),
      };
    }
    const tier = chatModelTier(exact.name);
    /** @type {{ ok: true, model: string, auto: boolean, tier: ChatModelTier, warning?: string }} */
    const out = { ok: true, model: exact.name, auto: false, tier };
    if (tier === "degraded") {
      out.warning =
        `${exact.name} is tool-capable but degraded for Occam chat (may emit tool JSON as text or call tools unnecessarily). Prefer qwen2.5:7b when installed.`;
    }
    return out;
  }

  if (toolModels.length === 0) {
    return {
      ok: false,
      code: "no_tool_models",
      message:
        "Ollama is running, but none of the installed models report tool support.\n\n" +
        formatNoToolsList(models) +
        "\n\nOccam web access requires a tool-capable model.",
      installed: models.map((m) => ({ name: m.name, tools: m.tools, tier: chatModelTier(m.name) })),
    };
  }

  if (toolModels.length === 1) {
    const only = toolModels[0];
    const tier = chatModelTier(only.name);
    /** @type {{ ok: true, model: string, auto: boolean, tier: ChatModelTier, warning?: string }} */
    const out = { ok: true, model: only.name, auto: true, tier };
    if (tier === "degraded") {
      out.warning = `${only.name} is the only tool-capable model, but it is degraded for Occam chat.`;
    }
    return out;
  }

  const interactive = opts.interactive !== false && Boolean(opts.stdin && opts.stdout);
  const def = pickDefaultToolModel(toolModels);
  const defaultIndex = Math.max(
    0,
    toolModels.findIndex((m) => m.name === def?.name),
  );

  if (!interactive) {
    const model = toolModels[defaultIndex].name;
    const tier = chatModelTier(model);
    return { ok: true, model, auto: true, tier };
  }

  const out = opts.stdout;
  out.write("\nTool-capable Ollama models:\n\n");
  toolModels.forEach((m, i) => {
    const tier = chatModelTier(m.name);
    const mark = i === defaultIndex ? " (default)" : "";
    const note = tier === "supported" ? "" : tier === "degraded" ? "  [degraded]" : "  [unverified]";
    out.write(`${i + 1}. ${m.name}${mark}${note}\n`);
  });
  out.write(`\nChoose model [${defaultIndex + 1}]: `);

  const answer = await readLine(opts.stdin);
  const trimmed = answer.trim();
  let chosen = toolModels[defaultIndex];
  if (trimmed) {
    const asNum = Number.parseInt(trimmed, 10);
    if (Number.isFinite(asNum) && asNum >= 1 && asNum <= toolModels.length) {
      chosen = toolModels[asNum - 1];
    } else {
      const byName = toolModels.find((m) => m.name === trimmed || m.name.startsWith(trimmed));
      if (byName) chosen = byName;
      else out.write(`Unrecognized choice; using ${toolModels[defaultIndex].name}\n`);
    }
  }

  const tier = chatModelTier(chosen.name);
  /** @type {{ ok: true, model: string, auto: boolean, tier: ChatModelTier, warning?: string }} */
  const result = { ok: true, model: chosen.name, auto: false, tier };
  if (tier === "degraded") {
    result.warning =
      `${chosen.name} is degraded for Occam chat (may leak tool JSON / over-call). Prefer qwen2.5:7b when installed.`;
  }
  return result;
}

/**
 * @param {import('./ollama-api.mjs').OllamaModelInfo[]} models
 */
export function formatNoToolsList(models) {
  if (!models.length) return "Installed models:\n  (none)";
  const lines = models.map((m) => {
    if (!m.tools) return `  ${m.name} — no tools`;
    const tier = chatModelTier(m.name);
    return `  ${m.name} — tools (${formatTierLabel(tier)})`;
  });
  return `Installed models:\n${lines.join("\n")}`;
}

/**
 * @param {NodeJS.ReadableStream} stdin
 * @returns {Promise<string>}
 */
function readLine(stdin) {
  return new Promise((resolve) => {
    let buf = "";
    const onData = (chunk) => {
      buf += chunk.toString();
      if (buf.includes("\n")) {
        cleanup();
        resolve(buf.split(/\r?\n/)[0] ?? "");
      }
    };
    const onEnd = () => {
      cleanup();
      resolve(buf);
    };
    const cleanup = () => {
      stdin.off("data", onData);
      stdin.off("end", onEnd);
      if (stdin.isTTY && typeof stdin.setRawMode === "function") {
        try {
          stdin.setRawMode(false);
        } catch {
          /* ignore */
        }
      }
      stdin.pause();
    };
    stdin.resume();
    stdin.on("data", onData);
    stdin.on("end", onEnd);
  });
}
