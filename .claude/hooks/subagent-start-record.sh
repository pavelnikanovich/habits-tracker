#!/usr/bin/env bash
# SubagentStart: records a snapshot of the working tree the subagent starts from (a git tree id) in
# .dev/subagents/<agent_id>, so the SubagentStop hook can tell whether the subagent itself changed
# code (process document, "Epic 1").
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/working-tree-snapshot.sh"

agent_id="$(sed -n 's/.*"agent_id"[[:space:]]*:[[:space:]]*"\([A-Za-z0-9_-]*\)".*/\1/p' | head -n 1)"
[ -n "$agent_id" ] || exit 0

root="$(git -C "${CLAUDE_PROJECT_DIR:-$PWD}" rev-parse --show-toplevel)"
cd "$root"
snapshot="$(working_tree_snapshot)"
mkdir -p .dev/subagents
echo "$snapshot" > ".dev/subagents/$agent_id"
