/** Public copy for get-ff-occam welcome / setup menu. */

export const GET_INSTALL_WELCOME = {
  title: "Occam",
  tagline: "One URL → honest Markdown. Typed failures, no file cache.",
  // Kept for verbose/internal banner compatibility; public installer uses title only.
  architecture: ".NET 10 Core (Native AOT)",
  mode: "extract-only",
  workers: "Node http + browser",
  statusRows: [
    { label: "Extract", value: "Live only" },
    { label: "Tools", value: "15 occam_*" },
    { label: "Playbooks", value: "seeds + heal/save" },
  ],
};

export const SETUP_MODE_COPY = {
  title: "First-run setup",
  description: "Install Occam, then connect it to your AI app.",
  auto: {
    id: "auto",
    label: "Auto",
    summary: "Detect and connect supported AI apps",
  },
  manual: {
    id: "manual",
    label: "Manual",
    summary: "Choose which AI app to connect",
  },
  hint: "OCCAM_SETUP=ask only. Press 1 or 2, then Enter (default Auto). Ctrl+C aborts.",
  defaultChoice: "auto",
};
