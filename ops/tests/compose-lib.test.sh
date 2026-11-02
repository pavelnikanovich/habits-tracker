#!/usr/bin/env bash
# Test of ops/lib/compose.sh (design review M6): `compose` builds one `-f <file>` per file of HABITS_COMPOSE_FILES
# (colon-separated, default compose.yaml at the repository root) and refuses a value that names a missing file, an empty
# value or an empty entry, so a malformed value never acts on another compose project; `db_psql` runs psql as habits on
# habits inside the db service. A stub `docker` on PATH records its arguments, so no container is needed.
set -uo pipefail

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
failures=0

fail() {
  echo "FAIL: $1" >&2
  failures=$((failures + 1))
}

dir="$(mktemp -d)"
trap 'rm -rf "$dir"' EXIT
mkdir -p "$dir/bin"
printf '#!/usr/bin/env bash\necho "docker $*" >> "%s/calls"\n' "$dir" > "$dir/bin/docker"
chmod +x "$dir/bin/docker"
touch "$dir/a.yaml" "$dir/b.yaml"

# Runs `$@` in a fresh shell that sources the library, with the stub docker first on PATH; sets status, output
# (stdout and stderr) and calls (what the stub recorded).
run() {
  rm -f "$dir/calls"
  output="$(PATH="$dir/bin:$PATH" bash -c 'source "$1"; shift; "$@"' _ "$repo_root/ops/lib/compose.sh" "$@" 2>&1)"
  status=$?
  calls="$(cat "$dir/calls" 2> /dev/null)"
}

expect_calls() { # case name, expected docker call
  [ "$status" -eq 0 ] || fail "$1: exited $status: $output"
  [ "$calls" = "$2" ] || fail "$1: docker was called with '$calls', expected '$2'"
}

expect_refused() { # case name, text the message must contain
  [ "$status" -ne 0 ] || fail "$1: exited 0"
  [[ "$output" == *"$2"* ]] || fail "$1: the message '$output' does not contain '$2'"
  [ -z "$calls" ] || fail "$1: docker was called: $calls"
}

unset HABITS_COMPOSE_FILES
run compose ps
expect_calls "unset" "docker compose -f $repo_root/compose.yaml ps"

HABITS_COMPOSE_FILES="$dir/a.yaml" run compose up --detach db
expect_calls "one file" "docker compose -f $dir/a.yaml up --detach db"

HABITS_COMPOSE_FILES="$dir/a.yaml:$dir/b.yaml" run compose ps
expect_calls "two files" "docker compose -f $dir/a.yaml -f $dir/b.yaml ps"

HABITS_COMPOSE_FILES="$dir/a.yaml:$dir/missing.yaml" run compose ps
expect_refused "a missing file" "$dir/missing.yaml"

HABITS_COMPOSE_FILES="" run compose ps
expect_refused "an empty value" "HABITS_COMPOSE_FILES"

HABITS_COMPOSE_FILES="$dir/a.yaml:" run compose ps
expect_refused "a trailing colon" "HABITS_COMPOSE_FILES"

HABITS_COMPOSE_FILES="$dir/a.yaml::$dir/b.yaml" run compose ps
expect_refused "an empty entry" "HABITS_COMPOSE_FILES"

HABITS_COMPOSE_FILES="$dir/a.yaml" run db_psql --command "select 1"
expect_calls "db_psql" "docker compose -f $dir/a.yaml exec -T db psql --username habits --dbname habits -v ON_ERROR_STOP=1 --command select 1"

HABITS_COMPOSE_FILES="$dir/missing.yaml" run db_psql --command "select 1"
expect_refused "db_psql with a missing file" "$dir/missing.yaml"

if [ "$failures" -ne 0 ]; then
  echo "$failures failure(s)" >&2
  exit 1
fi
echo "compose-lib.test.sh: ok"
