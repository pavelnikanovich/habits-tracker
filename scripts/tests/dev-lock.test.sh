#!/usr/bin/env bash
# Test of the development lock (research R17): while another instance holds .dev/dev.lock, scripts/dev.sh exits 1
# within 5 seconds with "scripts/dev.sh is already running" and starts nothing. It runs a copy of dev.sh in a
# temporary folder, so a developer's running instance and its lock are untouched, with stub dotnet, ng, npm, npx and
# docker first on PATH: each stub records its call, so "started nothing" means no stub was called.
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

mkdir -p "$dir/scripts" "$dir/.dev" "$dir/stubs"
cp "$repo_root/scripts/dev.sh" "$dir/scripts/dev.sh" 2> /dev/null
for command in dotnet ng npm npx docker; do
  printf '#!/usr/bin/env bash\necho "%s $*" >> "%s/started"\n' "$command" "$dir" > "$dir/stubs/$command"
  chmod +x "$dir/stubs/$command"
done

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

if [ "$failures" -eq 0 ]; then
  echo "dev-lock: 1 passed"
else
  echo "dev-lock: $failures failed" >&2
  exit 1
fi
