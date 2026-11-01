#!/usr/bin/env bash
# Test of ops/latency-p95.sh (FR-039): against a throwaway PostgreSQL started from compose.yaml's image under its own
# compose project (so no development data is touched), with request_timings rows seeded, it prints the share within
# 300 ms and the 95th-percentile bucket. The table is created here with the columns of data-model.md; the Host tests
# check that the migration creates the same.
set -uo pipefail

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
failures=0

fail() {
  echo "FAIL: $1" >&2
  failures=$((failures + 1))
}

dir="$(mktemp -d)"
export COMPOSE_PROJECT_NAME="habits-latency-test-$$"
export HABITS_COMPOSE_FILES="$dir/compose.yaml"
image="$(sed -n 's/^ *image: *//p' "$repo_root/compose.yaml" | head -1)"
cleanup() {
  [ -f "$dir/compose.yaml" ] && docker compose -f "$dir/compose.yaml" down --volumes --timeout 1 > /dev/null 2>&1
  rm -rf "$dir"
}
trap cleanup EXIT

cat > "$dir/compose.yaml" <<YAML
services:
  db:
    image: $image
    environment:
      POSTGRES_DB: habits
      POSTGRES_USER: habits
      POSTGRES_HOST_AUTH_METHOD: trust
YAML

source "$repo_root/ops/lib/compose.sh"

if ! compose up --detach --wait db > "$dir/up.log" 2>&1; then
  cat "$dir/up.log" >&2
  echo "FAIL: the throwaway database did not start" >&2
  exit 1
fi
ready=""
for _ in $(seq 1 60); do
  if compose exec -T db pg_isready --quiet --host 127.0.0.1 --username habits --dbname habits; then ready=yes; break; fi
  sleep 0.5
done
[ -n "$ready" ] || { echo "FAIL: the throwaway database accepts no connections" >&2; exit 1; }

db_psql --quiet --command "
  create schema operations;
  create table operations.request_timings (
    hour timestamptz not null, endpoint text not null, bucket_upper_ms integer not null, count bigint not null,
    primary key (hour, endpoint, bucket_upper_ms));" || { echo "FAIL: the table was not created" >&2; exit 1; }

# seed <hour> <bucket> <count>: one row; the endpoint varies, as rows of several routes add up.
seed() {
  db_psql --quiet --command "insert into operations.request_timings values ('$1', 'GET /api/$2', $2, $3)"
}
reset() { db_psql --quiet --command "truncate operations.request_timings"; }

# expect <case> <since> <within line> <p95 line>
expect() {
  local output
  output="$("$repo_root/ops/latency-p95.sh" "$2" 2>&1)"
  [ "$output" = "$3"$'\n'"$4" ] || fail "$1: printed '$output', expected '$3' and '$4'"
}

reset
seed 2026-11-01T10:00Z 100 50
seed 2026-11-01T10:00Z 300 45
seed 2026-11-01T11:00Z 400 5
expect "95 within 300 ms" 2026-11-01T10:00Z "within 300 ms: 95.0%" "p95 bucket: 300 ms"

reset
seed 2026-11-01T10:00Z 100 50
seed 2026-11-01T10:00Z 300 44
seed 2026-11-01T11:00Z 400 6
expect "94 within 300 ms" 2026-11-01T10:00Z "within 300 ms: 94.0%" "p95 bucket: 400 ms"

# Rows of hours before <since> do not count; the hour that holds <since> counts whole.
seed 2026-11-01T09:00Z 5000 1000
expect "since inside an hour" 2026-11-01T10:20:00Z "within 300 ms: 94.0%" "p95 bucket: 400 ms"

# The last bucket is printed as +inf.
reset
seed 2026-11-01T10:00Z 10 90
seed 2026-11-01T10:00Z 2147483647 10
expect "the +inf bucket" 2026-11-01 "within 300 ms: 90.0%" "p95 bucket: +inf"

# No rows, no argument, and a since that is no timestamp are errors, not numbers.
reset
output="$("$repo_root/ops/latency-p95.sh" 2026-11-01 2>&1)" && fail "no rows: exited 0"
[[ "$output" == *"no requests recorded since 2026-11-01"* ]] || fail "no rows: the message was '$output'"
"$repo_root/ops/latency-p95.sh" > /dev/null 2>&1 && fail "no argument: exited 0"
"$repo_root/ops/latency-p95.sh" "not a time" > /dev/null 2>&1 && fail "bad since: exited 0"

if [ "$failures" -ne 0 ]; then
  echo "$failures failure(s)" >&2
  exit 1
fi
echo "latency-p95.test.sh: ok"
