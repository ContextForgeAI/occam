# PHASE 6 — Runtime Reproductions (agent P6-06)

Runtime confirmations for source-proven Phase-4/5 engineering findings. **Code-reading evidence
remains authoritative**: a finding that could not be run at runtime is marked `BLOCKED` /
`NOT_REPRODUCED` with a reason, and its source proof stands unchanged. No public docs, product
code, or shipped tests were modified. All new files live under `docs-audit/repro/` (audit scope,
gitignored via `docs-audit/`).

## Environment (shared)

| Item | Value |
|------|-------|
| OS | Windows 10.0.26200, PowerShell |
| .NET SDK | 10.0.301 |
| Node | v24.16.0 |
| Repo | `c:\PROJECTS\FFOccamMCP` @ branch `docs/site-overhaul` |
| `OCCAM_HOME` | repo root (set per command) |
| Core build | `dotnet build docs-audit/repro/P6Repro.csproj -c Debug` → `OccamMcp.Core.dll` (Debug, non-AOT), 0 errors |

## Harness (`docs-audit/repro/`)

| File | Purpose |
|------|---------|
| `P6Repro.csproj` / `Program.cs` | Console harness that project-references `FFOccamMcp.Core`. Cases selected via `--case=EF-0NN`. Prints `CASE <id> \| <label> \| OBSERVED=… \| PASS/FAIL`; `PASS` = finding reproduced as described. |
| `fake-daemon.mjs` | Stand-in for `browser-daemon.mjs`: answers `GET /health`, records its PID. Lets EF-041 observe pool-process lifecycle without Playwright/Chromium. |
| `ef043-css-probe.mjs` | Loopback HTTP server + `css-extract.mjs` child process for EF-043. |

Non-C# findings (EF-054, EF-051) were run against the real shipped scripts / host binary, not the
harness. `EF-002` was assessed at source level only (see its row).

Result key: **CONFIRMED** = runtime behavior matches the finding · **NOT_REPRODUCED** = attempted,
did not reproduce · **BLOCKED** = not runnable cheaply in this environment.

---

## EF-041 — BrowserPoolManager.InstallShared kills the process-wide pool on every new session DI

- **ENVIRONMENT:** C# harness + `fake-daemon.mjs`; `OCCAM_BROWSER_POOL_SIZE=1`, `OCCAM_BROWSER_POOL_BASE_PORT=39311`, `OCCAM_BROWSER_DAEMON_SCRIPT`→fake daemon.
- **COMMAND:** `dotnet run --project docs-audit/repro/P6Repro.csproj -- --case=EF-041`
- **INPUT:** Build DI graph #1 (`new ServiceCollection().AddOccamCore()`), resolve `IBrowserPoolManager`, `TryEnsureMinimumHealthyAsync` (spawns fake daemon, records PID). Then build DI graph #2 and resolve `IBrowserPoolManager` again — the shape of a second WebSocket/Remote session, each of which calls `Host.CreateApplicationBuilder()` + `AddOccamMcpServer()`.
- **EXPECTED:** Session-2 DI resolution runs the `IBrowserPoolManager` factory → `BrowserPoolManager.InstallShared(manager)` → `_shared?.StopAll()`, terminating session-1's daemon and zeroing its healthy slots.
- **OBSERVED:**
  ```
  CASE EF-041 | session 1 pool slot healthy | OBSERVED=enabled=True,started=True | PASS
  CASE EF-041 | session 1 daemon alive before second DI build | OBSERVED=pid=8428,alive=True,healthySlots=1 | PASS
  CASE EF-041 | second DI build yields a different manager instance | OBSERVED=different | PASS
  CASE EF-041 | session 1 daemon killed by session 2 DI build | OBSERVED=pid=8428,alive=False | PASS
  CASE EF-041 | session 1 pool reports no healthy slot after session 2 DI build | OBSERVED=s1Healthy=0,s2Healthy=0 | PASS
  CASE EF-041 | process-wide Shared now points at the session 2 manager | OBSERVED=session2 | PASS
  ```
- **PASS/FAIL/BLOCKED:** **CONFIRMED (PASS).** Building a second DI container killed the first session's live daemon process (PID 8428) and dropped its healthy slot count from 1 to 0.
- **CODE PATH:** `Composition/OccamServiceCollectionExtensions.cs:39-46` (factory calls `InstallShared`) → `Workers/BrowserPoolManager.cs:45-49` (`_shared?.StopAll(); _shared = manager;`) → `:160-169` (`StopAll` → `SlotState.StopUnsafe` → process terminate). Per-session builder: `Transport/StdioMcpTransport.cs:41-43` and the WS/Remote transports each `AddOccamMcpServer()`.
- **REGRESSION TEST CREATED?** No. Propose `benchmarks/l0-gate/L6BrowserPoolRunner.cs` (or a new `BrowserPoolManagerUnitTests` under `#if OCCAM_GATE` using the existing `ResetSharedForTests`/`IBrowserDaemonClient` seam and a fake healthy client) asserting that a second `InstallShared` does **not** stop a still-leased pool.

---

## EF-045 — URL fragment drives focus but is dropped from the cache/materialization keys

- **ENVIRONMENT:** C# harness (pure functions; no network).
- **COMMAND:** `dotnet run --project docs-audit/repro/P6Repro.csproj -- --case=EF-045`
- **INPUT:** `https://example.com/guide#installation` vs `…#uninstall`, identical `OccamTranscodeOptions{PlaybookPolicy="off"}`.
- **EXPECTED:** `FocusIntent.FromUrl` extracts distinct fragments (they become focus intent), yet `TranscodeCacheKey.Compute` and `MaterializationKey.Compute` normalize the URL by dropping the fragment → identical keys → a cache hit for one fragment can replay the other fragment's stored response.
- **OBSERVED:**
  ```
  CASE EF-045 | FocusIntent fragments differ | OBSERVED=installation|uninstall | PASS
  CASE EF-045 | TranscodeCacheKey collides across fragments | OBSERVED=0337641991918e36==0337641991918e36 | PASS
  CASE EF-045 | MaterializationKey collides across fragments | OBSERVED=697fdb4a546c0581==697fdb4a546c0581 | PASS
  CASE EF-045 | control: focus_query does split the key | OBSERVED=distinct | PASS
  ```
- **PASS/FAIL/BLOCKED:** **CONFIRMED (PASS).** Two different focus fragments hash to the same cache key and same materialization key; the control shows an explicit `focus_query` (the non-fragment path) *does* split the key, isolating the defect to the fragment.
- **CODE PATH:** `Compile/FocusIntent.cs:8-31` (fragment → focus) vs `Caching/TranscodeCacheKey.cs:54-72` (`NormalizeUrl` uses `uri.PathAndQuery`, no fragment) and `Compile/MaterializationKey.cs:32` (same normalizer). Fragment applied as focus at `Routing/TranscodePipeline.cs:112-114`. Cache key used at `Tools/OccamTranscodeTool.cs:125`. Note: collision is only reachable with the opt-in disk cache (`cache_ttl_s>0`, no session profile) per `Caching/TranscodeCacheEligibility.cs`.
- **REGRESSION TEST CREATED?** No. Propose adding to `benchmarks/l0-gate/L0InfraUnitTests.cs` `RunTranscodeCacheInfra` (which currently *asserts* `keyA == keyB` for `#frag` at lines ~3520-3522 — i.e. the current test encodes the buggy behavior). A fix + test should assert fragment-focused reads get distinct keys (or are cache-ineligible).

---

## EF-051 — Docker HEALTHCHECK `/app/occam --version` never returns a version

- **ENVIRONMENT:** Windows host binary `OccamMcp.Core.exe` (Debug). Full `docker build` not run (BLOCKED: no Docker daemon in this environment); the finding is construction + CLI-arg behavior, reproduced against the host binary directly.
- **COMMAND (A, EOF stdin):** `OccamMcp.Core.exe --version` with stdin redirected from an empty file.
- **COMMAND (B, open stdin):** `OccamMcp.Core.exe --version` with stdin held open (the shape of a `HEALTHCHECK CMD` with no piped input).
- **INPUT:** the literal argument `--version`.
- **EXPECTED (from source):** `--version` is not a recognized verb, so it is silently ignored and the process starts the **stdio MCP server** instead of printing a version and exiting. A health probe therefore never gets a version string; with stdin open it never terminates → container reported perpetually unhealthy.
- **OBSERVED:**
  - (A) EOF stdin: no version on stdout; the banner + `Listening via stdio...` printed to stderr and a worker `occam-http-daemon` was spawned before EOF shut it down — i.e. it booted the full server, not a version check.
  - (B) open stdin: `exited_within_6s=False elapsed_ms=6006 STILL_RUNNING_AFTER_6s` — the process blocked as a running MCP server and never returned; killed manually.
- **PASS/FAIL/BLOCKED:** **CONFIRMED (PASS) for the root cause** (`--version` boots the stdio server rather than reporting a version); **BLOCKED** for the end-to-end Docker `HEALTHCHECK … unhealthy` state (no container runtime here). The Dockerfile healthcheck has `--timeout=5s --retries=3`; behavior (B) exceeds the timeout, matching the predicted perpetual-unhealthy outcome.
- **CODE PATH:** `Cli/OccamCliVerbs.cs:36-55` (`TryRun` handles only `keys`/`verify`/`install-browser`/`version-surface`/`lifecycle` — no `--version`) → `Transport/OccamMcpCli.cs:34-222` (help check is `-h/--help/-help//help//?` only at :36; the arg loop ignores unrecognized tokens and leaves `Mode=Stdio`, `IsValid=true`) → `Program.cs:50-59` starts `StdioMcpTransport`. Dockerfile: `Dockerfile:75-76` (`HEALTHCHECK … CMD /app/occam --version`).
- **REGRESSION TEST CREATED?** No. Propose `benchmarks/l0-gate/CliVerbsUnitTests.cs` case: `OccamMcpCli.Parse(["--version"])` should either surface a real version (new verb) or `IsValid==false` — and a doc/Dockerfile fix to use `occam version-surface` (which exists and exits) for the health probe.

---

## EF-058 — Playbook signature excludes the whole `provenance` block; quality claims + keyId are unsigned

- **ENVIRONMENT:** C# harness (ephemeral ECDsa P-256 key; no network).
- **COMMAND:** `dotnet run --project docs-audit/repro/P6Repro.csproj -- --case=EF-058`
- **INPUT:** A minimal playbook signed via `PlaybookSignature.BuildSignedJson(score:41, passesGate:false, noise:0.42)`. Then, on the signed JSON, mutate fields inside `provenance` and re-run `Verify` / `Inspect`.
- **EXPECTED:** `ContentHash` excludes top-level `provenance`, so editing `provenance.verify.{score,passesGate,noiseLeakage}` or `provenance.signedAt` does not invalidate the signature; swapping `provenance.keyId` makes `Inspect` branch to `unknown_key` (a softer verdict) rather than `invalid`.
- **OBSERVED:**
  ```
  CASE EF-058 | baseline signed playbook inspects verified | OBSERVED=verified,score=41,passesGate=False | PASS
  CASE EF-058 | forged verify{} still passes signature Verify | OBSERVED=True | PASS
  CASE EF-058 | forged verify{} inspects as verified with forged score | OBSERVED=verified,score=100,passesGate=True | PASS
  CASE EF-058 | forged signedAt still passes signature Verify | OBSERVED=True | PASS
  CASE EF-058 | keyId swap yields unknown_key (not invalid) | OBSERVED=unknown_key,keyId=0000000000000000,score=41 | PASS
  CASE EF-058 | control: signed-body tamper is caught as invalid | OBSERVED=invalid | PASS
  ```
- **PASS/FAIL/BLOCKED:** **CONFIRMED (PASS).** A tampered recipe advertising `score=100, passesGate=true` (up from the signed `41/false`) still verifies and inspects as `verified`. The control confirms the signed body *is* protected.
- **CODE PATH:** `Playbooks/PlaybookSignature.cs:30-40` (`ContentHash` → `WriteCanonical(..., excludeTopKey:"provenance")`), `:97-134` (`Inspect` reads `verify`/`keyId` from the unsigned block; `:128-131` returns `unknown_key` on keyId mismatch), `:143-161` (`Verify`).
- **REGRESSION TEST CREATED?** No. Propose a security unit test (e.g. `benchmarks/l0-gate/PlaybookLintUnitTests.cs` or a new `PlaybookSignatureUnitTests`) asserting that mutating `provenance.verify.score` after signing flips the verdict to `invalid`, once the signed scope is fixed to cover the quality claim.

---

## EF-059 — Wholly-unsigned watch chain returns `history_verified` (exit 0)

- **ENVIRONMENT:** C# harness for construction + shipped host binary for the CLI/operator surface.
- **COMMAND:** `dotnet run … -- --case=EF-059` (writes `ef059-chain.json` + `ef059-unrelated.pem`), then `OccamMcp.Core.exe verify --mode history --input ef059-chain.json --pubkey ef059-unrelated.pem`.
- **INPUT:** A 3-entry chain built with `signer: null` (the `OCCAM_RECEIPTS=off` path) — every entry has `Sig == null`. Verified against an **unrelated** public key.
- **EXPECTED:** `WatchHistoryChain.Verify` skips the signature check for null-`Sig` entries, so a fully unsigned but correctly-linked chain returns `true`; the tool/CLI label it `history_verified` regardless of key.
- **OBSERVED:**
  ```
  CASE EF-059 | wholly unsigned chain verifies against an unrelated key | OBSERVED=entries=3,signed=0,verify=True | PASS
  CASE EF-059 | control: broken link still fails | OBSERVED=False | PASS
  CASE EF-059 | fabricated unsigned chain also verifies | OBSERVED=True | PASS
  ```
  CLI (operator-visible):
  ```
  {"ok":true,"mode":"history","verdict":"history_verified","message":"3 entries"}   (exit 0)
  ```
- **PASS/FAIL/BLOCKED:** **CONFIRMED (PASS).** An unsigned chain — and a freshly fabricated one with arbitrary content hashes — both return `history_verified` / exit 0 against a key that signed nothing. The hash-link is still enforced (broken link → fail), so this is link-integrity presented under a verification-sounding verdict name.
- **CODE PATH:** `Watch/WatchHistory.cs:132-163` (`Verify`; `:155` `if (e.Sig is not null …)` skips unsigned). Verdict naming: `Tools/OccamVerifyTool.cs:92-107` and CLI `Cli/OccamCliVerbs.cs:403-408` (`history_verified`).
- **REGRESSION TEST CREATED?** No. `benchmarks/l0-gate/ReceiptUnitTests.cs:264-266` currently *asserts* the unsigned-chain-verifies behavior. Propose distinguishing verdicts (e.g. `history_linked` vs `history_verified`) and updating that assertion.

---

## EF-060 — Merkle duplicate-last-leaf ambiguity (CVE-2012-2459 shape)

- **ENVIRONMENT:** C# harness (pure functions).
- **COMMAND:** `dotnet run … -- --case=EF-060`
- **INPUT:** 3 leaves `[alpha#a, beta#b, gamma#c]` vs 4 leaves `[…, gamma#c, gamma#c]` (last leaf duplicated), through `MerkleTree.Root`, `LeafHashesHex`+`RootFromLeafHashes`, and `Proof`/`VerifyProof`.
- **EXPECTED:** Odd levels duplicate the last node, so a 3-leaf tree and a 4-leaf tree whose 4th leaf equals the 3rd produce the **same root** — the leaf count is not bound by the root, and a membership proof for the phantom 4th leaf validates against the 3-leaf root.
- **OBSERVED:**
  ```
  CASE EF-060 | 3-leaf root == 4-leaf duplicate-last root | OBSERVED=912e461975fa28d1==912e461975fa28d1 | PASS
  CASE EF-060 | leaf count is not recoverable from the root | OBSERVED=count3=3,count4=4,sameRoot=True | PASS
  CASE EF-060 | RootFromLeafHashes collides identically | OBSERVED=912e461975fa28d1==912e461975fa28d1 | PASS
  CASE EF-060 | proof for the phantom 4th leaf verifies against the 3-leaf root | OBSERVED=True | PASS
  CASE EF-060 | control: different content -> different root | OBSERVED=distinct | PASS
  ```
- **PASS/FAIL/BLOCKED:** **CONFIRMED (PASS).** The structural ambiguity is present through both the block API and the leaf-hash API used by receipts / live verify; leaf-count-derived quantities are unsigned. (Observation-class: the collision needs a crafted duplicate-last leaf set; it is not a break of the signed root over a fixed leaf list.)
- **CODE PATH:** `Receipts/MerkleTree.cs:74-101` (`Root`, `:93` `duplicate last when odd`), `:42-68` (`RootFromLeafHashes`, same padding), `:109-148` (`Proof`, `:130` odd-tail duplicates itself).
- **REGRESSION TEST CREATED?** No. Propose a `benchmarks/l0-gate/ReceiptUnitTests.cs` case asserting that a duplicate-last leaf set is rejected or domain-separated (e.g. length-tagging / distinct internal-vs-leaf prefixes) once mitigated.

---

## EF-043 — css-extract fetches without the private-IP pin and without the body cap (parity gap)

- **ENVIRONMENT:** Node; loopback HTTP server on `127.0.0.1`; `OCCAM_MAX_RESPONSE_BYTES=65536`.
- **COMMAND:** `node docs-audit/repro/ef043-css-probe.mjs`
- **INPUT:** A local server on `127.0.0.1:<port>` serving a ~2 MiB HTML body; `css-extract.mjs <loopback-url> <fields.json>` with the byte cap set to 64 KiB.
- **EXPECTED:** `css-extract.mjs` calls `egressFetch`, which (no proxy configured) is a bare `fetch` with no `private-ip` DNS pin and no `response-body-cap` — unlike `http-extract`. So it should reach the loopback target (no `private_url_blocked`) and read the full 2 MiB body despite the cap.
- **OBSERVED:**
  ```
  CASE EF-043 | css-extract reaches loopback (no SSRF block) | OBSERVED=ok=true,title=PRIVATE-LOOPBACK-TITLE,failure=none | PASS
  CASE EF-043 | OCCAM_MAX_RESPONSE_BYTES (64KiB) ignored by css-extract | OBSERVED=html_length=2097283,cap=65536 | PASS
  ```
- **PASS/FAIL/BLOCKED:** **CONFIRMED (PASS).** css-extract fetched a loopback/private target with no SSRF refusal and returned a 2,097,283-byte body while the cap was 65,536 — both parity gaps demonstrated live.
- **CODE PATH:** `workers/css-extract/css-extract.mjs:3,39-46` (imports/uses `egressFetch`; no `private-ip.mjs` / `response-body-cap.mjs` import) vs `workers/http-extract/lib/http-extract-run.mjs:26` (imports both). `egressFetch` no-proxy path = plain `fetch`: `workers/shared/lib/egress-proxy.mjs:189-193`. (EF-013 Nuxt-eval in `css-schema-extract` remains OPEN and untouched here.)
- **REGRESSION TEST CREATED?** No. Propose extending `workers/shared/lib/private-ip.selftest.mjs` / a new `css-extract.selftest.mjs` to assert css-extract refuses loopback and honors the byte cap once the guards are added.

---

## EF-054 — `occam-session import` retains raw plaintext `cookies.txt` under `_imports/`

- **ENVIRONMENT:** Node; `OCCAM_SESSIONS_ROOT` → temp dir; shipped `scripts/occam-session.mjs`.
- **COMMAND:** `node scripts/occam-session.mjs import --from <cookies.txt> --host example.com --id example.com.work` then `node scripts/occam-session.mjs list`.
- **INPUT:** A Netscape `cookies.txt` containing `SECRETSESSION=super-secret-token-ABC123`, imported with default flags (no `--no-keep-import`).
- **EXPECTED:** `keepImport` defaults true → the source file is `copyFileSync`'d verbatim into `<root>/_imports/`, retaining plaintext cookies; `list` prints header key names only (no secret values).
- **OBSERVED:**
  ```
  IMPORT_FILE_EXISTS=True
  PLAINTEXT_SECRET_PRESENT=True        (…/_imports/p6-cookies2.txt still contains super-secret-token-ABC123)
  LIST_LEAKS_SECRET=False
  LIST_SHOWS_HEADER_KEYS=True          (list shows "Cookie", not its value)
  ```
- **PASS/FAIL/BLOCKED:** **CONFIRMED (PASS).** After a default import, the raw plaintext cookie file persists on disk under `_imports/`, while `list` (correctly) hides values — the exact mismatch the finding describes (list ≠ what is retained on disk).
- **CODE PATH:** `scripts/occam-session.mjs:123-128` (`keepImport` default + `copyFileSync` to `_imports/`), `:159-173` (profile `source` points at `_imports/…`), `:74-94` (`cmdList` emits `headerKeys` only). Layout: `scripts/lib/occam-sessions-lib.mjs:103-106,159-162`.
- **REGRESSION TEST CREATED?** No. Propose a `scripts`-level selftest (or doc note) that default import either does not retain, encrypts, or restrictively-permissions `_imports/`; and that `list`/docs disclose the retained plaintext.

---

## EF-002 — Anonymous browser context reuse (cookie bleed) — source-confirmed, runtime BLOCKED

- **ENVIRONMENT:** Would require live Playwright Chromium + two cross-site pages that set cookies. Not cheap in this environment (no browser provisioned; heavy).
- **COMMAND:** (not executed)
- **INPUT / EXPECTED:** Two consecutive **anonymous** browser extracts (both `headersFile==null`, `storageStateFile==null`) should reuse the same `BrowserContext`; cookies set by page A's response would persist into page B's fetch because the pool only recycles on a *changed* headers/storageState value, not for anon→anon.
- **OBSERVED:** n/a (not run).
- **PASS/FAIL/BLOCKED:** **BLOCKED (runtime).** Source strongly supports it: `workers/browser-extract/lib/browser-pool.mjs:26-58` reuses `this.#session` and recycles only when `options.headersFile !== this.#headersFile` or the storageState differs (`:31-38`); two anonymous calls leave both unchanged → no recycle → same context. This matches EF-040's refinement (session→anon *does* recycle at `:35-36`; the bleed vector is anon→anon). Context creation is a single `browser.newContext` (`browser-session.mjs:149`). Finding remains valid on code evidence.
- **CODE PATH:** `workers/browser-extract/lib/browser-pool.mjs:26-58,113-121`; `workers/browser-extract/lib/browser-session.mjs:149`.
- **REGRESSION TEST CREATED?** No. Propose an `L7ResourceSafety` / browser-pool live case: extract page A (sets a cookie) then anonymous page B on a different origin and assert B's request carries no cookie from A — gated behind Playwright availability.

---

## Summary

| Finding | Result | Evidence |
|---------|--------|----------|
| EF-041 | **CONFIRMED** | live daemon PID killed by 2nd DI build; healthy slots 1→0 |
| EF-045 | **CONFIRMED** | identical cache + materialization keys for `#installation` vs `#uninstall` |
| EF-051 | **CONFIRMED (root cause)** / Docker end-state BLOCKED | `--version` boots stdio server; open-stdin never returns |
| EF-058 | **CONFIRMED** | forged `verify.score=100,passesGate=true` still `verified` |
| EF-059 | **CONFIRMED** | unsigned/fabricated chain → `history_verified`, exit 0, wrong key |
| EF-060 | **CONFIRMED** | 3-leaf root == 4-leaf duplicate-last root; phantom-leaf proof verifies |
| EF-043 | **CONFIRMED** | css-extract reads loopback + 2 MiB body past 64 KiB cap |
| EF-054 | **CONFIRMED** | plaintext `cookies.txt` retained in `_imports/`; `list` hides values |
| EF-002 | **BLOCKED (runtime), source-confirmed** | anon→anon pool reuse; no recycle path |

No finding was refuted. Nothing under `README.md`, `INSTALL.md`, `docs/`, product code, or shipped
tests was changed. Harness is re-runnable from `docs-audit/repro/`.
