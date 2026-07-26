# P6-04 — Packaging / Installability dispositions

**Agent:** P6-04  
**Phase:** 6 (post Phase 5 COMPLETE)  
**Scope:** EF-034, EF-035, EF-051, EF-052, EF-053  
**Product code:** unchanged (analysis + patch plans only). Public `docs/` not modified.  
**SoT cross-check:** `packages/occam-mcp/*`, `scripts/lib/build-release.mjs`, `Dockerfile`, `.github/workflows/{playbook-marketplace,sign-release,occam-release}.yml`, `docs-audit/negative-space/H-packaging-ci-blind.md`, `PRODUCT-VS-ENGINEERING.md` rows, `SHIPPED-CODE-MAP.md`.

Disposition vocabulary: `FIX_NOW` | `FIX_BEFORE_PUBLIC_DOCS` | `DOCUMENT_LIMITATION` | `REMOVE_SURFACE` | `OWNER_DECISION` | `DEFER`.

---

## EF-034 — `@ff-occam/mcp` imports outside npm `files` set

### CURRENT BEHAVIOR (path:line)

- `packages/occam-mcp/package.json:28-31` — `"files": ["bin/", "lib/"]` only.
- `packages/occam-mcp/bin/occam-mcp.js:23` — top-level ESM import:
  `import { formatInstallBlockerMessage } from "../../../scripts/lib/host-install-gate.mjs";`
  Resolves to **repo-root** `scripts/lib/host-install-gate.mjs`, outside the publish set. Evaluated at module load.
- In-repo / monorepo checkout: import works (relative path walks to repo root).
- Packed / published tarball: module resolution fails → bin is DOA.
- No CI workflow publishes npm (`NPM_TOKEN` / `npm publish` absent from `.github/workflows/*`). Registry identity `@ff-occam/mcp@1.0.0-rc.2` is unpublished today (prior audit 404).
- Adjacent (not separate EF): `bin/occam-mcp.js` RID map (~50–55) omits `win-arm64`/`linux-arm64` while `package.json:41-49` advertises `cpu: [x64, arm64]`.

### USER IMPACT

- Documenting or recommending `npx @ff-occam/mcp` as a supported install path would be false.
- If someone publishes as-is, every consumer hits an immediate import error before download/sha256 logic runs.
- Agent-sdk peer-depends on this package → transitive DOA if npm is treated as the distribution channel.

### RECOMMENDED CONTRACT

npm install path must guarantee:

1. Everything the `bin` entry imports at load time is inside `package.json` `files` (or vendored under `lib/`).
2. `npm pack --dry-run` + a smoke that starts the packed bin (or at least resolves its imports) in a clean temp dir **without** the monorepo tree.
3. Until (1)–(2) pass and a real publish exists, product docs must not present npm as an available install channel.

### DISPOSITION

**FIX_BEFORE_PUBLIC_DOCS** (also blocks any npm GA claim). Rank 5 on `NEEDS_FIX_BEFORE_DOC`.

Optional alternate: **REMOVE_SURFACE** (drop `publishConfig` / bin until fixed) — only if owner decides npm is not a 1.0 channel. Default remains fix-then-doc.

### PATCH PLAN (smallest)

1. Vendor (copy or thin re-export) `formatInstallBlockerMessage` (+ minimal helpers it needs) into `packages/occam-mcp/lib/host-install-gate.mjs`.
2. Change `bin/occam-mcp.js:23` to `from "../lib/host-install-gate.mjs"`.
3. Add `test/pack-boundary.test.js`: assert no `bin/`/`lib/` import paths escape package root; optionally run `npm pack` and inspect tarball file list.
4. Do **not** widen `"files"` to `../../scripts` (breaks publishable hygiene rule in AGENTS.md).

Out of minimal scope but should ride along if npm is unblocked: align `RID_MAP` with advertised `os`/`cpu` or narrow `package.json` platforms to shipped RIDs `{win-x64, linux-x64, osx-arm64}` (+ mapped osx-x64 only if built).

### TEST / VERIFY PLAN

| Check | How |
|-------|-----|
| Pack dry-run | `cd packages/occam-mcp && npm pack --dry-run` — only `bin/` + `lib/` (+ package.json metadata) |
| Isolated start | Extract pack tarball to temp dir; `node bin/occam-mcp.js --help` must not throw `ERR_MODULE_NOT_FOUND` for `scripts/lib/*` |
| Unit | Existing `npm test` in package after vendoring |
| Publish readiness | Confirm registry still 404 until intentional publish; no public-doc npm quickstart until green |

### BREAKING?

No for current users (package unpublished). Breaking only if a private fork depended on monorepo-relative imports after a future publish of the broken shape — N/A. Vendoring is additive.

---

## EF-035 — Level B tarball omits advertised `connect` / `contract` entry scripts

### CURRENT BEHAVIOR (path:line)

- `scripts/lib/operator/occam-cli-subcommands.mjs:35-40` — `connect` → `script: "occam-connect.mjs"`.
- Same file `:110-116` — `contract` (alias `version-surface`) → `script: "check-public-mcp-contract.mjs"`.
- `formatSubcommandUsage()` (`:130-138`) always lists every `CLI_SUBCOMMANDS` row → `occam --help` advertises both.
- `scripts/lib/build-release.mjs:98-119` — Level B `scriptFiles` allow-list includes doctor/onboard/session/… but **omits** `occam-connect.mjs` and `check-public-mcp-contract.mjs`.
- Entry scripts **exist in the repo**: `scripts/occam-connect.mjs`, `scripts/check-public-mcp-contract.mjs`.
- `scripts/lib/**` is copied wholesale (`build-release.mjs:129-131`), so connect *libraries* ship, but dispatch looks for the top-level entry script:
  `occam-cli-dispatch.mjs:31-35` → `error: missing ${scriptPath}` exit 1.
- Docker is different: `Dockerfile:60` `COPY scripts/ /app/scripts/` includes the entry scripts. EF-035 is **Level B tarball / npm-downloaded Level B**, not Docker.

### USER IMPACT

- Fresh Level B install: `occam connect` / `occam contract` fail with missing-script error while help still lists them.
- Primary post-install “wire Cursor/Claude” path is broken on the main non-clone distribution.
- Operator confusion: help lies about available commands.

### RECOMMENDED CONTRACT

Level B tarball must guarantee: every command listed by `occam --help` / `CLI_SUBCOMMANDS` has its `script` (or shell twin) present under `$OCCAM_HOME/scripts/`, **or** the command is removed from the help map for Level B.

### DISPOSITION

**FIX_BEFORE_PUBLIC_DOCS** (LIMITED block on connect/contract chapters for Level B). Rank 6.

### PATCH PLAN (smallest)

Add to `scriptFiles` in `scripts/lib/build-release.mjs`:

```js
"occam-connect.mjs",
"check-public-mcp-contract.mjs",
```

Optional hardening (same PR or follow-up):

- Selftest: after stage, assert every `CLI_SUBCOMMANDS[].script` exists under staged `scripts/`.
- Or Level-B filter in `formatSubcommandUsage` / dispatch — worse UX; prefer shipping the scripts.

### TEST / VERIFY PLAN

| Check | How |
|-------|-----|
| Stage content | `node scripts/lib/build-release.mjs` (or `build-release.ps1`) → `Test-Path stage/scripts/occam-connect.mjs` and `check-public-mcp-contract.mjs` |
| Fresh install | Extract tarball → `node scripts/occam.mjs connect --help` (or dry-run) must not print `error: missing …/occam-connect.mjs` |
| Contract | `node scripts/occam.mjs contract` reaches the checker (may fail later on host-not-running — that is OK; missing file is not) |
| Regression | Confirm `scripts/lib/operator/connect/**` still present via lib copy |

### BREAKING?

No. Additive packaging. Commands become reachable where they were advertised.

---

## EF-051 — Docker HEALTHCHECK uses invalid `--version` and blocks on stdio

### CURRENT BEHAVIOR (path:line)

- `Dockerfile:75-76`:
  `HEALTHCHECK … CMD /app/occam --version || exit 1`
- Host offline verbs: `keys`, `verify`, `install-browser`, `version-surface`, `lifecycle` — `OccamCliVerbs.cs:36-55`. **No `--version`.**
- `Program.cs:12-17`: `TryRun` first; unknown first-token falls through to `OccamMcpCli.Parse`.
- `OccamMcpCli.Parse` (`Transport/OccamMcpCli.cs:53-160`) silently ignores unrecognized args → default **stdio** transport.
- HEALTHCHECK runs without stdin → process blocks until `--timeout=5s` → unhealthy every cycle; short-lived host processes leak each interval.
- Correct offline verb already exists: `version-surface` (`OccamCliVerbs.cs:47-48`, `:173+`) — prints JSON to stdout and exits.

### USER IMPACT

- Orchestrators (Compose, K8s, Docker Desktop) mark the container perpetually **unhealthy**.
- Any “production-ready image / healthcheck” claim is false.
- Extra CPU/process churn every 30s.

### RECOMMENDED CONTRACT

Docker HEALTHCHECK must invoke a **finite, non-stdio** host verb that exits 0 on a healthy binary (prefer `version-surface`), with timeout shorter than the verb’s worst-case exit.

### DISPOSITION

**FIX_NOW** (trivial, local, no public-doc dependency to *fix*; still **FIX_BEFORE_PUBLIC_DOCS** for any health/production claim — Rank 8).

Code not patched in this Phase 6 pass (analysis-first); one-liner is ready.

### PATCH PLAN (smallest)

```dockerfile
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD /app/occam version-surface || exit 1
```

Do **not** use `--version` or bare `/app/occam`. Optional: also copy `profiles/` if seed/community tiers are required in-container (separate finding / CAP-1029); not required to fix health.

### TEST / VERIFY PLAN

| Check | How |
|-------|-----|
| Local verb | `docker run --rm <image> version-surface` → exit 0 + JSON |
| Health | Build image → `docker run -d` → wait ≥60s → `docker inspect --format='{{.State.Health.Status}}'` → `healthy` |
| Negative | Confirm old `--version` still hangs if reintroduced |

### BREAKING?

No meaningful break. Health status flips from unhealthy→healthy (desired). Clients using broken health as a gate may start routing traffic — that is the point.

---

## EF-052 — Marketplace can auto-merge after skipped validation

### CURRENT BEHAVIOR (path:line)

`.github/workflows/playbook-marketplace.yml`:

| Mechanism | Lines | Effect |
|-----------|-------|--------|
| Trigger paths recursive | `:6-7` | `profiles/playbooks/community/**/*.json{,.sig}` |
| Diff detection single-level | `:74` | `git diff … -- 'profiles/playbooks/community/*.json'` — nested JSON invisible |
| Empty set → success | `:83-86` | `result=skipped` + `exit 0` → job **success** |
| Cosign community sign | `:100-110` | `cosign sign-blob` with env `COSIGN_PRIVATE_KEY` but **no `--key`**; job perms (`:16-19`) lack `id-token: write` → signing misconfigured (also EF-053) |
| Auto-merge | `:139-149` | `if: needs.validate.result == 'success'` → `gh pr merge --auto --squash` |

Combined: `.sig`-only or nested-path PR can trigger workflow, skip L4 gate, still **success**, enable auto-merge of unvalidated community content into `main` (feeds `occam_playbook_resolve` community tier).

Branch-protection / rulesets / required checks / “allow auto-merge” are **not in the git tree** — unknown whether GitHub settings currently block this path.

### USER IMPACT

- Community playbook tier cannot be documented as “CI-validated” or “trusted auto-merge.”
- Supply-chain: unvalidated JSON can land on `main` and be resolved by agents.

### RECOMMENDED CONTRACT

1. Auto-merge (if kept) only when L4 gate **explicitly `passed`** for ≥1 playbook path.
2. Trigger glob ≡ validation glob (both recursive `**/*.json`).
3. `skipped` / empty diff must **fail** the validate job **or** disable auto-merge (never count as merge-green).
4. Branch protection on `main`: required status check = this workflow’s validate job; no bypass for the bot without human review — **owner-enforced outside repo**.

### DISPOSITION

**OWNER_DECISION** + split:

| Part | Disposition |
|------|-------------|
| Workflow logic (skip=success, glob mismatch, auto-merge condition, cosign flags) | **FIX_BEFORE_PUBLIC_DOCS** (in-repo) — Rank 1 |
| Branch protection / rulesets / who can merge | **OWNER_DECISION** (external) — see Appendix A |
| Whether marketplace auto-merge should exist at all | **OWNER_DECISION** |

### PATCH PLAN (smallest, in-repo)

1. Change L4 step: if `PLAYBOOKS` empty → `exit 1` (or set job outcome that auto-merge does not treat as success). Prefer: auto-merge `if: steps.l4-gate.outputs.result == 'passed'` via job output.
2. Diff glob → `'profiles/playbooks/community/**/*.json'`.
3. Fix or remove community cosign step (see EF-053).
4. Consider deleting `auto-merge` job until protection verified.

### TEST / VERIFY PLAN

| Check | How |
|-------|-----|
| Nested PB PR | Open PR with `community/foo/bar.json` only → gate must **run** (not skip) |
| Sig-only PR | `.json.sig` only → must **not** auto-merge as validated |
| Happy path | Real PB change → `result=passed` → sign (if fixed) → merge only if owner policy allows |
| Protection | Owner confirms required checks in GitHub UI (Appendix A) |

### BREAKING?

Yes for contributors relying on auto-merge of skipped runs (undesirable behavior). Intentional tighten. May require human merge until protection + workflow green.

---

## EF-053 — Cosign theater: misconfigured community sign; release `.bundle` unused by install

### CURRENT BEHAVIOR (path:line)

**A. Community marketplace signing (misconfigured)**  
- `playbook-marketplace.yml:21-23,100-110` — env has `COSIGN_PRIVATE_KEY` / `COSIGN_PASSWORD` but invocation has no `--key env://COSIGN_PRIVATE_KEY`; permissions omit `id-token: write` → neither key-based nor keyless OIDC works as written.

**B. Release signing (produces artifacts; no install consumer)**  
- `sign-release.yml:15-19,86-97` — keyless `cosign sign-blob --bundle "${f}.bundle"` on **tarballs** (has `id-token: write`). Uploads `ff-occam-*-{linux-x64,osx-arm64,win-x64}.tar.gz.bundle`.
- Install consumers verify **sha256 vs unsigned manifest only**:
  - `scripts/get-ff-occam.sh:205-220`
  - `scripts/lib/release-install.mjs:161-172`
  - `packages/occam-mcp/bin/occam-mcp.js:237-241`
- None download or `cosign verify-blob` the release `.bundle`.
- `scripts/lib/verify-install.mjs:75-87` optionally checks `${binaryPath}.bundle` next to the **extracted binary** — release workflow signs the **tarball**, not the binary → even optional path rarely matches shipped assets. Failures are **warned and skipped**, not hard-fail.
- `scripts/verify-install.ps1` references stale `occam-mcp-win-x64.exe` names — dead/example surface.

### USER IMPACT

- “Cosign-verified install” / “signed supply chain” claims are false for all shipped paths.
- Community `.sig` provenance (if any) is unreliable due to (A).
- Operators who see `.bundle` on the Release page may believe install already checked it.

### RECOMMENDED CONTRACT

Pick one product contract (owner):

1. **Honest integrity-only (minimal):** document sha256-manifest as the install trust bar; stop implying cosign; optionally stop uploading unused `.bundle` **or** keep bundles for manual third-party verify only.
2. **Real cosign install (larger):** download `.bundle` beside the tarball in `get-ff-occam` / `release-install` / npm `ensureBinary`; `cosign verify-blob --bundle …` **before** extract; hard-fail on missing/invalid bundle when `OCCAM_REQUIRE_COSIGN=1` (or always for GA).

Community path: fix `--key` **or** keyless+OIDC; do not commit `.sig` from a broken step.

### DISPOSITION

**FIX_BEFORE_PUBLIC_DOCS** for any signed-supply-chain claim (Rank 4).  
Implementation split:

| Sub-issue | Disposition |
|-----------|-------------|
| Marketplace cosign flags | **FIX_BEFORE_PUBLIC_DOCS** (with EF-052) |
| Wire install to verify `.bundle` | **OWNER_DECISION** (honest sha256 vs real cosign) |
| Remove/repurpose unused `.bundle` upload | **DEFER** until owner picks contract — or **REMOVE_SURFACE** if choosing honesty-only |

### PATCH PLAN (smallest coherent options)

**Option H (honesty, smallest docs-unblocking engineering):**  
- Fix or disable marketplace sign step.  
- Leave install on sha256.  
- Do not claim cosign in product docs.  
- Optional: add release-note / internal flag that `.bundle` is manual-only.

**Option C (cosign-real, larger):**  
1. In `release-install.mjs` / `get-ff-occam.sh` / npm `ensureBinary`: fetch `${stem}.tar.gz.bundle`, verify, then sha256 (defense in depth).  
2. Hard-fail when cosign missing if required.  
3. Align `verify-install.mjs` with **tarball** bundle path, not `${binary}.bundle`.  
4. Fix marketplace `--key env://COSIGN_PRIVATE_KEY` (and secret presence) **or** keyless + `id-token: write`.

### TEST / VERIFY PLAN

| Check | How |
|-------|-----|
| Negative (today) | Fresh `get-ff-occam` / release-install — confirm no `cosign` / `.bundle` fetch in logs; only `sha256: OK` |
| Option C | Install with deliberately wrong bundle → must fail before extract |
| Marketplace | Dry-run `cosign sign-blob --key env://…` in CI after secret wired |
| Manual | `cosign verify-blob --bundle ff-occam-….tar.gz.bundle ff-occam-….tar.gz` against a published release |

### BREAKING?

- Option H: No runtime break.  
- Option C: Breaks air-gapped / cosign-less installs unless gated by env; announce. Wrong historical releases without bundles fail closed.

---

## Appendix A — Draft for `docs-audit/EXTERNAL-OWNER-ACTIONS.md`

### EA-052 — Marketplace branch protection & merge policy (EF-052)

**Why external:** Branch protection, rulesets, “Allow auto-merge”, bypass actors, and required status checks live in GitHub org/repo settings — not in this repository’s tree. Workflow YAML can be fixed in-repo; enforcement that unvalidated PRs cannot land still needs owner confirmation.

**In-repo fixable (engineering):**

- Fail or non-success when L4 gate is `skipped`.
- Align recursive trigger ↔ recursive diff globs.
- Gate `gh pr merge --auto` on `result == 'passed'` only; or delete auto-merge job.
- Fix community cosign invocation / permissions (EF-053-A).

**Owner must do / verify in GitHub UI (or API):**

1. **Branch protection** on `main` (or ruleset): require the Playbook Marketplace **validate** job; block merges when red/skipped.
2. Confirm whether **auto-merge** is enabled at repo level; if workflow keeps `--auto`, protection must still require the passed check.
3. Restrict bypass (admins / maintainers) for community playbook paths if policy demands human review.
4. Confirm secrets `COSIGN_PRIVATE_KEY` / `COSIGN_PASSWORD` exist and match the chosen signing mode — or switch to keyless and grant `id-token: write`.
5. Record current protection state in this file after inspection (screenshot or `gh api` dump) — **UNKNOWN until owner runs the check**.

**Unblocks:** public docs for “validated community marketplace” (PvE Rank 1). Until both in-repo + external are done: **DOCUMENT_LIMITATION** / withhold trusted-merge claims only.

### EA-053 — Cosign product contract (EF-053)

**Owner chooses:** honesty-only sha256 install **vs** mandatory cosign verify on download.  
Engineering cannot truthfully document “signed install” without that choice and matching consumer code.

### Other packaging externals (not EF-assigned here)

- npm publish credentials / decision to publish `@ff-occam/mcp` at all (EF-034 depends on publish intent).
- Whether Docker images are pushed to a registry (in-repo Dockerfile only; no publish workflow found).

---

## Appendix B — Install path matrix (draft)

Legend: **PASS** = works for the named step on that channel today · **FAIL** = code-proven broken · **UNKNOWN** = not runtime-reproduced here / environment-dependent.  
“How to verify” is the minimal operator check.

### Channel: npm (`npx @ff-occam/mcp` / `@ff-occam/mcp`)

| Step | Status | How to verify |
|------|--------|----------------|
| INSTALL | **FAIL** | `npm view @ff-occam/mcp version` → 404; if ever packed as-is, `npm pack` + extract → import `scripts/lib/host-install-gate.mjs` missing (EF-034) |
| START | **FAIL** | Packed bin dies at module load before MCP stdio |
| DOCTOR | **FAIL** / N/A | Not a package surface; depends on downloaded Level B tree after START |
| CONNECT | **FAIL** / N/A | Not shipped in npm `files`; would need Level B scripts after host install |
| FIRST READ | **FAIL** | No running host |
| VERIFY | **FAIL** | No install completes; even after hypothetical fix, npm path only sha256’s tarball (EF-053) |
| UPDATE | **FAIL** / N/A | No published versions to update |

### Channel: Level B tarball (`get-ff-occam` / GitHub Release / `release-install`)

| Step | Status | How to verify |
|------|--------|----------------|
| INSTALL | **PASS** (integrity = sha256 only) | Bootstrap → log `sha256: OK`; tree has `VERSION`, `OccamMcp.Core*`, `workers/`, `scripts/` |
| START | **PASS** | `node scripts/launch-mcp-host.mjs` or binary; MCP client `tools/list` |
| DOCTOR | **PASS** | `occam doctor` / `occam-doctor.*` present in allow-list; Level B adds `--skip-build` |
| CONNECT | **FAIL** | `occam connect` → `error: missing …/scripts/occam-connect.mjs` (EF-035) |
| FIRST READ | **PASS** | `occam_transcode` / smoke `hermes-smoke.mjs` with live URL |
| VERIFY | **PASS*** | Host verb `OccamMcp.Core verify …` offline works; *cosign `.bundle` not consumed (EF-053); wrapper may not route `verify` (EF-025, out of P6-04 scope) |
| UPDATE | **PASS** (check) / **PASS** (reinstall) | `occam update` read-only check; re-run bootstrap for new version |

### Channel: Docker (`Dockerfile` / Compose)

| Step | Status | How to verify |
|------|--------|----------------|
| INSTALL | **PASS** | `docker build -t occam .` succeeds (linux-x64 AOT) |
| START | **PASS** | `docker run -i occam` speaks MCP on stdio |
| DOCTOR | **UNKNOWN** | Full `scripts/` copied; doctor may need extra OS pkgs / network — not runtime-tested this pass |
| CONNECT | **UNKNOWN** | Entry scripts present via `COPY scripts/`; connecting host-side IDEs to container MCP is env-specific |
| FIRST READ | **PASS** (when client attached) | Stdio MCP `tools/list` + transcode |
| VERIFY (health) | **FAIL** | `docker inspect` health → unhealthy; `HEALTHCHECK` uses `--version` (EF-051) |
| VERIFY (receipts) | **UNKNOWN** | Binary verbs exist; not exercised in-container this pass |
| UPDATE | **UNKNOWN** | Rebuild/repull — no published image workflow in-repo |

\*Matrix excludes clone/Level A (dev) except as contrast: clone has connect scripts → CONNECT **PASS** on clone.

---

## Summary table

| ID | Disposition | Patch ready? | Blocks public claim |
|----|-------------|--------------|---------------------|
| EF-034 | FIX_BEFORE_PUBLIC_DOCS | Yes (vendor import) | npm install / npx zero-config |
| EF-035 | FIX_BEFORE_PUBLIC_DOCS | Yes (+2 scriptFiles lines) | Level B connect/contract |
| EF-051 | FIX_NOW | Yes (1-line HEALTHCHECK) | Docker healthy/production-ready |
| EF-052 | FIX_BEFORE_PUBLIC_DOCS + OWNER_DECISION | Workflow yes; protection external | Validated marketplace auto-merge |
| EF-053 | FIX_BEFORE_PUBLIC_DOCS + OWNER_DECISION | Marketplace flags yes; install wire needs contract | Cosign-verified / signed supply chain |

**Trivial fix not applied this session:** EF-051 Dockerfile one-liner (ready; prefer orchestrator apply with gate).
