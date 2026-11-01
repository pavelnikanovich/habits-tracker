#!/usr/bin/env bash
# SubagentStart: records the commit the subagent starts from in .dev/subagents/<agent_id>, so the
# SubagentStop hook can tell whether the subagent changed code (process document, "Epic 1").
set -euo pipefail

agent_id="$(sed -n 's/.*"agent_id"[[:space:]]*:[[:space:]]*"\([A-Za-z0-9_-]*\)".*/\1/p' | head -n 1)"
[ -n "$agent_id" ] || exit 0

root="$(git -C "${CLAUDE_PROJECT_DIR:-$PWD}" rev-parse --show-toplevel)"
mkdir -p "$root/.dev/subagents"
git -C "$root" rev-parse HEAD > "$root/.dev/subagents/$agent_id"
