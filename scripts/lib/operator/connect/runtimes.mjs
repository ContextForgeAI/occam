/**
 * Model runtime detectors (Tier D) — classify only; never register Occam into these.
 */
import { existsSync } from "node:fs";
import { homedir, platform } from "node:os";
import { join } from "node:path";
import { which } from "./process.mjs";

/**
 * @typedef {{ id: string, name: string, kind: 'MODEL_RUNTIME', detected: boolean, confidence: 'high'|'medium'|'low', signals: string[] }} RuntimeDetection
 */

/** @returns {RuntimeDetection} */
export function detectOllama() {
  const signals = [];
  const bin = which("ollama");
  if (bin) signals.push(`executable:${bin}`);
  const home = join(homedir(), ".ollama");
  if (existsSync(home)) signals.push(`dir:${home}`);
  return {
    id: "ollama",
    name: "Ollama",
    kind: "MODEL_RUNTIME",
    detected: signals.length > 0,
    confidence: bin ? "high" : signals.length ? "medium" : "low",
    signals,
  };
}

/** @returns {RuntimeDetection} */
export function detectLlamaCpp() {
  const signals = [];
  for (const name of ["llama-server", "llama-cli", "llama.cpp"]) {
    const bin = which(name);
    if (bin) signals.push(`executable:${bin}`);
  }
  return {
    id: "llamacpp",
    name: "llama.cpp",
    kind: "MODEL_RUNTIME",
    detected: signals.length > 0,
    confidence: signals.length ? "medium" : "low",
    signals,
  };
}

/** @returns {RuntimeDetection} */
export function detectLmStudio() {
  const signals = [];
  const home = join(homedir(), ".lmstudio");
  if (existsSync(home)) signals.push(`dir:${home}`);
  if (platform() === "win32") {
    const local = join(
      process.env.LOCALAPPDATA || join(homedir(), "AppData", "Local"),
      "LM-Studio",
    );
    if (existsSync(local)) signals.push(`dir:${local}`);
  } else if (platform() === "darwin") {
    const app = "/Applications/LM Studio.app";
    if (existsSync(app)) signals.push(`app:${app}`);
  }
  return {
    id: "lmstudio",
    name: "LM Studio",
    kind: "MODEL_RUNTIME",
    detected: signals.length > 0,
    confidence: signals.length ? "medium" : "low",
    signals,
  };
}

/** @returns {RuntimeDetection} */
export function detectMlx() {
  const signals = [];
  if (platform() !== "darwin") {
    return {
      id: "mlx",
      name: "MLX / MLX-LM",
      kind: "MODEL_RUNTIME",
      detected: false,
      confidence: "low",
      signals,
    };
  }
  const bin = which("mlx_lm") || which("mlx-lm");
  if (bin) signals.push(`executable:${bin}`);
  return {
    id: "mlx",
    name: "MLX / MLX-LM",
    kind: "MODEL_RUNTIME",
    detected: signals.length > 0,
    confidence: signals.length ? "medium" : "low",
    signals,
  };
}

export function detectAllRuntimes() {
  return [detectOllama(), detectLlamaCpp(), detectLmStudio(), detectMlx()].filter(
    (r) => r.detected,
  );
}
