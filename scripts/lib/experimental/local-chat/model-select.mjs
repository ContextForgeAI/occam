/**
 * Model selection UX for experimental local chat.
 */
import { pickDefaultToolModel } from "./ollama-api.mjs";

/**
 * @param {import('./ollama-api.mjs').OllamaModelInfo[]} models
 * @param {{ modelFlag?: string|null, stdin?: NodeJS.ReadableStream|null, stdout?: NodeJS.WritableStream|null, interactive?: boolean }} [opts]
 * @returns {Promise<{ ok: true, model: string, auto: boolean } | { ok: false, code: string, message: string, installed?: object[] }>}
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
        installed: models.map((m) => ({ name: m.name, tools: m.tools })),
      };
    }
    if (!exact.tools) {
      return {
        ok: false,
        code: "model_no_tools",
        message: `Model ${flag} does not report tool support.`,
        installed: models.map((m) => ({ name: m.name, tools: m.tools })),
      };
    }
    return { ok: true, model: exact.name, auto: false };
  }

  if (toolModels.length === 0) {
    return {
      ok: false,
      code: "no_tool_models",
      message:
        "Ollama is running, but none of the installed models report tool support.\n\n" +
        formatNoToolsList(models) +
        "\n\nOccam web access requires a tool-capable model.",
      installed: models.map((m) => ({ name: m.name, tools: m.tools })),
    };
  }

  if (toolModels.length === 1) {
    return { ok: true, model: toolModels[0].name, auto: true };
  }

  const interactive = opts.interactive !== false && Boolean(opts.stdin && opts.stdout);
  const def = pickDefaultToolModel(toolModels);
  const defaultIndex = Math.max(
    0,
    toolModels.findIndex((m) => m.name === def?.name),
  );

  if (!interactive) {
    return { ok: true, model: toolModels[defaultIndex].name, auto: true };
  }

  const out = opts.stdout;
  out.write("\nTool-capable Ollama models:\n\n");
  toolModels.forEach((m, i) => {
    out.write(`${i + 1}. ${m.name}\n`);
  });
  out.write(`\nChoose model [${defaultIndex + 1}]: `);

  const answer = await readLine(opts.stdin);
  const trimmed = answer.trim();
  if (!trimmed) {
    return { ok: true, model: toolModels[defaultIndex].name, auto: false };
  }
  const asNum = Number.parseInt(trimmed, 10);
  if (Number.isFinite(asNum) && asNum >= 1 && asNum <= toolModels.length) {
    return { ok: true, model: toolModels[asNum - 1].name, auto: false };
  }
  const byName = toolModels.find((m) => m.name === trimmed || m.name.startsWith(trimmed));
  if (byName) return { ok: true, model: byName.name, auto: false };

  out.write(`Unrecognized choice; using ${toolModels[defaultIndex].name}\n`);
  return { ok: true, model: toolModels[defaultIndex].name, auto: false };
}

/**
 * @param {import('./ollama-api.mjs').OllamaModelInfo[]} models
 */
export function formatNoToolsList(models) {
  if (!models.length) return "Installed models:\n  (none)";
  const lines = models.map((m) => `  ${m.name} — ${m.tools ? "tools" : "no tools"}`);
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
