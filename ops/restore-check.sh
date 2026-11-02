#!/usr/bin/env bash
# The restore check (FR-036, research R10): `ops/restore-check.sh` restores the newest backup of BACKUP_DIR (default
# /var/backups/habits; the directory name with the latest <UTC yyyyMMddTHHmmssZ>) into a temporary database
# habits_restore_check next to the live one, computes its manifest, compares it with the backup's manifest.tsv table by
# table, prints the comparison, then `PASS` or `FAIL` and `time: <seconds> s` (from the start of the restore to the
# end of the check), and drops the temporary database. Exit 0 on PASS, 1 on FAIL or any error. It never connects to
# the database habits: it works from the maintenance database postgres and in habits_restore_check only.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
source "$here/lib/compose.sh"
source "$here/lib/manifest.sh"

backup_root="${BACKUP_DIR:-/var/backups/habits}"
check_database=habits_restore_check

compose_file_arguments > /dev/null || exit 1

newest=""
if [ -d "$backup_root" ]; then
  newest="$(find "$backup_root" -mindepth 1 -maxdepth 1 -type d -printf '%f\n' | grep -E "$backup_name_pattern" | LC_ALL=C sort | tail -1)" || true
fi
if [ -z "$newest" ]; then
  echo "restore-check.sh: no backup in $backup_root" >&2
  echo FAIL
  exit 1
fi
backup="$backup_root/$newest"
for file in habits.dump manifest.tsv; do
  [ -f "$backup/$file" ] || { echo "restore-check.sh: $backup has no $file" >&2; echo FAIL; exit 1; }
done
echo "backup: $backup"

work="$(mktemp -d)"
created=""
result=""
on_exit() {
  local status=$?
  set +e
  # An error before the comparison (a failed restore, say) is a FAIL too, with exit 1.
  if [ "$status" -ne 0 ]; then
    [ -n "$result" ] || echo FAIL
    status=1
  fi
  if [ -n "$created" ]; then
    psql_on postgres -c "drop database if exists $check_database with (force)" < /dev/null \
      || echo "restore-check.sh: $check_database was not dropped; drop it by hand" >&2
  fi
  rm -rf "$work"
  exit "$status"
}
trap on_exit EXIT

started="$(date +%s)"
# A database left by a check that was killed is the check's own, never live data.
psql_on postgres -c "drop database if exists $check_database with (force)" < /dev/null
psql_on postgres -c "create database $check_database template template0" < /dev/null
created=yes
compose exec -T db pg_restore --username habits --dbname "$check_database" --single-transaction --exit-on-error \
  < "$backup/habits.dump"
manifest_of "$check_database" > "$work/restored.tsv"

outcome=PASS
compare_manifests "$backup/manifest.tsv" "$work/restored.tsv" || outcome=FAIL
result="$outcome"
echo "$result"
echo "time: $(($(date +%s) - started)) s"

psql_on postgres -c "drop database $check_database with (force)" < /dev/null
created=""
[ "$result" = PASS ]
