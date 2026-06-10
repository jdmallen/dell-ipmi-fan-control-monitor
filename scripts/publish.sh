#!/usr/bin/env bash
# Publishes self-contained, single-file binaries of the IPMI temperature monitor
# for every supported runtime, then bundles each into a release archive.
#
# Usage:
#   scripts/publish.sh                         # publish all default runtimes
#   scripts/publish.sh linux-arm64             # publish a single runtime
#   scripts/publish.sh linux-x64 win-x64       # publish a specific set
#   scripts/publish.sh -v 2.2.0 linux-x64      # override the version
#
# Output:
#   dist/<rid>/JDMallen.IPMITempMonitor[.exe]                  (raw binary)
#   dist/JDMallen.IPMITempMonitor-<version>-<rid>.{tar.gz,zip} (release archive)
#
# Run with -h or --help to list the known and default runtimes.
#
# The version defaults to the <Version> in the project file, so the csproj is the
# single source of truth; pass -v/--version to override it for a one-off build.

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

project="src/JDMallen.IPMITempMonitor/JDMallen.IPMITempMonitor.csproj"
exe_name="JDMallen.IPMITempMonitor"
output_dir="dist"

# macOS RIDs are intentionally omitted: the app only supports Linux and Windows
# (Settings.Platform throws PlatformNotSupportedException elsewhere), so an osx
# binary would fail at launch. Add osx-x64/osx-arm64 here only if that changes.
KNOWN_RUNTIMES=(
	"linux-x64" "linux-arm64" "linux-musl-x64" "linux-musl-arm64"
	"win-x64" "win-arm64" "win-x86"
)

DEFAULT_RUNTIMES=("linux-x64" "linux-arm64" "win-x64" "win-arm64")

# Pull the default version straight from the project file so the csproj remains
# the single source of truth.
version="$(grep -oP '(?<=<Version>)[^<]+' "$project" | head -n1)"

print_help() {
	echo "Usage: $0 [-v|--version <version>] [runtime ...]"
	echo ""
	echo "Publishes self-contained binaries for the given runtime identifiers (RIDs)."
	echo ""
	echo "Known runtimes:"
	printf '  %s\n' "${KNOWN_RUNTIMES[@]}"
	echo ""
	echo "If no runtimes are given, publishes for the defaults:"
	printf '  %s\n' "${DEFAULT_RUNTIMES[@]}"
	echo ""
	echo "Version defaults to <Version> in the csproj (currently $version)."
}

# Parse a leading -v/--version flag, then treat the rest as runtimes.
runtimes=()
while [[ $# -gt 0 ]]; do
	case "$1" in
		-h|--help)
			print_help
			exit 0
			;;
		-v|--version)
			version="${2:?--version requires a value}"
			shift 2
			;;
		-*)
			echo "Error: unknown option '$1'." >&2
			exit 1
			;;
		*)
			runtimes+=("$1")
			shift
			;;
	esac
done

if [[ ${#runtimes[@]} -eq 0 ]]; then
	runtimes=("${DEFAULT_RUNTIMES[@]}")
fi

if [[ -z "$version" ]]; then
	echo "Error: could not determine version (no <Version> in $project)." >&2
	exit 1
fi

# Validate every requested runtime up front so a typo fails fast.
for runtime in "${runtimes[@]}"; do
	match=""
	for known in "${KNOWN_RUNTIMES[@]}"; do
		[[ "$runtime" == "$known" ]] && match="yes" && break
	done
	if [[ -z "$match" ]]; then
		echo "Error: unknown runtime '$runtime'." >&2
		echo "Known runtimes: ${KNOWN_RUNTIMES[*]}" >&2
		exit 1
	fi
done

rm -rf "$output_dir"
mkdir -p "$output_dir"

echo "Publishing JDMallen.IPMITempMonitor v$version"
echo ""

for runtime in "${runtimes[@]}"; do
	echo ">>> Publishing $runtime"
	runtime_dir="$output_dir/$runtime"

	dotnet publish "$project" \
		--configuration Release \
		--runtime "$runtime" \
		--self-contained true \
		--output "$runtime_dir" \
		-p:Version="$version" \
		-p:PublishSingleFile=true \
		-p:IncludeNativeLibrariesForSelfExtract=true \
		-p:DebugType=none \
		-p:DebugSymbols=false \
		--nologo \
		--verbosity minimal

	# Bundle just the executable into a release archive named with version + RID.
	if [[ "$runtime" == win-* ]]; then
		archive="$output_dir/$exe_name-$version-$runtime.zip"
		(cd "$runtime_dir" && zip -q "$repo_root/$archive" "$exe_name.exe")
	else
		chmod +x "$runtime_dir/$exe_name"
		archive="$output_dir/$exe_name-$version-$runtime.tar.gz"
		tar -C "$runtime_dir" -czf "$archive" "$exe_name"
	fi

	echo "    -> $archive ($(du -h "$archive" | cut -f1))"
	echo ""
done

echo "Done. Artifacts in $output_dir/"
ls -lh "$output_dir"/*.{tar.gz,zip} 2>/dev/null
