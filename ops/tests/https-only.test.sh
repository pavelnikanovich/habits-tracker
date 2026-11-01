#!/usr/bin/env bash
# Test of the reverse proxy (FR-016, FR-027, research R13, R20): compose.yaml with ops/tests/compose.https-test.yaml puts
# the one Caddyfile of production, for `localhost` with Caddy's internal certificates, in front of the app. Plain HTTP
# is redirected with 308 and no body; HTTPS answers /health and carries Strict-Transport-Security and the host's CSP; the
# access log goes to standard output with the client address and without the value of a cookie. It binds ports 80 and 443
# of 127.0.0.1 and runs under its own compose project (no development data is touched).
set -uo pipefail

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
failures=0

fail() {
  echo "FAIL: $1" >&2
  failures=$((failures + 1))
}

dir="$(mktemp -d)"
export COMPOSE_PROJECT_NAME="habits-https-only-test-$$"
export HABITS_COMPOSE_FILES="$repo_root/compose.yaml:$repo_root/ops/tests/compose.https-test.yaml"
cleanup() {
  docker compose -f "$repo_root/compose.yaml" -f "$repo_root/ops/tests/compose.https-test.yaml" down --volumes --timeout 1 --remove-orphans > /dev/null 2>&1
  rm -rf "$dir"
}
trap cleanup EXIT
source "$repo_root/ops/lib/compose.sh"

if ! compose up --build --detach --wait > "$dir/up.log" 2>&1; then
  tail -40 "$dir/up.log" >&2
  echo "FAIL: compose up did not succeed (are ports 80 and 443 of 127.0.0.1 free?)" >&2
  exit 1
fi

# Waits up to 60 s for the HTTPS site to answer /health.
ready=""
for _ in $(seq 1 120); do
  [ "$(curl -sk --max-time 2 -o /dev/null -w '%{http_code}' https://localhost/health)" = "200" ] && { ready=yes; break; }
  sleep 0.5
done
[ -n "$ready" ] || { compose logs --tail 30 caddy app >&2; echo "FAIL: https://localhost/health did not answer 200" >&2; exit 1; }

# Plain HTTP: a permanent redirect to the same address over HTTPS, no HTML body.
headers="$(curl -s -D - -o "$dir/redirect.body" http://localhost/ | tr -d '\r')"
[[ "$headers" == "HTTP/"*" 308"* ]] || fail "http://localhost/ did not answer 308: $(head -1 <<< "$headers")"
[ "$(sed -n 's/^[Ll]ocation: //p' <<< "$headers")" = "https://localhost/" ] || fail "the redirect does not point to https://localhost/"
[ ! -s "$dir/redirect.body" ] || fail "the redirect carries a body: $(head -c 100 "$dir/redirect.body")"

[ "$(curl -sk https://localhost/health)" = "Healthy" ] || fail "https://localhost/health did not print Healthy"

headers="$(curl -skI https://localhost/ | tr -d '\r')"
[ "$(sed -n 's/^[Ss]trict-[Tt]ransport-[Ss]ecurity: //p' <<< "$headers")" = "max-age=31536000" ] || fail "Strict-Transport-Security is not max-age=31536000"
policy="$(grep -A1 'const string ContentSecurityPolicy =' "$repo_root/backend/src/HabitsTracker.Host/Web/SecurityHeaders.cs" | tail -1 | sed 's/^ *"//; s/";$//')"
[ -n "$policy" ] || fail "the CSP was not read from SecurityHeaders.cs"
[ "$(sed -n 's/^[Cc]ontent-[Ss]ecurity-[Pp]olicy: //p' <<< "$headers")" = "$policy" ] || fail "the CSP of the host does not reach the client"

# The access log: the client's address is there, the cookie's value is not.
curl -sk -H 'Cookie: session=abc-secret-cookie-value' https://localhost/health > /dev/null
sleep 1
log="$(compose logs --no-log-prefix caddy 2>&1 | grep '"logger":"http.log.access' | grep '/health')"
[ -n "$log" ] || fail "Caddy wrote no access log line to standard output"
grep -q '"remote_ip":"[0-9a-f.:]*"' <<< "$log" || fail "the access log has no client address"
grep -q 'abc-secret-cookie-value' <<< "$log" && fail "the access log holds the cookie's value"
grep -q 'abc-secret-cookie-value' < <(compose logs --no-log-prefix 2>&1) && fail "a log holds the cookie's value"

if [ "$failures" -ne 0 ]; then
  echo "$failures failure(s)" >&2
  exit 1
fi
echo "https-only.test.sh: ok"
