# How Occam works

This page is the **user mental model**. Contributor architecture lives in the semantic contract and source tree.

## Normal flow

```text
AI Agent
   → MCP (stdio or WebSocket)
   → Occam host
   → HTTP and/or browser extraction
   → clean Markdown / structured output
   → optional signed receipt
   → AI Agent
```

Everything after “AI Agent asks” runs **on your machine** in the default install.

## Pieces in plain language

| Piece | Role |
|-------|------|
| **MCP** | How your AI app talks to Occam (tools over a local connection) |
| **Occam host** | Routes the request, applies budgets/playbooks, signs receipts |
| **HTTP extraction** | Fast fetch + HTML→Markdown without a full browser |
| **Browser extraction** | Playwright Chromium when the page needs a real render |
| **Playbooks** | Optional per-site recipes (selectors, structured schemas) |
| **Sessions** | Local cookie/profile files for login walls you already unlocked |
| **Receipts** | Signed proof of what was extracted (URL, time, hashes, backend) |

## HTTP vs browser

- Start with the default policy (`http_then_browser`): try HTTP first, escalate when the extract is thin or failed.  
- Use browser-only when you already know the site is a heavy SPA.  
- A definitive HTTP 404/410 is not “fixed” by opening a browser.

Details: [Concepts](concepts.md).

## Playbooks

A playbook is a site-specific recipe. Occam can resolve one automatically (`playbook_policy=auto`) or ignore them (`off`). Authoring (heal → lint → save) is for people fixing hard sites — not the default read path.

## Sessions

Occam does not solve CAPTCHAs or log in for you. If you already have a browser session, export it to a local profile and pass `session_profile`. Profiles stay on disk under your control.

## Receipts vs honesty

- **Honesty** — behavior: `ok: false` means unknown content; never invent the page.  
- **Verification** — mechanism: a receipt lets another process check hashes and signatures later.

Human overview: [Receipts](receipts.md) · Normative bytes: [Receipt verification](receipt_verification.md)

## Next

- [Your first web read](getting-started.md)
- [Trust & Safety](trust-and-safety.md)
- [Semantic contract](architecture/semantic-contract.md) (developers)
