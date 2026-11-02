#!/usr/bin/env bash
# Restores the live database from a backup (FR-036, research R22; runbook, "Restore the live database"):
# `ops/restore-live.sh <backup directory>`, a directory written by ops/backup.sh. It never overwrites in place:
#   1. it asks for the database name and changes nothing, exit 1, unless `habits` is typed;
#   2. it restores the backup into a new database habits_restored and compares it with the backup's manifest.tsv
#      (ops/lib/manifest.sh); on FAIL it drops habits_restored, leaves habits and app as they were, and exits 1;
#   3. only on PASS it stops app, renames habits to habits_broken_<UTC yyyyMMddTHHmmssZ> and habits_restored to habits
#      in one transaction (both renames or neither), starts app, waits for /health to answer Healthy and prints the time.
# No statement drops habits; the only database it drops is habits_restored, and only one it created itself. If a step
# after app was stopped fails, the exit trap starts app again and prints the state it left. The database is the one of
# HABITS_COMPOSE_FILES (ops/lib/compose.sh).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
source "$here/lib/compose.sh"
source "$here/lib/manifest.sh"

if [ "$#" -ne 1 ] || [ -z "$1" ]; then
  echo "usage: ops/restore-live.sh <backup directory>" >&2
  exit 2
fi
backup="$1"
for file in habits.dump manifest.tsv; do
  if [ ! -f "$backup/$file" ]; then
    echo "restore-live.sh: $backup has no $file, so it is not a backup of ops/backup.sh; nothing was changed" >&2
    exit 2
  fi
done

# Fail before asking anything when the compose files are malformed (ops/lib/compose.sh).
compose_file_arguments > /dev/null || exit 1

printf 'This replaces the live database habits with the backup %s.\nType the database name to confirm: ' "$backup" >&2
confirmation=""
read -r confirmation || true
if [ "$confirmation" != habits ]; then
  echo "restore-live.sh: the name differs; nothing was changed" >&2
  exit 1
fi

started="$(date +%s)"

database_exists() {
  [ "$(psql_on postgres -c "select count(*) from pg_database where datname = '$1'" < /dev/null)" = "1" ]
}

# Waits up to about 60 s for /health to answer Healthy inside the app container. The app publishes no port in
# production and its image has no curl, so the request goes over bash's /dev/tcp to the app's own port 8080.
wait_healthy() {
  local answer
  for _ in $(seq 1 120); do
    answer="$(compose exec -T app bash -c \
      'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /health HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3 && cat <&3' \
      < /dev/null 2> /dev/null | tr -d '\r' | tail -n 1)" || true
    [ "$answer" = "Healthy" ] && return 0
    sleep 0.5
  done
  return 1
}

if ! database_exists habits; then
  echo "restore-live.sh: there is no database habits; nothing was changed" >&2
  exit 1
fi
# A habits_restored that exists is not this run's: an earlier run, or the undo of a restore (runbook), left it, and it
# may be the only copy of something. It is never dropped here.
if database_exists habits_restored; then
  echo "restore-live.sh: a database habits_restored exists already; rename or drop it first; nothing was changed" >&2
  exit 1
fi

work="$(mktemp -d)"
restored_created=""
app_stopped=""
broken=""
swap_tried=""
swapped=""
on_exit() {
  local status=$?
  set +e
  if [ "$status" -ne 0 ]; then
    # The state is read from the server, not from the flags alone: the swap is one transaction, so either the broken
    # database exists (both renames happened) or habits is still the live database.
    if [ -n "$swap_tried" ] && [ -z "$swapped" ] && database_exists "$broken"; then swapped=yes; fi
    if [ -z "$swapped" ] && [ -n "$restored_created" ]; then
      psql_on postgres -c "drop database if exists habits_restored with (force)" < /dev/null \
        || echo "restore-live.sh: habits_restored was not dropped; drop it by hand" >&2
    fi
    if [ -n "$app_stopped" ]; then
      echo "restore-live.sh: starting app again" >&2
      compose start app < /dev/null || echo "restore-live.sh: app did not start; run: docker compose start app" >&2
    fi
    if [ -n "$swapped" ]; then
      cat >&2 <<EOF
restore-live.sh: the databases were swapped, but the restore did not finish: habits is the restored backup and
"$broken" the previous database. To undo: stop app; in psql on database postgres run
  alter database habits rename to habits_restored; alter database "$broken" rename to habits;
then start app.
EOF
    else
      echo "restore-live.sh: the live database habits was not changed" >&2
    fi
    status=1
  fi
  rm -rf "$work"
  exit "$status"
}
trap on_exit EXIT
trap 'exit 130' INT TERM HUP

psql_on postgres -c "create database habits_restored template template0" < /dev/null
restored_created=yes
compose exec -T db pg_restore --username habits --dbname habits_restored --single-transaction --exit-on-error \
  < "$backup/habits.dump"
manifest_of habits_restored > "$work/restored.tsv"
if ! compare_manifests "$backup/manifest.tsv" "$work/restored.tsv"; then
  echo FAIL
  exit 1 # the trap drops habits_restored; habits and app were not touched
fi
echo PASS

broken="habits_broken_$(date -u +%Y%m%dT%H%M%SZ)"
if database_exists "$broken"; then
  echo "restore-live.sh: a database $broken exists already; run again in a second" >&2
  exit 1
fi

# Set before the stop, so a stop that fails halfway still has the trap start app.
app_stopped=yes
compose stop app < /dev/null
swap_tried=yes
# Both renames in one transaction: they need no session on either database, and with app stopped none is expected. A
# session that is still open (an operator's psql, say) makes the rename fail after PostgreSQL waits 5 s for it; it is
# not terminated, because it may be doing something that matters, and the trap restores the state before the swap.
# The broken name is quoted: unquoted, PostgreSQL would fold its T and Z to lower case.
if ! psql_on postgres <<SQL; then
begin;
alter database habits rename to "$broken";
alter database habits_restored rename to habits;
commit;
SQL
  echo "restore-live.sh: the databases were not swapped; is a session open on habits? (select * from pg_stat_activity)" >&2
  exit 1
fi
swapped=yes
echo "habits is the restored backup; the previous database is \"$broken\""

compose start app < /dev/null
app_stopped=""
if ! wait_healthy; then
  echo "restore-live.sh: app did not answer Healthy on /health" >&2
  exit 1
fi
echo Healthy
echo "time: $(($(date +%s) - started)) s"
echo "to undo: stop app; in psql on database postgres run: alter database habits rename to habits_restored; alter database \"$broken\" rename to habits; start app"
