#!/usr/bin/env bash
# Test of the development lock (research R17): while another instance holds .dev/dev.lock, scripts/dev.sh exits 1
# within 5 seconds with "scripts/dev.sh is already running" and starts nothing. It runs a copy of dev.sh in a
# temporary folder laid out like the repository, so a developer's running instance and its lock are untouched. Every
# command dev.sh starts is a stub that records its call: `docker` and `dotnet` first on PATH, and ng at
# frontend/node_modules/.bin/ng, where dev.sh runs it from. "Started nothing" means no stub was called and nothing
# opened the log .dev/app.log, which every start (the database and both servers) appends to: a server started before
# the lock check is killed on exit, perhaps before its stub runs, but its log is opened as soon as it forks.
set -uo pipefail

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
failures=0

fail() {
  echo "FAIL: $1" >&2
  failures=$((failures + 1))
}

dir="$(mktemp -d)"
holder_pid=""
cleanup() {
  [ -n "$holder_pid" ] && kill "$holder_pid" 2> /dev/null
  rm -rf "$dir"
}
trap cleanup EXIT

mkdir -p "$dir/scripts" "$dir/.dev" "$dir/stubs" "$dir/backend/src/HabitsTracker.Host" "$dir/frontend/node_modules/.bin"
cp "$repo_root/scripts/dev.sh" "$dir/scripts/dev.sh" 2> /dev/null

# A stub at path $1 that records "$2 <arguments>" in $dir/started.
stub() {
  printf '#!/usr/bin/env bash\necho "%s $*" >> "%s/started"\n' "$2" "$dir" > "$1"
  chmod +x "$1"
}
stub "$dir/stubs/docker" docker
stub "$dir/stubs/dotnet" dotnet
stub "$dir/frontend/node_modules/.bin/ng" ng

# Another instance: hold the lock until this test ends. The holder is the sleep itself, so killing it releases the
# lock and leaves no process behind.
(exec 8> "$dir/.dev/dev.lock" && flock 8 && exec sleep 60) > /dev/null 2>&1 &
holder_pid=$!
for _ in $(seq 1 50); do
  flock -n "$dir/.dev/dev.lock" true || break
  sleep 0.1
done

output="$(PATH="$dir/stubs:$PATH" timeout 5 bash "$dir/scripts/dev.sh" 2>&1)"
status=$?

[ "$status" -ne 124 ] || fail "dev.sh was still running after 5 seconds"
[ "$status" -eq 1 ] || fail "dev.sh exited $status, expected 1"
[ "$output" = "scripts/dev.sh is already running" ] \
  || fail "dev.sh printed '$output', expected 'scripts/dev.sh is already running'"
[ ! -e "$dir/started" ] || fail "dev.sh started: $(tr '\n' ';' < "$dir/started")"
[ ! -e "$dir/.dev/app.log" ] || fail "dev.sh opened its log .dev/app.log, so it started something"

if [ "$failures" -eq 0 ]; then
  echo "dev-lock: 1 passed"
else
  echo "dev-lock: $failures failed" >&2
  exit 1
fi
