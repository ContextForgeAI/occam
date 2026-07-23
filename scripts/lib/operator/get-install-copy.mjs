/** Copy for get-ff-occam.sh welcome — mirrors honest logger banner rows. */

export const GET_INSTALL_WELCOME = {
  title: "FF-Occam MCP",
  tagline: "One URL → honest Markdown. Typed failures, no file cache.",
  architecture: ".NET 10 Core (Native AOT)",
  mode: "L0 extract-only",
  workers: "Node http + browser",
  statusRows: [
    { label: "Extract", value: "Live only" },
    { label: "Tools", value: "14 occam_*" },
    { label: "Playbooks", value: "seeds + heal/save" },
  ],
};

export const SETUP_MODE_COPY = {
  title: "First-run setup",
  description:
    "Install the release bundle, then configure your MCP host.\n" +
    "Core MCP tools — see INSTALL.md.",
  auto: {
    id: "auto",
    label: "Auto",
    summary: "Defaults from OCCAM_HOST (hermes → wrapper; cursor → global JSON snippet)",
  },
  manual: {
    id: "manual",
    label: "Manual",
    summary: "Guided wizard — host, browser, profile, MCP snippet (occam-onboard)",
  },
  hint: "Press 1 or 2, then Enter. Ctrl+C aborts.",
  defaultChoice: "auto",
};
