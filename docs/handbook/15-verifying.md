# Chapter 15 — Verifying: five modes, two surfaces, four asymmetries

**Status:** STABLE · **Prerequisites:** [Chapter 14](14-what-a-receipt-proves.md)

---

## Mental model

**Verification is arithmetic over bytes and keys.** No verdict in this codebase is about truth, origin, identity, or trusted time. The MCP tool `occam_verify` and the host-binary CLI `occam verify` implement the same math on different surfaces — and they are not interchangeable.

---

## Explanation

Occam ships six distinct proof kinds across two programs:

| Surface | Modes | What they check |
|---------|-------|-----------------|
| MCP `occam_verify` | `offline` (default), `live`, `prove`, `citation`, `history` | Envelope signature, optional content hash, Merkle membership, watch-chain links |
| Host CLI `occam verify` | `receipt`, `citation`, `manifest`, `history` | Same offline math plus dataset manifest binding; **requires `--pubkey`** |

### Five MCP modes

1. **`offline`** — Recompute canonical bytes, verify ECDSA signature, optionally check `contentHash` against supplied markdown. Time anchor is reported but **non-gating** on MCP.
2. **`live`** — Re-fetch `finalUrl` through a bare anonymous pipeline (no session, no playbook, no token budget) and compare hashes. `drifted` often means "my re-fetch lacked the original's context," not "the page changed."
3. **`prove`** — Emit a citation package `{leaf, proof[]}` for block index *i* after verifying leaves reconstruct the signed root.
4. **`citation`** — Verify Merkle membership plus envelope signature for a supplied citation package.
5. **`history`** — Verify watch-chain link integrity and per-entry signatures. **`history_verified` requires every entry signed and verified**; unsigned chains report chain integrity separately (`chainIntegrity` / `signatureStatus`), not `history_verified`.

### CLI-only `manifest`

Dataset manifest verification exists **only** on the CLI. A pure-MCP agent cannot verify a dataset export manifest in-band.

### Four asymmetries (name them when you verify)

1. **`manifest` is CLI-only** — MCP agents structurally cannot verify dataset manifests.
2. **`live` and `prove` are MCP-only** — the CLI has no equivalent.
3. **Time anchor gates the CLI verdict but not the MCP verdict** — a broken anchor yields `verified` over MCP and exit `1` on the CLI.
4. **`--pubkey` is mandatory on the CLI, optional on MCP** — MCP defaults to the running host's own key, so verifying a foreign receipt without `public_key` reports `signature_invalid` or `wrong_key`, not "foreign author."

### Neither surface via the operator wrapper

The friendly `occam` wrapper has no `verify` or `keys` subcommand. Invoke the host binary directly:

```powershell
dotnet run --project src\FFOccamMcp.Core -- verify --mode receipt --pubkey pub.pem --receipt r.json
# or the published AOT binary with the same verb
```

Unknown verify modes on MCP silently downgrade to `offline` and the response claims `"mode":"offline"`.

### Verdict vocabulary

Write **"the signature did not validate under this key"** — not "verified against the wrong key" vs "tampered," which the vocabulary cannot distinguish on older paths. Phase 6 added `wrong_key` / `key_mismatch` on some surfaces; still no verdict about truth.

---

## CHECK

**LOCAL.** Hand a receipt and exported public PEM to a colleague. They run:

```powershell
# CLI — pubkey mandatory
occam-host verify --mode receipt --pubkey colleague.pem --receipt receipt.json
```

Then run the same receipt through MCP `occam_verify` **without** `public_key`. Observe `signature_invalid` or `wrong_key` because MCP used the local host's key.

**LOCAL (Phase 6 behavior).** Build a watch history, strip every `Sig` field, rebuild `prevEntryHash` if needed, and verify. Expect chain integrity reporting without `history_verified` — not exit 0 / `history_verified` on an unsigned chain.

---

## Common misconception

**"`live` mode proves whether the page changed."** The re-fetch drops session profile, playbook overlay, content selectors, token budget, and backend pin. `drifted` usually means the verification fetch lacked the original's context. Every re-fetch failure collapses to `refetch_failed` with no failure code.

---

## Limitations

- No verdict proves truth, accuracy, origin, identity, or trusted time.
- MCP `occam_verify` defaults to the local key — not a third-party verification surface unless you pass `public_key`.
- `live` mode is not reproducible without network and is context-blind.
- CLI trust verbs are unreachable through `occam verify` on the operator wrapper.
- Crosscheck verdicts and attest aggregates have **no verify mode** — they are unsigned.
- Time anchor validity gates CLI exit codes but not MCP verdicts.

---

## Links

- [Chapter 14 — What a receipt proves](14-what-a-receipt-proves.md)
- [Chapter 16 — Evidence for claims and corpora](16-evidence-for-claims.md)
- [Chapter 18 — Exposure](18-exposure.md) — `reader` profile and verify visibility
- [appendix-status-labels.md](appendix-status-labels.md)
- User docs: [Receipt verification](../receipts.md) · [occam_verify tool](../tools/occam_verify.md)
- Contract: `MCP_API_SPEC.md` · Source audit: `docs-audit/TRUST-MODEL.md` §7–§8
