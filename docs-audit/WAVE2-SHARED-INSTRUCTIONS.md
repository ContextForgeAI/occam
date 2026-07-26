# Wave 2 shared instructions (for tool subagents)

REPO: c:\PROJECTS\FFOccamMCP
SOURCE OF TRUTH = CURRENT EXECUTABLE CODE ONLY.
Do NOT read docs/, README, llms.txt, MCP_API_SPEC, CHANGELOG for behavior.

Reuse Wave-1 CAP IDs from docs-audit/CAPABILITY-INVENTORY.md when the tool activates existing behavior.
Mint NEW IDs only in your assigned range, and only for genuine new PRODUCT capabilities.

Write the FULL report to your assigned path using the mandatory section list from WAVE2-ASSIGNMENT.md.

Include section `## Capability graph edges` with machine-ish lines:
TOOL|USES|CAP-xxx
PARAM:<name>|ENABLES|CAP-xxx
CAP-xxx|ROUTES_TO|<backend>
CAP-xxx|FALLS_BACK_TO|<backend>
CAP-xxx|PRODUCES|<artifact>
CAP-xxx|CONSUMES|session
etc.

Explicitly check cross-cutting categories (proxy, session, cookies, headers, http, browser, managed, retry, cache, diff, blocks, tables, chunks, budget, receipts, merkle, capsules, playbooks, datasets, claims, trust tags, screenshots, translate, llms.txt, feeds, profile, env). Answer "not used" when proven absent.

Answer: "Which capabilities would a user NEVER discover from this tool's short MCP description?" under HIDDEN / NON-OBVIOUS CAPABILITIES.

Do NOT audit opt-in tools fully. If core tool touches watch/batch/consensus/atlas, record edge only.

Do NOT edit product docs or product code. Do NOT commit.

Return ONLY the compact envelope to the orchestrator.
