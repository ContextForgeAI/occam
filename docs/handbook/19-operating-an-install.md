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
| `occam refresh` / `restart` | Kills Occam processes, re-runs doctor path | **Every** `OccamMcp.Core[.exe]` on the machine — no scope flag |
| `occam session` | Import/export session profiles | Plaintext cookie retention under `_imports/` by default |
| `occam smoke` | Live extract smoke | Network |
| `occam update` | Release fetch | Network |
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

- **Level A / Level B tarball:** Primary supported install. Integrity check is **sha256 manifest**, not cosign — the release `.bundle` is unused by shipped install paths (honesty-only metadata).
- **npm `@ff-occam/mcp`:** NOT GA — do not document as public install path.
- **Docker:** HEALTHCHECK may invoke unsupported verbs — no production-readiness claim.

Install is destructive replacement with no rollback. Onboarding may write config before verification completes.

### Connect honesty

- Mutates third-party configs; rollback is dead for restart-required hosts — back up before connecting.
- Tarball may omit scripts that help text advertises — verify files on disk after install.

### Keys export trap

`occam keys export --keys-root <empty dir>` **mints a new key** and exports it — a key that never signed anything. Consumers pinning that PEM verify nothing meaningful.

---

## CHECK

**LOCAL.** Run host-binary `keys export` against an empty `--keys-root` directory. A key appears that never signed any receipt.

**LOCAL (careful).** Before `occam refresh`, note other Occam installs on the machine; refresh kills all hosts by binary name regardless of `OCCAM_HOME`.

---

## Common misconception

**"`occam verify` and `occam keys export` are wrapper subcommands."** The Node/PowerShell `occam` wrapper's closed subcommand table has no `verify`, `keys`, or `install-browser`. These are direct host-binary verbs — show the exact invocation.

---

## Limitations

- `occam refresh` is machine-wide collateral kill — not fleet management.
- Uninstalling the install tree leaves `~/.occam`, host configs, skills, and Playwright cache — see [Chapter 21](21-state-and-footprint.md).
- Cosign bundle does not gate install trust.
- npm is not a supported 1.0 channel.
- Connect rollback gaps for some hosts.
- Skill install may ship stale version/tool counts.

---

## Links

- [Chapter 3 — Install minimum path](03-standing-up-an-install.md)
- [Chapter 21 — State and footprint](21-state-and-footprint.md)
- [Chapter 22 — Configuration](22-configuration.md)
- User docs: [Getting started](../getting-started.md) · [Install](../install.md) · [Connect](../connect/index.md) · [Troubleshooting](../troubleshooting.md)
- Audit: `docs-audit/CLI-SURFACE.md` · `docs-audit/CONNECT-PLATFORM.md` · OD-2, OD-3
