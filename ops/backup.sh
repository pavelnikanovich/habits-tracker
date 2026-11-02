#!/usr/bin/env bash
# Backs up the database (FR-035, research R10): `ops/backup.sh` writes <BACKUP_DIR>/<UTC yyyyMMddTHHmmssZ>/ with
# habits.dump (pg_dump --format=custom of database habits) and manifest.tsv (ops/lib/manifest.sh), then deletes the
# backup directories whose name dates more than 7 days back. BACKUP_DIR defaults to /var/backups/habits; the database
# is the one of HABITS_COMPOSE_FILES (ops/lib/compose.sh). It prints the directory written and each one deleted.
#
# The manifest is taken in the dump's snapshot: one session opens a repeatable-read transaction and exports its
# snapshot, pg_dump reads that snapshot, and the same session then computes the manifest before it commits. A write
# between the dump and the manifest therefore cannot make a correct restore fail.
#
# The backup is written to .partial-<time> and renamed only when complete, so a failed run never leaves a directory
# that the restore scripts would take for a backup; nothing is deleted unless the new backup is complete.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
source "$here/lib/compose.sh"
source "$here/lib/manifest.sh"

backup_root="${BACKUP_DIR:-/var/backups/habits}"
kept_seconds=$((7 * 24 * 3600))

# Fail before writing anything when the compose files are malformed (ops/lib/compose.sh).
compose_file_arguments > /dev/null || exit 1

# The dump holds personal data: only the owner reads it.
umask 077
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
target="$backup_root/$stamp"
partial="$backup_root/.partial-$stamp"
mkdir -p "$backup_root"
if [ -e "$target" ] || [ -e "$partial" ]; then
  echo "backup.sh: $target exists already; run again in a second" >&2
  exit 1
fi
mkdir "$partial"

session_pid=""
session_in=""
on_exit() {
  local status=$?
  set +e
  # Closing the session's input ends psql, which rolls the snapshot's transaction back.
  [ -n "$session_in" ] && exec {session_in}>&-
  [ -n "$session_pid" ] && kill "$session_pid" 2> /dev/null
  if [ "$status" -ne 0 ] && [ -n "$partial" ] && [ -d "$partial" ]; then
    [ -s "$partial/session.err" ] && cat "$partial/session.err" >&2
    rm -rf "$partial"
    echo "backup.sh: the backup failed; nothing was kept and no old backup was deleted" >&2
  fi
}
trap on_exit EXIT

# The session that holds the snapshot until the dump and the manifest are taken.
coproc session { psql_on habits 2> "$partial/session.err"; }
# shellcheck disable=SC2154 # coproc sets session_PID
session_pid="$session_PID"
session_in="${session[1]}"
session_out="${session[0]}"
printf '%s\n' "begin transaction isolation level repeatable read, read only;" "select pg_export_snapshot();" >&"$session_in"
snapshot=""
read -r -t 60 -u "$session_out" snapshot || true
if [[ ! "$snapshot" =~ ^[0-9A-F]+-[0-9A-F]+-[0-9]+$ ]]; then
  echo "backup.sh: no snapshot was exported ('$snapshot')" >&2
  exit 1
fi

compose exec -T db pg_dump --username habits --dbname habits --format=custom --snapshot="$snapshot" \
  < /dev/null > "$partial/habits.dump"
[ -s "$partial/habits.dump" ] || { echo "backup.sh: pg_dump wrote nothing" >&2; exit 1; }

# The manifest in the same transaction, then a marker line: the session prints nothing else.
{ manifest_sql; printf '%s\n' "select '#end';"; } >&"$session_in"
: > "$partial/manifest.tsv"
ended=""
while read -r -t 3600 -u "$session_out" line; do
  if [ "$line" = "#end" ]; then ended=yes; break; fi
  printf '%s\n' "$line" >> "$partial/manifest.tsv"
done
[ -n "$ended" ] || { echo "backup.sh: the manifest was not taken" >&2; exit 1; }
printf 'commit;\n' >&"$session_in"
exec {session_in}>&-
session_in=""
wait "$session_pid" || { echo "backup.sh: the snapshot's session failed" >&2; exit 1; }
session_pid=""
rm -f "$partial/session.err"

mv -T "$partial" "$target"
partial=""
echo "backup: $target"

# Retention: backup directories, and partial ones a killed run left, whose name dates more than 7 days back. Only
# names in the format above are read; anything else in BACKUP_DIR is left alone.
now="$(date -u +%s)"
for entry in "$backup_root"/* "$backup_root"/.partial-*; do
  [ -d "$entry" ] || continue
  name="$(basename "$entry")"
  name="${name#.partial-}"
  [[ "$name" =~ $backup_name_pattern ]] || continue
  taken="$(date -u -d "${name:0:4}-${name:4:2}-${name:6:2} ${name:9:2}:${name:11:2}:${name:13:2} UTC" +%s 2> /dev/null)" || continue
  if [ $((now - taken)) -gt "$kept_seconds" ]; then
    rm -rf "$entry"
    echo "deleted: $entry"
  fi
done
