#!/usr/bin/env bash
# Thin wrapper around the shared JDMallen publish engine (publish-dotnet.sh,
# vendored from https://github.com/jdmallen/toolbox). All arguments (runtimes,
# -v/--version, -h/--help) are handled by the engine.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

project="src/JDMallen.IPMITempMonitor/JDMallen.IPMITempMonitor.csproj"
exe_name="JDMallen.IPMITempMonitor"

# macOS RIDs are intentionally omitted: the app only supports Linux and Windows
# (Settings.Platform throws PlatformNotSupportedException elsewhere), so an osx
# binary would fail at launch. Add osx-x64/osx-arm64 here only if that changes.
known_runtimes=(
	"linux-x64" "linux-arm64" "linux-musl-x64" "linux-musl-arm64"
	"win-x64" "win-arm64" "win-x86"
)
default_runtimes=(linux-x64 linux-arm64 win-x64 win-arm64)

# Ship release binaries without debug symbols.
extra_publish_args=(-p:DebugType=none -p:DebugSymbols=false)

source "$repo_root/scripts/publish-dotnet.sh"
