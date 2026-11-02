#!/usr/bin/env bash
# The development loop (research R17): the database from compose.yaml, the host under `dotnet watch` in Development on
# http://localhost:5080, and `ng serve` on http://localhost:4200 proxying /api and /health to the host. Every line of
# the host and of ng serve is appended to .dev/app.log. One instance at a time: .dev/dev.lock. The host inherits this
# script's environment, so a caller can override configuration, e.g. Identity__CodeRequestLimits__PerIpAddress=1000.
set -euo pipefail
cd "$(dirname "$0")/.."

mkdir -p .dev
exec 9> .dev/dev.lock
if ! flock -n 9; then
  echo "scripts/dev.sh is already running" >&2
  exit 1
fi

# The font is not kept in the repository (research R24) and ng serve cannot build without it; `npm start` would fetch
# it first, but this script starts ng serve directly. With the files in place this checks their sums and prints nothing.
if ! bash frontend/scripts/fetch-fonts.sh; then
  echo "dev.sh: the font could not be fetched; see the message above" >&2
  exit 1
fi

source scripts/lib/dotnet-path.sh
ensure_dotnet dev.sh

log=.dev/app.log
echo "dev.sh: starting; the log of the host and ng serve is $log"

if ! docker compose up --detach --wait db >> "$log" 2>&1; then
  echo "dev.sh: the database (compose service db) did not start; see $log" >&2
  exit 1
fi
# The host migrates at start, so PostgreSQL must accept connections first; its first start initialises the cluster,
# which the db healthcheck can pass early (it checks the socket of the temporary init server), so this checks over TCP.
database_ready() {
  docker compose exec -T db pg_isready --quiet --host 127.0.0.1 --username habits --dbname habits
}
for _ in $(seq 1 60); do
  database_ready && break
  sleep 0.5
done
if ! database_ready; then
  echo "dev.sh: the database accepts no connections after 30 s; see docker compose logs db" >&2
  exit 1
fi

# Job control gives each server its own process group, so stopping a server reaches every process it started. Without
# a terminal, `dotnet watch` outlives SIGINT and SIGTERM: it stops the host, then waits for a file change. Its group,
# host included, is therefore killed; a development host keeps no state outside the database. `ng serve` stops on
# SIGTERM. On exit, for whatever reason, both servers are stopped; the database keeps running
# (`docker compose stop db` stops it). The servers do not inherit the lock (fd 9), so it ends with this script.
set -m
stop_servers() {
  trap - EXIT INT TERM
  if [ -n "${host_group:-}" ]; then kill -KILL -- "-$host_group" 2> /dev/null || true; fi
  if [ -n "${web_group:-}" ]; then kill -TERM -- "-$web_group" 2> /dev/null || true; fi
  wait 2> /dev/null || true
}
trap stop_servers EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

(cd backend/src/HabitsTracker.Host \
  && ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 \
    exec dotnet watch --non-interactive --no-launch-profile run) >> "$log" 2>&1 < /dev/null 9>&- &
host_group=$!
(cd frontend && exec node_modules/.bin/ng serve --port 4200 --proxy-config proxy.conf.json) \
  >> "$log" 2>&1 < /dev/null 9>&- &
web_group=$!

echo "dev.sh: the page is http://localhost:4200, the host http://localhost:5080; Ctrl+C stops both"
# Either server ending ends the loop; the trap stops the other.
wait -n
