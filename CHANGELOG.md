# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.1]

A maintenance release covering build, packaging, and internal hosting cleanups.
There are **no changes to runtime behavior or user-facing configuration**.

### Added

- **Release checksums**: each GitHub release now ships a `SHA256SUMS.txt`
  alongside the archives so downloads can be verified.
- **`nuget.config`** pinning `nuget.org` as the sole package source for
  reproducible restores.

### Changed

- **Re-adopted the `JDMallen.Toolbox.Hosting` package (3.0.0)** for the scoped
  background-service hosting primitives, replacing the local copy that was
  vendored in 2.1.0. The `ScopedBackgroundService`, `OverlapBehavior`, and their
  source-generated log messages have been removed in favor of the package. This
  is an internal change with no effect on configuration or runtime behavior.
- **Refactored `scripts/publish.sh`** into a thin wrapper around a shared,
  vendored publish engine (`scripts/publish-dotnet.sh`); its command-line
  interface (runtimes, `-v`/`--version`, `-h`/`--help`) is unchanged.

## [2.1.0]

A large internal refactor and modernization. There are **no breaking changes to
the user-facing configuration**: existing `appsettings.json` files and
`Settings__*` environment variables continue to work unchanged (the .NET
configuration system binds setting keys case-insensitively), and the raw IPMI
commands sent to the iDRAC are byte-for-byte identical to 2.0.0.

### Added

- **Automated publishing** via `scripts/publish.sh`, which produces
  self-contained, single-file binaries and release archives for the default
  runtimes (`linux-x64`, `linux-arm64`, `win-x64`, `win-arm64`). Other Linux and
  Windows RIDs are supported as arguments; run `scripts/publish.sh --help` for
  the full list.
- **Single source of truth for the version**: the `<Version>` property in the
  csproj is read by the publish script to stamp binaries and name archives.
- **Fail-fast configuration validation**: settings are validated at startup
  (`ValidateOnStart`) against their data annotations. `IPMIHost`, `IPMIUser`, and
  `RegexToRetrieveTemp` are now required, numeric settings have sensible ranges,
  and `RegexToRetrieveTemp` must be a valid regular expression. Misconfiguration
  now stops the service immediately with a clear error instead of failing later
  at first use.
- **Docker support**, validated end to end: a modern multi-stage `Dockerfile`
  running as a non-root user, a `docker-compose.yml` that reads connection
  details from a git-ignored `.env` file, and a `.env.example` template. The
  monitor talks to the iDRAC over the network, so the container needs no host
  device passthrough or extra capabilities.
- **Unit test suite** (`test/JDMallen.IPMITempMonitor.Tests`) covering the fan
  controller, IPMI command executor, temperature monitor, worker loop, and
  settings validation.
- Expanded README sections for building & publishing and running as a Docker
  container.

### Changed

- **Password handling hardened**: the iDRAC password is no longer passed to
  `ipmitool` on the command line (`-P`). It is supplied via the `IPMI_PASSWORD`
  environment variable on the `ipmitool` child process and invoked with the `-E`
  flag, so the password is no longer visible in the host's process list (e.g.
  via `ps` or `/proc`). The behavior is otherwise equivalent; `-E` is supported
  by all modern builds of `ipmitool`.
- **Upgraded to .NET 10** (from .NET 5).
- **Upgraded dependencies**: `Microsoft.Extensions.*` to 10.0.2 and Polly to 8.x.
- **Restructured the codebase** into `src/` and `test/` projects, splitting the
  monolithic `Worker` into dedicated services (`IPMICommandExecutor`,
  `TemperatureMonitor`, `FanController`) behind interfaces, with source-generated
  logging. These are internal changes with no effect on configuration or runtime
  behavior.

### Removed

- Dropped the `JDMallen.Toolbox.Hosting` dependency in favor of the built-in
  .NET hosting primitives.

### Security

- The iDRAC password is no longer exposed on the command line or in the process
  list — see the password handling note under **Changed**.

[2.1.1]: https://github.com/jdmallen/dell-ipmi-fan-control-monitor/releases/tag/2.1.1
[2.1.0]: https://github.com/jdmallen/dell-ipmi-fan-control-monitor/releases/tag/2.1.0
