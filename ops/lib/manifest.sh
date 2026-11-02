# shellcheck shell=bash
# Sourced by ops/backup.sh, ops/restore-check.sh and ops/restore-live.sh (research R10, R22); it runs nothing by itself.
# Needs ops/lib/compose.sh sourced first. One manifest query and one comparison for the three scripts, so a backup and
# its checks cannot drift apart.
#
# A manifest has one line per table of schemas `identity` and `operations`: <schema>.<table>\t<row count>\t<md5>, where
# the md5 is of every row's text, joined by newlines, in byte order of that text ("C" collation), so equal data gives
# equal sums whatever the database's collation. An empty table sums to md5(''). Lines are in byte order of the name.

# The name of a backup directory: the UTC time it was taken, yyyyMMddTHHmmssZ.
backup_name_pattern='^[0-9]{8}T[0-9]{6}Z$'

# psql as habits on the named database inside the db service, unaligned, without headers; stops at the first SQL error.
# The scripts reach databases other than `habits` with it, since a database cannot be renamed or dropped from a
# session connected to it. Further arguments go to psql.
psql_on() {
  local database="$1"
  shift
  compose exec -T db psql --username habits --dbname "$database" -v ON_ERROR_STOP=1 \
    --no-psqlrc --quiet --tuples-only --no-align "$@"
}

# Prints the SQL that prints the manifest of the database it runs on. The session settings fix how timestamps,
# intervals, floats and bytes print, so the text of a row does not depend on the server's or a role's defaults.
manifest_sql() {
  cat <<'SQL'
set time zone 'UTC';
set datestyle to 'ISO, YMD';
set intervalstyle to 'postgres';
set extra_float_digits to 1;
set bytea_output to 'hex';
select format(
  'select concat_ws(E''\t'', %L, count(*), coalesce(md5(string_agg(t::text, E''\n'' order by t::text collate "C")), md5(''''))) from %I.%I as t',
  n.nspname || '.' || c.relname, n.nspname, c.relname)
from pg_class c join pg_namespace n on n.oid = c.relnamespace
where n.nspname in ('identity', 'operations') and c.relkind in ('r', 'p') and not c.relispartition
order by n.nspname collate "C", c.relname collate "C"
\gexec
SQL
}

# Prints the manifest of the named database.
manifest_of() {
  manifest_sql | psql_on "$1"
}

# Compares a backup's manifest (first file) with the manifest of the restored database (second file), prints one line
# per table and returns 0 when every table is equal. A malformed line, a table on one side only, or a manifest that
# lists no table fails: a backup of no table cannot be the one meant.
compare_manifests() {
  local expected="$1" actual="$2" line name count sum table result=0
  local -A want=() got=()
  local tab=$'\t'
  local format="^([a-zA-Z0-9_]+\\.[^$tab]+)$tab([0-9]+)$tab([0-9a-f]{32})\$"

  while IFS= read -r line || [ -n "$line" ]; do
    if [[ ! "$line" =~ $format ]]; then
      echo "the backup's manifest has a malformed line: '$line'"
      result=1
      continue
    fi
    want["${BASH_REMATCH[1]}"]="${BASH_REMATCH[2]} ${BASH_REMATCH[3]}"
  done < "$expected"
  while IFS="$tab" read -r name count sum || [ -n "$name" ]; do
    got["$name"]="$count $sum"
  done < "$actual"

  if [ "${#want[@]}" -eq 0 ]; then
    echo "the backup's manifest lists no table"
    return 1
  fi

  while IFS= read -r table; do
    [ -n "$table" ] || continue
    if [ -z "${got[$table]+set}" ]; then
      echo "$table: missing from the restored database"
      result=1
    elif [ -z "${want[$table]+set}" ]; then
      echo "$table: not in the backup's manifest"
      result=1
    elif [ "${want[$table]}" = "${got[$table]}" ]; then
      echo "$table: ${want[$table]%% *} rows, equal"
    else
      echo "$table: differs: backup ${want[$table]%% *} rows, md5 ${want[$table]#* }; restored ${got[$table]%% *} rows, md5 ${got[$table]#* }"
      result=1
    fi
  done < <(printf '%s\n' "${!want[@]}" "${!got[@]}" | LC_ALL=C sort -u)
  return "$result"
}
