#!/usr/bin/env bash
# SubagentStop: when the subagent's tree differs from the commit it started from under a source
# folder (committed or not, untracked files included), runs scripts/test-fast.sh. A red run keeps
# the subagent working: the last 40 lines go to stderr and the hook exits 2 (process document, "Epic 1").
set -euo pipefail

agent_id="$(sed -n 's/.*"agent_id"[[:space:]]*:[[:space:]]*"\([A-Za-z0-9_-]*\)".*/\1/p' | head -n 1)"
[ -n "$agent_id" ] || exit 0

root="$(git -C "${CLAUDE_PROJECT_DIR:-$PWD}" rev-parse --show-toplevel)"
record="$root/.dev/subagents/$agent_id"
# An agent started before the SubagentStart hook was registered has no record to compare with.
[ -f "$record" ] || exit 0
base="$(cat "$record")"

cd "$root"
folders=(backend frontend scripts ops load)
if git diff --quiet "$base" -- "${folders[@]}" \
  && [ -z "$(git ls-files --others --exclude-standard -- "${folders[@]}")" ]; then
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
exit 2
