# EXTERNAL-OWNER-ACTIONS

**Status:** Updated Phase 6.5E (2026-07-26) with owner decisions OD-1/OD-2/OD-3.  
**Purpose:** Track actions that cannot be completed from repository code alone (GitHub org/repo settings, secrets, publish credentials, product-policy choices).  
**Related:** `docs-audit/OWNER-DECISIONS.md`; `docs-audit/phase6/P6-04-packaging.md` Appendix A; findings EF-052, EF-053, EF-034.

## Status summary (post-6.5)

| EA | Owner decision | Status | Docs v3 blocker? |
|----|----------------|--------|------------------|
| EA-052 | OD-1 | **REQUIRED EXTERNAL VERIFICATION** | Only blocks marketplace-**trust** claims, not core docs |
| EA-053 | OD-2 | **DEFERRED; honesty-only for current release** | No |
| EA-034 | OD-3 | **npm NOT GA** (INTERNAL/EXPERIMENTAL) | No (must be classified non-GA) |

---

## EA-052 — Marketplace branch protection & merge policy

**Finding:** EF-052 (`playbook-marketplace.yml` can treat skipped L4 validation as job success and enable `gh pr merge --auto --squash`).  
**Owner decision:** OD-1 — repository code alone is NOT sufficient evidence. Marketplace may be documented as **operational machinery** but NOT as a trusted auto-merge / trusted distribution path until the external state below is verified and recorded. Do not change GitHub settings automatically.  
**Status:** REQUIRED EXTERNAL VERIFICATION. In-repo workflow hardened (Phase 6: requires `l4_result==passed`). External branch-protection state still UNKNOWN → marketplace-trust claims excluded from Docs v3.  
**Docs block:** Blocks marketplace-trust claims only; core product docs proceed.

### In-repo (engineering — not owner-blocked)

- Make empty/skipped playbook sets **non-success** for auto-merge purposes.
- Align PR path filters with recursive `**/*.json` diff detection.
- Require `l4-gate` output `passed` before auto-merge; or remove auto-merge job.
- Fix community cosign step (`--key` or keyless + `id-token: write`) — shared with EF-053.

### External owner actions

| # | Action | How to verify | Status |
|---|--------|---------------|--------|
| 1 | Protect `main` (ruleset or classic): require Playbook Marketplace **validate** check | GitHub → Settings → Rules / Branches; or `gh api repos/{owner}/{repo}/rulesets` | **UNKNOWN** (not readable from git tree) |
| 2 | Confirm repo “Allow auto-merge” + whether bot can merge without human review | Settings → General → Pull Requests; team permissions | **UNKNOWN** |
| 3 | Restrict bypass actors for `profiles/playbooks/community/**` if policy requires human review | Ruleset bypass list | **UNKNOWN** |
| 4 | Ensure `COSIGN_PRIVATE_KEY` / `COSIGN_PASSWORD` secrets match chosen signing mode, **or** abandon key secrets and use keyless OIDC | Settings → Secrets; test sign job | **UNKNOWN** |
| 5 | After (1)–(4), record evidence (date, ruleset id, required check name) in this table | Paste `gh api` summary here | Open |

**Unblocks:** honest marketplace / community-tier documentation.

---

## EA-053 — Cosign product contract

**Finding:** EF-053 — community cosign step misconfigured; release `.bundle` produced (`sign-release.yml`) but no shipped install path verifies it (sha256-manifest only).

**Owner decision:** OD-2 — **Honesty-only for the current release.** Cosign is NOT made mandatory this phase. Classify the release `.bundle` as release metadata / unused signing surface. Do not claim cosign-verified releases/installers or signed supply-chain guarantees the installers do not check. Cosign-required install stays a future hardening item. **Not a Docs v3 blocker.**  
**Status:** DEFERRED; honesty-only.

Chosen contract (recorded): **Honesty-only** — install trust bar = sha256 of tarball vs release manifest; `.bundle` is optional/manual (`cosign verify-blob`). Future option (Cosign-required across `get-ff-occam` / `release-install` / npm `ensureBinary`) remains open, non-blocking.

| # | Action | Status |
|---|--------|--------|
| 1 | Record chosen contract (H or C) | Open |
| 2 | If C: confirm Release assets always include `.bundle` before advertising | Open |
| 3 | If H: approve removing or labeling `.bundle` as manual-only | Open |

---

## EA-034 — npm publish intent

**Finding:** EF-034 — `@ff-occam/mcp` would be DOA if published (import outside `files`); pack boundary fixed in Phase 6 (vendored `lib/host-install-gate.mjs`), still unpublished.

**Owner decision:** OD-3 — npm is **NOT a supported 1.0 install channel**. Keep classified INTERNAL / EXPERIMENTAL / NOT PUBLIC INSTALL PATH until an end-to-end contract passes: package available → install → host runtime available → doctor → MCP launch → first read → verify/update. Do not advertise npm/npx install as GA. Do not remove package code. **Not a Docs v3 blocker** if the exposure matrix marks npm non-GA.  
**Status:** npm not GA.

| # | Action | Status |
|---|--------|--------|
| 1 | npm as 1.0 channel? | **NO (OD-3)** — non-GA until end-to-end contract passes |
| 2 | Before any future publish: pack-boundary fix (done) + end-to-end install smoke | Open (future) |
| 3 | Docs v3: classify npm non-GA; no npx quickstart as GA | Applied |

---

## EA-DOCKER — Image registry (optional)

In-repo `Dockerfile` exists; no workflow pushes a public image. Owner decides whether a registry image is a supported channel before documenting it.
