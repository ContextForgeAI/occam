# Access and consent handling

**Slug:** `access-consent` · **Product system:** PS-1 Acquisition · **CAPs:** 11 · **Public relevance:** HIGH

**Member CAPs:** CAP-095, CAP-096, CAP-107, CAP-179–CAP-181, CAP-183, CAP-210–CAP-213  
**Product capability:** CAP-095  
**Engineering findings:** None on family ledger (related EF-046 bypassCSP is browser-acquisition).

## What it is

Detection of **login walls / challenge interstitials**, optional **consent-banner dismissal**, and semantic **access** outcome mapping — without claiming universal bypass of CAPTCHA, paywalls, or anti-bot systems.

## Why it exists

Give agents typed refusals (`captcha_or_challenge`, `requires_login`) and mild browser UX help (cookie banners) so they do not treat challenge HTML as page content.

## User-visible entrypoints

| Surface | Role | Evidence |
|---------|------|----------|
| Post-processors after router | Challenge → RequiresLogin → Thin | CAP-094 ordering; PP orders 100/150/200 |
| Router success gate | Pre-escalation challenge/thin check | `OccamRouter.cs:188-199` |
| Browser worker | Consent dismiss, challenge fail-fast, stealth | CAP-210–212, CAP-209 |
| Probe AccessClassifier | Shared login/access signals | CAP-096/181 |
| Response `access` semantic field | CAP-107 mapping | `OccamTranscodeTool` semantics |

## Core behavior

### Challenge detection (CAP-095 / CAP-179 / CAP-210)

Two layers (independent):

1. **Router / ChallengePagePostProcessor:** markdown ≤2000 chars + challenge keywords → `captcha_or_challenge` (`ChallengePagePostProcessor`; router threshold mirrors PP — A5).
2. **Browser Q-019 fail-fast:** DOM markers (Cloudflare/Turnstile/hCaptcha/reCAPTCHA) + near-zero readable text (&lt;200 chars) → typed failure without burning full budget (`browser-session.mjs:76-105,544-570`).

A long article that merely *mentions* “captcha” is not classified as interstitial (length guard).

### Requires login (CAP-096 / CAP-181 / CAP-182)

- Shared `AccessClassifier` Restricted → login-wall semantics.
- `RequiresLoginPostProcessor` (order 150): if no `session_profile`, may convert to `requires_login`.
- Skipped when `session_profile` set (operator-supplied session).

### Consent (CAP-211 / CAP-212 / CAP-178)

- Site-agnostic CMP selectors (OneTrust, Cookiebot, …) click dismiss across frames.
- CSS-hide fallback for unresolved overlays; also strip-consent on HTTP-style preprocess.
- Aggressive mode via recipe / `consentAggressive`.

### Recipe cookies (CAP-213)

`WT_COOKIE_INJECT=1` + playbook recipe `cookies[]` — privacy-reviewed per domain; distinct from `session_profile`.

### Semantic outcomes (CAP-107)

Maps Access / Focus / Completeness into response fields — access channel reflects wall classification, not content quality (quality family owns thin).

### Access evidence (CAP-183)

Leak-resistant collection — avoids dumping sensitive page bits into diagnostics.

## Advanced behavior

| Behavior | Notes |
|----------|-------|
| Stealth Lean-A | Supports consent path but is **not** CAPTCHA solve (browser-acquisition CAP-209) |
| bypassCSP always | Silent; security finding EF-046 — not a product “bypass” feature |
| Dual anti-bot detectors | CAP-179 — host PP vs worker DOM |

## Automatic / silent behavior

| Behavior | Automation |
|----------|------------|
| Consent dismiss + CSS-hide | AUTOMATION #8 — may hide real UI |
| Challenge keyword scan on short MD | Router + PP |
| RequiresLogin when no session | Automatic conversion |

## Parameters

| Name | Default | Effect |
|------|---------|--------|
| `session_profile` | unset | Skips RequiresLogin PP conversion |
| (no `solve_captcha`) | — | **Does not exist** |
| Recipe / `WT_*` | off | Consent aggressiveness / cookie inject |

## Configuration

| Knob | Default | Effect |
|------|---------|--------|
| `WT_COOKIE_INJECT` | off | Recipe cookies |
| Recipe `consentAggressive` / selectors | per host | Stronger dismiss |
| Post-processor registration | always on for pipeline | Orders 100/150/200 |

## Backends

Detection works on markdown from any backend; consent/challenge DOM logic is **browser-worker** specific. HTTP relies on markdown keyword/classifier paths.

## Sessions / state

Consent clicks mutate the live page in the browser context. Session profiles change login PP behavior. No durable “consent solved” store.

## Network behavior

Does not open extra network for CAPTCHA solving. Challenge pages still consumed bandwidth for the failed extract.

## Artifacts produced

Typed failures; optional access semantic field; access evidence (redacted). No captcha-token artifacts.

## Trust / provenance properties

`ok:false` + challenge/login codes mean **content unknown** — agents must not invent page text (`AGENTS.md` trust rule). Consent dismiss is best-effort UX, not a legal consent record.

## Failure / fallback behavior

| Condition | Code | Next step for agent |
|-----------|------|---------------------|
| Short challenge MD | `captcha_or_challenge` | session / different URL / stop — **no heal-as-content** |
| Login wall, no session | `requires_login` | export-state → `session_profile` |
| Login wall with session | other http/challenge codes | fix cookies; do not assume PP will flip |
| Real captcha widget on content page | usually not fail-fast | may still extract surrounding content |

## Platform differences

None specific to classifiers. Browser consent runs wherever Playwright runs.

## Composition with other capabilities

- Runs after `acquisition-routing` success candidates; may flip ok→fail.
- Thin extract is **separate** PP (quality-failure-semantics) order 200.
- Session-fetch supplies the lever for login walls.
- Browser-acquisition owns stealth/bypassCSP.

## Known limitations

- **No CAPTCHA solving**; no paywall bypass product.
- Consent automation can hide legitimate UI.
- Keyword detectors are heuristic (language coverage limited; one Russian phrase in browser list).
- Name “access” must not be read as authorization/IAM.

## Engineering findings

None family-primary. Related: **EF-046** (bypassCSP), browser pool session bleed (EF-002) affecting authenticated extracts.

## Code evidence

- `PostProcessors/ChallengePagePostProcessor.cs`, `RequiresLoginPostProcessor.cs`
- `Routing/OccamRouter.cs:185-199`
- `workers/browser-extract/lib/consent.mjs`, `browser-session.mjs` challenge probes
- `docs-audit/subsystems/network-fetch-proxy.md` §4
- `docs-audit/ACQUISITION-ROUTING-MODEL.md` Difficult acquisition playbook

## Public-doc relevance

**HIGH.** Explicit non-goals (CAPTCHA/paywall) and typed failure guidance are mandatory for agent honesty.

## Handbook relevance

Troubleshooting walls; recipe “do not heal captcha”; session export for login.
