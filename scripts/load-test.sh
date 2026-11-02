#!/usr/bin/env bash
# The load test of the main screen (FR-040, SC-006, research R14), about 12 minutes: compose.yaml with
# load/compose.loadtest.yaml under its own compose project (the development database and scripts/dev.sh are not
# touched), 100 users signed in with curl (codes from the development log), then k6 for 10 minutes. Exits with k6's code.
# LOAD_USERS and LOAD_DURATION shorten the run; LOAD_P95_MS replaces the 300 ms threshold (the gate check uses 1).
set -uo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
users="${LOAD_USERS:-100}"
port=18080
k6_image="grafana/k6:2.3.0"

export COMPOSE_PROJECT_NAME="habits-load-test-$$"
export HABITS_COMPOSE_FILES="$repo_root/compose.yaml:$repo_root/load/compose.loadtest.yaml"
sessions="$repo_root/load/.sessions.json"
dir="$(mktemp -d)"
cleanup() {
  docker compose -f "$repo_root/compose.yaml" -f "$repo_root/load/compose.loadtest.yaml" down --volumes --rmi local --timeout 1 --remove-orphans > /dev/null 2>&1
  rm -rf "$dir" "$sessions"
}
trap cleanup EXIT
source "$repo_root/ops/lib/compose.sh"

if ! compose up --build --detach --wait > "$dir/up.log" 2>&1; then
  tail -40 "$dir/up.log" >&2
  echo "load-test.sh: compose up did not succeed" >&2
  exit 1
fi

# The app has no health check of its own in compose, so `--wait` returns before it listens: wait up to 60 s for /health.
ready=""
for _ in $(seq 1 120); do
  [ "$(curl -s --max-time 2 -o /dev/null -w '%{http_code}' "http://localhost:$port/health")" = "200" ] && { ready=yes; break; }
  sleep 0.5
done
[ -n "$ready" ] || { echo "load-test.sh: http://localhost:$port/health did not answer 200" >&2; exit 1; }

# Sign in: for each user, take a mark (the log's line count), request a code, read the code logged after the mark
# (the line `code-email to=<email> locale=<locale> code=<6 digits> …`), enter it, keep the token of Set-Cookie.
: > "$dir/tokens.txt"
for i in $(seq 1 "$users"); do
  email="load-user-$i@example.com"
  mark="$(compose logs --no-log-prefix app 2> /dev/null | wc -l)"
  status="$(curl -s -o /dev/null -w '%{http_code}' -H 'Content-Type: application/json' \
    -d "{\"email\":\"$email\",\"locale\":\"en\"}" "http://localhost:$port/api/code-requests")"
  [ "$status" = "202" ] || { echo "load-test.sh: code request for $email answered $status" >&2; exit 1; }

  code=""
  for _ in $(seq 1 50); do
    code="$(compose logs --no-log-prefix app 2> /dev/null | tail -n +"$((mark + 1))" \
      | grep -F "code-email to=$email locale=" | sed -n 's/.* code=\([0-9]\{6\}\)\([^0-9].*\)\{0,1\}$/\1/p' | tail -1)"
    [ -n "$code" ] && break
    sleep 0.2
  done
  [ -n "$code" ] || { echo "load-test.sh: no code for $email in the log" >&2; exit 1; }

  headers="$(curl -s -D - -o /dev/null -H 'Content-Type: application/json' \
    -d "{\"email\":\"$email\",\"code\":\"$code\",\"rememberMe\":false,\"timeZone\":\"UTC\",\"locale\":\"en\"}" \
    "http://localhost:$port/api/sessions" | tr -d '\r')"
  [[ "$headers" == "HTTP/"*" 200"* ]] || { echo "load-test.sh: sign-in of $email failed: $(head -1 <<< "$headers")" >&2; exit 1; }
  token="$(sed -n 's/^[Ss]et-[Cc]ookie: session=\([^;]*\).*/\1/p' <<< "$headers" | head -1)"
  [ -n "$token" ] || { echo "load-test.sh: no session cookie for $email" >&2; exit 1; }
  echo "$token" >> "$dir/tokens.txt"
done
node -e 'const fs=require("fs");const t=fs.readFileSync(process.argv[1],"utf8").split("\n").filter(Boolean);fs.writeFileSync(process.argv[2],JSON.stringify(t));' "$dir/tokens.txt" "$sessions"
chmod 644 "$sessions"
echo "load-test.sh: $users users signed in"

docker run --rm --network host \
  -v "$repo_root/load:/load:ro" \
  -e LOAD_USERS="$users" -e LOAD_DURATION="${LOAD_DURATION:-10m}" -e LOAD_P95_MS="${LOAD_P95_MS:-300}" \
  -e BASE_URL="http://localhost:$port" \
  "$k6_image" run /load/main-screen.js
k6_status=$?
exit "$k6_status"
