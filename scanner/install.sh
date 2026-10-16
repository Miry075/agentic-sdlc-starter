#!/usr/bin/env bash
# Builds the scanner from this checkout and installs or updates the global `ai-readiness` tool.
# Each run gets a new version number, so the NuGet cache never serves an older build.
#
# Usage: scanner/install.sh
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
version="1.$(date -u +%Y%m%d).$((10#$(date -u +%H%M%S)))"
out="$(mktemp -d)"
trap 'rm -rf "$out"' EXIT

dotnet pack "$here/src/AiReadiness.Scanner" -c Release -o "$out" -p:PackageVersion="$version" -p:InformationalVersion="$version" --nologo -v quiet

if dotnet tool list --global | grep -qi '^ai-readiness '; then
  dotnet tool update --global ai-readiness --add-source "$out" --version "$version" --ignore-failed-sources
else
  dotnet tool install --global ai-readiness --add-source "$out" --version "$version" --ignore-failed-sources
fi

echo "Installed ai-readiness $version. Check with: ai-readiness --version"
case ":$PATH:" in
  *":$HOME/.dotnet/tools:"*) ;;
  *) echo "Add the tools folder to your PATH: export PATH=\"\$PATH:\$HOME/.dotnet/tools\"" ;;
esac
