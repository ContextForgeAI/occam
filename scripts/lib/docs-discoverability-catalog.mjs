/**
 * Canonical capability-family discoverability catalog (Phase 7X–Z).
 * Source: docs-audit/DOCUMENTATION-EXPOSURE-MATRIX.md + DISCOVERABILITY-GATE.md
 *
 * Each family maps to path globs (not line numbers). A path matches when the file
 * exists and contains the family slug OR any listed marker (case-insensitive).
 */

/** @typedef {'PUBLIC_CORE' | 'PUBLIC_ADVANCED' | 'EXPERIMENTAL' | 'OPERATOR' | 'DO_NOT_DOCUMENT_AS_FEATURE'} ExposureClass */

/**
 * @typedef {Object} FamilyDiscovery
 * @property {string} slug
 * @property {ExposureClass} exposureClass
 * @property {string[]} llmsMarkers - must appear in llms.txt (slug is always required)
 * @property {{ globs: string[], markers?: string[] }} task - TASK or QUICKSTART path
 * @property {{ globs: string[], markers?: string[] }} [quickstart]
 * @property {{ globs: string[], markers?: string[] }} reference - REFERENCE or CAPABILITY
 * @property {{ globs: string[], markers?: string[] }} [handbook]
 * @property {{ globs: string[], markers?: string[] }} [llmsDoc] - secondary llms-adjacent doc paths
 */

/** @type {FamilyDiscovery[]} */
export const DISCOVERABILITY_FAMILIES = [
  // ── PUBLIC_CORE (13) ──────────────────────────────────────────────────────
  {
    slug: "acquisition-routing",
    exposureClass: "PUBLIC_CORE",
    llmsMarkers: ["acquisition-routing", "http_then_browser"],
    task: { globs: ["docs/choosing-a-tool.md", "docs/guides/read-a-page.md"], markers: ["occam_transcode", "backend_policy"] },
    reference: { globs: ["docs/acquisition.md", "docs/how-occam-works.md"], markers: ["ladder", "http_then_browser", "acquisition"] },
  },
  {
    slug: "http-acquisition",
    exposureClass: "PUBLIC_CORE",
    llmsMarkers: ["http-acquisition"],
    task: { globs: ["docs/choosing-a-tool.md", "docs/guides/read-a-page.md"], markers: ["occam_transcode"] },
    reference: { globs: ["docs/acquisition.md", "docs/tools/occam_transcode.md"], markers: ["HTTP", "http"] },
  },
  {
    slug: "browser-acquisition",
    exposureClass: "PUBLIC_CORE",
    llmsMarkers: ["browser-acquisition"],
    task: { globs: ["docs/choosing-a-tool.md"], markers: ["browser", "occam_transcode"] },
    reference: { globs: ["docs/acquisition.md", "docs/concepts.md"], markers: ["browser", "Playwright"] },
  },
  {
    slug: "token-budget",
    exposureClass: "PUBLIC_CORE",
    llmsMarkers: ["token-budget"],
    task: { globs: ["docs/choosing-a-tool.md", "docs/guides/read-a-page.md"], markers: ["max_tokens", "fit_markdown", "focus_query"] },
    reference: { globs: ["docs/materialization.md", "docs/tools/occam_client_capabilities.md"], markers: ["token", "budget", "max_tokens"] },
  },
  {
    slug: "focus-selection",
    exposureClass: "PUBLIC_CORE",
    llmsMarkers: ["focus-selection"],
    task: { globs: ["docs/choosing-a-tool.md", "docs/guides/read-a-page.md"], markers: ["focus_query", "fit_markdown"] },
    reference: { globs: ["docs/materialization.md", "docs/tools/occam_transcode.md"], markers: ["focus_query", "fit_markdown", "fragment"] },
  },
  {
    slug: "quality-failure-semantics",
    exposureClass: "PUBLIC_CORE",
    llmsMarkers: ["quality-failure-semantics", "ok:false"],
    task: { globs: ["docs/quick-start.md", "docs/choosing-a-tool.md"], markers: ["ok: false", "ok:false", "failure.code"] },
    reference: { globs: ["docs/failure-codes.md", "docs/trust/honest-failures.md"], markers: ["thin_extract", "ok: false", "unknown"] },
  },
  {
    slug: "probe-diagnostics",
    exposureClass: "PUBLIC_CORE",
    llmsMarkers: ["probe-diagnostics"],
    task: { globs: ["docs/choosing-a-tool.md", "docs/guides/search-and-discover.md"], markers: ["occam_probe"] },
    reference: { globs: ["docs/tools/occam_probe.md"], markers: ["occam_probe", "extractability"] },
  },
  {
    slug: "site-mapping",
    exposureClass: "PUBLIC_CORE",
    llmsMarkers: ["site-mapping"],
    task: { globs: ["docs/choosing-a-tool.md", "docs/guides/search-and-discover.md"], markers: ["occam_map"] },
    reference: { globs: ["docs/tools/occam_map.md"], markers: ["occam_map", "sitemap"] },
  },
  {
    slug: "digest-synthesis",
    exposureClass: "PUBLIC_CORE",
    llmsMarkers: ["digest-synthesis"],
    task: { globs: ["docs/choosing-a-tool.md", "docs/guides/research-multiple.md"], markers: ["occam_digest"] },
    reference: { globs: ["docs/tools/occam_digest.md"], markers: ["occam_digest"] },
  },
  {
    slug: "runtime-transports",
    exposureClass: "PUBLIC_CORE",
    llmsMarkers: ["runtime-transports"],
    task: { globs: ["docs/quick-start.md"], markers: ["stdio", "MCP"] },
    reference: { globs: ["docs/transports.md", "docs/handbook/18-exposure.md"], markers: ["stdio", "transport", "WebSocket"] },
  },
  {
    slug: "mcp-exposure",
    exposureClass: "PUBLIC_CORE",
    llmsMarkers: ["mcp-exposure", "tools/list"],
    task: { globs: ["docs/choosing-a-tool.md", "docs/tools/index.md"], markers: ["occam_"] },
    reference: { globs: ["docs/tools-reference.md", "MCP_API_SPEC.md", "docs/handbook/18-exposure.md"], markers: ["tools/list", "OCCAM_PROFILE"] },
  },
  {
    slug: "client-context",
    exposureClass: "PUBLIC_CORE",
    llmsMarkers: ["client-context"],
    task: { globs: ["docs/choosing-a-tool.md"], markers: ["occam_client_capabilities", "context_tokens"] },
    reference: { globs: ["docs/tools/occam_client_capabilities.md", "docs/materialization.md"], markers: ["occam_client_capabilities", "OCCAM_CLIENT_CONTEXT_TOKENS"] },
  },
  {
    slug: "install-onboarding",
    exposureClass: "PUBLIC_CORE",
    llmsMarkers: ["install-onboarding"],
    task: { globs: ["docs/quick-start.md", "README.md"], markers: ["install", "doctor", "connect"] },
    quickstart: { globs: ["INSTALL.md", "docs/install.md"], markers: ["install", "doctor"] },
    reference: { globs: ["docs/operators.md", "docs/getting-started.md"], markers: ["install", "bootstrap"] },
  },

  // ── PUBLIC_ADVANCED (15) ───────────────────────────────────────────────────
  {
    slug: "network-safety",
    exposureClass: "PUBLIC_ADVANCED",
    llmsMarkers: ["network-safety"],
    task: { globs: ["docs/guides/sessions.md"], markers: ["private", "SSRF", "network"] },
    reference: { globs: ["docs/networking.md", "docs/trust/local-first.md"], markers: ["SSRF", "private", "network-safety"] },
  },
  {
    slug: "session-fetch",
    exposureClass: "PUBLIC_ADVANCED",
    llmsMarkers: ["session-fetch"],
    task: { globs: ["docs/guides/sessions.md", "docs/choosing-a-tool.md"], markers: ["session_profile"] },
    reference: { globs: ["docs/sessions.md", "docs/configuration.md"], markers: ["session_profile", "OCCAM_SESSIONS_ROOT"] },
  },
  {
    slug: "access-consent",
    exposureClass: "PUBLIC_ADVANCED",
    llmsMarkers: ["access-consent"],
    task: { globs: ["docs/acquisition.md"], markers: ["consent", "CAPTCHA"] },
    reference: { globs: ["docs/handbook/20-automatic-behaviors.md"], markers: ["consent", "bypassCSP", "virtual scroll"] },
    handbook: { globs: ["docs/handbook/20-automatic-behaviors.md"], markers: ["consent dismiss"] },
  },
  {
    slug: "structured-materialization",
    exposureClass: "PUBLIC_ADVANCED",
    llmsMarkers: ["structured-materialization"],
    task: { globs: ["docs/choosing-a-tool.md"], markers: ["json_blocks", "json_tables"] },
    reference: { globs: ["docs/materialization.md", "docs/tools/occam_transcode.md"], markers: ["json_blocks", "json_tables", "structured"] },
  },
  {
    slug: "differential-materialization",
    exposureClass: "PUBLIC_ADVANCED",
    llmsMarkers: ["differential-materialization"],
    task: { globs: ["docs/recipes.md"], markers: ["if_none_match", "diff_against"] },
    reference: { globs: ["docs/materialization.md", "docs/handbook/08-structured-differential-output.md"], markers: ["diff_against", "if_none_match"] },
  },
  {
    slug: "web-search",
    exposureClass: "PUBLIC_ADVANCED",
    llmsMarkers: ["web-search"],
    task: { globs: ["docs/choosing-a-tool.md", "docs/guides/search-and-discover.md"], markers: ["occam_search"] },
    reference: { globs: ["docs/tools/occam_search.md", "docs/configuration.md"], markers: ["occam_search", "OCCAM_SEARCH_PROVIDER"] },
  },
  {
    slug: "schema-knowledge-extraction",
    exposureClass: "PUBLIC_ADVANCED",
    llmsMarkers: ["schema-knowledge-extraction"],
    task: { globs: ["docs/guides/structured-extraction.md", "docs/choosing-a-tool.md"], markers: ["occam_extract_knowledge"] },
    reference: { globs: ["docs/tools/occam_extract_knowledge.md"], markers: ["occam_extract_knowledge", "knowledge_schema"] },
  },
  {
    slug: "playbook-resolution",
    exposureClass: "PUBLIC_ADVANCED",
    llmsMarkers: ["playbook-resolution"],
    task: { globs: ["docs/choosing-a-tool.md"], markers: ["occam_playbook_resolve"] },
    reference: { globs: ["docs/tools/occam_playbook_resolve.md", "docs/handbook/11-playbooks-resolution.md"], markers: ["occam_playbook_resolve", "playbook"] },
  },
  {
    slug: "playbook-authoring",
    exposureClass: "PUBLIC_ADVANCED",
    llmsMarkers: ["playbook-authoring"],
    task: { globs: ["docs/choosing-a-tool.md"], markers: ["occam_playbook_save"] },
    reference: { globs: ["docs/tools/occam_playbook_save.md"], markers: ["occam_playbook_save", "sign"] },
  },
  {
    slug: "playbook-healing",
    exposureClass: "PUBLIC_ADVANCED",
    llmsMarkers: ["playbook-healing"],
    task: { globs: ["docs/choosing-a-tool.md"], markers: ["occam_playbook_heal"] },
    reference: { globs: ["docs/tools/occam_playbook_heal.md"], markers: ["occam_playbook_heal"] },
  },
  {
    slug: "playbook-validation",
    exposureClass: "PUBLIC_ADVANCED",
    llmsMarkers: ["playbook-validation"],
    task: { globs: ["docs/choosing-a-tool.md"], markers: ["occam_playbook_lint"] },
    reference: { globs: ["docs/tools/occam_playbook_lint.md"], markers: ["occam_playbook_lint", "lint"] },
  },
  {
    slug: "receipts",
    exposureClass: "PUBLIC_ADVANCED",
    llmsMarkers: ["receipts"],
    task: { globs: ["docs/guides/verify-sources.md"], markers: ["receipt"] },
    reference: { globs: ["docs/receipts.md", "docs/receipt_verification.md"], markers: ["Receipt v1", "receipt", "signature"] },
    handbook: { globs: ["docs/handbook/02-honesty-contract.md"], markers: ["receipt", "integrity"] },
  },
  {
    slug: "verification",
    exposureClass: "PUBLIC_ADVANCED",
    llmsMarkers: ["verification"],
    task: { globs: ["docs/guides/verify-sources.md", "docs/choosing-a-tool.md"], markers: ["occam_verify"] },
    reference: { globs: ["docs/tools/occam_verify.md", "docs/handbook/15-verifying.md"], markers: ["occam_verify", "verify"] },
  },
  {
    slug: "claims-attestation",
    exposureClass: "PUBLIC_ADVANCED",
    llmsMarkers: ["claims-attestation"],
    task: { globs: ["docs/guides/claims.md", "docs/choosing-a-tool.md"], markers: ["occam_claim_check", "occam_attest"] },
    reference: { globs: ["docs/tools/occam_claim_check.md", "docs/tools/occam_attest.md"], markers: ["occam_claim_check", "occam_attest"] },
    handbook: { globs: ["docs/handbook/16-evidence-for-claims.md"], markers: ["claim_check", "attest"] },
  },
  {
    slug: "dataset-provenance",
    exposureClass: "PUBLIC_ADVANCED",
    llmsMarkers: ["dataset-provenance"],
    task: { globs: ["docs/choosing-a-tool.md"], markers: ["occam_dataset_export"] },
    reference: { globs: ["docs/tools/occam_dataset_export.md"], markers: ["occam_dataset_export", "manifest"] },
  },
];

/** Families that must not appear as positive capability headlines in llms/README feature lists. */
export const DO_NOT_FEATURE_FAMILIES = [
  {
    slug: "canonical-knowledge-ir",
    forbiddenHeadlineMarkers: ["canonical-knowledge-ir", "Canonical IR", "canonical IR codec"],
  },
  {
    slug: "consensus-crosscheck",
    forbiddenHeadlineMarkers: ["consensus proof", "multi-node consensus", "N-of-M consensus"],
  },
];

/** Opt-in env gates that must co-locate with tool names in experimental docs (R2). */
export const OPT_IN_ENV_GATES = [
  { env: "OCCAM_BATCH_MCP", tools: ["occam_batch_submit", "occam_batch_status", "occam_batch_results"], docGlobs: ["docs/experimental.md", "docs/tools/occam_batch.md"] },
  { env: "OCCAM_WATCH_MCP", tools: ["occam_watch"], docGlobs: ["docs/experimental.md", "docs/tools/occam_watch.md"] },
  { env: "OCCAM_CONSENSUS_MCP", tools: ["occam_crosscheck"], docGlobs: ["docs/experimental.md", "docs/tools/occam_crosscheck.md"] },
  { env: "OCCAM_ATLAS_MCP", tools: ["occam_failure_atlas"], docGlobs: ["docs/experimental.md", "docs/tools/occam_failure_atlas.md"] },
];
