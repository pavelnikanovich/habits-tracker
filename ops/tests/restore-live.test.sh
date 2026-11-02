#!/usr/bin/env bash
# Test of ops/restore-live.sh (FR-036, research R22; runbook, "Restore the live database"): compose.yaml with the app
# built from the Dockerfile, under its own compose project and on random host ports (so a developer's database and
# scripts/dev.sh are neither touched nor reused), BACKUP_DIR in a temporary directory, rows seeded here in schemas
# `identity` and `operations`. After a backup, rows are changed, deleted and added; the live restore brings the backup
# back (a new backup's manifest equals the old one's) and keeps the changed rows in habits_broken_*. A backup with an
# altered manifest, a wrong name at the prompt and a swap that fails midway (a session holds the live database) each
# leave the live database as it was, no habits_restored, and app running.
set -uo pipefail

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
failures=0

fail() {
  echo "FAIL: $1" >&2
  failures=$((failures + 1))
}

dir="$(mktemp -d)"
export COMPOSE_PROJECT_NAME="habits-restore-live-test-$$"
export HABITS_COMPOSE_FILES="$repo_root/compose.yaml:$dir/ports.yaml"
export BACKUP_DIR="$dir/backups"
holder="habits-restore-live-test-holder"
cleanup() {
  docker compose -f "$repo_root/compose.yaml" -f "$dir/ports.yaml" down --volumes --rmi local --timeout 1 --remove-orphans > /dev/null 2>&1
  rm -rf "$dir"
}
trap cleanup EXIT

# Random host ports in place of the fixed 5432 and 8080 of compose.yaml.
cat > "$dir/ports.yaml" <<'YAML'
services:
  db:
    ports: !override
      - "127.0.0.1::5432"
  app:
    ports: !override
      - "127.0.0.1::8080"
YAML

source "$repo_root/ops/lib/compose.sh"

# The app's /health over its published port, which changes when the container restarts; empty if it does not answer.
health() {
  local port
  port="$(compose port app 8080 2> /dev/null | sed 's/.*://')"
  [ -n "$port" ] && curl -s --max-time 2 "http://127.0.0.1:$port/health"
}
wait_healthy() {
  for _ in $(seq 1 120); do
    [ "$(health)" = "Healthy" ] && return 0
    sleep 0.5
  done
  return 1
}
sql() { db_psql --no-psqlrc --quiet --tuples-only --no-align "$@"; }
sql_on() { compose exec -T db psql --username habits --dbname "$1" -v ON_ERROR_STOP=1 --no-psqlrc --quiet --tuples-only --no-align -c "$2"; }
databases() { sql_on postgres "select datname from pg_database order by 1"; }
broken_databases() { databases | grep -E '^habits_broken_[0-9]{8}T[0-9]{6}Z$'; }
app_started_at() { docker inspect --format '{{.State.StartedAt}}' "$(compose ps --quiet app)" 2> /dev/null; }
# The seeded rows of the live database (or another one), as one line per row.
rows() {
  sql_on "${1:-habits}" "select email || ' ' || locale from identity.users where email like '%@restore.test' order by email" \
    && sql_on "${1:-habits}" "select endpoint || ' ' || count from operations.request_timings where endpoint like 'GET /seeded%' order by endpoint"
}

if ! compose up --build --detach --wait > "$dir/up.log" 2>&1; then
  tail -40 "$dir/up.log" >&2
  echo "FAIL: compose up --build did not succeed" >&2
  exit 1
fi
wait_healthy || { echo "FAIL: /health did not answer Healthy" >&2; exit 1; }

# The app migrated schemas identity and operations at start; the rows are seeded into its tables.
sql <<'SQL' > /dev/null || { echo "FAIL: the rows were not seeded" >&2; exit 1; }
insert into identity.users (id, email, time_zone, time_zone_recognized, locale, created_at) values
  ('0198a000-0000-7000-8000-0000000000a1', 'ann@restore.test', 'UTC', true, 'en', now()),
  ('0198a000-0000-7000-8000-0000000000b2', 'bob@restore.test', 'Europe/Warsaw', true, 'pl', now()),
  ('0198a000-0000-7000-8000-0000000000c3', 'cid@restore.test', 'UTC', false, 'ru', now());
insert into operations.request_timings (hour, endpoint, bucket_upper_ms, count) values
  ('2026-10-01 10:00+00', 'GET /seeded/a', 100, 7),
  ('2026-10-01 10:00+00', 'GET /seeded/b', 300, 3);
SQL

if ! "$repo_root/ops/backup.sh" > "$dir/backup.log" 2>&1; then
  cat "$dir/backup.log" >&2
  echo "FAIL: the backup was not taken" >&2
  exit 1
fi
backup="$BACKUP_DIR/$(ls "$BACKUP_DIR" | tail -1)"
backed_up="$(rows)"

# The damage after the backup: a row changed, a row deleted, a row added, in both schemas.
sql <<'SQL' > /dev/null || fail "the rows were not changed"
update identity.users set locale = 'ru' where email = 'ann@restore.test';
delete from identity.users where email = 'bob@restore.test';
insert into identity.users (id, email, time_zone, time_zone_recognized, locale, created_at) values
  ('0198a000-0000-7000-8000-0000000000d4', 'dan@restore.test', 'UTC', true, 'en', now());
update operations.request_timings set count = 70 where endpoint = 'GET /seeded/a';
delete from operations.request_timings where endpoint = 'GET /seeded/b';
SQL
damaged="$(rows)"
[ "$damaged" != "$backed_up" ] || fail "the change did not change the seeded rows"

# Usage errors change nothing.
"$repo_root/ops/restore-live.sh" < /dev/null > /dev/null 2>&1 && fail "no argument: exited 0"
mkdir "$dir/empty"
printf 'habits\n' | "$repo_root/ops/restore-live.sh" "$dir/empty" > /dev/null 2>&1 && fail "a directory without a backup: exited 0"

# a wrong name typed at the prompt: nothing changes.
started_at="$(app_started_at)"
output="$(printf 'habit\n' | "$repo_root/ops/restore-live.sh" "$backup" 2>&1)"
status=$?
[ "$status" -eq 1 ] || fail "a wrong name: exited $status, expected 1: $output"
printf '' | "$repo_root/ops/restore-live.sh" "$backup" > /dev/null 2>&1 && fail "no name (the input ends): exited 0"
[ "$(rows)" = "$damaged" ] || fail "a wrong name: the live rows changed"
databases | grep -qx habits_restored && fail "a wrong name: habits_restored exists"
[ -z "$(broken_databases)" ] || fail "a wrong name: a habits_broken_* database exists"
[ "$(app_started_at)" = "$started_at" ] || fail "a wrong name: app was restarted"

# The live restore: the live database equals the backup, habits_broken_* holds the changed rows.
output="$(printf 'habits\n' | "$repo_root/ops/restore-live.sh" "$backup" 2>&1)"
status=$?
[ "$status" -eq 0 ] || fail "restore: exited $status: $output"
grep -qx PASS <<< "$output" || fail "restore: no PASS line: $output"
grep -qx Healthy <<< "$output" || fail "restore: no Healthy line: $output"
grep -qE '^time: [0-9]+ s$' <<< "$output" || fail "restore: no 'time: <seconds> s' line: $output"
[ "$(rows)" = "$backed_up" ] || fail "restore: the live rows are '$(rows | tr '\n' ';')', expected '$(tr '\n' ';' <<< "$backed_up")'"
# A new backup of the live database has the manifest of the restored backup: every table of both schemas is equal.
sleep 1 # a new directory name, to the second
"$repo_root/ops/backup.sh" > "$dir/backup2.log" 2>&1 || fail "restore: the backup after the restore failed: $(cat "$dir/backup2.log")"
after="$BACKUP_DIR/$(ls "$BACKUP_DIR" | tail -1)"
if [ "$after" = "$backup" ] || ! diff "$backup/manifest.tsv" "$after/manifest.tsv" > "$dir/manifest.diff"; then
  fail "restore: the live database differs from the backup: $(cat "$dir/manifest.diff")"
fi
broken="$(broken_databases)"
[ "$(wc -l <<< "$broken")" -eq 1 ] && [ -n "$broken" ] || fail "restore: expected one habits_broken_* database, found '$broken'"
[ -n "$broken" ] && { [ "$(rows "$broken" 2> /dev/null)" = "$damaged" ] || fail "restore: $broken does not hold the changed rows"; }
grep -qF "$broken" <<< "$output" || fail "restore: the output does not name $broken"
databases | grep -qx habits_restored && fail "restore: habits_restored is left"
[ "$(health)" = "Healthy" ] || fail "restore: app does not answer Healthy after the restore"

# A new change, which the next attempts must keep.
sql -c "insert into identity.users (id, email, time_zone, time_zone_recognized, locale, created_at) values ('0198a000-0000-7000-8000-0000000000e5', 'eve@restore.test', 'UTC', true, 'en', now())" > /dev/null \
  || fail "eve was not inserted"
changed="$(rows)"

# A backup with an altered manifest: exit 1, the live database still holds the changed rows, no habits_restored left.
altered="$dir/altered/$(basename "$backup")"
mkdir -p "$(dirname "$altered")"
cp -r "$backup" "$altered"
sed -i 's/^\(identity\.users\t\)\([0-9]*\)\t/\1999\t/' "$altered/manifest.tsv"
started_at="$(app_started_at)"
output="$(printf 'habits\n' | "$repo_root/ops/restore-live.sh" "$altered" 2>&1)"
status=$?
[ "$status" -eq 1 ] || fail "an altered manifest: exited $status, expected 1: $output"
grep -qx FAIL <<< "$output" || fail "an altered manifest: no FAIL line: $output"
grep -v '^FAIL$' <<< "$output" | grep -q 'identity.users' || fail "an altered manifest: the table is not named: $output"
[ "$(rows)" = "$changed" ] || fail "an altered manifest: the live rows changed"
databases | grep -qx habits_restored && fail "an altered manifest: habits_restored is left"
[ "$(broken_databases)" = "$broken" ] || fail "an altered manifest: the habits_broken_* databases changed: $(broken_databases | tr '\n' ' ')"
[ "$(app_started_at)" = "$started_at" ] || fail "an altered manifest: app was restarted"

# A swap that fails midway: a session holds the live database, so the rename cannot run after app is stopped. The
# live database stays as it was, habits_restored is dropped, and the trap starts app again.
compose exec -T -e PGAPPNAME="$holder" db psql --username habits --dbname habits --no-psqlrc --quiet -c "select pg_sleep(300)" \
  < /dev/null > /dev/null 2>&1 &
held=$!
for _ in $(seq 1 40); do
  [ "$(sql -c "select count(*) from pg_stat_activity where application_name = '$holder'")" = "1" ] && break
  sleep 0.25
done
output="$(printf 'habits\n' | "$repo_root/ops/restore-live.sh" "$backup" 2>&1)"
status=$?
sql -c "select pg_terminate_backend(pid) from pg_stat_activity where application_name = '$holder'" > /dev/null
wait "$held" 2> /dev/null
[ "$status" -eq 1 ] || fail "a failed swap: exited $status, expected 1: $output"
grep -qx PASS <<< "$output" || fail "a failed swap: the restore did not reach PASS, so the swap was not tried: $output"
[ "$(rows)" = "$changed" ] || fail "a failed swap: the live rows changed"
databases | grep -qx habits_restored && fail "a failed swap: habits_restored is left"
[ "$(broken_databases)" = "$broken" ] || fail "a failed swap: the habits_broken_* databases changed: $(broken_databases | tr '\n' ' ')"
wait_healthy || fail "a failed swap: app is not running and Healthy after the trap"

if [ "$failures" -ne 0 ]; then
  echo "$failures failure(s)" >&2
  exit 1
fi
echo "restore-live.test.sh: ok"
