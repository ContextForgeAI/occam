# CLI-SURFACE (Wave 3 S3-07 — Main CLI surface)

**Generated:** 2026-07-26
**SoT:** executable code only (`src/FFOccamMcp.Core/Cli/OccamCliVerbs.cs`, `scripts/occam.mjs` +
`scripts/lib/operator/*.mjs`). Docs (`docs/getting-started.md` §Operator CLI) are UNTRUSTED —
used only for gap comparison in §5.
**CAP range assigned:** CAP-920…939 (20 minted below, none unused).
**Reuses:** CAP-001, CAP-002 (Wave 1, `docs-audit/subsystems/runtime-mcp.md`).

---

## 1. Three distinct "CLI surfaces" (do not conflate)

| # | Surface | Binary/entry | Dispatch | Verbs |
|---|---------|--------------|----------|-------|
| A | **Host binary offline verbs** | `OccamMcp.Core` (or AOT publish) | `OccamCliVerbs.TryRun(args)` — first check in `Program.cs`, before MCP transport starts | `keys export`, `verify`, `install-browser`, `version-surface`, `lifecycle` (5) |
| B | **Unified operator CLI** (`occam <sub>`) | `scripts/occam.mjs` | Data table `CLI_SUBCOMMANDS` in `occam-cli-subcommands.mjs` → `dispatchSubcommand()` (node / shell / internal) | `doctor`, `onboard`(`settings`), `connect`, `help`, `refresh`(`restart`), `smoke`, `update`, `session`, `snippet`, `skill`, `control`, `status`, `contract`(`version-surface`) (13 names, 3 with aliases) |
| C | **Full command registry** (`occam-help.mjs`, `COMMAND_REGISTRY`) | `scripts/lib/operator/occam-command-registry.mjs` | Data only — consumed by `help-catalog.mjs`; **not** all rows are `occam <sub>` | 21 rows total: the 13 from B (13 have `cliAlias`) + 8 registry-only entries (`install`, `install.ps1`, `launch-mcp-host`, `occam-wrapper`, `occam-playbook-publish`, `print-connection-snippet`, `get-ff-occam`, `ci-agent-mvp-gate`, `run-agent-mvp-gate`, `run-agent-popular-hosts`, `run-l0-fast`, `run-wide-cursor-desk`, `build-release`, `OccamMcp.Core`) |

Surface A and Surface B/C **share one name by coincidence, not by delegation, for `keys`/`verify`** —
those are host-binary-only, never routed through `occam.mjs`. Surface A and B **do** share
`version-surface` as a name — see CAP-929 / EF-020 (real collision, different depth).

---

## 2. Surface A — `OccamCliVerbs` (host binary, 5 verbs)

Dispatched in `Program.cs` before any MCP transport; network-free, no worker spawn (except
`install-browser`, which spawns `npx playwright install chromium`).

| Verb | Behavior | Exit codes | Class (task taxonomy) |
|------|----------|------------|------------------------|
| `keys export [--keys-root]` | Prints host's public key PEM to stdout | 0 | ADVANCED — cross-ref S3-06 deep dive |
| `verify --mode receipt\|citation\|manifest\|history --pubkey <path> …` | Offline verify against a pinned key | 0 verified / 1 not verified / 2 usage | ADVANCED — cross-ref S3-06 deep dive |
| `install-browser` | Downloads Playwright chromium into per-user cache unless a system browser is configured (`OCCAM_BROWSER_EXECUTABLE_PATH`/`OCCAM_CHROME_PATH`/non-chromium `OCCAM_BROWSER_CHANNEL`) | 0 ready / 1 failed / 2 worker tree missing | OPERATOR — this is literally the `fix.command` a `playwright_missing` MCP failure points at (agent/script self-remediation, no human) |
| `version-surface` | Emits `{hostVersion, assemblyPath, packageVersion, protocolVersion:null, schemaFingerprint:null}` — reads `AssemblyInformationalVersionAttribute`, strips `+<commit>`, prefers `Environment.ProcessPath` (AOT-safe, `Assembly.Location` is empty under Native AOT) | 0 | DEVELOPER — partial diagnostic; the two null fields are filled only by Surface-C `contract` script |
| `lifecycle self` / `lifecycle diagnose [--peers <json>]` | `self`: this instance's `HostIdentityDescriptor` (INV-10 read-only identity). `diagnose`: same + optional caller-supplied peer descriptors → `ObservedPeers` + `OverlapWarnings`. Code comment: **"Occam never scans/kills by process name from this verb"** — peers are supplied by the caller, not OS-discovered | 0 | INTERNAL — undocumented anywhere in `docs/`; no public consumer found |

---

## 3. Surface B — `occam <sub>` unified operator CLI (13 names)

Entry `scripts/occam.mjs`: global flags `--json`, `-h/--help`; no subcommand + interactive TTY (not
`CI=1`/`true`) launches Surface B's own `control` loop; no subcommand + non-TTY → usage + exit 1.

| Name (aliases) | Delegate | Target | Class | In `docs/getting-started.md`? |
|---|---|---|---|---|
| `doctor` | shell | `occam-doctor.ps1`/`.sh` | CORE_USER | Yes |
| `onboard` (`settings`) | node | `occam-onboard.mjs` | CORE_USER | No (only via INSTALL.md territory — S3-09) |
| `connect` | node | `occam-connect.mjs` | CORE_USER | Yes |
| `help` | node | `occam-help.mjs` | CORE_USER | No (implied by `occam --help`, not `occam help`) |
| `refresh` (`restart`) | node | `occam-refresh-host.mjs` | OPERATOR | No |
| `smoke` | node | `hermes-smoke.mjs` | CORE_USER | Yes |
| `update` | internal (`runUpdateCheck`) | `update-check.mjs` | OPERATOR | No |
| `session` | node | `occam-session.mjs` | ADVANCED (S3-05 owns depth) | Yes |
| `snippet` | node | `lib/print-mcp-snippet.mjs` | ADVANCED (doc literally labels it "(advanced)") | Yes |
| `skill` | node | `occam-skill-install.mjs` | CORE_USER (S3-12 owns depth) | Yes |
| `control` | internal (`runControlLoop`) | `control-loop.mjs` | OPERATOR | No |
| `status` | internal (`showStatus`) | `control-actions.mjs` | CORE_USER | Yes |
| `contract` (`version-surface`) | node | `check-public-mcp-contract.mjs` | DEVELOPER | No |

**Dispatch mechanics (`occam-cli-dispatch.mjs`):**
- `delegate:"node"` → `spawnSync(process.execPath, [scriptPath, ...args])`; `snippet` gets an
  implicit `occamHome` positional arg when the caller passed none.
- `delegate:"shell"` → picks `.ps1` (Windows, via `powershell -File`) or `.sh` (else, via `bash`);
  **auto-injects `--skip-build`** for `doctor` when `isLevelBInstall()` (has `VERSION` file, no
  `.git`) and the caller didn't already pass it.
- `delegate:"internal"` → handled directly in `occam.mjs`/`control-actions.mjs` (`control`,
  `status`, `update`); no subprocess.

**`control` menu** (`control-loop.mjs`, TTY-only unless `--json`): numeric keys `1`–`6` map to
`onboard/doctor/update/help/refresh/smoke`, `s`→`status`, `h`→spawns `occam-help.mjs` directly
(bypassing `runControlAction`), `q`/blank/`quit`/`exit` exits. Non-TTY + no `--json` is a hard
error ("requires an interactive TTY").

**`update`** (`update-check.mjs`): read-only GitHub Releases check
(`https://api.github.com/repos/ContextForgeAI/occam/releases/latest`, overridable via
`OCCAM_RELEASES_API_URL`); `OCCAM_LATEST_VERSION` env fully bypasses the network call;
`OCCAM_RELEASE_ALLOW_HTTP=1` required to accept a plain-`http://` release URL. Never
downloads/installs — only prints an `upgradeHint` pointing at `get-ff-occam.sh` or `install.sh`.

**`help`** (`occam-help.mjs` + `help-catalog.mjs`): three renderers (`tty`/`json`/`plain`);
`occam help next-steps` prints the fixed `OPERATOR_NEXT_STEPS` checklist (5 items: path, control,
verify-install, connect, reload-mcp, hermes-smoke); `occam help <id>` looks up one
`COMMAND_REGISTRY` row by id/alias/path-suffix and lists up to 5 `relatedCommands` sharing the same
`seeAlso` doc anchor.

**`contract`/`version-surface`** (`check-public-mcp-contract.mjs`, ~180 LOC pipeline): resolves the
published AOT binary, runs raw `version-surface` (Surface A) for host metadata, spawns the real
`launch-mcp-host.mjs` path, does a live `initialize`+`tools/list` MCP handshake, asserts the RC1
public-contract shape, computes/pins a `schemaFingerprint` against
`corpora/public-mcp-schema-fingerprint.txt` (`--write-fingerprint` to reseed), optionally
(`--ws`) proves the WebSocket transport listens + stdio/launch parity, optionally
(`--invoke-smoke`) round-trips one real `occam_digest`/`occam_transcode` call, and always prints
one final JSON line (`hostVersion/assemblyPath/packageVersion/protocolVersion/schemaFingerprint/
launchPath/binaryPath`) plus `PUBLIC_MCP_CONTRACT_OK` marker.

---

## 4. Surface C — registry-only rows (8 extra, not `occam <sub>`)

Confirmed in `occam-command-registry.mjs` (`COMMAND_REGISTRY`, 21 rows, `tier: operator|ci|maintainer`):

| id | tier | Class | Note |
|---|---|---|---|
| `install` / `install.ps1` | operator | INSTALLATION | Level A clone or Level B tarball, doctor, verify — S3-09 owns depth |
| `launch-mcp-host` | operator | INSTALLATION | Cross-platform launcher (AOT-first, `dotnet run` fallback); also the thing `contract` spawns |
| `occam-wrapper` | operator | INSTALLATION | Hermes/OpenClaw stdio wrapper shell script; **not** in `CLI_SUBCOMMANDS`, only in registry |
| `occam-playbook-publish` | operator (registry) but summary says "PB4c **maintainer** publish CLI" | DEVELOPER | See §4.1 |
| `print-connection-snippet` | operator | ADVANCED / INSTALLATION | Post-install host-specific snippet (Hermes YAML, Cursor JSON, …); sibling of `snippet` (which is Cursor-only, `print-mcp-snippet.mjs`) |
| `get-ff-occam` | operator | INSTALLATION | Level B one-liner (`curl \| bash`) |
| `ci-agent-mvp-gate` | ci | INTERNAL | CI entry point |
| `run-agent-mvp-gate` | ci | INTERNAL | Agent-First MVP gate |
| `run-agent-popular-hosts` | ci | INTERNAL | Popular-host integration corpus |
| `run-l0-fast` | maintainer | INTERNAL | Fast gate subset |
| `run-wide-cursor-desk` | maintainer | INTERNAL | Maintainer QA recipe runner |
| `build-release` | maintainer | DEVELOPER | Builds the Level B tarball + manifest — maintainer action producing a user-facing artifact |
| `occam-help` | operator | CORE_USER | Duplicate of Surface B `help` (`cliAlias: "help"`) |
| `OccamMcp.Core` | operator | CORE_USER (boundary) | The host binary itself as a registry row; deep dive is S3-11 |

### 4.1 `occam-playbook-publish` (PB4c maintainer publish CLI)

`scripts/occam-playbook-publish.sh`/`.ps1` → `node scripts/lib/playbook-publish.mjs` →
`workers/shared/lib/playbook-publish-sanitize.mjs`. Required: `--input <path>` (a local-tier
playbook JSON) + `--ack-community-review` (explicit human gate — no auto-upload, ever). Optional
`--output <dir>` (default `$OCCAM_HOME/artifacts/playbook-publish/{id}/`), `--summary <text>`.
Exit 0 export ok / 1 validation-or-`secrets_detected`-or-`ack_required` / 2 usage. Zero MCP
surface — explicitly "not an MCP tool" (`AGENTS.md` §7 confirms: PB4c is planned as a CLI, never a
10th tool). `docs/roadmap.md#not-shipped-out-of-l0-scope` is the only doc pointer (registry
`seeAlso`); not in `docs/getting-started.md`.

---

## 5. Doc-vs-code gap ("INVISIBLE PRODUCT" answer)

`docs/getting-started.md` §Operator CLI lists exactly 7 of the 21 registry rows: `connect`,
`doctor`, `smoke`, `snippet`, `status`, `session`, `skill`. Everything else in this report is
**invisible to an MCP-only / docs-only user**:

- **Entirely undocumented commands:** `onboard`/`settings`, `help`, `refresh`/`restart`, `update`,
  `control` (the interactive menu — the thing `occam` with no args launches!), `contract`/
  `version-surface`, `keys export`, `verify`, `install-browser`, `lifecycle self`/`diagnose`,
  `occam-playbook-publish`, `print-connection-snippet`, `launch-mcp-host`, `occam-wrapper`,
  `get-ff-occam`, `build-release`.
- **The default no-argument experience is undocumented.** Running bare `occam` in a TTY opens a
  full interactive menu (`control-loop.mjs`) — `docs/getting-started.md` never mentions this;
  a user who only reads the docs table would not know `occam` (no args) does anything but print
  usage.
- **Self-update discovery is invisible.** `occam update` / the `update-check.mjs` GitHub-release
  check exists and is wired into `control` menu key `3`, but has zero mention in `docs/`
  (`AGENTS.md` §9 task table references a "P2-5a" install/browser row but not this).
  `docs/troubleshooting.md` was not found to reference it either (not verified beyond a grep —
  see Uncertainties).
- **`occam contract` is the closest thing to a "is my install healthy end-to-end" superset check**
  (spawns the real launch path, does a live MCP handshake, verifies the public schema didn't
  drift) and is completely absent from user docs — it currently reads as CI/maintainer-only, but
  nothing in code gates it from an end user running it.

---

## 6. Capability graph edges

**Reused (Wave 1, no new mint):**
- `CLI` |USES| **CAP-001** (`Cli/OccamCliVerbs.TryRun` sits on the process-entry dual-path
  established before transport selection)
- `CLI:keys export`, `CLI:verify`, `CLI:install-browser`, `CLI:version-surface`, `CLI:lifecycle`
  |USES| **CAP-002** (`Offline CLI verb dispatch (pre-transport)` — Wave 1 already enumerated
  these 5 as impl bullets; this report is the deep dive)

**New (this report, CAP-920…939 — 20 of 20 used):**

| CAP | Name | Edge |
|-----|------|------|
| CAP-920 | `version-surface` deployment diagnostic (Surface A) | REFINES CAP-002 |
| CAP-921 | `lifecycle self` — read-only `HostIdentityDescriptor` (INV-10) | REFINES CAP-002 |
| CAP-922 | `lifecycle diagnose` — caller-supplied peer overlap warnings, no OS process scan/kill | REFINES CAP-002 |
| CAP-923 | Unified operator CLI dispatcher — data-only `CLI_SUBCOMMANDS` + node/shell/internal delegate kinds | new subsystem root |
| CAP-924 | `occam control` — interactive TTY soft menu (numeric/letter keys → actions) | USES CAP-923 |
| CAP-925 | `occam status` — install summary (version + onboard meta + update) with `--json` | USES CAP-923, CAP-926 |
| CAP-926 | `occam update` — read-only GitHub Releases check, `OCCAM_LATEST_VERSION`/`OCCAM_RELEASE_ALLOW_HTTP` escape hatches | USES CAP-923 |
| CAP-927 | `occam help` / `help next-steps` — `COMMAND_REGISTRY`-driven catalog, tty/json/plain renderers, `relatedCommands` via shared `seeAlso` | USES CAP-936 |
| CAP-928 | `occam refresh`/`restart` — stop running host processes, re-run doctor, print manual reload hint (Cursor/Hermes reload is NOT automated) | USES CAP-923 |
| CAP-929 | `occam contract`/`version-surface` — full public-contract pipeline (schema fingerprint pin, stdio/WS parity, RC1 invoke-smoke); **internally spawns Surface-A `version-surface`** for host metadata, then adds `protocolVersion`/`schemaFingerprint` from a live handshake | USES CAP-920; see EF-020 |
| CAP-930 | `occam snippet` implicit-arg injection (`occamHome` auto-appended when no passthrough args) | REFINES CAP-923 |
| CAP-931 | `occam skill` install entrypoint (delegates to `occam-skill-install.mjs`) | USES CAP-923 |
| CAP-932 | `occam-playbook-publish` maintainer CLI (PB4c) — sanitize + `--ack-community-review` gate, zero auto-upload, zero MCP surface | standalone (not `occam <sub>`) |
| CAP-933 | Global `--json` flag uniformly reshapes stdout for every Surface-B subcommand | REFINES CAP-923 |
| CAP-934 | Bare `occam` (no subcommand) auto-launches `control` loop only when `stdin.isTTY && !json && CI!=1/true`; else usage+exit 1 | REFINES CAP-923 |
| CAP-935 | Level B auto-`--skip-build` injection for `doctor` shell delegate (`isLevelBInstall` = has `VERSION`, no `.git`) | REFINES CAP-923 |
| CAP-936 | `COMMAND_REGISTRY` tier taxonomy (`operator`\|`ci`\|`maintainer`) — 21 rows, only 13 carry `cliAlias`; the other 8 exist for docs/help only, never reachable via `occam <sub>` | new (Surface C) |
| CAP-937 | Dual-name command resolution — `findCommand`/`findSubcommand` match by id, alias, or path-suffix | REFINES CAP-936 |
| CAP-938 | EF-019 anchor: `occam-refresh-host.mjs` hardcodes a stale post-reload tool count | see §7 EF-019 |
| CAP-939 | EF-020 anchor: `version-surface` name collision between Surface A (raw verb) and Surface B (`contract` alias, full pipeline) | see §7 EF-020 |

---

## 7. Engineering findings (candidates for `ENGINEERING-FINDINGS.md`)

Both below are **PROVEN in code** (static read, no execution needed) and were appended to
`docs-audit/ENGINEERING-FINDINGS.md` as EF-019 and EF-020.

- **EF-019** (OBSERVATION, CAP-928/CAP-938): `scripts/occam-refresh-host.mjs` line
  `log("After reload, tools/list should show 9 occam_* tools with the new binary.");` hardcodes a
  stale core-tool count. The current always-on registry (`OccamMcpServerRegistration.OccamToolNames`,
  Wave 1 CAP-007) is **15**, not 9. Anyone running `occam refresh` sees a wrong number in the
  operator-facing reload hint.
- **EF-020** (DESIGN-QUESTION, CAP-929/CAP-939): The string `version-surface` names two different
  commands at two different depths: (1) the raw host-binary verb `OccamCliVerbs.VersionSurface()`
  — instant, network-free, returns a **partial** record with `protocolVersion`/`schemaFingerprint`
  null; (2) the Surface-B alias `occam version-surface` → `contract` → `check-public-mcp-contract.mjs`
  — spawns the real launch path, performs a live MCP handshake, checks/writes a fingerprint corpus
  file, and returns the **full** record. An operator typing `occam version-surface` gets the heavy
  pipeline; a script invoking the published binary directly with `version-surface` gets the light
  one. Same name, non-overlapping guarantees. Not a bug (the light verb is intentionally a building
  block for the heavy one — see CAP-929), but worth a docs note if `version-surface` is ever
  surfaced to end users directly.

---

## 8. Artifacts created/consumed

| Artifact | Created by | Consumed by |
|---|---|---|
| `corpora/public-mcp-schema-fingerprint.txt` | `check-public-mcp-contract.mjs --write-fingerprint` | Same script, subsequent runs (drift check) |
| `~/.occam/onboard.json` (or `OCCAM_CONFIG` path) | `occam-onboard.mjs` (S3-09 territory) | `occam status`, `occam refresh` (reload hints via `hostTargetToConnectionKind`) |
| `VERSION` file at `OCCAM_HOME` root | Release tarball (`build-release`, S3-12 territory) | `update-check.mjs` (`readInstalledVersion`), `occam-cli-dispatch.mjs` (`isLevelBInstall`) |
| `$OCCAM_HOME/artifacts/playbook-publish/{id}/` (incl. `PULL_REQUEST.md`) | `occam-playbook-publish` | Human maintainer (manual PR) — no automated consumer |
| Peer descriptor JSON (arbitrary path via `--peers`) | Operator/host-supplied, not Occam-generated | `lifecycle diagnose` (read-only input) |
| stdout JSON line (one per verb: `CliVerifyResult`, `CliInstallBrowserResult`, `CliVersionSurfaceResult`, `CliLifecycleSelfResult`, `CliLifecycleDiagnoseResult`) | Surface A verbs | Scripts/CI parsing stdout (e.g. `check-public-mcp-contract.mjs` parses the last stdout line of `version-surface`) |

No file cache; no other persistent product artifacts found for this surface beyond the above.

---

## 9. Uncertainties

- Whether `occam update` / `occam contract` are referenced anywhere in `docs/troubleshooting.md`
  or `INSTALL.md` was checked only by targeted `Grep` on `docs/getting-started.md`; a full-repo doc
  grep for these two command names was not exhaustively run. Low risk — does not change the
  code-level classification.
- `occam-wrapper.sh` content itself was not opened (only its `COMMAND_REGISTRY` row); classified
  INSTALLATION by registry summary text alone ("Hermes/OpenClaw stdio wrapper — sets OCCAM_HOME,
  suppresses banner"), not by reading the script body.
- `print-connection-snippet.mjs` internals were not opened; classified by registry summary only.
- Did not verify whether any external (non-repo) automation invokes the raw `lifecycle`/
  `version-surface` Surface-A verbs directly (e.g. from a host adapter under `scripts/lib/operator/
  connect/adapters/*`) — flagged for S3-10 (Connect platform) to confirm/deny during their pass.

---

## 10. Completeness verdict

**COMPLETE for assigned scope.** All 5 `OccamCliVerbs` (Surface A), all 13 `CLI_SUBCOMMANDS` names
(3 with aliases = 16 invocable strings) (Surface B), and all 8 registry-only rows not reachable via
`occam <sub>` (Surface C) were read from source and classified. 20/20 CAP-920…939 minted and used;
no leftover budget, no overflow. 2 engineering findings proven and appended to
`ENGINEERING-FINDINGS.md` (EF-019, EF-020). Deep behavioral verification of `keys export`/`verify`
(S3-06), `session` (S3-05), `skill`/`OccamMcp.Core` binary flags (S3-11/S3-12), `install`/`onboard`/
`get-ff-occam`/`launch-mcp-host` (S3-09), and `connect` adapters (S3-10) is intentionally left to
their owning agents per `NONCORE-SURFACE-MAP.md` §Phase 3B — this report only establishes their
existence, name, and top-level classification for the main-CLI-surface inventory.
