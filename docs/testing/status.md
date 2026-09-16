# Host availability during the cross-platform run

Recorded 2026-09-15. Reachability was checked before any build, so an unreachable host produces an
explicit gap rather than a missing row.

| Host | Access | Reachable | Result |
|---|---|---|---|
| `macmini` — Mac mini, macOS 26.6.2 arm64 | SSH (key auth, LAN) | ✅ | Full run completed, artefacts in `macos-arm64/` |
| `hermesvm` — Ubuntu 24.04.4 x64 | SSH via `proxmox-lan` jump host | ✅ | Full run completed, artefacts in `linux-x64/` |
| local — Windows 11 Pro x64 | local shell | ✅ | Full run completed, artefacts in `windows-x64/` |

Reachability probes:

```
$ ssh macmini "uname -a"
Darwin Macmini 25.6.0 … RELEASE_ARM64_T8103 arm64

$ ssh hermesvm "uname -a"
Linux ubuntu-docker 6.8.0-136-generic … x86_64 GNU/Linux
```

## Environment preparation

Neither remote host had a .NET SDK. Both were provisioned with the official install script into the
**user** directory only:

```
curl -fsSL https://dot.net/v1/dotnet-install.sh | sh -s -- --channel 10.0 --install-dir $HOME/.dotnet
```

No package manager was invoked, no system package was installed, and nothing was written outside
`~/occam-test/` and `~/.dotnet/` on either machine. Native AOT publish succeeded without any
additional system toolchain on all three hosts.

## Platforms with no host available

Not tested, and not claimed anywhere in the documentation:

- Linux arm64 (including Raspberry Pi and ARM cloud instances)
- macOS x64 (Intel Macs)
- Windows arm64
- Alpine / musl libc
- FreeBSD

`linux-musl-x64` in particular is worth calling out: the Linux binary above is glibc-linked
(`interpreter /lib64/ld-linux-x86-64.so.2`), so it will not run on Alpine as published.
