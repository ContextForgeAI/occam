# WAVE 2 COMPLETENESS GATE + NEGATIVE-SPACE MINI PASS

## Core tool registration (code)

`OccamMcpServerRegistration.OccamToolNames` = 15 names. All have `docs-audit/tools/<name>.md`.

| Tool | Report | Completeness claim in report | Notes |
|------|--------|------------------------------|-------|
| occam_client_capabilities | ✓ | COMPLETE | |
| occam_transcode | ✓ (W1) | High / COMPLETE | Older section titles; evidence dense |
| occam_probe | ✓ | COMPLETE | Late envelope; CAP-420…437 in inventory |
| occam_digest | ✓ | COMPLETE | |
| occam_playbook_resolve | ✓ | COMPLETE | |
| occam_map | ✓ | COMPLETE | |
| occam_playbook_heal | ✓ | COMPLETE | |
| occam_playbook_save | ✓ | COMPLETE | |
| occam_extract_knowledge | ✓ | COMPLETE | |
| occam_search | ✓ | COMPLETE | |
| occam_verify | ✓ | COMPLETE | |
| occam_claim_check | ✓ | COMPLETE | |
| occam_attest | ✓ | COMPLETE | |
| occam_playbook_lint | ✓ | COMPLETE | |
| occam_dataset_export | ✓ | COMPLETE | |

Checklist dimensions (registration, params, path, backends, artifacts, profile, env, trust, network/session, cache/materialization, tests, hidden, graph edges, uncertainties) are covered **across** each report’s sections even when heading names differ. No tool sent for second-pass as BLOCKED.

## Negative-space mini pass (core MCP only)

**Q:** Executable behavior reachable from a **core** MCP tool missing from reports/CAPs/artifacts/graph?

| Candidate | Verdict |
|-----------|---------|
| Opt-in batch/watch/crosscheck/atlas | Out of Wave 2 scope → Wave 3 |
| BatchServer HTTP mode | Host mode, not core tool → Wave 3 |
| CLI `keys`/`verify`/`install-browser` | Documented in S19 + verify tool; not missing from core surface understanding |
| Translation / LibreTranslate | Covered as transcode CAP-081 |
| Managed providers | W1+W2 edges; EF-003 |
| Proxy rotation | W1 CAP-162…; digest/transcode inherit |
| Canonical Knowledge discard | W1 CAP-330; not a separate tool |
| Connect/install platform | Not MCP core tools → Wave 3 |

**Result:** No substantial **core-tool** gap requiring a targeted second-pass agent. Remaining gaps are Wave-3 surfaces or known engineering findings.

## Product interpretation seed (for WAVE2-REPORT)

See WAVE2-REPORT.md PRODUCT INTERPRETATION bullets.
