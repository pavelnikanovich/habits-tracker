# Quickstart: Walking skeleton

How to run epic 1 and check that it works, locally and on the server. Paths and commands are those of `plan.md`, "Project Structure"; the API is in `contracts/http-api.md`, the records in `data-model.md`.

## Prerequisites

- Docker with the compose plugin.
- .NET SDK and Node.js at the versions of `plan.md`, "Technical Context" (for the development loop; the compose run needs only Docker).
- Access to `github.com` on the first frontend build: `frontend/scripts/fetch-fonts.sh` downloads the font and checks its SHA-256 (research R24); later builds reuse the files.
- For the server: SSH access to the VPS, the A record of `habits.cardlane.eu`, the Resend account and the DNS records of `mail.cardlane.eu`, the GitHub secrets of research R13.

## 1. Run the application with one command

```bash
docker compose up --build
```

Expected: the application answers at `http://localhost:8080`; `curl -s http://localhost:8080/health` prints `Healthy`. Stop and start again: a user created before is still there (constitution, Additional Constraints: data survives restarts).

## 2. Development loop

```bash
scripts/dev.sh
```

Starts PostgreSQL, the API in development mode and the Angular dev server; writes every log line to `.dev/app.log` (git-ignored). A second `scripts/dev.sh` while one runs exits with an error and starts nothing.

## 3. Sign in locally (US-1.1, US-1.2)

1. Open `http://localhost:4200/sign-in`, enter a new email, request a code.
2. Expected: the page shows "4 of 5 left"; `.dev/app.log` holds the code email in the page's language, with the 6-digit code (FR-010).
3. Enter the code. Expected: the main screen says there are no habits yet and offers sign out, sign out everywhere, the language switcher and the links to the Privacy and Terms pages (FR-018).

## 4. Tests

```bash
scripts/test-fast.sh   # unit and integration tests, backend and frontend; its time is in CLAUDE.md
scripts/test-full.sh   # fast tests, then the end-to-end tests at 360 px and the backup/restore check
scripts/load-test.sh   # the load test of FR-040 with the server's limits: 100 sign-ins, then 10 minutes of measuring
```

Expected: each exits with 0 and prints its summary. The load test prints the share of requests answered within 300 ms and fails below 95% (SC-006).

## 5. Pull request checks (FR-034)

Opening a pull request runs `.github/workflows/pr-checks.yml`: the full tests, the load test, static analysis with no new warnings, and the test-integrity check. Expected: all green.

## 6. Branch deployment and server check (process step 12)

1. The developer adds the label `deploy` to the pull request. `.github/workflows/deploy-branch.yml` builds the image, backs up the database, applies the migrations, starts the new version and runs the availability check once.
2. Expected: `curl -sI http://habits.cardlane.eu` answers with a redirect to HTTPS and no page; `curl -s https://habits.cardlane.eu/health` prints `Healthy` (SC-007, SC-014).
3. The developer signs in at `https://habits.cardlane.eu` with a real Gmail address: the code email arrives within 1 minute, in the inbox, and sign-in reaches the main screen within 2 minutes (SC-001).
4. The developer reads the 95th percentile of request timing since the deployment: `ops/latency-p95.sh <deployment time>` on the server (SC-008).

## 7. Backup and the one restore (FR-035, FR-036)

On the server:

```bash
ops/backup.sh            # also run daily by the systemd timer and before every migration
ops/restore-check.sh     # restores the newest backup into a temporary database, compares, times, drops it
```

Expected: `ops/backup.sh` leaves a dump and its manifest (row count and content checksum of every table) under the backup directory, and deletes backups older than 7 days. `ops/restore-check.sh` prints `PASS`, the per-table comparison and the time from the start of the restore to the passed check (SC-010); the live database is untouched. The time goes into the project ledger at step 13.

## 8. Recovery and manual duties

The procedures for rolling back a deployment, restoring the live database and deleting a user's data on request are in `docs/operations/runbook.md` (research R22, constitution XI). The first two are rehearsed once in Task 24, while the server holds only test data; the rollback by a manual workflow run can only run from `main`, so it is rehearsed once after merge.
