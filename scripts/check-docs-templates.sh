#!/usr/bin/env bash
# Checks that ADRs and specs contain the sections agents rely on.
#
# Usage: scripts/check-docs-templates.sh [repo-root]

set -euo pipefail

root="${1:-.}"
errors=0

check_sections() {
  local file="$1"; shift
  local missing=()
  for section in "$@"; do
    grep -qE "^## ${section}\b" "$file" || missing+=("$section")
  done
  if (( ${#missing[@]} > 0 )); then
    echo "::error file=$file::Missing sections: ${missing[*]}"
    errors=$((errors + 1))
  fi
}

shopt -s nullglob

for adr in "$root"/docs/adr/[0-9][0-9][0-9][0-9]-*.md; do
  [[ "$(basename "$adr")" == 0000-* ]] && continue
  check_sections "$adr" "Status" "Context" "Decision" "Consequences"
  if ! grep -qE '^(Proposed|Accepted|Deprecated|Superseded)' "$adr"; then
    echo "::error file=$adr::Status must be Proposed, Accepted, Deprecated or Superseded."
    errors=$((errors + 1))
  fi
done

for spec in "$root"/docs/specs/*.md; do
  [[ "$(basename "$spec")" == TEMPLATE.md ]] && continue
  check_sections "$spec" "Goal" "Acceptance criteria" "Non-goals"
done

if (( errors > 0 )); then
  echo "$errors document(s) do not follow their template."
  exit 1
fi

echo "All ADRs and specs follow their template."
