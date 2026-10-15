#!/usr/bin/env bash
# Fails when a change touches the project structure without updating any AGENTS.md.
#
# Usage: scripts/check-agents-md-freshness.sh [base-ref]
#   base-ref  branch or commit to compare with (default: origin/main)
#
# Skip for one PR: set SKIP_AGENTS_MD_CHECK=true (state the reason in the PR).

set -euo pipefail

base="${1:-origin/main}"

if [[ "${SKIP_AGENTS_MD_CHECK:-false}" == "true" ]]; then
  echo "AGENTS.md freshness check skipped (SKIP_AGENTS_MD_CHECK=true)."
  exit 0
fi

# Files changed in this branch compared with base, with status (A/M/D/R...).
changes="$(git diff --name-status "$base"...HEAD)"

if [[ -z "$changes" ]]; then
  echo "No changes compared with $base."
  exit 0
fi

# Structural changes: solution/project/build files modified, or any project file added, deleted or renamed.
structural_files='(\.slnx?|\.csproj|\.fsproj|\.vbproj|Directory\.Build\.(props|targets)|Directory\.Packages\.props|global\.json|Dockerfile|docker-compose[^/]*\.ya?ml|package\.json|azure-pipelines[^/]*\.ya?ml|\.github/workflows/[^/]+\.ya?ml)$'

structural="$(awk '{print $NF}' <<<"$changes" | grep -E "$structural_files" || true)"
moved="$(grep -E '^(A|D|R[0-9]*)[[:space:]]' <<<"$changes" | grep -E '\.(cs|fs|vb)proj' || true)"

if [[ -z "$structural" && -z "$moved" ]]; then
  echo "No structural change. AGENTS.md check passed."
  exit 0
fi

if awk '{print $NF}' <<<"$changes" | grep -qE '(^|/)AGENTS\.md$'; then
  echo "Structural change detected and AGENTS.md updated. Check passed."
  exit 0
fi

echo "::error::The project structure changed but no AGENTS.md was updated."
echo
echo "Structural changes:"
{ echo "$structural"; echo "$moved" | awk '{print $NF}'; } | grep -v '^$' | sort -u | sed 's/^/  - /'
echo
echo "Update AGENTS.md (build commands, layout, conventions) so agents don't work from stale context."
echo "If no update is needed, set SKIP_AGENTS_MD_CHECK=true for this PR and say why."
exit 1
