#!/usr/bin/env bash
# Test of the development compose file (research R17): every port it publishes is bound to 127.0.0.1. The
# application runs in Development there and writes one-time codes to its log, and the database trusts every
# connection, so neither is reachable from another machine. It reads `docker compose config` only and starts nothing.
set -uo pipefail

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
failures=0

fail() {
  echo "FAIL: $1" >&2
  failures=$((failures + 1))
}

if ! config="$(docker compose -f "$repo_root/compose.yaml" config --format json)"; then
  echo "FAIL: docker compose config failed" >&2
  exit 1
fi

published="$(jq -r '.services | to_entries[] | .key as $service | (.value.ports // [])[]
  | "\($service) \(.host_ip // "all interfaces"):\(.published)"' <<< "$config")"

# Both services publish a port; an empty list would pass the check below without checking anything.
for service in db app; do
  grep -q "^$service " <<< "$published" || fail "$service publishes no port"
done

while read -r service binding; do
  [[ -z "$service" ]] && continue
  [[ "$binding" == 127.0.0.1:* ]] || fail "$service is published on $binding, not on 127.0.0.1"
done <<< "$published"

if ((failures > 0)); then
  echo "dev-compose.test.sh: $failures failure(s)" >&2
  exit 1
fi
echo "dev-compose.test.sh: passed"
