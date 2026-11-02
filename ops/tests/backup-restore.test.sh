#!/usr/bin/env bash
# Test of ops/backup.sh and ops/restore-check.sh (FR-035, FR-036, research R10): the db service of compose.yaml under its
# own compose project and on a random host port (so a developer's database and scripts/dev.sh are neither touched nor
# reused), with tables created and seeded here in schemas `identity` and `operations`, and BACKUP_DIR in a temporary
# directory. A restore of an untouched backup passes; an altered manifest fails and names the table; a backup 8 days old
# is deleted and one 6 days old is kept; the restore leaves the live database untouched; a backup taken while rows are
# written still passes, because the manifest is taken in the dump's snapshot.
set -uo pipefail

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
failures=0

fail() {
  echo "FAIL: $1" >&2
  failures=$((failures + 1))
}

dir="$(mktemp -d)"
export COMPOSE_PROJECT_NAME="habits-backup-restore-test-$$"
export HABITS_COMPOSE_FILES="$repo_root/compose.yaml:$dir/ports.yaml"
export BACKUP_DIR="$dir/backups"
cleanup() {
  docker compose -f "$repo_root/compose.yaml" -f "$dir/ports.yaml" down --volumes --rmi local --timeout 1 --remove-orphans > /dev/null 2>&1
  rm -rf "$dir"
}
trap cleanup EXIT

# A random host port in place of the fixed 5432 of compose.yaml; app is never started here.
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

# Values that a careless checksum would treat alike or break on: a quote, a newline, a backslash, a null, bytes, a
# non-ASCII letter, a time zone offset; one table is empty.
db_psql --quiet --no-psqlrc <<'SQL' || { echo "FAIL: the tables were not seeded" >&2; exit 1; }
create schema identity;
create schema operations;
create table identity.people (id integer primary key, email text not null, born timestamptz, photo bytea, note text);
create table identity.empty_box (id integer);
create table operations.counters (hour timestamptz not null, endpoint text not null, count bigint not null,
  primary key (hour, endpoint));
insert into identity.people values
  (1, 'ann@example.com', '2026-10-01 10:00:00+02', '\x00ff', 'o''brien'),
  (2, 'bob@example.com', null, null, E'two\nlines and a \\ backslash'),
  (3, 'zoë@example.com', '2026-10-02 23:59:59.5+00', '\x', ''),
  (4, 'dan@example.com', '1999-12-31 00:00:00-11', null, null);
insert into operations.counters
  select timestamptz '2026-10-01 00:00+00' + n * interval '1 hour', 'GET /api/x', n from generate_series(1, 50) as n;
SQL

# Rows per table of the live database, as "table=count" lines.
state() {
  db_psql --no-psqlrc --quiet --tuples-only --no-align <<'SQL'
select 'identity.empty_box=' || count(*) from identity.empty_box
union all select 'identity.people=' || count(*) from identity.people
union all select 'operations.counters=' || count(*) from operations.counters
order by 1;
SQL
}
databases() { db_psql --no-psqlrc --quiet --tuples-only --no-align -c "select datname from pg_database order by 1"; }
backups() { find "$BACKUP_DIR" -mindepth 1 -maxdepth 1 -printf '%f\n' 2> /dev/null | LC_ALL=C sort; }
stamp_days_ago() { date -u -d "$1 days ago" +%Y%m%dT%H%M%SZ; }

# Malformed compose files are refused before anything is written.
HABITS_COMPOSE_FILES="$dir/missing.yaml" "$repo_root/ops/backup.sh" > "$dir/bad.out" 2>&1 && fail "bad compose files: backup.sh exited 0"
grep -q HABITS_COMPOSE_FILES "$dir/bad.out" || fail "bad compose files: the message was '$(cat "$dir/bad.out")'"
[ -z "$(backups)" ] || fail "bad compose files: something was written to BACKUP_DIR: $(backups | tr '\n' ' ')"

# A restore check with no backup at all fails.
mkdir -p "$BACKUP_DIR"
"$repo_root/ops/restore-check.sh" > "$dir/none.out" 2>&1 && fail "no backup: restore-check.sh exited 0"

# Old backups and a directory that is not a backup, for the retention check.
old="$(stamp_days_ago 8)"
recent="$(stamp_days_ago 6)"
mkdir -p "$BACKUP_DIR/$old" "$BACKUP_DIR/$recent" "$BACKUP_DIR/notes"
touch "$BACKUP_DIR/$old/habits.dump" "$BACKUP_DIR/$recent/habits.dump" "$BACKUP_DIR/notes/keep.txt"

before="$(state)"

output="$("$repo_root/ops/backup.sh" 2>&1)"
status=$?
[ "$status" -eq 0 ] || fail "backup: exited $status: $output"
backup="$(backups | grep -E '^[0-9]{8}T[0-9]{6}Z$' | grep -vx -e "$old" -e "$recent" | tail -1)"
if [ -z "$backup" ]; then
  fail "backup: no directory named <UTC yyyyMMddTHHmmssZ> was written: $(backups | tr '\n' ' ')"
else
  [ -s "$BACKUP_DIR/$backup/habits.dump" ] || fail "backup: habits.dump is missing or empty"
  [ "$(stat -c %a "$BACKUP_DIR/$backup")" = "700" ] || fail "backup: the directory is readable by others ($(stat -c %a "$BACKUP_DIR/$backup"))"
  manifest="$BACKUP_DIR/$backup/manifest.tsv"
  # One line per table of both schemas, in the format <schema>.<table>\t<row count>\t<md5>.
  [ "$(cut -f1,2 "$manifest" 2> /dev/null)" = $'identity.empty_box\t0\nidentity.people\t4\noperations.counters\t50' ] \
    || fail "backup: the manifest's tables and counts are '$(cut -f1,2 "$manifest" 2> /dev/null | tr '\t\n' ' ;')'"
  grep -qvE $'^[a-z_]+\\.[a-z_]+\t[0-9]+\t[0-9a-f]{32}$' "$manifest" && fail "backup: a manifest line is malformed: $(cat "$manifest")"
fi

# a backup 8 days old is deleted and one 6 days old is kept
[ ! -e "$BACKUP_DIR/$old" ] || fail "a backup 8 days old is deleted and one 6 days old is kept: $old is still there"
[ -d "$BACKUP_DIR/$recent" ] || fail "a backup 8 days old is deleted and one 6 days old is kept: $recent was deleted"
[ -f "$BACKUP_DIR/notes/keep.txt" ] || fail "retention: a directory that is not a backup was deleted"

# a restore of an untouched backup passes
output="$("$repo_root/ops/restore-check.sh" 2>&1)"
status=$?
[ "$status" -eq 0 ] || fail "a restore of an untouched backup passes: exited $status: $output"
grep -qx PASS <<< "$output" || fail "a restore of an untouched backup passes: no PASS line: $output"
grep -qE '^time: [0-9]+ s$' <<< "$output" || fail "a restore of an untouched backup passes: no 'time: <seconds> s' line: $output"
grep -q 'identity.people' <<< "$output" || fail "a restore of an untouched backup passes: the comparison does not list the tables: $output"

# the restore leaves the live database untouched
[ "$(state)" = "$before" ] || fail "the restore leaves the live database untouched: rows changed: $(state | tr '\n' ' ')"
databases | grep -qx habits_restore_check && fail "the restore leaves the live database untouched: habits_restore_check still exists"

# an altered manifest fails: one count changed, the table is named.
if [ -n "$backup" ]; then
  cp "$manifest" "$dir/manifest.saved"
  sed -i 's/^\(identity\.people\t\)4\t/\15\t/' "$manifest"
  output="$("$repo_root/ops/restore-check.sh" 2>&1)"
  status=$?
  [ "$status" -eq 1 ] || fail "an altered manifest fails: exited $status, expected 1: $output"
  grep -qx FAIL <<< "$output" || fail "an altered manifest fails: no FAIL line: $output"
  grep -v '^FAIL$' <<< "$output" | grep -q 'identity.people' || fail "an altered manifest fails: the table is not named: $output"
  databases | grep -qx habits_restore_check && fail "an altered manifest fails: habits_restore_check still exists"
  [ "$(state)" = "$before" ] || fail "an altered manifest fails: the live rows changed"
  cp "$dir/manifest.saved" "$manifest"
fi

# A backup taken while rows are written passes: the manifest is the dump's snapshot. The writer commits a row every
# few milliseconds for several seconds; the backup starts in the middle.
sleep 1 # a new directory name, to the second
writer_out="$dir/writer.log"
db_psql --quiet --no-psqlrc > "$writer_out" 2>&1 <<'SQL' &
do $$
begin
  for n in 1..1500 loop
    insert into operations.counters values (timestamptz '2026-10-20 00:00+00', 'writer ' || n, n);
    commit;
    perform pg_sleep(0.004);
  end loop;
end
$$;
SQL
writer=$!
sleep 1.5
output="$("$repo_root/ops/backup.sh" 2>&1)" || fail "backup during writes: exited non-zero: $output"
wait "$writer" || fail "backup during writes: the writer failed: $(cat "$writer_out")"
written="$(backups | grep -E '^[0-9]{8}T[0-9]{6}Z$' | tail -1)"
taken="$(sed -n 's/^operations\.counters\t\([0-9]*\)\t.*/\1/p' "$BACKUP_DIR/$written/manifest.tsv" 2> /dev/null)"
if [ "$written" = "$backup" ] || [ -z "$taken" ] || [ "$taken" -le 50 ] || [ "$taken" -ge 1550 ]; then
  fail "backup during writes: the backup was not taken while rows were written (directory $written, counters $taken)"
fi
output="$("$repo_root/ops/restore-check.sh" 2>&1)"
status=$?
[ "$status" -eq 0 ] || fail "backup during writes: the restore check exited $status: $output"
grep -qx PASS <<< "$output" || fail "backup during writes: no PASS line: $output"

if [ "$failures" -ne 0 ]; then
  echo "$failures failure(s)" >&2
  exit 1
fi
echo "backup-restore.test.sh: ok"
