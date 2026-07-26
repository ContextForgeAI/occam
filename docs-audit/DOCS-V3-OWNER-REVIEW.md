# DOCS-V3-OWNER-REVIEW

**Branch:** `docs/v3-canonical` · Phase 8P · 2026-07-26  
**Purpose:** Short enough to read before friend test / publication decision.

---

## 1. What Occam is (one paragraph)

Occam is a **locally run host** that helps AI agents acquire usable web content, shape it for context windows, fail honestly when content is unknown (`ok:false`), and optionally attach integrity artifacts that can later be checked against a key. It is not “HTML→Markdown” alone, not a cryptography product first, and not a fact-checker.

## 2. Newly visible vs old docs

Acquisition ladder (real EF-056), sessions tiers, proxies/networking, materialization/token budgets, playbook v1/v2 signatures, experimental surfaces (watch/crosscheck/batch/atlas), operators/CLI/connect as first-class, full handbook (27 chapters), honesty vocabulary for claim/attest/crosscheck/extract-telemetry, install channel honesty (npm not GA; Cosign not enforced).

## 3. Public core

Install/connect, stdio MCP, transcode/probe/digest/map, client capabilities, token/focus materialization, HTTP→browser gated acquisition, quality/failure honesty, tool exposure/profiles.

## 4. Advanced

Sessions, networking/SSRF, structured/differential output, playbooks (resolve/heal/lint/save), extract_knowledge, search (provider-gated), receipts/verify, claim_check/attest, datasets.

## 5. Experimental (labeled)

Watch, crosscheck (multi-source comparison — **not** consensus proof), batch, failure atlas, managed acquisition, opt-in response cache.

## 6. Trust proves / does not prove

**Proves:** integrity of checked bytes relative to a key (Receipt v1, playbook sig, dataset manifest, signed watch entries).  
**Does not prove:** truth, origin authenticity, identity, trusted time, claim correctness, consensus correctness.  
Playbook **v2** makes gate snapshot tamper-evident; it does not make the score “true.” **v1** leaves gate fields unsigned. Extract `receipt` is **telemetry**, not Receipt v1.

## 7. Known product limitations

YELLOW Truth-Gate families; session secrets on disk; anonymous browser pool not a hard isolation boundary; proxy/SSRF path asymmetry; managed/search egress; marketplace trust blocked on EA-052; npm/cosign deferred (OD-2/3).

## 8. Known documentation limitations

Capsules / proxy rotation deeper than Quick Start but now on `receipts.md` / `networking.md`; access-consent documented as automatic behavior; friend-test is repo-only (not in site nav). Package/skill READMEs honesty-softened in Phase 8.

## 9. External actions remaining

- **EA-052** marketplace branch protection evidence  
- **EA-053** optional Cosign-required install later  
- **EA-034** npm end-to-end GA contract  

## 10. Pages the owner should personally read

1. `README.md`  
2. `docs/quick-start.md`  
3. `docs/what-is-occam.md`  
4. `docs/acquisition.md`  
5. `docs/trust-and-safety.md`  
6. `docs/playbooks.md` (v1/v2)  
7. `docs/experimental.md`  
8. `docs/operators.md`  
9. `docs/handbook/index.md` + Ch 2 + Ch 5 + Ch 14  
10. `docs/friend-test.md`  
11. `llms.txt`  
12. `docs-audit/DOCS-V3-REVIEW.md` (Phase 7) + this file  

## 11. Friend-test instructions

Give the tester **only** the public repo / docs site entry points. Ask them to follow `docs/friend-test.md` without coaching. Collect their confusion log. Do not explain Occam verbally beyond the docs.

## 12. Publication recommendation

**READY FOR FRIEND TEST + OWNER REVIEW.**  
Not yet “push to public main” until friend-test feedback and owner skim of §10 pages. Do **not** advertise npm/Cosign/marketplace trust.

---

## Phase 8 gate snapshot

| Gate | Result |
|------|--------|
| Capability coverage PUBLIC_CORE | 13/13 |
| PUBLIC_ADVANCED | 15/15 |
| OPERATOR | 4/4 |
| Hidden-capability regression | 34/34 visible after soft-fixes |
| 15 core tools | 15/15 pages; minor residual notes only |
| Honesty / acquisition | Aligned; package/skill overclaims soft-fixed |
| User journey | PASS |
| Handbook 27/27 | YES |
| Machine gates | docs/honesty/discoverability/brand PASS; mkdocs --strict PASS |
