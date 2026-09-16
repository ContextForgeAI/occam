#!/usr/bin/env node
/**
 * Deterministic mock agents for H2/H3 pipeline selftests.
 * These are NOT models — they encode selection-failure rates so the measurement
 * pipeline can be proven without inventing LLM results.
 */
export const CAPABILITIES = ["weak", "medium", "strong"];
export const SURFACES = ["minimal", "basic", "reader", "full"];

/** Tools exposed per profile — keep in sync with OccamToolProfile.cs */
export const SURFACE_TOOLS = {
  minimal: ["occam"],
  basic: ["occam", "occam_digest", "occam_search"],
  reader: [
    "occam_client_capabilities",
    "occam",
    "occam_transcode",
    "occam_probe",
    "occam_digest",
    "occam_map",
    "occam_search",
    "occam_extract_knowledge",
    "occam_verify",
  ],
  full: null, // means "all" — selection among a large set
};

const FULL_DISTRACTORS = [
  "occam_playbook_heal",
  "occam_playbook_save",
  "occam_attest",
  "occam_dataset_export",
  "occam_claim_check",
];

/**
 * Probability of picking an irrelevant tool, by capability × surface size class.
 * Strong on minimal gets a small penalty (cannot use digest/search when needed).
 */
export function irrelevantPickRate(capability, surface) {
  const size = surface === "minimal" ? 1 : surface === "basic" ? 3 : surface === "reader" ? 9 : 16;
  if (size === 1) return 0; // selection failure impossible
  const table = {
    weak: { 3: 0.35, 9: 0.45, 16: 0.55 },
    medium: { 3: 0.12, 9: 0.18, 16: 0.25 },
    strong: { 3: 0.02, 9: 0.03, 16: 0.04 },
  };
  return table[capability]?.[size] ?? 0.2;
}

/** Seeded PRNG (mulberry32) for reproducible mock runs. */
export function mulberry32(seed) {
  let t = seed >>> 0;
  return () => {
    t += 0x6d2b79f5;
    let r = Math.imul(t ^ (t >>> 15), 1 | t);
    r ^= r + Math.imul(r ^ (r >>> 7), 61 | r);
    return ((r ^ (r >>> 14)) >>> 0) / 4294967296;
  };
}

/**
 * Choose which tool a mock agent calls for a task under a surface.
 * @returns {{ tool: string, irrelevant: boolean, reason: string }}
 */
export function selectTool(task, capability, surface, rand) {
  const needs = task.needs?.[0] || "read";
  const preferred =
    needs === "digest"
      ? "occam_digest"
      : needs === "search"
        ? "occam_search"
        : surface === "minimal" || surface === "basic"
          ? "occam"
          : "occam";

  const available =
    SURFACE_TOOLS[surface] ??
    [
      "occam",
      "occam_transcode",
      "occam_digest",
      "occam_search",
      ...FULL_DISTRACTORS,
    ];

  // Task needs a tool the surface does not expose → hard fail (capability-matched disclosure cost).
  if (needs === "digest" && !available.includes("occam_digest")) {
    return {
      tool: preferred,
      irrelevant: false,
      missingCapability: true,
      reason: "surface_lacks_digest",
    };
  }
  if (needs === "search" && !available.includes("occam_search")) {
    return {
      tool: preferred,
      irrelevant: false,
      missingCapability: true,
      reason: "surface_lacks_search",
    };
  }

  const rate = irrelevantPickRate(capability, surface);
  if (rand() < rate) {
    const distractors = (task.irrelevant_tools || FULL_DISTRACTORS).filter((t) =>
      surface === "full" ? true : available.includes(t),
    );
    const pool = distractors.length > 0 ? distractors : FULL_DISTRACTORS;
    const tool = pool[Math.floor(rand() * pool.length)];
    return { tool, irrelevant: true, missingCapability: false, reason: "selection_error" };
  }

  const tool = available.includes(preferred) ? preferred : available[0];
  return { tool, irrelevant: false, missingCapability: false, reason: "ok" };
}

/**
 * Simulate task success without network: selection + surface coverage only.
 * Live fetch scoring is a separate arm (`--live`).
 */
export function simulateTaskSuccess(task, capability, surface, rand) {
  const pick = selectTool(task, capability, surface, rand);
  if (pick.missingCapability) {
    return { ok: false, ...pick, callsToSuccess: null };
  }
  if (pick.irrelevant) {
    // Weak agents loop; strong recover once.
    if (capability === "strong" && rand() < 0.85) {
      return {
        ok: true,
        tool: pick.tool,
        irrelevant: true,
        recovered: true,
        reason: "recovered_after_irrelevant",
        callsToSuccess: 2,
      };
    }
    return {
      ok: false,
      tool: pick.tool,
      irrelevant: true,
      recovered: false,
      reason: "unrecoverable_irrelevant",
      callsToSuccess: null,
    };
  }
  return {
    ok: true,
    tool: pick.tool,
    irrelevant: false,
    recovered: false,
    reason: "first_call",
    callsToSuccess: 1,
  };
}

/** Build an exam submission JSON for a mock capability (H3 arm). */
export function examSubmissionFor(capability, subject = {}) {
  const base = {
    clientInfo: subject.clientInfo || "research-harness/1.0",
    modelHint: subject.modelHint || `mock-${capability}`,
    sessionId: subject.sessionId || `session-${capability}`,
    selfReportTier: subject.selfReportTier || capability,
  };

  if (capability === "strong") {
    return {
      ...base,
      canaryVerdict: "READ_VERIFIED",
      basicCallArguments: { url: "https://example.com" },
      focusBudgetArguments: { task: "closures", budget: 800 },
      chain: {
        calls: [
          { url: "https://example.com" },
          { url: "https://example.com", if_none_match: "sha256:abc" },
        ],
        producedValue: "sha256:abc",
      },
    };
  }

  if (capability === "medium") {
    return {
      ...base,
      canaryVerdict: "READ_VERIFIED",
      basicCallArguments: { url: "https://example.com" },
      focusBudgetArguments: { task: "overview", budget: 512 },
      chain: { calls: [{ url: "https://example.com" }], producedValue: null },
    };
  }

  return {
    ...base,
    canaryVerdict: "HALLUCINATED",
    basicCallArguments: { url: "notaurl" },
    focusBudgetArguments: { max_tokens: 800 },
    chain: { calls: [], producedValue: null },
  };
}
