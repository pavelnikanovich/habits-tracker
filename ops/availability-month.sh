#!/usr/bin/env bash
# The monthly availability value (FR-038, research R16): `ops/availability-month.sh YYYY-MM` lists that month's runs of
# availability.yml through the GitHub REST API (`gh api`, in the repository of the current directory) and prints
# `passed/ran: <p>/<r> (<share with two decimals>%)`, or `no value` for a month with no runs. A run that was cancelled
# or skipped did not check anything, so it is not counted as ran.
set -euo pipefail

month="${1:-}"
if ! [[ "$month" =~ ^[0-9]{4}-(0[1-9]|1[0-2])$ ]]; then
  echo "usage: $0 YYYY-MM" >&2
  exit 2
fi

first="$month-01"
last="$(date -u -d "$first +1 month -1 day" +%F)"

conclusions="$(gh api --paginate \
  "repos/{owner}/{repo}/actions/workflows/availability.yml/runs?status=completed&per_page=100&created=$first..$last" \
  --jq '.workflow_runs[].conclusion')"

ran="$(grep -cE '^(success|failure|timed_out)$' <<< "$conclusions" || true)"
passed="$(grep -cx 'success' <<< "$conclusions" || true)"

if [ "$ran" -eq 0 ]; then
  echo "no value"
else
  awk -v p="$passed" -v r="$ran" 'BEGIN { printf "passed/ran: %d/%d (%.2f%%)\n", p, r, 100 * p / r }'
fi
