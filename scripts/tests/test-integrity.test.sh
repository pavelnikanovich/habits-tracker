#!/usr/bin/env bash
# Test of the test integrity check (research R15): scripts/test-integrity.sh <base-ref> exits 1 when an added line of
# `git diff <base-ref>...HEAD` holds a skip marker (printing file:line: marker), or when the test count of HEAD is
# below that of the base; it exits 0 otherwise, 2 without a base-ref. It runs in a temporary repository, with
# TEST_COUNT_COMMAND replaced by a stub that prints the number in the file `count` of the tree it runs in (0 when
# there is none). The markers are spelled in two pieces below, so that this file does not hold them itself.
set -uo pipefail

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
script="$repo_root/scripts/test-integrity.sh"
failures=0

fail() {
  echo "FAIL: $1" >&2
  failures=$((failures + 1))
}

markers=("Ski""p =" ".ski""p(" ".onl""y(" "xi""t(" "xdescri""be(" "test.fi""xme(")

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
export TEST_COUNT_COMMAND='cat count 2> /dev/null || echo 0'
export GIT_AUTHOR_NAME=t GIT_AUTHOR_EMAIL=t@t GIT_COMMITTER_NAME=t GIT_COMMITTER_EMAIL=t@t
# The temporary repositories ignore the developer's global and system git config: a global commit.gpgsign,
# core.hooksPath or commit template would make their commits fail or prompt.
export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1

# A repository whose commit "base" holds a.ts (three lines) and the count file $1; sets $repo.
new_repo() {
  repo="$work/repo-$RANDOM$RANDOM"
  mkdir -p "$repo"
  git -C "$repo" init -q -b main
  printf 'one\ntwo\nthree\n' > "$repo/a.ts"
  [ -n "$1" ] && echo "$1" > "$repo/count"
  git -C "$repo" add -A
  git -C "$repo" commit -q -m base
  git -C "$repo" checkout -q -b feature
}

commit_all() {
  git -C "$repo" add -A
  git -C "$repo" commit -q -m change
}

# Runs the check in $repo against main; sets $status and $output.
check() {
  output="$(cd "$repo" && bash "$script" main 2>&1)"
  status=$?
}

expect() { # expect <status> <case name>
  [ "$status" -eq "$1" ] || fail "$2: exit $status, expected $1 ($output)"
}

# One case per marker in an added line: exit 1 naming file, line and marker.
for marker in "${markers[@]}"; do
  new_repo 5
  printf 'one\ntwo\nbefore %s after\nthree\n' "$marker" > "$repo/a.ts"
  commit_all
  check
  expect 1 "added line with '$marker'"
  case "$output" in
    *"a.ts:3: $marker"*) ;;
    *) fail "added line with '$marker': output lacks 'a.ts:3: $marker' ($output)" ;;
  esac
done

# A marker in a new file, outside test files, counts too.
new_repo 5
mkdir "$repo/src"
printf 'x\n%s\n' "${markers[1]}" > "$repo/src/new.js"
commit_all
check
expect 1 "marker in a new file"
case "$output" in *"src/new.js:2: ${markers[1]}"*) ;; *) fail "new file: output lacks the file and line ($output)" ;; esac

# A marker inside a longer word is none: a call of exit is not the marker that ends in it.
new_repo 5
printf 'one\nprocess.exit(1)\nsys.exit(2)\nthree\n' > "$repo/a.ts"
commit_all
check
expect 0 "exit( is not a marker"

new_repo 5
printf 'one\nexit(1) (%s\nthree\n' "xi""t(2)" > "$repo/a.ts"
commit_all
check
expect 1 "a marker after a longer word on the same line"

# A marker only in a removed line, or untouched since the base, passes.
new_repo 5
printf 'one\n%s\nthree\n' "${markers[0]}" > "$repo/a.ts"
commit_all
git -C "$repo" branch -q -f main HEAD
printf 'one\nthree\nfour\n' > "$repo/a.ts"
commit_all
check
expect 0 "marker only in a removed line"

new_repo 5
printf 'one\n%s\nthree\n' "${markers[0]}" > "$repo/a.ts"
commit_all
git -C "$repo" branch -q -f main HEAD
printf 'one\n%s\nthree\nfour\n' "${markers[0]}" > "$repo/a.ts"
commit_all
check
expect 0 "marker untouched in a context line"

# The count: lower fails, equal and higher pass.
new_repo 5
echo 4 > "$repo/count"
commit_all
check
expect 1 "count lower on HEAD"
case "$output" in *5*4*) ;; *) fail "count lower: output names neither count ($output)" ;; esac

new_repo 5
echo 5 > "$repo/count"
echo more >> "$repo/a.ts"
commit_all
check
expect 0 "count equal"

new_repo 5
echo 9 > "$repo/count"
commit_all
check
expect 0 "count higher"

# A base with no tests counts 0 and does not error.
new_repo ""
echo 3 > "$repo/count"
commit_all
check
expect 0 "base with no tests"

# An empty base (no files, no common history with HEAD), as main is empty in epic 1.
repo="$work/empty-base"
mkdir -p "$repo"
git -C "$repo" init -q -b main
git -C "$repo" commit -q --allow-empty -m empty
git -C "$repo" checkout -q --orphan feature
echo 3 > "$repo/count"
echo code > "$repo/a.ts"
commit_all
check
expect 0 "empty base"

# An empty base does not hide a marker.
printf '%s\n' "${markers[2]}" >> "$repo/a.ts"
commit_all
check
expect 1 "marker against an empty base"

# A counting command that fails fails the check closed (exit 2, its stderr in the message), at the base and at HEAD.
export TEST_COUNT_COMMAND='if [ -f broken ]; then echo "boom: no package source" >&2; exit 1; fi; cat count 2> /dev/null || echo 0'
new_repo 5
touch "$repo/broken"
git -C "$repo" add -A
git -C "$repo" commit -q -m broken
git -C "$repo" branch -q -f main HEAD
rm "$repo/broken"
echo 9 > "$repo/count"
commit_all
check
expect 2 "counting command failing at the base"
case "$output" in *"boom: no package source"*) ;; *) fail "failing base: the message lacks the command's stderr ($output)" ;; esac

new_repo 5
touch "$repo/broken"
commit_all
check
expect 2 "counting command failing at HEAD"
export TEST_COUNT_COMMAND='cat count 2> /dev/null || echo 0'

# A shallow clone has lost the history the merge base needs: exit 2 asking for full history, not the two-dot fallback.
new_repo 5
echo more >> "$repo/a.ts"
commit_all
clone="$work/shallow"
git clone -q --depth 1 --no-single-branch "file://$repo" "$clone" 2> /dev/null
git -C "$clone" checkout -q feature
output="$(cd "$clone" && bash "$script" origin/main 2>&1)"
status=$?
expect 2 "shallow clone"
case "$output" in *"full history"*) ;; *) fail "shallow clone: the message does not ask for full history ($output)" ;; esac

# No base-ref is a usage error.
output="$(cd "$repo" && bash "$script" 2>&1)"
status=$?
expect 2 "no base-ref"

if [ "$failures" -ne 0 ]; then
  echo "test-integrity: $failures failed" >&2
  exit 1
fi
echo "test-integrity: all passed"
