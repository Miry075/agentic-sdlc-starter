#!/usr/bin/env bash
# PreToolUse hook for Claude Code: blocks access to classified files.
# Reads the tool call as JSON on stdin. Exit code 2 blocks the call and sends stderr to the agent.
# Patterns: .claude/classified-paths.txt (extended regex, one per line).
# Requires: jq.

set -euo pipefail

input="$(cat)"
patterns_file="${CLAUDE_PROJECT_DIR:-.}/.claude/classified-paths.txt"

[[ -f "$patterns_file" ]] || exit 0

# File tools use file_path; Grep and Glob use path.
target="$(jq -r '.tool_input.file_path // .tool_input.path // empty' <<<"$input")"
[[ -n "$target" ]] || exit 0

while IFS= read -r pattern; do
  [[ -z "$pattern" || "$pattern" == \#* ]] && continue
  if [[ "$target" =~ $pattern ]]; then
    echo "Blocked: '$target' is classified (matches '$pattern'). Use the knowledge gateway or ask a human." >&2
    exit 2
  fi
done < "$patterns_file"

exit 0
