#!/usr/bin/env bash
# Test of the end-to-end entry point's preflight (research R12): `npm --prefix frontend run e2e` starts its own
# scripts/dev.sh, so while a developer's instance holds .dev/dev.lock, or another process listens on 4200 or 5080, it
# exits non-zero naming scripts/dev.sh and starts no browser and no server; with neither, it runs Playwright with the
# arguments it was given. It runs a copy of the entry point (frontend/package.json and scripts/e2e-preflight.sh) in a
# temporary folder laid out like the repository, with a stub Playwright that records its call.
set -uo pipefail

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
failures=0

fail() {
  echo "FAIL: $1" >&2
  failures=$((failures + 1))
}

dir="$(mktemp -d)"
holder_pid=""
listener_pid=""
cleanup() {
  [ -n "$holder_pid" ] && kill "$holder_pid" 2> /dev/null
  [ -n "$listener_pid" ] && kill "$listener_pid" 2> /dev/null
  rm -rf "$dir"
}
trap cleanup EXIT

mkdir -p "$dir/scripts" "$dir/.dev" "$dir/frontend/node_modules/.bin"
cp "$repo_root/frontend/package.json" "$dir/frontend/package.json"
cp "$repo_root/scripts/e2e-preflight.sh" "$dir/scripts/e2e-preflight.sh" 2> /dev/null
printf '#!/usr/bin/env bash\necho "playwright $*" >> "%s/started"\n' "$dir" > "$dir/frontend/node_modules/.bin/playwright"
chmod +x "$dir/frontend/node_modules/.bin/playwright"

# Runs the entry point with arguments $@; sets status and output, and clears the stub's record.
run_e2e() {
  rm -f "$dir/started"
  output="$(npm --prefix "$dir/frontend" run --silent e2e -- "$@" 2>&1)"
  status=$?
}

# Expects the last run to be refused with a message containing $2; $1 names the case.
expect_refused() {
  [ "$status" -ne 0 ] || fail "$1: the entry point exited 0"
  [[ "$output" == *"$2"* ]] || fail "$1: the output '$output' does not contain '$2'"
  [ ! -e "$dir/started" ] || fail "$1: Playwright was started: $(cat "$dir/started")"
}

# Nothing held: Playwright runs with the given arguments.
run_e2e --project=phone-360
[ "$status" -eq 0 ] || fail "free: the entry point exited $status: $output"
[ "$(cat "$dir/started" 2> /dev/null)" = "playwright test --project=phone-360" ] \
  || fail "free: Playwright was not run as 'playwright test --project=phone-360'"

# A developer's scripts/dev.sh holds the lock.
(exec 8> "$dir/.dev/dev.lock" && flock 8 && exec sleep 60) > /dev/null 2>&1 &
holder_pid=$!
for _ in $(seq 1 50); do
  flock -n "$dir/.dev/dev.lock" true || break
  sleep 0.1
done
run_e2e
expect_refused "lock held" "scripts/dev.sh is already running"
kill "$holder_pid" 2> /dev/null
wait "$holder_pid" 2> /dev/null
holder_pid=""

# Another process listens on a port of the development loop, without the lock.
for port in 4200 5080; do
  node -e "require('net').createServer().listen($port, '127.0.0.1')" &
  listener_pid=$!
  for _ in $(seq 1 50); do
    ss -Hltn "sport = :$port" | grep -q . && break
    sleep 0.1
  done
  run_e2e
  expect_refused "port $port held" "port $port"
  [[ "$output" == *"scripts/dev.sh"* ]] || fail "port $port held: the output '$output' does not name scripts/dev.sh"
  kill "$listener_pid" 2> /dev/null
  wait "$listener_pid" 2> /dev/null
  listener_pid=""
done

if [ "$failures" -eq 0 ]; then
  echo "e2e-preflight: 4 cases passed"
else
  echo "e2e-preflight: $failures failed" >&2
  exit 1
fi
