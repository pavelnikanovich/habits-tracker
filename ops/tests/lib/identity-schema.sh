# shellcheck shell=bash
# Sourced by shell tests that need the real schema `identity`; it runs nothing by itself. Needs ops/lib/compose.sh
# sourced first. `apply_identity_schema <work directory>` builds the migrate-identity bundle of the repository (the
# same migrations the image's bundle runs, research R2) into the work directory and applies it to the db service of
# HABITS_COMPOSE_FILES, over the host port the compose file publishes for 5432 (`ports: ["127.0.0.1::5432"]`).
# dotnet-ef is the local tool pinned in backend/.config/dotnet-tools.json, restored here if the machine has not yet.

apply_identity_schema() {
  local work="$1" repo_root dotnet_root port
  repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

  if ! command -v dotnet > /dev/null 2>&1; then
    dotnet_root="${DOTNET_ROOT:-$HOME/.dotnet}"
    [ -x "$dotnet_root/dotnet" ] || { echo "apply_identity_schema: no dotnet on PATH or in $dotnet_root" >&2; return 1; }
    export PATH="$dotnet_root:$PATH"
  fi
  # dotnet-ef is the repository's local tool (backend/.config/dotnet-tools.json), at the pinned version.
  (
    cd "$repo_root/backend" || exit 1
    dotnet tool restore > "$work/tool-restore.log" 2>&1 || { cat "$work/tool-restore.log" >&2; exit 1; }
    DOTNET_ENVIRONMENT=Development Database__MigrateOnStart=false dotnet tool run dotnet-ef -- \
      migrations bundle --context IdentityDbContext \
      --project "$repo_root/backend/src/HabitsTracker.Identity.Adapters" \
      --startup-project "$repo_root/backend/src/HabitsTracker.Host" \
      --output "$work/migrate-identity" --force > "$work/bundle.log" 2>&1
  ) || { cat "$work/bundle.log" >&2; echo "apply_identity_schema: the bundle was not built" >&2; return 1; }

  port="$(compose port db 5432 | sed 's/.*://')"
  [ -n "$port" ] || { echo "apply_identity_schema: the db service publishes no port 5432" >&2; return 1; }
  # The bundle builds the host's services too, which need the translation files' folder (the image sets it itself).
  DOTNET_ENVIRONMENT=Development Translations__Path="$repo_root/frontend/public/i18n" \
    "$work/migrate-identity" --connection "Host=127.0.0.1;Port=$port;Database=habits;Username=habits" > "$work/migrate.log" 2>&1 \
    || { cat "$work/migrate.log" >&2; echo "apply_identity_schema: the bundle failed" >&2; return 1; }
}
