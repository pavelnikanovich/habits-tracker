# Runbook

Procedures for the server at `habits.cardlane.eu`. A living document: each epic that adds a recovery path or a manual duty adds its procedure here (constitution XI). Commands run on the server in `/opt/habits` unless a step says otherwise. Decisions and their reasons are in `specs/001-walking-skeleton/research.md`, R13 and R22.

## Roll back a deployment

When: a deployment broke the server and the previous version worked.

1. Find the previous image's SHA: the last green run of `deploy-main.yml` or `deploy-branch.yml` before the broken one, or `docker image ls ghcr.io/*/habits-tracker` on the server.
2. From a machine with `gh`: `gh workflow run deploy.yml -f image_tag=<SHA> -f migrate=false`. The run backs up the database, starts that image without migrations and checks `/health`.
3. Check: `curl -s https://habits.cardlane.eu/health` prints `Healthy`; `docker compose -f compose.prod.yaml ps --format '{{.Image}}' app` shows the SHA.

If GitHub is unavailable: `ops/backup.sh`, then `IMAGE_TAG=<SHA> docker compose -f compose.prod.yaml up -d app`, then the check of step 3.

Migrations only expand the schema (constitution VI), so the previous image runs on the newer schema. If a migration damaged data, roll back first, then restore the backup taken before the migration (next section).

## Restore the live database

When: data was damaged or deleted by mistake, and the disk is intact.

1. Choose the backup: `ls /var/backups/habits/`; the newest one before the damage, or the one taken before the migration that caused it.
2. `ops/restore-live.sh /var/backups/habits/<directory>`; type the database name when asked. The script restores into `habits_restored`, checks it against the backup's manifest, and only on `PASS` stops `app`, renames `habits` to `habits_broken_<time>` and `habits_restored` to `habits`, starts `app`, checks `/health` and prints the time.
3. Check: the script printed `PASS` and `Healthy`; sign in on the site.
4. Keep `habits_broken_<time>` until the cause is understood, then drop it.

To undo a restore: stop `app`, rename `habits` back to `habits_restored` and `habits_broken_<time>` to `habits`, start `app`.

## Delete a user's data on request

When: a request arrives at the contact email (Privacy page, FR-030), until account deletion exists (epic 8).

1. Accept the request only from the address of the account itself. From another address, reply: send the request from the address you sign in with.
2. `ops/delete-user.sh <email>`; type the email again when asked. The script deletes, in one transaction, the user's sessions, the user, and the one-time code and code requests of that email, and prints the rows it deleted.
3. Reply from the contact email, within a month of the request (GDPR art. 12(3)): the data is deleted; copies in backups are gone within 7 days, and the email service deletes its copy of the code email within 30 days.
