#!/usr/bin/env bash
# Test of ops/delete-user.sh (FR-030, research R22): against a throwaway PostgreSQL under its own compose project (so no
# development data is touched), with the real schema `identity` applied by the migrate-identity bundle, users A and B
# each own two sessions, a one-time code and code requests. Deleting A leaves no row of schema `identity` that holds A's
# email or id and all of B's rows; a wrong email at the prompt deletes nothing and exits 1.
set -uo pipefail

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
failures=0

fail() {
  echo "FAIL: $1" >&2
  failures=$((failures + 1))
}

dir="$(mktemp -d)"
export COMPOSE_PROJECT_NAME="habits-delete-user-test-$$"
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
    ports:
      - "127.0.0.1::5432"
YAML

source "$repo_root/ops/lib/compose.sh"
source "$repo_root/ops/tests/lib/identity-schema.sh"

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

apply_identity_schema "$dir" || { echo "FAIL: the real schema was not applied" >&2; exit 1; }

id_a="0198a000-0000-7000-8000-00000000000a"
id_b="0198b000-0000-7000-8000-00000000000b"
db_psql --quiet --no-psqlrc <<SQL || { echo "FAIL: the rows were not seeded" >&2; exit 1; }
insert into identity.users (id, email, time_zone, time_zone_recognized, locale, created_at) values
  ('$id_a', 'ann@example.com', 'UTC', true, 'en', now()),
  ('$id_b', 'bob@example.com', 'UTC', true, 'en', now());
insert into identity.sessions (id, user_id, remembered, created_at, last_activity_at) values
  ('\x0a01', '$id_a', false, now(), now()), ('\x0a02', '$id_a', true, now(), now()),
  ('\x0b01', '$id_b', false, now(), now()), ('\x0b02', '$id_b', true, now(), now());
insert into identity.one_time_codes (email, code_hash, issued_at, expires_at, entry_attempts) values
  ('ann@example.com', '\x00', now(), now() + interval '10 minutes', 0),
  ('bob@example.com', '\x00', now(), now() + interval '10 minutes', 0);
insert into identity.code_requests (email, ip_prefix, requested_at) values
  ('ann@example.com', '203.0.113.7/32', now()), ('ann@example.com', '203.0.113.7/32', now()),
  ('ann@example.com', '203.0.113.7/32', now()),
  ('bob@example.com', '203.0.113.8/32', now()), ('bob@example.com', '203.0.113.8/32', now());
SQL

# Rows of schema identity per table, as "table=count" lines.
state() {
  db_psql --no-psqlrc --quiet --tuples-only --no-align <<'SQL'
select 'users=' || count(*) from identity.users
union all select 'sessions=' || count(*) from identity.sessions
union all select 'one_time_codes=' || count(*) from identity.one_time_codes
union all select 'code_requests=' || count(*) from identity.code_requests
order by 1;
SQL
}
before="$(state)"

# A wrong email at the prompt: nothing is deleted, exit 1.
output="$(printf 'bob@example.com\n' | "$repo_root/ops/delete-user.sh" ann@example.com 2>&1)"
status=$?
[ "$status" -eq 1 ] || fail "wrong confirmation: exited $status, expected 1"
[ "$(state)" = "$before" ] || fail "wrong confirmation: rows changed"
# An empty answer (the input ends) counts as wrong.
"$repo_root/ops/delete-user.sh" ann@example.com < /dev/null > /dev/null 2>&1 && fail "no confirmation: exited 0"
[ "$(state)" = "$before" ] || fail "no confirmation: rows changed"
# No argument is a usage error.
"$repo_root/ops/delete-user.sh" < /dev/null > /dev/null 2>&1 && fail "no argument: exited 0"

# The right confirmation deletes A's data and prints the rows per table, with nothing personal.
output="$(printf 'ann@example.com\n' | "$repo_root/ops/delete-user.sh" ann@example.com 2>&1)"
status=$?
[ "$status" -eq 0 ] || fail "deletion: exited $status: $output"
expected=$'identity.sessions: 2\nidentity.users: 1\nidentity.one_time_codes: 1\nidentity.code_requests: 3'
[[ "$output" == *"$expected" ]] || fail "deletion: printed '$output', expected the lines '$expected'"
[[ "$output" != *"ann@example.com"$'\n'* ]] || fail "deletion: the output repeats the email"
[[ "$output" != *"$id_a"* ]] || fail "deletion: the output holds the user id"

# No row of schema identity holds A's email or id; B's rows are all there.
remaining="$(db_psql --no-psqlrc --quiet --tuples-only --no-align <<SQL
select count(*) from identity.users where email = 'ann@example.com' or id = '$id_a'
  or (select count(*) from identity.sessions where user_id = '$id_a') > 0
  or (select count(*) from identity.one_time_codes where email = 'ann@example.com') > 0
  or (select count(*) from identity.code_requests where email = 'ann@example.com') > 0;
SQL
)"
[ "$remaining" = "0" ] || fail "deletion: some row of A's is left"
[ "$(state)" = $'code_requests=2\none_time_codes=1\nsessions=2\nusers=1' ] || fail "deletion: B's rows changed: $(state | tr '\n' ' ')"
b_rows="$(db_psql --no-psqlrc --quiet --tuples-only --no-align <<SQL
select (select count(*) from identity.users where id = '$id_b' and email = 'bob@example.com')
  || ',' || (select count(*) from identity.sessions where user_id = '$id_b')
  || ',' || (select count(*) from identity.one_time_codes where email = 'bob@example.com')
  || ',' || (select count(*) from identity.code_requests where email = 'bob@example.com');
SQL
)"
[ "$b_rows" = "1,2,1,2" ] || fail "deletion: B's rows are '$b_rows', expected '1,2,1,2'"

# A user that does not exist: the counts are all zero and nothing else changes.
output="$(printf 'ann@example.com\n' | "$repo_root/ops/delete-user.sh" ann@example.com 2>&1)"
[[ "$output" == *$'identity.sessions: 0\nidentity.users: 0'* ]] || fail "second deletion: printed '$output'"

if [ "$failures" -ne 0 ]; then
  echo "$failures failure(s)" >&2
  exit 1
fi
echo "delete-user.test.sh: ok"
