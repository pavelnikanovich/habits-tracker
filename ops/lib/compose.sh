# shellcheck shell=bash
# Sourced by the ops scripts (design review M6); it runs nothing by itself.
#
# HABITS_COMPOSE_FILES lists the compose files, separated by colons like Docker's own COMPOSE_FILE; unset, it is
# compose.yaml at the repository root. A value that is empty, has an empty entry or names a missing file is refused:
# acting on the development project when production was meant is worse than failing.

compose_default_file="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/compose.yaml"

# Prints the `-f <file>` arguments, one word per line, or a message on stderr and status 1.
compose_file_arguments() {
  local value file
  local -a files
  if [ -z "${HABITS_COMPOSE_FILES+set}" ]; then
    value="$compose_default_file"
  elif [ -z "$HABITS_COMPOSE_FILES" ]; then
    echo "HABITS_COMPOSE_FILES is set but empty: name the compose files, colon-separated, or unset it for compose.yaml" >&2
    return 1
  else
    value="$HABITS_COMPOSE_FILES"
  fi

  # read drops the empty field after a last colon, so one is appended: "a:" then reads as a and an empty entry.
  IFS=: read -r -a files <<< "$value:"
  for file in "${files[@]}"; do
    if [ -z "$file" ]; then
      echo "HABITS_COMPOSE_FILES has an empty entry: '$value'" >&2
      return 1
    fi
    if [ ! -f "$file" ]; then
      echo "HABITS_COMPOSE_FILES names a missing file: $file" >&2
      return 1
    fi
    printf '%s\n%s\n' -f "$file"
  done
}

# docker compose over the files of HABITS_COMPOSE_FILES, with the given arguments.
compose() {
  local arguments word
  local -a files=()
  arguments="$(compose_file_arguments)" || return 1
  while IFS= read -r word; do files+=("$word"); done <<< "$arguments"
  docker compose "${files[@]}" "$@"
}

# psql as habits on habits inside the db service; stops at the first SQL error. Arguments go to psql.
db_psql() {
  compose exec -T db psql --username habits --dbname habits -v ON_ERROR_STOP=1 "$@"
}
