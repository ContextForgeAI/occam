# What is Occam?

Your AI is good at reasoning. It is bad at silently inventing what a web page said.

Occam is a **locally run host** that sits between your AI and the public web. It acquires real page content, shapes it for a model’s context window, returns a typed refusal when the content is unknown, and can optionally attach integrity artifacts you can verify later against a key.

```text
Your AI
   |
   | MCP / CLI
   v
 Occam (local host)
   |
   +-- acquires the page (HTTP → browser → optional managed)
   +-- materializes token-budgeted Markdown / structured fields
   +-- returns ok:false when content is UNKNOWN
   +-- optionally signs what it produced (integrity vs a key)
```

## The problem

Without a dedicated extract layer, agents either:

- skip the fetch and **hallucinate** from training memory, or  
- dump raw HTML into the context window and waste tokens  

Neither is trustworthy for research, citations, or automation.

## What happens on a normal read

1. The AI asks Occam to read a URL (usually `occam_transcode` with only `url`).  
2. Occam acquires the page **now** through a gated ladder (HTTP first; browser when the HTTP result is unusable; optional managed provider only after both local attempts fail).  
3. Usable content is **materialized**: budgeted, optionally focused, optionally structured.  
4. On success: `ok: true` plus Markdown (and optional structured sidecars). A signed **receipt** may be attached.  
5. On failure: `ok: false` with a typed `failure.code` — content is **unknown**, not guessed.

Live extraction is the default. An opt-in response cache (`cache_ttl_s`) can replay a prior materialization on the same machine — it is not a CDN, and it is off unless you set it.

## Beyond one URL

Occam also supports:

- **Discovery** before a full read — probe, map, search  
- **Several URLs** in one call — digest  
- **Site recipes (playbooks)** — resolve, heal, lint, save  
- **Typed fields** — extract_knowledge (needs a schema)  
- **Evidence lookup / citation assessment** — claim_check, attest (heuristic — not truth proof)  
- **Integrity checks** — receipts, verify, dataset export  
- **Experimental** opt-ins — watch, crosscheck, batch, failure atlas  

## What it does **not** promise

| Not this | Reality |
|----------|---------|
| Proves the page was true | Signatures prove **integrity relative to a key** |
| Proves origin authenticity / identity | Local self-minted key; no PKI / registry |
| Trusted timestamps | Signer clock only (optional TSA is separate and limited) |
| CAPTCHA solving | Detects walls; does not bypass them |
| Universal npm install | **npm is not a GA 1.0 channel** |
| Cosign-enforced install | Installers check **SHA-256** vs the release manifest |
| Marketplace “trusted auto-merge” | Community automation is operational machinery, not a trust guarantee |

## Who it is for

| You | Occam helps you |
|-----|-----------------|
| Person using Cursor / Claude / Codex / Hermes | One install, then ask the agent to read the web |
| Operator | Local install, connect, doctor, sessions, packaging |
| LLM agent | Typed tools, honest failures, compact `llms.txt` map |
| Auditor | Verify receipts offline without trusting the chat transcript |

## Next

- [Quick Start](quick-start.md)
- [How Occam works](how-occam-works.md)
- [Trust & Safety](trust-and-safety.md)
- [Handbook](handbook/index.md)
