# Dev container

Gets you to a passing `dotnet test` and a working `occam canary selftest`. It does **not** provision
a browser.

```bash
dotnet test tests/OccamMcp.Core.Tests/OccamMcp.Core.Tests.csproj -c Release
dotnet run --project src/FFOccamMcp.Core -c Release -- canary selftest
dotnet run --project src/FFOccamMcp.Core -c Release -- canary smoke
```

## What is deliberately not here

**Playwright Chromium.** The browser backend and the `benchmarks/l0-gate` integration gate need it,
and it is a ~150 MB download plus system libraries. Installing it on every container create would
make the environment slow to build for the majority of changes, which touch neither. Provision it
explicitly when you need it:

```bash
cd workers && npm ci --no-fund --no-audit
cd .. && dotnet run --project src/FFOccamMcp.Core -- install-browser
```

**A published AOT binary.** `dotnet publish -c Release -r linux-x64` works inside the container but
takes about a minute, so it is not part of container creation.

## Which verification layer to use

Deterministic code (the canary, compile/budget logic, receipts) belongs in
`tests/OccamMcp.Core.Tests` and runs here in well under a second. Live extraction belongs in the L0
gate and needs the browser. `docs/testing/README.md` explains why the two are kept separate and why
a single blended coverage number over this repository would be misleading.
