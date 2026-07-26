# AUTOMATION-MODEL (Phase 5L)

**Agent:** P5-07  
**SoT:** executable code. Input list: `AUTOMATIC-BEHAVIORS.md` (29 Wave-4 findings). Wave-4 corrections apply (C1 cascade, C6 receipts, EF-041…054).  
**Docs untrusted.**  
**Date:** 2026-07-26

---

## 0. Product question

**What does Occam decide automatically on behalf of the user?**

Answer: Occam automates **routing/escalation, resource provisioning, content shaping, trust/provenance side-effects, hygiene/cleanup, network politeness (mostly off/fail-open), and host mutation**. Many decisions are **correct engineering** (post-processors, TTL cache eligibility) but several are **surprising or EF-flagged** (key mint, always-sign save, bypassCSP, pool kill, name-wide process kill, onboard env inject, marketplace merge).

Only **proven** behaviors below. Bugs are flagged with EF IDs — not documented as features.

---

## 1. Automation classes

| Class | ID prefix | Meaning |
|-------|-----------|---------|
| Routing / escalation | A-ROUT | Backend choice, short-circuits, managed attempt |
| Resource provisioning | A-PROV | Daemons, browsers, pools, DI shared install |
| Content shaping | A-SHAPE | DOM mutation, compile/budget, features inject, focus |
| Trust / provenance | A-TRUST | Key mint, signing, receipt/cache replay |
| Hygiene / cleanup | A-HYGIENE | Temp deletes, skill wipe, install replace |
| Network politeness | A-NET | Robots/throttle, proxy rotation (often fail-open) |
| Host mutation | A-HOST | Onboard env, connect config, process kill, CI merge |

---

## 2. Automatic decisions (canonical table)

Promoted from AUTOMATIC-BEHAVIORS #1–29; verified against code. Count = **29**.

| # | CLASS | TRIGGER | DECISION | USER VISIBILITY | USER CONTROL | TRUST IMPACT | PERF IMPACT | Evidence |
|---|-------|---------|----------|-----------------|--------------|--------------|-------------|----------|
| 1 | A-TRUST | MCP host DI start | `ReceiptSigner.LoadOrCreate` mints/loads ECDSA key | No (unless `keys export`) | Path `OCCAM_KEYS_ROOT`; **cannot disable mint** | Key exists even if receipts off — **EF-044** | Disk once | `OccamServiceCollectionExtensions.cs:23`; `ReceiptSigner.cs:26-41` |
| 2 | A-PROV | Host start + default | HTTP daemon prewarm | Banner/log | `OCCAM_HTTP_DAEMON_PREWARM=0` | — | Cold-start↓ | `OccamMcpServerRegistration.cs:46` |
| 3 | A-PROV | New WS/Remote session DI | `InstallShared` → `StopAll` prior pool | Latency only | **None** | Availability — **EF-041** | Spike | `BrowserPoolManager.cs:45-48` |
| 4 | A-ROUT | Successful extract path | Post-processors (challenge/login/thin/EQM) may downgrade | Failure codes / `quality` | Partial (domain tiers; not full off) | Trust honesty | — | PostProcessors/* |
| 5 | A-ROUT | `http_then_browser` + public-ref HTTP fail | Skip browser silently | Same failure as HTTP | Domain tiers file | — | Saves browser | `OccamRouter.cs:149-152`; EF-056 |
| 6 | A-SHAPE | Features scope / tool defaults | Inject structured features into workers | Response shape | Tool params (omit) | — | CPU | OccamFeaturesScope / runners |
| 7 | A-SHAPE | Browser extract launch | Stealth + **`bypassCSP:true` always** | **No** | **None** — **EF-046** | Weakens page CSP | — | `browser-session.mjs:143` |
| 8 | A-SHAPE | Browser extract | Consent dismiss + CSS-hide + aggressive retry | No | Partial (`WT_*` / recipe) | May hide real UI | +latency | consent.mjs; browser-session |
| 9 | A-SHAPE | Browser extract default | Virtual-scroll simulation | No | `WT_VIRTUAL_SCROLL=0` | — | +latency | virtual-scroll.mjs |
| 10 | A-PROV | No Chromium binary | Auto-provision user Chromium | `browserProvisioned` (when surfaced) | `OCCAM_BROWSER_AUTOINSTALL=0` | — | First-call cost | `browser-session.mjs:119-136`; browser-provision.mjs |
| 11 | A-SHAPE | Recipe match | Cookie inject / host prune | Recipe path | Recipe registry; `WT_COOKIE_INJECT` | Privacy | — | recipes/*; cookie-inject |
| 12 | A-SHAPE | Playbook interaction plan | `page.evaluate` / `waitForFunction` | Heal/extract path only | No plan ⇒ no exec | **Code-exec surface — EF-046** | — | `interaction-steps.mjs:14`; wait_for.js |
| 13 | A-NET | `OCCAM_SITE_GENOME_FETCH` / tool flag | Live `/.well-known` genome fetch | Resolve fields | Off by default | Network trust | — | WellKnownGenomeFetcher |
| 14 | A-TRUST | `occam_playbook_save` success | Always sign + embed verify score | `SignedKeyId` | **Not** via `OCCAM_RECEIPTS` — **EF-005** | Trust switch incomplete (C6) | — | `PlaybookSaveService.cs:86-105` |
| 15 | A-SHAPE | Ambient client capabilities | Size default `max_tokens` → cache identity | After `client_capabilities` | Env bootstrap / omit call | — | — | ClientCapabilityStore |
| 16 | A-TRUST | Cache-eligible success | Write full post-sign envelope to disk | Transparent (`cached:true` on hit) | `cache_ttl_s`; eligibility | Replays receipt — EF-001/045 | I/O | TranscodeResponseCache; FLOW-019 |
| 17 | A-SHAPE | URL fragment present | Implicit focus without `focus_query` | Section rank | Strip fragment | May collide cache — **EF-045** | — | FocusIntent; FLOW-020 |
| 18 | A-SHAPE | Canonical materialization path | Build then **discard** IR | **None** | — | — | **CPU waste** | TranscodeToCanonical; EF-004 extend |
| 19 | A-ROUT | Search results | Extractability scorer + optional probe fan-out | Scores | Provider env | Honesty gap on paywall | N probes | SearchExtractabilityScorer |
| 20 | A-NET | Robots/throttle env | Polite delay / allow | Rarely | Default **off**; fail-open | Fail-open | Delay | RobotsThrottleService |
| 21 | A-NET | Proxy list configured | Round-robin + spawn side effect | Via egress | Unset list | Empty-file swallows inline | — | ProxyRotation* |
| 22 | A-SHAPE | CSS Nuxt schema attr | `(0,eval)(__NUXT__)` | Extracted fields | No Nuxt attr | **Page-controlled eval — EF-013** | — | css-schema-extract |
| 23 | A-HOST | `OCCAM_LOG` on | Stderr USD savings estimate | Stderr | Off default | — | — | OccamStderrAnsiSink |
| 24 | A-HOST | Banner default | Prints stdio listening line | Stderr | `OCCAM_BANNER=0` | Wrong on WS/Remote | — | BannerModel |
| 25 | A-HOST | `occam refresh` / stop-occam | Name-wide kill of every `OccamMcp.Core[.exe]` | Process death | **No scope flag** — **EF-049** | Collateral | — | `stop-occam-processes.mjs:77-92,135-138`; FLOW-021 |
| 26 | A-HOST | Every `launch-mcp-host` | Merge `~/.occam/onboard.json` env into host | **No** | Edit/delete onboard; explicit env wins | Config integrity — **EF-050** | — | `launch-mcp-host.mjs:29`; `onboard-config.mjs:17-28` |
| 27 | A-HYGIENE | Skill install | `rmSync` dest then copy | No confirm | — | Wipes customized skill | — | install-occam-skill |
| 28 | A-HOST | Marketplace validate success/skip | Auto-merge squash to main | PR merge | Workflow / branch protection? | **Supply chain — EF-052** | — | playbook-marketplace.yml; FLOW-022 |
| 29 | A-ROUT | Docker missing `profiles/` | Silent built-in seed/tier defaults | **No** | Bind-mount profiles | Behavior drift vs Level B | — | Dockerfile vs PlaybookPaths |

**Related routing note (not a separate silent “managed always”):** when `OCCAM_MANAGED_PROVIDER` is set, router may attempt managed after dual fail; managed **failure never wins surface** (EF-056). Success surfaces as ordinary `backend` string — third-party egress is easy to miss if domains unset (all hosts eligible).

---

## 3. Rank by surprise (operator / careful user)

Highest surprise first — writes disk, mints keys, executes page scripts, kills processes, mutates host config, or sends data off-box.

| Rank | # | Why surprising | EF / GAP |
|------|---|----------------|----------|
| 1 | 1 | Always mints signing key even with `OCCAM_RECEIPTS=off` | EF-044 / GAP-005 |
| 2 | 14 | `playbook_save` always signs; master switch lies | EF-005 / C6 |
| 3 | 25 | Refresh kills **all** hosts by binary name, ignores `OCCAM_HOME` | EF-049 / GAP-033 |
| 4 | 26 | Silent onboard env injection every launch | EF-050 / GAP-034 |
| 5 | 3 | New WS/Remote session kills process-wide browser pool | EF-041 / GAP-002 |
| 6 | 7 | Always-on `bypassCSP` — not disableable | EF-046 / GAP-007 |
| 7 | 12 | Playbook `page.evaluate` / waitForFunction JS | EF-046 |
| 8 | 28 | Marketplace can auto-merge community playbooks | EF-052 / GAP-036 |
| 9 | 22 | Nuxt `(0,eval)` on schema attr | EF-013 |
| 10 | 16 | Opt-in cache stores **full signed envelopes** (content + receipt) | EF-001/045 |
| 11 | 27 | Skill install silently wipes destination | — |
| 12 | 8–9 | Consent hide / virtual-scroll mutate DOM invisibly | — |
| 13 | 17 | `#fragment` focuses + can poison cache identity | EF-045 |
| 14 | 29 | Docker silent built-in profiles | GAP-043 |
| 15 | Managed egress | Third-party fetch when provider configured | STATEMENT_6 |

Lower surprise (expected automation): #2 prewarm, #4 post-processors, #5 public-ref skip, #6 features, #10 provision (flagged), #13 genome (opt-in), #15 client budget, #19 search scores, #20–21 politeness, #23–24 stderr cosmetics, #18 dead IR work (perf smell).

---

## 4. Invisible vs reported

### 4.1 Invisible (no / weak MCP disclosure)

| # | Behavior | Where it hides |
|---|----------|----------------|
| 1 | Key mint | Disk only |
| 3 | Pool `StopAll` | Latency |
| 7 | bypassCSP | Page context |
| 8–9 | Consent / virtual-scroll | DOM |
| 18 | Canonical build-discard | CPU |
| 26 | Onboard env merge | Process env |
| 27 | Skill rmSync | Filesystem |
| 29 | Docker built-ins | Resolve behavior |

### 4.2 Partially reported

| # | Signal |
|---|--------|
| 4 | Failure codes / quality |
| 5 | Same failure as HTTP (not “skipped browser”) |
| 6 | Structured fields present |
| 10 | `browserProvisioned` when carried |
| 11–12 | Recipe / heal path metadata |
| 13 | Genome fetch fields on resolve |
| 14 | `SignedKeyId` |
| 15 | Ambient budget after capabilities call |
| 16 | `cached:true` on hit |
| 17 | Section ranking effects |
| 19 | Scores |
| Managed success | `backend` string |

### 4.3 Clearly reported / operator-visible

| # | Channel |
|---|---------|
| 2, 23, 24 | Stderr / banner |
| 25 | Processes die |
| 28 | PR merge in GitHub |

---

## 5. Controllability matrix

| Tier | Behaviors | How |
|------|-----------|-----|
| **Fully disableable** | #2 prewarm, #9 virtual-scroll, #10 autoinstall, #13 genome (default off), #16 cache (omit TTL), #20 robots (default off), #23 log, #24 banner | Env / omit param |
| **Partially disableable** | #4 post-processors (tiers), #6 features (omit params), #8 consent (WT/recipe), #11 recipes (no match), #12 (no interaction plan), #15 (omit capabilities), #17 (strip `#`), #19 (no search), #21 (unset proxy), #22 (no Nuxt attr), managed (unset provider) | Incomplete knobs |
| **Not disableable** | #1 key mint, #3 InstallShared kill, #5 public-ref skip (policy), #7 bypassCSP, #14 save-always-sign, #18 IR discard, #25 name-wide kill, #26 onboard merge (while file exists — delete file to stop), #27 skill wipe on install, #28 marketplace (CI), #29 Docker built-ins | EF-flagged or product-hard |

---

## 6. Documentation duty (MUST vs invisible OK)

### MUST disclose in public docs (privacy, trust, security, host integrity)

| # | Why MUST |
|---|----------|
| 1 + 14 | `OCCAM_RECEIPTS` is **not** a complete master switch (C6); operators must know key mint + save signing |
| 7 + 12 | CSP bypass + playbook JS execution change the security model of “fetch page” |
| 16 | Opt-in cache retains full page + signed envelope on disk |
| 25 | Refresh collateral kill across installs |
| 26 | Onboard.json silently becomes host env |
| 3 | Multi-session / WS users hit pool kill |
| Managed egress | Third-party data leave when provider configured |
| 28 | Community supply-chain auto-merge (operator/maintainer docs) |
| 22 | Nuxt eval footgun for schema authors |
| ST footprint | Sessions/`_imports`/batch/watch retention (cross-ref STATE-MODEL) |

### Safely invisible implementation detail

| # | Why OK if undocumented at product surface |
|---|-------------------------------------------|
| 2 | Perf warm-up; env exists |
| 6 | Follows explicit tool params |
| 18 | Dead IR CPU cost — engineering debt, not user-facing semantics |
| 23–24 | Stderr cosmetics |
| 4 | Already surfaced via failure/quality taxonomy |
| 5 | Correct cascade policy (document cascade truth, not every short-circuit as “magic”) once handbook matches EF-056 |
| 20–21 | Off/default fail-open — document when enabling, not when unused |

---

## 7. Cross-references

| Source | Use |
|--------|-----|
| `AUTOMATIC-BEHAVIORS.md` | Raw 29-row input |
| `FAILURE-BEHAVIOR-MAP.md` | Cascade / fail-open / cache TTL delete-on-read |
| `CONFIG-NEGATIVE-SPACE.md` | Dual `OCCAM_RECEIPTS` parse; incomplete master |
| `ENVIRONMENT-VARIABLES.md` | Control knobs |
| `STATE-MODEL.md` | Disk side-effects of #1/#16/#26 and retention |
| `negative-space/*-blind.md` | Discovery provenance (AGENT-LOCAL) |
| EF-005, EF-013, EF-041, EF-044, EF-045, EF-046, EF-049, EF-050, EF-052 | Flagged automatics |

---

## 8. Taxonomy note

Automation is a **cross-cutting lens** across PS-1 (routing), PS-2 (shaping/cache), PS-5/6 (sign/genome), PS-8 (DI/pool), PS-9 (host mutation). Do not fold into a tenth system — attach class tags to behaviors.

---

## 9. Corrections to prior model

1. Wave-1–3 understated danger of **already-known** automatics (Wave-4 AUTOMATIC conclusion) — this file is the model layer.
2. “Receipts off” does **not** stop key mint or playbook_save signing (C6).
3. Cascade docs that imply density-ranked managed last-rung are **wrong** (C1 / EF-056); public-ref skip (#5) is intentional policy.
4. Atlas is **not** a silent cross-session leak (EF-024 WITHDRAWN) — omit from surprise rank as “hidden durable telemetry.”

---

## 10. Uncertainty

| Item | Status | Resolve by |
|------|--------|------------|
| Marketplace branch-protection actually blocking auto-merge | Outside repo (EF-052) | Org settings check |
| Exact MCP field for every managed-provider attempt vs success-only `backend` | Source: success surfaces backend | Optional response-field audit |
| Whether consent CSS-hide has a single master off switch | Partial `WT_*` | Worker env matrix |
