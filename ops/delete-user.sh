#!/usr/bin/env bash
# Deletes one user's data on request (FR-030; research R22; runbook, "Delete a user's data on request"), until account
# deletion exists (epic 8): `ops/delete-user.sh <email>`. It asks for the email again and deletes nothing, exit 1, if
# the two differ. Then, in one transaction, it deletes the user's sessions, the user, and the one-time code and the
# code requests of that email, and prints the rows deleted per table:
#   identity.sessions: 2
#   identity.users: 1
#   identity.one_time_codes: 1
#   identity.code_requests: 3
# The sessions go before the user because sessions.user_id → users.id has no cascade (R-19): a user delete that skipped
# them would fail, and the transaction with it. The script bypasses the core; it prints only the counts above, no email,
# code or id (research R20). The database is the one of HABITS_COMPOSE_FILES (ops/lib/compose.sh).
set -euo pipefail
source "$(dirname "$0")/lib/compose.sh"

if [ "$#" -ne 1 ] || [ -z "$1" ]; then
  echo "usage: ops/delete-user.sh <email>" >&2
  exit 2
fi
email="$1"

printf 'Type the email again to confirm the deletion: ' >&2
confirmation=""
read -r confirmation || true
if [ "$confirmation" != "$email" ]; then
  echo "delete-user.sh: the emails differ; nothing was deleted" >&2
  exit 1
fi

# Emails are stored lower-cased (data-model.md, Email). A failed statement stops psql and the open transaction rolls
# back; the counts are printed only after it committed, so they never describe a rollback.
counts="$(db_psql --no-psqlrc --quiet --tuples-only --no-align --variable "email=$email" <<'SQL'
begin;
with deleted as (
  delete from identity.sessions
  where user_id in (select id from identity.users where email = lower(:'email')) returning 1)
select 'identity.sessions: ' || count(*) from deleted;
with deleted as (delete from identity.users where email = lower(:'email') returning 1)
select 'identity.users: ' || count(*) from deleted;
with deleted as (delete from identity.one_time_codes where email = lower(:'email') returning 1)
select 'identity.one_time_codes: ' || count(*) from deleted;
with deleted as (delete from identity.code_requests where email = lower(:'email') returning 1)
select 'identity.code_requests: ' || count(*) from deleted;
commit;
SQL
)"
echo "$counts"
