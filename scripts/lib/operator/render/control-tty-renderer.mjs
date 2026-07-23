import { horizontalRule, sectionBox } from "./tty-layout.mjs";

/**
 * @param {string} occamHome
 * @param {string} version
 */
export function renderControlHeader(occamHome, version) {
  return [
    "",
    sectionBox("FF-Occam Control", [
      `Version: ${version}`,
      `OCCAM_HOME: ${occamHome}`,
      "Type a menu key and press Enter.",
    ]),
  ].join("\n");
}

export function renderControlMenu() {
  return [
    horizontalRule(),
    "  1  Settings (onboard)",
    "  2  Run doctor",
    "  3  Check for updates",
    "  4  Help (next steps)",
    "  5  Restart Occam + reload hint",
    "  6  Smoke test",
    "  h  Full help catalog",
    "  s  Status",
    "  q  Quit",
    horizontalRule(),
  ].join("\n");
}

/**
 * @param {{ ok: boolean, message: string, data?: unknown }} result
 */
export function renderActionResult(result) {
  const title = result.ok ? "Done" : "Failed";
  return sectionBox(title, result.message.split("\n"));
}
