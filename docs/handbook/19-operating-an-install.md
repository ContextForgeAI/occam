# Chapter 19 — Operating an install

**Status:** STABLE · **Prerequisites:** [Chapter 3](03-standing-up-an-install.md), [Chapter 18](18-exposure.md)

---

## Mental model

**The operator surface changes the machine, not just Occam.** It writes runtime assets, mutates up to fifteen third-party host config files, authors credential-bearing profiles, and kills processes. Treat every operator verb as having blast radius outside the install tree.

---

## Explanation

The operator path is **first-class product surface** — parallel to MCP tools, not a footnote.

### Core operator verbs

| Verb | What it does | Blast radius |
|------|--------------|--------------|
| `occam doctor` | npm, Playwright, dotnet publish readiness | May install browser bits and packages |
| `occam connect` | Detect + mutate host MCP configs (≤15 adapters) | Third-party config files + `.occam-bak` siblings |
| `occam onboard` / `settings` | Writes `~/.occam/onboard.json` | Merged into **every** later launch via `launch-mcp-host` |
| `occam refresh` / `restart` | Stops hosts launched from this `OCCAM_HOME`, then re-runs the doctor path | Processes whose executable or command line resolves inside this install tree |
| `occam session` | Import/export session profiles | Plaintext cookie retention under `_imports/` by default |
| `occam smoke` | Live extract smoke | Network |
| `occam update` | Release fetch | Network |
| `occam disconnect` | Removes only host registrations owned by this install | Third-party config files; unrelated entries are preserved |
| `occam uninstall` | Disconnects managed hosts and removes a recognized release install | Generated launchers and install tree; state/cache removal is explicit |
| `occam snippet` / `help` / `status` / `control` / `contract` / `skill` | Info, control, skill install | Skill install `rmSync`s destination |

### Host-binary offline verbs (not via `occam` wrapper)

Invoke the published host binary or `dotnet run --project src\FFOccamMcp.Core --`:

| Verb | Purpose |
|------|---------|
| `keys export` | Export public PEM from key store |
| `verify` | Offline receipt/citation/manifest/history verify (**`--pubkey` required**) |
| `install-browser` | Playwright Chromium install |
| `version-surface` | Host version JSON (distinct from `occam contract`) |

The operator wrapper exits with "unknown command" for `verify`, `keys`, and `install-browser`.

### Install paths

- **Level A / Level B tarball (published `v1.0.0-rc.2`):** Legacy public install. Integrity check is **sha256 manifest**; Cosign is not required on that channel.
- **`1.0.0-rc.4` (published; public default):** self-contained archives (`runtimeLayout=self-contained-v1`) with SHA-256 plus Cosign when `signaturePolicy=required-cosign-v1` (requires the `cosign` CLI). Executable helper overlays from mutable `main` are not part of that runtime contract.
- **npm `ff-occam`:** Published experimental RC and primary npm package; it wraps the lower-level `@ff-occam/mcp` runtime. Do not present npm as the guarded GA install path.
- **Docker:** HEALTHCHECK uses the non-blocking `version-surface` verb. It proves
  process startup, not browser, network, or extraction readiness.

Release replacement is transactional: the old install is retained until the
new runtime completes its post-install checks. A failed update restores the old
tree; a failed fresh install removes the incomplete tree. The installer rejects
unknown, source-checkout, symlinked, or metadata-inconsistent targets before it
stops processes or moves files.

### Connect honesty

- Mutates third-party configs and writes backups. Disconnect removes only an
  entry whose command and `OCCAM_HOME` match this install; ambiguous or changed
  ownership fails closed.
- The bootstrap checks the complete release runtime set before replacement;
  missing advertised helpers reject the archive before the existing tree moves.

### Keys export trap

`occam keys export --keys-root <empty dir>` **mints a new key** and exports it — a key that never signed anything. Consumers pinning that PEM verify nothing meaningful.

---

## CHECK

**LOCAL.** Run host-binary `keys export` against an empty `--keys-root` directory. A key appears that never signed any receipt.

**LOCAL (careful).** Start two fixture processes whose command lines reference
sibling install roots, then dry-run refresh against one root. Only the exact
root-bound process should be selected; a prefix sibling such as `ff-occam-old`
must not match.

---

## Common misconception

**"`occam verify` and `occam keys export` are wrapper subcommands."** The Node/PowerShell `occam` wrapper's closed subcommand table has no `verify`, `keys`, or `install-browser`. These are direct host-binary verbs — show the exact invocation.

---

## Limitations

- `occam refresh` is install-scoped process control, not fleet management.
- Default uninstall deliberately preserves `~/.occam`, skills, response cache,
  host-config backups, and the shared Playwright cache. Preview explicit cleanup
  with `occam uninstall --dry-run --remove-cache --remove-state`; see
  [Chapter 21](21-state-and-footprint.md).
- Cosign is policy-gated: published rc.4 manifests require it, while legacy rc.2 remains SHA-256-only.
- npm is a public experimental RC channel, not the guarded GA path.
- Connect rollback gaps for some hosts.
- Skill install may ship stale version/tool counts.

---

## Links

- [Chapter 3 — Install minimum path](03-standing-up-an-install.md)
- [Chapter 21 — State and footprint](21-state-and-footprint.md)
- [Chapter 22 — Configuration](22-configuration.md)
- User docs: [Getting started](../getting-started.md) · [Install](../install.md) · [Connect](../connect/index.md) · [Troubleshooting](../troubleshooting.md)
- Audit: `docs-audit/CLI-SURFACE.md` · `docs-audit/CONNECT-PLATFORM.md` · OD-2, OD-3
