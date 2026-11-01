#!/usr/bin/env bash
# Test of the image and of `docker compose up` (quickstart 1, FR-016, FR-027, FR-034), under its own compose project and
# on random host ports, so a developer's database and scripts/dev.sh are neither stopped nor reused. It builds the
# image from compose.yaml and checks: /health is Healthy; the font license and the icon are served; a row of
# identity.users survives `down` and `up`; the migration bundles exit 0 twice in a row on a fresh database; the image
# knows an IANA zone (tzdata, research R6); the image in Production answers /api/locales with the three locales
# (Translations__Path, no Development appsettings) under the host's CSP, and the built page renders under that CSP in
# a browser; every service of ops/compose.prod.yaml logs to journald (FR-032) and its secrets are not committed.
set -uo pipefail

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
failures=0

fail() {
  echo "FAIL: $1" >&2
  failures=$((failures + 1))
}

dir="$(mktemp -d)"
export COMPOSE_PROJECT_NAME="habits-compose-run-test-$$"
export HABITS_COMPOSE_FILES="$repo_root/compose.yaml:$dir/ports.yaml"
cleanup() {
  docker compose -f "$repo_root/compose.yaml" -f "$dir/ports.yaml" down --volumes --timeout 1 --remove-orphans > /dev/null 2>&1
  rm -rf "$dir"
}
trap cleanup EXIT

# Random host ports in place of the fixed 5432 and 8080 of compose.yaml.
cat > "$dir/ports.yaml" <<'YAML'
services:
  db:
    ports: !override
      - "127.0.0.1::5432"
  app:
    ports: !override
      - "127.0.0.1::8080"
YAML

source "$repo_root/ops/lib/compose.sh"

# compose.yaml itself: the app is built from the Dockerfile, in Development, on 8080, after the healthy db.
config="$(docker compose -f "$repo_root/compose.yaml" config --format json 2> /dev/null)"
[ "$(jq -r '.services.app.build.dockerfile' <<< "$config")" = "Dockerfile" ] || fail "compose.yaml: app is not built from Dockerfile"
[ "$(jq -r '.services.app.ports[0] | "\(.published):\(.target)"' <<< "$config")" = "8080:8080" ] || fail "compose.yaml: app does not publish 8080:8080"
[ "$(jq -r '.services.app.environment.ASPNETCORE_ENVIRONMENT' <<< "$config")" = "Development" ] || fail "compose.yaml: app is not in Development"
[ "$(jq -r '.services.app.environment.ConnectionStrings__Habits' <<< "$config")" = "Host=db;Database=habits;Username=habits" ] || fail "compose.yaml: the connection string differs"
[ "$(jq -r '.services.app.depends_on.db.condition' <<< "$config")" = "service_healthy" ] || fail "compose.yaml: app does not wait for a healthy db"

# Waits up to 60 s for the URL to answer 200.
wait_for() {
  for _ in $(seq 1 120); do
    [ "$(curl -s -o /dev/null -w '%{http_code}' "$1")" = "200" ] && return 0
    sleep 0.5
  done
  return 1
}
app_url() { echo "http://127.0.0.1:$(compose port app 8080 | sed 's/.*://')"; }

if ! compose up --build --detach --wait > "$dir/up.log" 2>&1; then
  tail -40 "$dir/up.log" >&2
  echo "FAIL: compose up --build did not succeed" >&2
  exit 1
fi
url="$(app_url)"
wait_for "$url/health" || fail "/health did not answer 200"
[ "$(curl -s "$url/health")" = "Healthy" ] || fail "/health did not print Healthy"
for path in /fonts/LICENSE-FONT.txt /icon.svg; do
  [ "$(curl -s -o /dev/null -w '%{http_code}' "$url$path")" = "200" ] || fail "$path did not answer 200"
done

# Data survives down (without --volumes) and up.
db_psql --quiet --no-psqlrc -c "insert into identity.users (id, email, time_zone, time_zone_recognized, locale, created_at) values ('0198a000-0000-7000-8000-0000000000aa', 'persist@example.com', 'UTC', true, 'en', now())" \
  || fail "the row was not inserted (is schema identity migrated by the app?)"
compose down --timeout 1 > /dev/null 2>&1 || fail "compose down failed"
compose up --detach --wait > "$dir/up2.log" 2>&1 || { tail -20 "$dir/up2.log" >&2; fail "compose up after down failed"; }
[ "$(db_psql --no-psqlrc --quiet --tuples-only --no-align -c "select count(*) from identity.users where email = 'persist@example.com'")" = "1" ] \
  || fail "the row did not survive down and up"

# The image knows IANA zones (tzdata): without it every browser zone becomes UTC and every stored zone is corrupt.
compose run --rm --no-deps app test -f /usr/share/zoneinfo/Europe/Warsaw > /dev/null 2>&1 || fail "the image has no tzdata (Europe/Warsaw)"

# The migration bundles exit 0 twice in a row on a fresh database.
compose down --volumes --timeout 1 > /dev/null 2>&1
compose up --detach --wait db > "$dir/up3.log" 2>&1 || { tail -20 "$dir/up3.log" >&2; fail "the fresh database did not start"; }
for bundle in migrate-identity migrate-operations; do
  for attempt in 1 2; do
    compose run --rm app "./$bundle" > "$dir/$bundle.log" 2>&1 || { tail -20 "$dir/$bundle.log" >&2; fail "$bundle exited non-zero (run $attempt)"; }
  done
done

# The image in Production: no Development appsettings, so the translation files come from Translations__Path alone, and
# the page is served under the host's CSP. The database is migrated by the bundles above.
key="$(head -c 32 /dev/zero | base64)"
compose run --detach --no-deps --name "$COMPOSE_PROJECT_NAME-production" --publish 127.0.0.1::8080 \
  -e ASPNETCORE_ENVIRONMENT=Production -e "Identity__CodeHashKey=$key" -e Resend__ApiKey=re_test -e Database__MigrateOnStart=false app > /dev/null 2>&1 \
  || fail "the Production container did not start"
production="http://127.0.0.1:$(docker port "$COMPOSE_PROJECT_NAME-production" 8080/tcp | head -1 | sed 's/.*://')"
wait_for "$production/health" || { docker logs "$COMPOSE_PROJECT_NAME-production" 2>&1 | tail -20 >&2; fail "Production /health did not answer 200"; }
locales="$(curl -s "$production/api/locales" | jq -r '[.[].code] | sort | join(",")')"
[ "$locales" = "en,pl,ru" ] || fail "Production /api/locales answered '$locales', expected en,pl,ru"
policy="$(grep -A1 'const string ContentSecurityPolicy =' "$repo_root/backend/src/HabitsTracker.Host/Web/SecurityHeaders.cs" | tail -1 | sed 's/^ *"//; s/";$//')"
[ -n "$policy" ] || fail "the CSP was not read from SecurityHeaders.cs"
[ "$(curl -sI "$production/" | tr -d '\r' | sed -n 's/^[Cc]ontent-[Ss]ecurity-[Pp]olicy: //p')" = "$policy" ] || fail "GET / does not carry the host's CSP"
[ "$(curl -s -o /dev/null -w '%{http_code}' "$production/i18n/en.json")" = "200" ] || fail "/i18n/en.json did not answer 200"
node "$repo_root/ops/tests/lib/csp-smoke.mjs" "$production" || fail "the built page does not render under the CSP"
docker rm --force "$COMPOSE_PROJECT_NAME-production" > /dev/null 2>&1

# The production compose file: every service logs to journald, the secrets come from the environment and are required.
prod="$repo_root/ops/compose.prod.yaml"
prod_env=(IMAGE_OWNER=owner IMAGE_TAG=abc123 POSTGRES_PASSWORD=pw IDENTITY_CODE_HASH_KEY="$key" RESEND_API_KEY=re_test)
prod_config="$(env "${prod_env[@]}" docker compose -f "$prod" config --format json 2> "$dir/prod.err")" || { cat "$dir/prod.err" >&2; fail "ops/compose.prod.yaml does not render"; }
[ "$(jq -r '.services | keys | join(",")' <<< "$prod_config")" = "app,caddy,db" ] || fail "compose.prod.yaml: services are not app, caddy, db"
[ "$(jq -r '[.services[] | .logging.driver] | unique | join(",")' <<< "$prod_config")" = "journald" ] || fail "compose.prod.yaml: a service does not log to journald"
[ "$(jq -r '.services.caddy.image' <<< "$prod_config")" = "caddy:2.11.7" ] || fail "compose.prod.yaml: caddy is not caddy:2.11.7"
[ "$(jq -r '.services.app.image' <<< "$prod_config")" = "ghcr.io/owner/habits-tracker:abc123" ] || fail "compose.prod.yaml: the app image differs"
[ "$(jq -r '.services.app.environment.ASPNETCORE_ENVIRONMENT' <<< "$prod_config")" = "Production" ] || fail "compose.prod.yaml: app is not in Production"
[ "$(jq -r '.services.app.environment.ForwardedHeaders__KnownNetwork' <<< "$prod_config")" = "172.30.0.0/24" ] || fail "compose.prod.yaml: the known network differs"
[ "$(jq -r '.networks.edge.ipam.config[0].subnet' <<< "$prod_config")" = "172.30.0.0/24" ] || fail "compose.prod.yaml: the edge subnet differs"
[ "$(jq -r '[.services.caddy.volumes[].target] | sort | join(",")' <<< "$prod_config")" = "/config,/data,/etc/caddy/Caddyfile" ] || fail "compose.prod.yaml: caddy volumes differ"
[ "$(jq -r '.services.db.ports // [] | length' <<< "$prod_config")" = "0" ] || fail "compose.prod.yaml: the database publishes a port"
for missing in IMAGE_OWNER IMAGE_TAG POSTGRES_PASSWORD IDENTITY_CODE_HASH_KEY RESEND_API_KEY; do
  rest=()
  for entry in "${prod_env[@]}"; do [[ "$entry" == "$missing="* ]] || rest+=("$entry"); done
  env "${rest[@]}" docker compose -f "$prod" config > /dev/null 2>&1 && fail "compose.prod.yaml renders without $missing"
done

# No secret in the repository: .env is ignored, the example holds names only.
(cd "$repo_root" && git check-ignore -q .env) || fail ".env is not git-ignored"
if [ -f "$repo_root/ops/.env.example" ]; then
  grep -vE '^(#|$)' "$repo_root/ops/.env.example" | grep -vE '^[A-Z_]+=$' > /dev/null && fail "ops/.env.example holds a value"
else
  fail "ops/.env.example is missing"
fi

if [ "$failures" -ne 0 ]; then
  echo "$failures failure(s)" >&2
  exit 1
fi
echo "compose-run.test.sh: ok"
