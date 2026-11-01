#!/usr/bin/env bash
# Tests of the SubagentStart and SubagentStop hooks (.claude/hooks), each case in a temporary git
# repository with the copied hooks and a stub scripts/test-fast.sh that records each run.
set -uo pipefail

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
failures=0

fail() {
  echo "FAIL: $1" >&2
  failures=$((failures + 1))
}

# A repository with one commit: a backend source file, a document and the stub fast command, which
# exits with the code in the file stub-exit and appends a line to stub-runs on every run.
new_repo() {
  local dir
  dir="$(mktemp -d)"
  git -C "$dir" init --quiet
  git -C "$dir" config user.email test@example.com
  git -C "$dir" config user.name test
  mkdir -p "$dir/.claude/hooks" "$dir/scripts" "$dir/backend" "$dir/docs"
  cp "$repo_root/.claude/hooks/subagent-start-record.sh" "$repo_root/.claude/hooks/subagent-stop-tests.sh" "$dir/.claude/hooks/"
  printf '.dev/\nstub-exit\nstub-runs\n' > "$dir/.gitignore"
  cat > "$dir/scripts/test-fast.sh" <<'STUB'
#!/usr/bin/env bash
cd "$(dirname "$0")/.."
echo run >> stub-runs
echo "stub fast tests: 1 failed"
exit "$(cat stub-exit)"
STUB
  chmod +x "$dir/scripts/test-fast.sh"
  echo 'class A {}' > "$dir/backend/A.cs"
  echo 'notes' > "$dir/docs/notes.md"
  echo 0 > "$dir/stub-exit"
  git -C "$dir" add -A
  git -C "$dir" commit --quiet -m initial
  echo "$dir"
}

# Hook input for repository $1, event $2, agent $3; $4 is stop_hook_active (default false).
input() {
  printf '{"session_id":"s1","cwd":"%s","hook_event_name":"%s","stop_hook_active":%s,"agent_id":"%s","agent_type":"general-purpose"}' \
    "$1" "$2" "${4:-false}" "$3"
}

start_agent() {
  input "$1" SubagentStart "$2" | CLAUDE_PROJECT_DIR="$1" bash "$1/.claude/hooks/subagent-start-record.sh"
}

# Runs the stop hook for agent $2 in repository $1 ($3: stop_hook_active); sets status and stderr.
stop_agent() {
  local err
  err="$(mktemp)"
  input "$1" SubagentStop "$2" "${3:-false}" | CLAUDE_PROJECT_DIR="$1" bash "$1/.claude/hooks/subagent-stop-tests.sh" 2> "$err" > /dev/null
  status=$?
  stderr="$(cat "$err")"
  rm -f "$err"
}

stub_runs() {
  if [ -f "$1/stub-runs" ]; then wc -l < "$1/stub-runs" | tr -d ' '; else echo 0; fi
}

case_no_change_under_source_folders() {
  local dir
  dir="$(new_repo)"
  start_agent "$dir" agent-1
  echo 'changed notes' > "$dir/docs/notes.md"
  echo 1 > "$dir/stub-exit"
  stop_agent "$dir" agent-1
  [ "$status" -eq 0 ] || fail "no change under the source folders: exit $status, expected 0"
  [ "$(stub_runs "$dir")" -eq 0 ] || fail "no change under the source folders: the fast tests ran"
  rm -rf "$dir"
}

case_changed_file_and_red_tests() {
  local dir
  dir="$(new_repo)"
  start_agent "$dir" agent-1
  echo 'class A { int x; }' > "$dir/backend/A.cs"
  echo 1 > "$dir/stub-exit"
  stop_agent "$dir" agent-1
  [ "$status" -eq 2 ] || fail "changed file, red tests: exit $status, expected 2"
  [[ "$stderr" == *"stub fast tests: 1 failed"* ]] || fail "changed file, red tests: the stub's output is not on stderr: $stderr"
  [ "$(stub_runs "$dir")" -eq 1 ] || fail "changed file, red tests: the fast tests ran $(stub_runs "$dir") times, expected 1"
  rm -rf "$dir"
}

case_changed_file_and_green_tests() {
  local dir
  dir="$(new_repo)"
  start_agent "$dir" agent-1
  echo 'class A { int x; }' > "$dir/backend/A.cs"
  stop_agent "$dir" agent-1
  [ "$status" -eq 0 ] || fail "changed file, green tests: exit $status, expected 0"
  [ "$(stub_runs "$dir")" -eq 1 ] || fail "changed file, green tests: the fast tests ran $(stub_runs "$dir") times, expected 1"
  rm -rf "$dir"
}

# A subagent already sent back once by this hook may stop: the red tail is still shown, exit 0.
case_red_tests_after_one_bounce() {
  local dir
  dir="$(new_repo)"
  start_agent "$dir" agent-1
  echo 'class A { int x; }' > "$dir/backend/A.cs"
  echo 1 > "$dir/stub-exit"
  stop_agent "$dir" agent-1 true
  [ "$status" -eq 0 ] || fail "red tests, stop_hook_active true: exit $status, expected 0"
  [[ "$stderr" == *"stub fast tests: 1 failed"* ]] || fail "red tests, stop_hook_active true: the stub's output is not on stderr: $stderr"
  [ "$(stub_runs "$dir")" -eq 1 ] || fail "red tests, stop_hook_active true: the fast tests ran $(stub_runs "$dir") times, expected 1"
  rm -rf "$dir"
}

case_untracked_file_counts() {
  local dir
  dir="$(new_repo)"
  start_agent "$dir" agent-1
  echo 'class B {}' > "$dir/backend/B.cs"
  echo 1 > "$dir/stub-exit"
  stop_agent "$dir" agent-1
  [ "$status" -eq 2 ] || fail "untracked file, red tests: exit $status, expected 2"
  rm -rf "$dir"
}

case_committed_change_counts() {
  local dir
  dir="$(new_repo)"
  start_agent "$dir" agent-1
  echo 'class A { int x; }' > "$dir/backend/A.cs"
  git -C "$dir" commit --quiet -am change
  echo 1 > "$dir/stub-exit"
  stop_agent "$dir" agent-1
  [ "$status" -eq 2 ] || fail "committed change, red tests: exit $status, expected 2"
  rm -rf "$dir"
}

case_agent_started_before_the_hook() {
  local dir
  dir="$(new_repo)"
  echo 'class A { int x; }' > "$dir/backend/A.cs"
  echo 1 > "$dir/stub-exit"
  stop_agent "$dir" agent-unrecorded
  [ "$status" -eq 0 ] || fail "agent without a record: exit $status, expected 0"
  [ "$(stub_runs "$dir")" -eq 0 ] || fail "agent without a record: the fast tests ran"
  rm -rf "$dir"
}

for hook in subagent-start-record.sh subagent-stop-tests.sh; do
  if [ ! -f "$repo_root/.claude/hooks/$hook" ]; then
    echo "FAIL: .claude/hooks/$hook does not exist" >&2
    exit 1
  fi
done

case_no_change_under_source_folders
case_changed_file_and_red_tests
case_changed_file_and_green_tests
case_red_tests_after_one_bounce
case_untracked_file_counts
case_committed_change_counts
case_agent_started_before_the_hook

if [ "$failures" -gt 0 ]; then
  echo "subagent-stop-hook: $failures failed" >&2
  exit 1
fi
echo "subagent-stop-hook: 7 cases passed"
