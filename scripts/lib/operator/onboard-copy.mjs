/** Operator-facing onboard copy (English — shipped operator UX). */

export const ONBOARD_WELCOME = {
  title: "Occam — First-run setup",
  subtitle: "Optional · skippable · core MCP tools unchanged",
  bullets: [
    "Writes ~/.occam/onboard.json and prints a paste-ready MCP snippet for your primary host.",
    "Prefer auto-connect when available: node scripts/occam.mjs connect",
    "You can register the same install in multiple MCP hosts — each gets its own stdio child.",
    "Skip anytime: node scripts/occam-onboard.mjs --skip",
  ],
  hint: "Press Enter to accept [defaults]. Ctrl+C aborts without harm.",
};

/** @type {Record<string, { title: string, description: string, choices?: { id: string, summary: string }[] }>} */
export const STEP_COPY = {
  occamHome: {
    title: "Install root",
    description:
      "Absolute path to your Occam install (repo clone or release tarball).\n" +
      "Doctor, workers, and MCP host resolve paths from OCCAM_HOME.",
    choices: undefined,
  },
  hostTarget: {
    title: "Primary MCP host (wiring snippet)",
    description:
      "Which host you are wiring first — not the only host that can use this install.\n" +
      "Affects launcher shape in the paste-ready snippet. Hermes YAML only when Hermes is selected.",
    choices: [
      { id: "cursor", summary: "Cursor — global or workspace JSON (Settings → MCP)" },
      { id: "hermes", summary: "Hermes — scripts/occam-wrapper.sh in ~/.hermes/config.yaml" },
      { id: "openclaw", summary: "OpenClaw — stdio via launch-mcp-host.mjs" },
      { id: "claude-desktop", summary: "Claude Desktop — claude_desktop_config.json" },
      { id: "generic-stdio", summary: "VS Code / Cline / Zed / other stdio MCP host" },
      { id: "cli-only", summary: "Scripts/smoke only — no host MCP wiring" },
    ],
  },
  browser: {
    title: "Browser engine",
    description:
      "Playwright backend for SPA / browser transcode.\n" +
      "Production default is bundled Chromium (reproducible extracts).",
    choices: [
      { id: "bundled", summary: "Playwright Chromium via doctor (recommended)" },
      { id: "system-dev", summary: "System Chrome/Edge — dev only (OCCAM_BROWSER_CHANNEL)" },
    ],
  },
  proxy: {
    title: "Proxy rotation pool",
    description:
      "Mass-scrape / egress slice. When yes, sets OCCAM_PROXY_LIST_FILE to\n" +
      "~/.occam/proxy-list.txt — you must create that file separately.",
    choices: [
      { id: "no", summary: "Direct egress (default)" },
      { id: "yes", summary: "Enable proxy list file path" },
    ],
  },
  profile: {
    title: "Runtime profile",
    description:
      "Preset env knobs for pool size, banner, and browser isolation.\n" +
      "Does not change MCP tool surface.",
    choices: [
      {
        id: "default",
        summary: "Daily use — pool 1, bundled Chromium, banner on",
      },
      {
        id: "hermes-headless",
        summary: "Headless CI profile — banner off, shared browser profile, pool 1",
      },
      {
        id: "mass-scrape",
        summary: "Higher parallelism — isolated browser, digest pool 4 (add proxies separately)",
      },
    ],
  },
};

export const ONBOARD_COMPLETE = {
  title: "Onboard complete",
  nextSteps: [
    "Prefer: node scripts/occam.mjs connect (auto-registers supported hosts).",
    "Or paste the snippet below into your chosen host, then reload MCP.",
    "Docs: INSTALL.md · CLI: node scripts/occam-help.mjs",
  ],
  hermesTip:
    "Hermes only: merge YAML below into ~/.hermes/config.yaml — command is occam-wrapper.sh",
};
