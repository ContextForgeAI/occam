# Chapter 20 — What Occam does without asking

**Status:** STABLE · **Prerequisites:** [Chapter 5](05-acquisition-ladder.md), [Chapter 14](14-what-a-receipt-proves.md), [Chapter 18](18-exposure.md)

---

## Mental model

**Seven classes of automatic decision** — routing, provisioning, content shaping, trust side effects, hygiene, network politeness, host mutation — each with a visibility answer and a controllability answer. Twenty-nine proven behaviors run without an explicit per-call request; eleven are not disableable.

---

## Explanation

### Automation classes

| Class | Examples |
|-------|----------|
| Routing / escalation | Post-processors, public-reference browser skip, managed attempt after dual fail |
| Resource provisioning | HTTP daemon prewarm, browser pool, Chromium auto-provision |
| Content shaping | Feature injection, consent dismiss, virtual scroll, IR build-and-discard |
| Trust / provenance | Key mint on start, default signing, playbook save always signs, cache write |
| Hygiene | Temp header files, skill install wipe |
| Network politeness | Robots/throttle (default off, fail-open), proxy rotation gaps |
| Host mutation | Onboard env merge, connect config edit, name-wide process kill |

### Must-disclose automatics (public docs duty)

These change privacy, trust, security, or host integrity:

1. **Key mint on every host start** — even with `OCCAM_RECEIPTS=off` (disk secret appears uninvited).
2. **`occam_playbook_save` always signs** — `OCCAM_RECEIPTS` does not apply.
3. Together, (1) and (2) prove **`OCCAM_RECEIPTS` is not a master signing switch**.
4. **`bypassCSP:true` unconditionally** on browser extract plus playbook `page.evaluate` — page scripts may run.
5. **Opt-in cache** stores full signed envelopes on disk when `cache_ttl_s>0`.
6. **`occam refresh` kills every host** by binary name on the machine.
7. **`launch-mcp-host` merges `~/.occam/onboard.json` env** into every launch.
8. **WS/Remote new session** may kill the shared browser pool.
9. **Managed provider egress** when configured — third party sees URL and returned bytes may be signed.
10. **Marketplace auto-merge** risk for community playbooks (operational machinery, not trusted distribution — OD-1).

Safely invisible at product surface: daemon prewarm, stderr cosmetics, dead IR CPU cost.

### Surprise ranking (operator / careful user)

Highest surprise first: key mint → save always signs → refresh kill-all → onboard env inject → WS pool kill → bypassCSP → playbook JS → marketplace merge → Nuxt eval → cache on disk → skill wipe → silent DOM mutation → fragment cache identity.

### First-call inventory (conceptual)

Your very first successful `occam_transcode` likely also: minted or loaded a signing key, prewarmed HTTP daemon, dismissed consent banners, ran with CSP bypass, possibly virtual-scrolled, built and discarded canonical IR, and may have written cache if TTL was set.

---

## CHECK

**NETWORK + LOCAL.** Run one browser-backed transcode with `OCCAM_LOG` enabled. Inspect stderr for launch options and page mutations you did not request. List `~/.occam/keys/` after starting with `OCCAM_RECEIPTS=off` — key file exists anyway.

---

## Common misconception

**"Nothing happens that I did not ask for."** Twenty-nine proven automatic behaviors say otherwise, and eleven of them are not disableable — including key mint, save-always-sign, bypassCSP, and machine-wide refresh kill.

---

## Limitations

- Automatic behaviors are not exhaustively surfaced in MCP responses.
- Many are correct engineering (post-processors, eligibility rules) but surprising in combination.
- Disabling receipts does not disable key mint or playbook signing.
- Robots compliance is off by default and fails open on fetch errors.

---

## Links

- [Chapter 14 — Receipts](14-what-a-receipt-proves.md) — `OCCAM_RECEIPTS` limits
- [Chapter 21 — State and footprint](21-state-and-footprint.md)
- [Chapter 22 — Configuration](22-configuration.md)
- [Chapter 23 — Security posture](23-security-posture.md)
- Audit: `docs-audit/AUTOMATION-MODEL.md`
