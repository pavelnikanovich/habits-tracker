#!/usr/bin/env bash
# Runs before the end-to-end tests (`npm --prefix frontend run e2e`). They start their own scripts/dev.sh, with the
# per-IP code request limit raised, so they refuse to start beside a developer's instance or another process on its
# ports. Playwright would only report the URL as used and suggest reusing the server, which would drop the raised limit.
set -euo pipefail
cd "$(dirname "$0")/.."

mkdir -p .dev
if ! flock -n .dev/dev.lock true; then
  echo "e2e: scripts/dev.sh is already running; stop it first (Ctrl+C in its terminal): the end-to-end run starts its own." >&2
  exit 1
fi
for port in 4200 5080; do
  if ss -Hltn "sport = :$port" | grep -q .; then
    echo "e2e: port $port is in use by a process other than scripts/dev.sh; stop it first: the end-to-end run starts its own scripts/dev.sh on ports 4200 and 5080." >&2
    exit 1
  fi
done
