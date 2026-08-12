# Chapter 23 — Security posture and threat model

**Status:** STABLE · **Prerequisites:** [Chapter 6](06-when-acquisition-is-hard.md), [Chapter 13](13-typed-field-extraction.md), [Chapter 14](14-what-a-receipt-proves.md), [Chapter 20](20-automatic-behaviors.md), [Chapter 21](21-state-and-footprint.md)

---

## Mental model

**Everything above the signature boundary is asserted; everything below it is tamper-evident.** The host binary is fully trusted and the whole model rests on it. Occam's trust layer defends against post-hoc edits and quoted-block tampering — not against a lying origin, a compromised host, or prompt injection in page content.

---

## Explanation

### Trust boundary (summary)

```
Origin server → can lie, cloak, inject
     ↓
Acquisition (HTTP / browser / managed / css-extract)
     ↓
Host compile + sign  ← FULL TRUST required here
     ═══ SIGNATURE BOUNDARY ═══
Consumer verify     ← tamper-evidence only
```

### In scope — genuinely addressed

| Threat | Mechanism |
|--------|-----------|
| Post-hoc edit of receipt or markdown | ECDSA + contentHash recheck |
| Post-hoc edit of quoted block | Merkle membership under signed root |
| Dataset row reordering / insertion | Manifest root over ordered rows |
| Watch history link tampering | Hash chain (when entries signed) |
| Canonicalizer drift | Fixed-order golden vector in gate |
| Hostile verifier input | Typed parse failures |
| SSRF on time-anchor outbound call | Private-host refusal + connect guard |

### Out of scope — named explicitly

| Threat | Why not |
|--------|---------|
| Compromised host | Host chooses every field and holds the key |
| Operator-controlled key | Can sign anything, back-date `ts` |
| Lying origin / cloaking | Faithfully hashed and signed as if genuine |
| Prompt injection in content | Injected text is signed like real content; `tag_trust` is off-by-default heuristic **outside** signature |
| Playbook / browser code execution | `bypassCSP:true` + `page.evaluate` / `waitForFunction` |
| Managed-provider intermediary | Third party sees URL; content may be signed as normal extract |
| Key distribution / identity / rotation | TOFU only; no PKI |
| Key at rest | Unencrypted PKCS8; weak Windows hardening |
| Multi-party attestation | Does not exist |
| Host supply chain | Mutable bootstrap delivery (T4); Cosign required only when `signaturePolicy=required-cosign-v1`; marketplace auto-merge risk; Docker health does not prove extract readiness |
| Session bleed in pooled browser | Anonymous contexts not a security boundary |

### Surfaces: do not point at untrusted URLs yet

1. **css-extract** — no DNS pin, no body cap on worker path.
2. **Nuxt `readNuxtPath`** — `(0,eval)` over page-controlled state.
3. **Pooled anonymous browser contexts** — not isolation boundaries for credentials.

Typed extraction ([Chapter 13](13-typed-field-extraction.md)) must carry these banners in the same breath as the tool name.

### Red-team your workflow

Ask: who could make this lie? Origin cloaking, compromised host, operator key, hostile playbook, managed intermediary, prompt injection in the page. None are detected by signatures alone.

---

## CHECK

**LOCAL.** Point `occam_extract_knowledge` at a local HTTP server on a private IP and compare with `occam_transcode` against the same target. Observe differential network-safety behavior — css-extract path lacks the same guards.

**LOCAL.** Read a signed receipt's markdown containing obvious injection text — note it hashes and Merkle-proves like any other content.

---

## Common misconception

**"`tag_trust` protects against prompt injection."** It is an off-by-default heuristic annotation requiring `json_blocks`; the tag is carried **outside** the signature. Injected text is hashed, signed, and Merkle-provable exactly like real content.

---

## Limitations

- No truth, origin, identity, or trusted-time claims from the trust layer.
- Self-signed keys are machine-local integrity logs — not auditor identity proof.
- Community playbooks: sha256 integrity only — not authenticated authors (OD-1).
- Install integrity: sha256 manifest only — not cosign-verified supply chain (OD-2).
- Crosscheck is multi-source comparison only — not security proof.

---

## Links

- [Chapter 13 — Typed extraction](13-typed-field-extraction.md)
- [Chapter 14 — Receipts](14-what-a-receipt-proves.md)
- [Chapter 17 — Opt-in surfaces](17-opt-in-surfaces.md)
- [Chapter 21 — State and footprint](21-state-and-footprint.md)
- User docs: [Trust and safety](../trust-and-safety.md) · [Security policy](../trust/security-policy.md)
- Audit: `docs-audit/TRUST-MODEL.md` §5, §10 · `docs-audit/PRODUCT-VS-ENGINEERING.md`
