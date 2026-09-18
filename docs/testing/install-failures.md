# Install failure regressions

Committed catalog of real install failures turned into tests.
Engineering write-ups (when present locally): `docs-internal/install-failures/` (gitignored).

| Id | Symptom | Test |
|----|---------|------|
| IF-01 | Clean VM, no Node | `clean-install-regression.selftest.mjs` + CI `clean-vm-no-node` |
| IF-02 | Agent builds from source / fears .NET | same selftest (INSTALL.md + AGENTS.md asserts) + CI matrix without `setup-dotnet` |
| IF-03 | Empty `workers/node_modules` | `install-workers.selftest.mjs` |

Machine-readable: [`corpora/install-failures.jsonl`](../../corpora/install-failures.jsonl).
CI: [`.github/workflows/clean-install.yml`](../../.github/workflows/clean-install.yml).
