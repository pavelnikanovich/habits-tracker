#!/usr/bin/env bash
# The API latency SLO from operations.request_timings (FR-039, research R9): the share of requests answered within
# 300 ms and the 95th-percentile bucket since an instant. `ops/latency-p95.sh <since>`, where <since> is a timestamp
# PostgreSQL accepts (2026-11-01 or 2026-11-01T10:20:00Z); the hour that holds it counts whole, because the table
# keeps hours. Prints exactly two lines:
#   within 300 ms: 95.0%
#   p95 bucket: 300 ms          (+inf for the last bucket)
# The database is the one of HABITS_COMPOSE_FILES (ops/lib/compose.sh).
set -euo pipefail
source "$(dirname "$0")/lib/compose.sh"

if [ "$#" -ne 1 ] || [ -z "$1" ]; then
  echo "usage: ops/latency-p95.sh <since>" >&2
  exit 2
fi

# bucket_upper_ms 2147483647 is the +inf bucket. The 95th percentile is the first bucket whose cumulative count
# reaches 95% of the total; integers throughout, so no rounding decides it.
result="$(db_psql --no-psqlrc --tuples-only --no-align --variable "since=$1" <<'SQL'
with timings as (
  select bucket_upper_ms, sum(count) as requests
  from operations.request_timings
  where hour >= date_trunc('hour', :'since'::timestamptz)
  group by bucket_upper_ms
),
cumulative as (
  select bucket_upper_ms, requests,
         sum(requests) over (order by bucket_upper_ms) as up_to_here,
         sum(requests) over () as total
  from timings
)
select line from (
  select 1 as position,
         'within 300 ms: ' || to_char(100.0 * sum(requests) filter (where bucket_upper_ms <= 300) / max(total), 'FM990.0') || '%' as line
  from cumulative
  union all
  select 2,
         'p95 bucket: ' || case when bucket_upper_ms = 2147483647 then '+inf' else bucket_upper_ms || ' ms' end
  from (select bucket_upper_ms from cumulative where up_to_here * 100 >= total * 95 order by bucket_upper_ms limit 1) first_bucket
) lines
order by position;
SQL
)"

if [ -z "$result" ]; then
  echo "latency-p95.sh: no requests recorded since $1" >&2
  exit 1
fi
echo "$result"
