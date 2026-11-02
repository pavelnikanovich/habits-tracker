#!/usr/bin/env bash
# SubagentStop: when the working tree differs under a source folder from the snapshot taken when the
# subagent started (committed or not, untracked files included; edits already there at the start do
# not count), runs scripts/test-fast.sh. A red run keeps
# the subagent working: the last 40 lines go to stderr and the hook exits 2 (process document, "Epic 1").
# A subagent already sent back once (stop_hook_active true) sees the red tail again but may stop,
# so a subagent that cannot get the tests green reports instead of looping.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/working-tree-snapshot.sh"

input="$(cat)"
agent_id="$(printf '%s' "$input" | sed -n 's/.*"agent_id"[[:space:]]*:[[:space:]]*"\([A-Za-z0-9_-]*\)".*/\1/p' | head -n 1)"
[ -n "$agent_id" ] || exit 0
red_exit=2
if printf '%s' "$input" | grep -q '"stop_hook_active"[[:space:]]*:[[:space:]]*true'; then
  red_exit=0
fi

root="$(git -C "${CLAUDE_PROJECT_DIR:-$PWD}" rev-parse --show-toplevel)"
record="$root/.dev/subagents/$agent_id"
# An agent started before the SubagentStart hook was registered has no record to compare with.
[ -f "$record" ] || exit 0
at_start="$(cat "$record")"

cd "$root"
at_stop="$(working_tree_snapshot)"
if git diff --quiet "$at_start" "$at_stop" -- backend frontend scripts ops load; then
  exit 0
fi

output="$(mktemp)"
trap 'rm -f "$output"' EXIT
if scripts/test-fast.sh > "$output" 2>&1; then
  exit 0
fi

{
  echo "scripts/test-fast.sh is red after this subagent's changes; its last 40 lines:"
  tail -n 40 "$output"
} >&2
exit "$red_exit"
