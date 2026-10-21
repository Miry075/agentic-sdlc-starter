#!/usr/bin/env bash
# Publishes the knowledge gateway to a folder you can run without the source code,
# with a launcher (run.sh) and a settings file (gateway.env) next to it.
#
# Usage: scripts/publish-gateway.sh [--output <dir>] [--self-contained] [--skip-tests]
#
#   --output <dir>      where to publish (default: ~/.knowledge-gateway)
#   --self-contained    include the .NET runtime (no SDK needed on the target machine)
#   --skip-tests        do not run the test suite before publishing
#
# Re-running it updates the published gateway and keeps your gateway.env.

set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="$HOME/.knowledge-gateway"
self_contained=false
run_tests=true

while [[ $# -gt 0 ]]; do
  case "$1" in
    --output) output="${2:?--output needs a folder}"; shift 2 ;;
    --self-contained) self_contained=true; shift ;;
    --skip-tests) run_tests=false; shift ;;
    -h|--help) sed -n '2,11p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done

command -v dotnet >/dev/null || { echo "The .NET SDK is required: https://dotnet.microsoft.com/download" >&2; exit 2; }

if [[ "$run_tests" == true ]]; then
  echo "Running the tests..."
  dotnet test "$repo/Knowledge.Gateway.sln" --nologo -v quiet
fi

publish_args=(--configuration Release --nologo -v quiet --output "$output")
if [[ "$self_contained" == true ]]; then
  case "$(uname -s)-$(uname -m)" in
    Darwin-arm64) rid=osx-arm64 ;;
    Darwin-x86_64) rid=osx-x64 ;;
    Linux-x86_64) rid=linux-x64 ;;
    Linux-aarch64) rid=linux-arm64 ;;
    *) echo "Unsupported platform for --self-contained: $(uname -s)-$(uname -m)" >&2; exit 2 ;;
  esac
  publish_args+=(--self-contained true --runtime "$rid")
else
  publish_args+=(--self-contained false)
fi

echo "Publishing to $output..."
mkdir -p "$output"
dotnet publish "$repo/src/Knowledge.Gateway/Knowledge.Gateway.csproj" "${publish_args[@]}"

# The policy and the demo repository are read from relative paths next to the binaries.
rm -rf "$output/policy" "$output/samples"
cp -R "$repo/policy" "$output/policy"
mkdir -p "$output/samples"
cp -R "$repo/samples/demo-repo" "$output/samples/demo-repo"

if [[ "$self_contained" == true ]]; then
  start='exec "$here/Knowledge.Gateway" "$@"'
else
  start='exec dotnet "$here/Knowledge.Gateway.dll" "$@"'
fi

cat > "$output/run.sh" <<EOF
#!/usr/bin/env bash
# Starts the published knowledge gateway. Settings come from gateway.env next to this file.
set -euo pipefail
here="\$(cd "\$(dirname "\${BASH_SOURCE[0]}")" && pwd)"
cd "\$here"
if [[ -f "\$here/gateway.env" ]]; then
  set -a
  . "\$here/gateway.env"
  set +a
fi
export ASPNETCORE_ENVIRONMENT="\${ASPNETCORE_ENVIRONMENT:-Development}"
export ASPNETCORE_URLS="\${ASPNETCORE_URLS:-http://localhost:5280}"
$start
EOF
chmod +x "$output/run.sh"

if [[ ! -f "$output/gateway.env" ]]; then
  cat > "$output/gateway.env" <<'EOF'
# Knowledge gateway settings, read by run.sh. No secrets here: dev mode uses your own `az login`.
# Quote values that contain spaces, for example Repo__Path="/Users/you/My Projects/repo".
# With everything below commented out, the gateway serves the demo repository in dev mode.

# ASPNETCORE_URLS=http://localhost:5280

# Your repository (absolute path):
# Repo__Source=FileSystem
# Repo__Path=/Users/you/path/to/your/repo

# Your Azure DevOps work items, read with your `az login`:
# Auth__Dev__UseAzureCli=true
# WorkItems__Source=AzureDevOps
# WorkItems__AzureDevOps__Organization=your-org
# WorkItems__AzureDevOps__Project=your-project
EOF
  echo "Created $output/gateway.env: edit it to point at your repository and Azure DevOps."
else
  echo "Kept your $output/gateway.env."
fi

cat <<EOF

Published the knowledge gateway to $output

Start it:        $output/run.sh
Check it:        curl http://localhost:5280/health/ready
Claude Code:     claude mcp add --scope user --transport http knowledge-gateway http://localhost:5280/mcp
Claude desktop:  add to claude_desktop_config.json under "mcpServers":
                 "knowledge-gateway": { "command": "npx", "args": ["-y", "mcp-remote", "http://localhost:5280/mcp"] }
EOF
