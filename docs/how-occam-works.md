# How Occam works

A plain-language architecture overview. Deep internals live in the [Handbook](handbook/index.md).

## The path of a read

```text
Agent / operator
      │
      ▼
 MCP (stdio / WS)  or  CLI
      │
      ▼
 Safety + session preflight
      │
      ▼
 Acquisition ladder
   HTTP extract ──► (if unusable) Browser extract
                 ──► (if both fail, optional) Managed provider
      │
      ▼
 Post-processors (challenge / login / thin extract …)
      │
      ▼
 Materialization (token budget, focus, blocks/tables/chunks …)
      │
      ▼
 Response: ok:true + markdown  |  ok:false + failure.code
      │
      └─ optional signed receipt / Merkle block commitments
```

## Acquisition (gated ladder)

Defaults matter:

- A **usable HTTP success stops** — Occam does not always open a browser.  
- **Thin**, challenge-like, or certain non-terminal failures may escalate to browser.  
- **404 / 410** short-circuit (no pointless browser chase).  
- Some public-reference hosts short-circuit on failed HTTP.  
- On dual local failure, Occam surfaces the more informative local outcome (`FailureRanking`) — never a managed-provider failure as the user-facing result.  
- **Managed providers** are opt-in and run only after local failure in the cascade. They are **not** a `backend_policy` enum value.  
- Occam does **not** solve CAPTCHAs.  
- Private-IP / SSRF protections apply on specific paths; see [networking](networking.md).

Full contract: [Acquisition](acquisition.md).

## Materialization

The object Occam returns is **compiled** content for a context window — not the raw origin bytes.

- Primary output: Markdown  
- Sized by token budget (explicit `max_tokens` or ambient client capabilities)  
- Optional focus prune, structured blocks/tables, chunks, diffs  
- Receipt content hashes bind the **compiled** form  

Why: fit useful content into an agent context window. See [Materialization](materialization.md).

## Parallel / special paths

| Path | Role |
|------|------|
| Probe / map / search | Cheap discovery before a full read |
| Digest | Several URLs → one combined answer |
| Playbooks | Per-site extraction recipes (overlay on acquire/materialize) |
| extract_knowledge | Typed fields via schema (separate CSS worker path) |
| claim_check / attest | Evidence lookup / heuristic citation assessment |
| verify | Offline / live / citation / history checks on integrity artifacts |
| Watch / batch / crosscheck / atlas | Experimental, env-gated |

## Trust artifacts (optional)

A successful cryptographic check proves: **the checked bytes match what the holder of the referenced key signed.**

It does **not** prove the page was truthful, the source authentic, the signer’s real-world identity, or an externally trusted timestamp.

Playbook signatures: **v2** signs the trust-relevant gate snapshot (tamper-evident heuristic); **v1** leaves those fields outside the signed boundary. Details: [Trust & verification](trust-and-safety.md) · [Receipts](receipts.md).

## Next

- [Acquisition](acquisition.md)
- [Materialization](materialization.md)
- [Choosing a tool](choosing-a-tool.md)
- [Handbook](handbook/index.md)
