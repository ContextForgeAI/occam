# What is Occam?

Your AI is good at reasoning. It is bad at silently inventing what a web page said.

Occam is the **local web and data layer** that sits between your AI and the public web: it fetches the real page, cleans it for the model, returns compact context, and can prove what was extracted.

```text
Your AI
   |
   | MCP
   v
 Occam
   |
   +-- fetches live web content
   +-- cleans it for the model
   +-- returns structured context
   +-- attaches provenance / receipts
   +-- reports failures explicitly
```

## The problem

Without a dedicated extract layer, agents either:

- skip the fetch and **hallucinate** from training memory, or  
- dump raw HTML into the context window and waste tokens  

Neither is trustworthy for research, citations, or automation.

## What happens on a normal read

1. The AI asks Occam to read a URL (usually `occam_transcode`).  
2. Occam fetches the source **now** (no file cache by design).  
3. It extracts useful content (HTTP worker, or browser when needed).  
4. It returns compact Markdown (and optional structured fields).  
5. On success it can attach a **signed receipt** — cryptographic provenance.  
6. On failure it returns `ok: false` with a typed `failure.code` — content is **unknown**, not guessed.

You do not need to know Native AOT, JSON-RPC, or ECDSA to use it. Those details live under [How Occam works](how-occam-works.md) and [Developers](architecture/semantic-contract.md).

## Who it is for

| You | Occam helps you |
|-----|-----------------|
| Person using Cursor / Claude / Codex / Hermes | One install, then ask the agent to read the web |
| Operator | Local install, connect platform, configuration knobs |
| LLM agent | Typed tools, honest failures, receipts, compact `llms.txt` map |
| Auditor | Verify receipts offline without trusting the chat transcript |

## What it is not

- Not a hosted scraping SaaS you must send traffic through  
- Not a CAPTCHA solver  
- Not “supports every MCP client in the world” — see honest [host tiers](mcp-hosts.md)

## Next

- [Quick Start](quick-start.md)
- [How Occam works](how-occam-works.md)
- [Trust & Safety](trust-and-safety.md)
