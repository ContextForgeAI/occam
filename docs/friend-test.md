# Friend test — Occam usability package (Phase 8K)

**Audience:** A friend with zero prior Occam knowledge.  
**Rules:** Use only public docs (README, docs site, `llms.txt`). Do **not** coach beyond what the docs say. Record confusion verbatim.

**Status:** Review artifact — **not** in MkDocs nav; excluded from site build via `mkdocs.yml` `exclude_docs`.

---

## Before you start

- Fresh machine or VM preferred (or note existing Node/Occam state).
- One supported MCP host installed (Cursor, Claude Desktop, etc.).
- Block ~45–60 minutes.
- Keep a simple log: timestamp, doc page, what you tried, what confused you.

---

## Checklist

### 1. Install

- [ ] Start at [README.md](https://github.com/ContextForgeAI/occam/blob/main/README.md) only.
- [ ] Run the bootstrap for your OS (Linux/macOS curl or Windows irm).
- [ ] Note: Did the doc say npm is GA? (Expected: **no**.)
- [ ] Record: install time, any prompt you did not expect.

### 2. Doctor

- [ ] Find what `doctor` does without asking the friend — search docs for “doctor”.
- [ ] Confirm Playwright/Chromium step completed or note failure message.
- [ ] Record: was doctor mentioned in README vs only INSTALL/quick-start?

### 3. Connect

- [ ] Run `occam connect` (or follow installer connect output).
- [ ] Follow restart/trust/paste instructions from docs only.
- [ ] Record: host tier (live / config / assisted) and whether rollback was explained.

### 4. First read

- [ ] Ask connected agent (or call tool): read `https://example.com`.
- [ ] Check `ok: true` and non-empty markdown.
- [ ] Record: did you understand what a receipt is from first-read docs alone?

### 5. JS-hard page

- [ ] Using docs only, find how to read a client-rendered SPA.
- [ ] Try default `http_then_browser`, then `backend_policy=browser` if needed (use a doc-suggested URL or a SPA you know).
- [ ] Record: how many doc hops to find browser escalation?

### 6. Find sessions

- [ ] Using docs only, locate where session profiles live on disk.
- [ ] Find the three session tiers (full browser vs headers-only).
- [ ] Record: did you find `OCCAM_SESSIONS_ROOT` / `~/.occam/sessions` without handbook?

### 7. Proxy

- [ ] Using docs only, find how to set `OCCAM_HTTP_PROXY` / `OCCAM_HTTPS_PROXY`.
- [ ] Record: did networking.md clarify path-scoped proxy limits?

### 8. Verification

- [ ] Find how to verify a receipt (MCP or CLI).
- [ ] Record: did docs state CLI `--pubkey` requirement vs MCP local-key default?

### 9. Explain Receipt in own words

Ask the friend to write **2–3 sentences** on what a receipt proves and what it does **not** prove — without quoting docs.

**Scoring guide (reviewer):**

| Pass | Fail |
|------|------|
| Mentions integrity vs a key / tampering | Says “proves the page is true” or “proves origin” |
| Mentions out-of-band key | Says “cryptographic proof of fetch” |

### 10. Find one experimental capability unaided

- [ ] Without being told env var names, find **one** of: watch, batch, crosscheck, failure atlas.
- [ ] Record: doc path taken and whether enablement gate was co-located with the feature name.

### 11. Confusion log (required)

Free-form bullets:

- Pages that contradicted each other
- Terms undefined on first use (`thin_extract`, `playbook_policy`, `storageState`, …)
- Expected rollback/safety that docs denied
- Anything that felt like “hidden automation”

---

## Reviewer summary template

```text
Friend test — YYYY-MM-DD
Install: PASS / FAIL — notes
Connect: PASS / FAIL — notes
First read: PASS / FAIL
JS page: PASS / FAIL — hops: N
Sessions: PASS / FAIL
Proxy: PASS / FAIL
Verification: PASS / FAIL
Receipt explanation: PASS / FAIL — quote friend
Experimental find: PASS / FAIL — which feature, hops: N
Top 3 confusions:
1.
2.
3.
Overall: PASS / FAIL
```

---

## Expected doc entry points (reviewer reference — do not give to friend)

| Task | Canonical doc |
|------|----------------|
| Install | README → INSTALL / quick-start |
| Connect | quick-start §2, mcp-hosts |
| First read | README, quick-start §4–5 |
| JS / browser | examples/difficult-js-page, acquisition |
| Sessions | sessions.md, guides/sessions |
| Proxy | networking.md, configuration |
| Verify | guides/verify-sources, receipts |
| Experimental | experimental.md |
| Automation / disk | handbook/20, handbook/21 |

---

## MkDocs note

This file lives at `docs/friend-test.md` for repo colocation but is **excluded** from the published site (`exclude_docs` in `mkdocs.yml`). It is not linked from nav or index — intentional review artifact.
