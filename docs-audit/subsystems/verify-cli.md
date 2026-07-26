# Subsystem audit: Verify CLI / offline trust (`OccamCliVerbs`)

Wave 3 subagent **S3-06** — FFOccamMCP capability audit.
**CAP ID range: CAP-900 – CAP-919** (used: 900–905; remainder reserved).
**Source of truth: executable code only.** Docs (`docs/receipts.md`, `docs/receipt_verification.md`,
`MCP_API_SPEC.md`, `docs/troubleshooting.md`, `docs/faq.md`) were read only to check for drift against
the code — never as evidence of capability existence.

Scope per assignment: deep-dive `occam verify` + `occam keys export` (both fully in Wave 1's CAP-275/276
but re-verified here against the exact question below), `install-browser` **briefly**, and a direct
comparison against the `occam_verify` MCP tool. `version-surface` / `lifecycle` are explicitly
out-of-scope here (owned by S3-07 — mentioned only where they share dispatch code).

**Heavy reuse of Wave 1 (`trust-receipts.md`, CAP-250–291) and Wave 2 (`tools/occam_verify.md`,
CAP-650–653).** This report does not re-derive the crypto/canonicalization model — it answers one
question precisely and documents what is new since those passes: the **operator-CLI-vs-host-binary
split**, the **fix.command→install-browser wiring**, and **two confirmed doc-vs-code drifts**.

---

## 0. THE QUESTION — answered precisely

> **Can evidence be verified without trusting the producing Occam instance?**

**Short answer: Partially — the math is instance-independent, the trust anchor is not.**

Split into the two things "trust the instance" can mean:

### 0.1 Trusting the *running process* (liveness / control-flow) — **NO dependency, by design**

`occam verify` / `occam keys export` (`Cli/OccamCliVerbs.cs`) are dispatched in `Program.cs` **before**
any MCP transport, worker spawn, or browser pool starts (`Program.cs:12`,
`if (OccamCliVerbs.TryRun(args, out var verbExit)) { return verbExit; }`). Verification is pure,
offline, static-key ECDSA-P256 math (`ReceiptVerifier.VerifyOffline`, `MerkleTree.VerifyProof`,
`DatasetManifestBuilder.Verify`, `WatchHistoryChain.Verify`) over a `--pubkey` file supplied on the
command line. **No MCP handshake, no live re-fetch, no dependency on the producing host still being
up.** A consumer who received a receipt file yesterday and the producing machine is off today can still
run `occam verify` and get a correct verdict. This half of the question is a genuine **yes**.

### 0.2 Trusting the *key's authenticity* (who is `k1:…`) — **YES, currently required (TOFU)**

The CLI's own doc-comment states the honest limit verbatim (`OccamCliVerbs.cs:15-24`): the verifier
proves *"whoever holds the private key for THIS pem signed THIS envelope, unmodified"* — it does **not**
prove the key belongs to the entity the caller thinks produced the receipt. There is **no PKI, no
signed registry, no well-known endpoint, no certificate chain** anywhere in this codebase that binds
`k1:<16-hex>` to an operator identity (confirmed: `grep` for any registry/well-known-key-fetch
call site in `Receipts/*` finds none; the only "well-known" fetcher in the repo,
`WellKnownGenomeFetcher`, is for **playbook genomes**, unrelated to key distribution). The public key
PEM must be obtained **out-of-band** — and the only in-repo mechanism to obtain it is `occam keys export`
run **against the exact same key store** (`OCCAM_KEYS_ROOT`, default `~/.occam/keys/`) the producing
instance uses. This is textbook TOFU (trust-on-first-use): the first time a consumer gets a `k1:…` PEM,
they are trusting *the channel that handed it to them* (a file copy, a chat message, a publish site) —
identical in structure to pinning an SSH host key. **The CLI's offline verify step is real and correct
crypto; the bootstrapping of the public key it verifies against is not solved by this codebase at all —
it is explicitly out of scope (see CAP-288, restated below with the CLI-specific angle at CAP-903).**

**Net:** a third party can check *"this exact bag of bytes was signed by the same key that signed
receipt X yesterday, and nothing has changed"* with zero trust in the live process. They **cannot**
independently establish *"and that key belongs to the Occam instance/operator I think it does"* without
some out-of-band channel this codebase does not provide (no registry exists yet — SI-08 is explicitly
deferred, `docs/receipt_verification.md`: *"A signed registry with reputation is deferred"*).

---

## 1. `occam verify` / `occam keys export` — confirmed against Wave 1 CAP-275/276

Re-verified via direct code read (`Cli/OccamCliVerbs.cs:26-215, 217-433`) — Wave 1's description holds:
dispatch table (`TryRun`, lines 28-56) is `keys export` / `verify` / `install-browser` /
`version-surface` / `lifecycle`, evaluated **before** `OccamMcpCli.Parse` — a non-matching first arg
(e.g. `--mcp-server`) correctly falls through (`return false`, verified in
`benchmarks/l0-gate/CliVerbsUnitTests.cs:68`).

- `keys export --keys-root <path>` → `ReceiptSigner.LoadOrCreate(keysRoot)` then
  `signer.ExportPublicKeyPem()` to **stdout**; a `# occam public key (keyId …)` banner to **stderr**
  (`OccamCliVerbs.cs:208-215`). **This call has a side effect**: `LoadOrCreate` generates a new keypair
  on disk if the store is empty (`ReceiptSigner.cs:26-45`) — i.e. running `keys export` against a fresh
  `--keys-root` **creates** a signing identity, it does not merely read one. A consumer pointing
  `--keys-root` at their own empty directory (instead of the producer's) will silently mint a brand-new,
  unrelated keypair and export *that* — no error, no warning that no existing key was found.
- `verify --mode receipt|citation|manifest|history --pubkey <path> [--receipt|--input <path|->]
  [--markdown <path>] [--block-text <text>] [--proof <path>]` → always requires `--pubkey` (no implicit
  "use local key" fallback — `Usage("verify needs --pubkey <path> …")` at line 223) — this absence of a
  default is **the single line of code that makes the CLI meaningfully different from the MCP tool** for
  the trust question (§0.2): the CLI forces the caller to be explicit about which key they trust; the
  MCP tool does not (§2).
- Exit codes are exactly as documented and gate-tested: `0` verified, `1` parsed-but-not-verified, `2`
  usage/IO error (`CliVerbsUnitTests.cs:41-76` exercises all three for `verify`, plus `keys export` exit 0
  and a bad-usage exit 2).
- `--mode receipt` is **strictly stronger** than the MCP tool's `offline` mode in one respect (restated
  from Wave 2 CAP-276/CLI-parity note): it folds a present time anchor into the pass/fail `verified`
  verdict (`anchorValid != false` required, `OccamCliVerbs.cs:282-288`); the MCP tool reports `timeAnchor`
  as a separate, non-gating field, so the **same receipt with a broken time anchor can be `"verdict":
  "verified"` via MCP but exit `1` via the CLI.**
- `--mode manifest` (dataset-export manifest verification) is **CLI-only** — `occam_verify` (MCP) has no
  equivalent mode (Wave 1 CAP-283, Wave 2 §6, reconfirmed here by direct read of
  `OccamVerifyTool.Verify`'s mode switch, which lists only `prove|citation|live|(default offline)`, no
  `manifest`, no `history`-adjacent `manifest` arm). A pure-MCP agent — no shell access to the compiled
  binary — **structurally cannot** verify a dataset manifest's signature at all.

---

## 2. Direct comparison: CLI `verify` vs. MCP `occam_verify` — the decisive asymmetry

| Axis | CLI (`occam verify`) | MCP (`occam_verify`) |
|---|---|---|
| Public key source | **Always explicit** — `--pubkey <path>`, no fallback (`OccamCliVerbs.cs:220-224`) | **Optional** — defaults to `localSigner.ExportPublicKeyPem()`, i.e. **this running process's own key**, when `public_key` param omitted (`OccamVerifyTool.cs:40`) |
| Modes | `receipt \| citation \| manifest \| history` | `offline \| live \| prove \| citation \| history` |
| Manifest verify | Yes | **No** |
| Live re-fetch | **No** (fully offline by construction) | Yes (`mode=live` — real network call through `TranscodePipeline`) |
| Time anchor | Gates the verdict | Reported separately, non-gating |
| Reachable without the host running | **Yes** (no MCP transport, no worker spawn) | No (requires a live MCP session) |
| Reachable without the *producing* instance's key file | Yes, **if** the consumer already has the correct PEM from elsewhere | **Effectively no for the common case**: an agent that omits `public_key` (the ergonomic default) is verifying against *whichever instance is running the MCP session right now* — which is almost never a foreign producer's key unless the consumer explicitly passes it |

**This is the crux finding for the assigned question.** The MCP tool's `public_key` default makes
`occam_verify` (as most agents will call it — omitting an optional param) into a **same-instance
self-check**, not a third-party check: if an agent calls `occam_verify` with a receipt produced by a
*different* Occam install (different `OCCAM_KEYS_ROOT`, different machine), the call will report
`"signature_invalid"` **not** because of tampering but because the tool silently substituted the wrong
key — and (Wave 2 finding, restated) the verdict vocabulary cannot distinguish "wrong key" from "actually
tampered" (`ReceiptVerification.SignatureInvalid` covers both). The **CLI is the only surface in this
codebase that forces the caller to name the key they trust**, which is the correct shape for the
"verify without trusting the producing instance" claim — but it requires shell access to the compiled
binary, which most MCP-only agents do not have (see §4).

---

## 3. `occam install-browser` — briefly, per assignment scope

`Cli/OccamCliVerbs.cs:65-166`. Not part of the trust/signature subsystem, but **wired to it only by
sharing the `TryRun` dispatch table and the same "agent can self-remediate" design philosophy** as
`verify`/`keys export` (all three are "MCP-adjacent CLI verbs a script can run headlessly"). Traced end
to end for this report (Wave 1 had bucketed it as "out of scope, adjacent"):

- **Short-circuit for system browsers**: if `OCCAM_BROWSER_EXECUTABLE_PATH` / `OCCAM_CHROME_PATH` is set,
  or `OCCAM_BROWSER_CHANNEL` is set to anything other than `chromium`, the verb does **no download** and
  reports `status:"already_present"`, exit `0` (lines 68-84).
- Otherwise resolves `workers/browser-extract` under `WorkerPaths.ResolveOccamHome()`; if not found,
  `status:"worker_missing"`, exit `2` (lines 87-96) — this is the one path that genuinely needs
  `OCCAM_HOME` set correctly, same precondition as every other worker-dependent surface.
  Then spawns `npx playwright install chromium` (via `cmd /c npx …` on Windows, `npx` directly elsewhere,
  `RunPlaywrightInstall`, lines 132-160), forwarding all child stdout/stderr to **our stderr** — stdout
  stays reserved for the one JSON marker (`CliInstallBrowserResult`).
- Exit contract: `0` installed/already_present, `1` failed (`npx` exit ≠ 0, or `Win32Exception`/
  `InvalidOperationException`/`IOException` launching it — e.g. Node not on PATH), `2` worker tree not
  found. Matches `docs/troubleshooting.md`'s and `MCP_API_SPEC.md`'s stated exit semantics exactly.

### CAP-900 — `OccamCliVerbs.InstallBrowser` (verified end-to-end trace)
**File:** `OccamCliVerbs.cs:65-160`. **Classification:** Public setup verb, adjacent to trust subsystem.
See bullets above; no gate unit test found specifically for `install-browser` (unlike `verify`/`keys
export`, which `CliVerbsUnitTests.cs` exercises) — **not gate-tested**, confirmed by reading the full
file (only `verify`, `keys export`, non-verb fallthrough, and `version-surface` are asserted).

### CAP-901 — Producer↔consumer wiring: `browser-launch-options.mjs` emits the literal string the CLI verb answers
**Files:** `workers/browser-extract/lib/browser-launch-options.mjs:88-92` (producer: on a "browser binary
not installed" launch error, sets `fix: { kind: "manual_install", command: "occam install-browser",
root_required: false }`) → surfaced verbatim through `WorkerExtractFixInfo`/`TranscodeOutcome.Fix`/
`OccamTranscodeFixInfo` to `occam_transcode`'s `failure.fix.command` field → an agent is told to run
**exactly** `occam install-browser`. See **CAP-902** for why that literal string does not work as
written for a large class of installs.

---

## 4. Finding — two disjoint CLI surfaces; the friendly `occam` wrapper does not expose ANY trust verb

### CAP-902 — `occam` (operator wrapper) has no route to `verify` / `keys export` / `install-browser` / `lifecycle`
**Files:** `scripts/occam` (bash: `exec node "$ROOT/scripts/occam.mjs" "$@"`), `scripts/occam.ps1`
(PowerShell: `& node $OccamMjs @Args`), `scripts/occam.mjs` (`findSubcommand`/`CLI_SUBCOMMANDS`,
`scripts/lib/operator/occam-cli-subcommands.mjs:17-118`).

There are **two structurally different CLI programs named/invoked similarly**, both documented as "the
occam CLI":
1. **The compiled host binary** (`OccamMcp.Core[.exe]`) — dispatches `keys export`, `verify`,
   `install-browser`, `version-surface`, `lifecycle` via `OccamCliVerbs.TryRun` (`Program.cs:12`).
2. **The operator wrapper** (`scripts/occam` / `occam.ps1`, meant to be the thing on `PATH` per
   `AGENTS.md`'s "PATH should include `$OCCAM_HOME/scripts`") — a **Node.js script** with its own
   closed subcommand table: `doctor, onboard/settings, connect, help, refresh/restart, smoke, update,
   session, snippet, skill, control, status, contract/version-surface`. **`verify`, `keys`, and
   `install-browser` are absent from this table.**

`occam.mjs`'s `main()` (lines 60-81) calls `findSubcommand(subName)`; if it returns `undefined`, the
program prints `error: unknown command '<name>'` and **exits 1** — it does **not** fall through to the
compiled binary. Confirmed by reading the full dispatch logic; there is no catch-all delegate to
`OccamCliVerbs` anywhere in `scripts/occam.mjs` or `occam-cli-subcommands.mjs`.

**Consequence for the exact remediation command an agent is told to run (CAP-901):** if `occam` on that
agent's `PATH` resolves to the **operator wrapper** (the documented, intended setup — `AGENTS.md` §7,
`docs/getting-started.md`) rather than directly to the compiled `OccamMcp.Core` binary, then literally
running `occam install-browser` produces `error: unknown command 'install-browser'`, exit `1` — **not**
"install failed" (which the CLI verb's own contract defines exit 1 to mean), but "this command does not
exist in this wrapper." An agent that trusts the documented exit-code contract (`0`/`1`/`2`) will
misdiagnose a **routing gap** as a **failed browser install**. The identical gap applies to `occam
verify` and `occam keys export` — an agent/operator who only knows the friendly `occam` command (the
one this repo's own onboarding funnels people toward) has **no path to the trust CLI at all** without
separately discovering the raw binary path (`OccamMcp.Core`) or `dotnet run --project src/FFOccamMcp.Core
-- verify …`.

**Doc corroboration of the confusion:** `docs/troubleshooting.md:14` tells the user to run
`occam install-browser` verbatim (the broken form); `MCP_API_SPEC.md:935` and `docs/receipts.md:83-89`
correctly use the raw binary name for the same verbs — i.e. **the repo's own docs disagree with each
other** about which invocation form works, which is exactly what this finding predicts from the code.

---

## 5. Two confirmed doc-vs-code drifts (found while tracing the CLI docs for §4)

### CAP-903 — Stale binary name `FFOccamMcp.Core` in 3 doc files (actual `AssemblyName` is `OccamMcp.Core`)
**Code:** `src/FFOccamMcp.Core/FFOccamMcp.Core.csproj:21-22` — `<RootNamespace>OccamMcp.Core</RootNamespace>`,
`<AssemblyName>OccamMcp.Core</AssemblyName>`. Confirmed also by `packages/occam-mcp/bin/occam-mcp.js`'s
own `BINARY_NAMES` map (`"OccamMcp.Core.exe"` / `"OccamMcp.Core"`) and
`scripts/lib/resolve-host-binary.mjs`'s primary candidate name.
**Docs still using the legacy `FFOccamMcp.Core` name for CLI examples** (grep-confirmed, verbatim):
- `MCP_API_SPEC.md:929,930,935` — the exact "Verifying without the host" / "Setup verb" prose this
  report's §0–3 depend on.
- `docs/receipt_verification.md` (normative byte-spec doc) — "The bundled verifier (`FFOccamMcp.Core
  verify`…)" and "…you get it with `FFOccamMcp.Core keys export`."
- `docs/faq.md:62` — `FFOccamMcp.Core verify --receipt receipt.json --pubkey pubkey.pem --markdown
  page.md`.
`docs/receipts.md` (lines 83-89) gets it **right** (`OccamMcp.Core keys export`, `OccamMcp.Core
verify …`) — so the correction exists in the repo, just not propagated to the other three files. This is
exactly the class of drift `AGENTS.md` §4.1 names explicitly ("FFOccamMCP … old namespace") but it
survived in the trust-CLI docs specifically. Low severity (a reader can infer the fix from context / the
correct file), but on the exact page (`receipt_verification.md`) whose entire purpose is *"so you can
re-implement the check in any language"* for a **third party who has never seen this repo**, citing a
binary name that does not exist is a real friction point for the audience that matters most for the
"verify without trusting the instance" claim.

### CAP-904 — Normative Merkle-leaf spec embeds a raw NUL control byte instead of an escaped `\0`
**File:** `docs/receipt_verification.md`, §3 ("Block Merkle root & citations"). Hex-dumped the source line
directly (`Format-Hex` on the raw file bytes): the formula
`leaf_i = hex(SHA256(utf8(text_i + "␀" + (source_selector_i or ""))))` contains a **literal `0x00` byte**
inside the quoted separator, not the two printable characters `\` `0`. This is **byte-identical in
effect** to the code (`MerkleTree.cs:20`: `$"{text}\0{sourceSelector ?? string.Empty}"` — C#'s `\0` is
also a real NUL char at runtime) — **not a correctness bug**, the spec and the code agree once you
resolve the byte. But: (a) this Read tool refused to open `receipt_verification.md` at all
("binary files … not supported") because of that embedded control byte — i.e. **the file trips
binary-content heuristics in at least one common tooling class**; (b) a raw NUL byte inside a markdown
prose file is fragile against editors/linters/git line-ending normalization/diff tools that may treat
the file as binary or mangle it; (c) a human reading the raw markdown source (not rendered) sees an
invisible/garbled character with no visual cue that it is semantically load-bearing. Recommend (to the
maintainer, not actioned here per audit rules) rendering the separator as the visible escape sequence
`\x00` or `\0` in prose, with the *code* comment (already present, `ReceiptCanonicalizer`/`MerkleTree`)
remaining the actual byte-level authority.

---

## 6. Capability graph edges

```
CLI:occam-verify|USES|CAP-259
CLI:occam-verify|USES|CAP-262
CLI:occam-verify|USES|CAP-283
CLI:occam-verify|USES|CAP-284
CLI:occam-verify|USES|CAP-261
CLI:occam-keys-export|USES|CAP-254
CLI:occam-keys-export|USES|CAP-255
CLI:occam-install-browser|PRODUCES_FOR|CAP-901
CAP-901|PRODUCED_BY|workers/browser-extract/lib/browser-launch-options.mjs
CAP-901|CONSUMED_BY|CAP-900
CAP-902|BLOCKS|CAP-900 (as literally documented)
CAP-902|BLOCKS|CAP-275 (as literally documented)
CAP-902|BLOCKS|CAP-276 (as literally documented)
TOOL:occam_verify|DIFFERS_FROM|CLI:occam-verify (see §2 table)
CAP-903|DOC_DRIFT_IN|MCP_API_SPEC.md
CAP-903|DOC_DRIFT_IN|docs/receipt_verification.md
CAP-903|DOC_DRIFT_IN|docs/faq.md
CAP-904|DOC_FRAGILITY_IN|docs/receipt_verification.md
CAP-288|GENERALIZES_TO|CAP-903-adjacent-question (this report §0.2)
```

---

## 7. Artifacts created/consumed

Consumed only (no new artifact TYPE minted — these are existing `ART-*` from Wave 2's `ARTIFACT-MAP.md`,
re-touched here): signed `ReceiptEnvelope` (public-key PEM as a **new** cross-cutting input, not
previously modeled as its own artifact — it is the trust anchor a consumer must source out-of-band, see
§0.2), `occam://capsule/...`, dataset manifest JSON, watch-history array, RFC3161 time-anchor token,
Merkle citation proof JSON. Produced: `CliVerifyResult` / `CliInstallBrowserResult` JSON markers (stdout).

---

## "INVISIBLE PRODUCT" — what an MCP-only user/agent never sees

1. **The entire CLI trust surface itself.** An agent that only ever calls MCP tools (never shells out)
   cannot: verify a dataset-export manifest signature (no MCP mode exists, CAP-283/§1), force a
   *specific* pinned public key without risking the silent same-instance-key default (§2), or get the
   stronger time-anchor-gated verdict the CLI's `receipt` mode provides.
2. **The producer-side wiring of `failure.fix.command`.** The exact remediation string
   `"occam install-browser"` an agent is told to run autonomously (CAP-901) is assembled in a Node
   worker file (`browser-launch-options.mjs`) an MCP-only consumer never reads — and, per CAP-902, it is
   not guaranteed to work as literally written depending on which `occam` is on `PATH`.
3. **That there are two different programs both informally called "the occam CLI."** Nothing in the MCP
   tool surface (or in `occam_client_capabilities`) discloses that `keys export`/`verify`/
   `install-browser`/`lifecycle` live on the *compiled binary* while `doctor`/`onboard`/`connect`/
   `session`/etc. live on a *separate Node wrapper* with a closed subcommand list.
4. **Key-store side effects.** `keys export` against an empty `--keys-root` silently generates a new
   keypair rather than erroring "no key found here" — an MCP-only user has no equivalent surface to even
   notice this (there is no MCP tool that generates/rotates keys).

---

## Engineering findings (appended to `ENGINEERING-FINDINGS.md`)

- **EF-020** — `occam install-browser` / `occam verify` / `occam keys export` are unreachable through the
  documented, on-PATH operator wrapper (`scripts/occam` / `occam.ps1` → `occam.mjs`); the wrapper's
  closed `CLI_SUBCOMMANDS` table has no entry for any of the three, and unmatched subcommands exit 1 with
  "unknown command" instead of falling through to the compiled host binary. The literal remediation
  string `"occam install-browser"` emitted in `failure.fix.command` (CAP-901/902) does not work as
  written against that wrapper. `docs/troubleshooting.md:14` repeats the broken invocation verbatim.
- **EF-021** — Stale legacy binary name `FFOccamMcp.Core` (actual: `OccamMcp.Core`) in CLI examples across
  `MCP_API_SPEC.md` (×3), `docs/receipt_verification.md` (×2), `docs/faq.md` (×1) — `docs/receipts.md`
  has the correct name; the fix was not propagated to the other three files (CAP-903).
- **EF-022** — `docs/receipt_verification.md`'s Merkle-leaf formula embeds a literal raw `0x00` byte in
  prose instead of the visible escape `\0`/`\x00`; byte-correct vs. code but fragile (trips at least one
  "is this binary?" tooling heuristic) and invisible to a human reading raw markdown source (CAP-904).

---

## Completeness verdict

**Complete for assigned scope.** `occam verify` (4 modes) and `occam keys export` fully re-traced against
Wave 1 CAP-275/276 (confirmed accurate, no corrections needed to that report); `install-browser` traced
end-to-end as instructed ("briefly") including its producer wiring in the Node worker, which Wave 1 had
not traced; the operator-vs-binary CLI split (CAP-902) and two doc drifts (CAP-903/904) are new findings
not present in Wave 1 or Wave 2 reports. The assigned question (§0) is answered with an explicit,
code-cited yes/no split rather than a single verdict, because the honest answer genuinely has two
different parts (process-independence: yes; key-authenticity-independence: no, TOFU only, by documented
design). `version-surface` and `lifecycle` were read only enough to confirm they share `TryRun` and carry
no trust-signature logic — full depth left to S3-07 as instructed.
