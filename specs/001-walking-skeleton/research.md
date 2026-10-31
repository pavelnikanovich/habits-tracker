# Research: Walking skeleton

Phase 0 of `plan.md`. Each section records a decision, why, and what else was considered. Facts were read from primary sources on 2026-10-30 (release notes, package registries, vendor documentation); the binding facts of `docs/superpowers/specs/2026-10-30-external-constraints-check.md`, "For `speckit-plan`", are taken as given and cited as "constraints check". Every library, service or tool outside ASP.NET Core, Angular, PostgreSQL and docker compose carries its case — the function that breaks without it (constitution, principle I) — in "Dependencies and their cases" at the end.

## R1. Backend structure: hexagonal packages

**Decision.** One .NET solution (`backend/HabitsTracker.slnx`, the .NET 10 default format). Identity, the only context of this epic, is two projects:

- `HabitsTracker.Identity.Core` — a plain class library (`Microsoft.NET.Sdk`): value objects, aggregates, the code request policy, the use cases (driving ports) and the driven port interfaces of `data-model.md`. It references nothing but the .NET base library; `TimeProvider` and `System.Security.Cryptography` are part of it.
- `HabitsTracker.Identity.Adapters` — references the core and ASP.NET Core: the HTTP endpoints of `contracts/http-api.md`, the EF Core storage, the Resend and development-log email senders, the translation-file reader, the session cookie handling.

`HabitsTracker.Host` is the composition root: it wires the adapters to the core, serves the Angular build, and holds what belongs to no context — the health endpoint, request timing, the hourly cleanup, the migrations entry point. Habit Tracking and Live Updates get the same two projects in their epics.

The build holds the dependency rule (principle IV): the core's project has no `PackageReference`, `FrameworkReference` or `ProjectReference`, so ASP.NET Core or EF Core types do not compile in it. A test reads the core's `.csproj` and fails if any such reference appears, so the rule cannot be dropped by a one-line edit.

**Rationale.** Two projects per context is the smallest form that lets the compiler hold the rule; folders inside one project would not. Ports with hand-written fakes make the core testable without a database, an email service or a clock.

**Alternatives.** One project per context with folders: the rule is held only by review. Separate `Domain` and `Application` projects per context (onion style): a third project per context with no rule the compiler needs. An architecture-test library (NetArchTest, ArchUnitNET): a dependency doing what project references already do.

## R2. Data access and migrations

**Decision.** EF Core 10 with `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.x in the adapters; one `DbContext` per context, schema `identity`; the host's own `DbContext` for schema `operations`. Migrations are EF Core migrations, shipped as a migration bundle (`efbundle`) built into the image; the deployment runs the bundle after the backup and before the new version starts. Npgsql's connection pooling is used as it comes.

**Rationale.** The branch deployment applies migrations to the one database (principle VI): versioned, ordered migrations that a deployment step can run are needed from the first table. EF Core's documentation recommends bundles for automated deployment and warns against migrating at application start in production. EF Core also maps the aggregates without hand-written SQL for every query.

**Alternatives.** Plain Npgsql with SQL scripts and a hand-written migration runner: fewer packages, but a runner, a history table and row mapping to write and test. Dapper: mapping only, migrations still missing. `Database.MigrateAsync()` at start: rejected by EF Core's own guidance for production.

## R3. Email service adapter (Resend) and the step 5 spike

**Decision.** The code email sender port has two adapters: **Resend** over its HTTP API (`POST https://api.resend.com/emails`, `HttpClient`, no SDK) in production, and the **development log** in development (R17). Production refuses to start without the Resend API key; development never registers the Resend adapter (FR-010). Open and click tracking stay off: they are off by default for every Resend domain and are a per-domain setting, so the developer checks `mail.cardlane.eu` once in the dashboard (FR-007).

Outcome mapping of a send (FR-004, FR-005):

| Resend answer | Outcome | `retryAt` |
|---|---|---|
| `2xx` | sent | — |
| `429 daily_quota_exceeded` | refused: email service's daily limit | next 00:00 UTC — the free plan's daily quota "resets at midnight UTC" |
| `429 monthly_quota_exceeded` | refused: email service's limit | none (reset date not documented) |
| `429 rate_limit_exceeded` | refused: email service unavailable | from `retry-after` when present |
| other `4xx`, `503 service_unavailable` | refused: email service unavailable | none |
| `500 application_error`, timeout after 10 s, connection lost after the request was sent | unclear: counts as sent | — |

The timeout of 10 seconds is the agent's call. A `500` is treated as unclear because the email may have left. Not used: `Idempotency-Key` — an unclear result is not retried, it counts as sent (FR-004).

TLS to the recipient's mail server stays Resend's default, "Opportunistic": a server without TLS still gets the code email, unencrypted. "Enforced" would fail delivery to such servers, each failure a bounce against Resend's 4% threshold, for a code that lives 10 minutes.

**Spike (process step 5, email service design "Left to verify"): passed, 2026-10-31.** One `curl` from the VPS to `POST https://api.resend.com/emails`, from `no-reply@mail.cardlane.eu` (domain verified, region eu-west-1) to a Gmail address, with a key limited to sending from that domain and deleted afterwards. The spike left no code in the repository (principle VII).

1. Delivery: `HTTP 200 in 0.22s`; Gmail's "Show original" reads "Delivered after 1 second", label Inbox, not spam (FR-006, SC-001). `SPF: PASS`, `DKIM: 'PASS' with domain mail.cardlane.eu`, `DMARC: 'PASS'`; TLS on delivery.
2. No Resend branding or footer: the email holds only the text sent.
3. Headers: `ratelimit-limit: 10`, `ratelimit-policy: 10;w=1`, `ratelimit-remaining`, `ratelimit-reset`, `x-resend-daily-quota: 0`, `x-resend-monthly-quota: 0`. The quota headers read 0 after the first email of the day, so they do not report the emails left; the adapter does not read them and relies on the `429` names of the table, which come from Resend's error reference and were not triggered (using up the daily quota would spend 100 emails).

Tracking (FR-007): the domain's configuration offers "Enable tracking metrics → Configure" with no tracking subdomain set, so opens and clicks are not tracked. The Message-ID ends in `eu-west-1.amazonses.com`: Resend sends through Amazon SES in Ireland, so the record of processing names Amazon Web Services as Resend's sub-processor.

**Alternatives.** The Resend .NET SDK: one call wrapped by a package. SMTP: ports 25 and 465 are blocked on Hetzner Cloud.

## R4. Sessions and the cookie

**Decision.** Server-side sessions in table `identity.sessions` (data-model). The token is 32 bytes from `RandomNumberGenerator`, base64url in a cookie named `session`: `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`, no `Domain`. The table stores the SHA-256 hash of the token, not the token, so a leaked database opens no session. A small ASP.NET Core authentication handler in the adapters reads the cookie, calls the core's "authenticate a session" use case, and on success sets the user for the request; the core updates `last activity at` on every authenticated request. For a Remembered session every authenticated response renews `Max-Age` to 30 days; for a session that is not remembered the cookie carries no `Expires` or `Max-Age` (FR-014). Revocation deletes the row, so the next request fails (FR-015).

No cookie prefix: `__Host-` cookies are not accepted by Chromium over `http://localhost`, where development, the end-to-end tests and the load test run; a plain `Secure` cookie is accepted there (MDN: "The `https:` requirements are ignored when the `Secure` attribute is set by localhost"). The end-to-end tests run in Chromium only; WebKit rejects `Secure` cookies on localhost.

Cross-site request forgery: `SameSite=Lax` keeps the cookie off cross-site `POST`, `PUT` and `DELETE`, and every state-changing endpoint takes a JSON body, which a cross-site form cannot send without a preflight. No antiforgery tokens.

**Rationale.** FR-012–FR-015 ask for exactly a server-side record with an opaque id. ASP.NET Core's cookie authentication keeps the whole ticket in the cookie; replacing that with a ticket store would leave most of it unused.

**Alternatives.** ASP.NET Core cookie authentication with an `ITicketStore`: a serialized ticket and Data Protection keys to persist for nothing the spec needs. ASP.NET Core Identity: passwords, stores and UI the product does not have. `SameSite=Strict`: a visitor following the README link would arrive signed out of an existing session.

## R5. One-time codes and code request limits

**Decision.**

- **Code.** `RandomNumberGenerator.GetInt32(0, 1_000_000)` formatted as 6 digits. Stored as HMAC-SHA-256 over the lower-cased email and the code, keyed by a 32-byte server secret (`Identity__CodeHashKey`, R13); compared in constant time. A plain hash of a 6-digit code falls to a million guesses; the key keeps a database copy useless without the server's secret, and binding the email means a code presented for another email never matches.
- **One code per email.** `identity.one_time_codes` is keyed by email; storing a new code replaces the earlier one (FR-002). The sequence in "request a code": take the locks, check the limits, send, and only if the send is sent or unclear, store the code and the code request in the same transaction (FR-004).
- **Limits.** Counted from `identity.code_requests` over the last 24 hours, by email and by IP address prefix, from configuration (`Identity__CodeRequestLimits__PerEmail = 5`, `__PerIpAddress = 10`, `__Window = 24:00:00`; FR-003). The check and the insert run in one transaction holding `pg_advisory_xact_lock` on the email and on the prefix, so concurrent requests cannot both pass the last free slot. The send happens inside that transaction; at most one request per email or prefix waits for it, bounded by the 10-second send timeout.
- **Entry attempts.** "Sign in" increments `entry_attempts` with a row lock (`SELECT … FOR UPDATE`), so two concurrent entries cannot both be counted as the fifth.

**Rationale.** The limits count only what the spec counts (FR-004): sent and unclear requests, never refused ones. That is a rule about stored records, not about request rates.

**Alternatives.** ASP.NET Core's rate limiter middleware: in memory, counts every request including refused ones, and cannot report the moment the oldest counted request leaves the window. A serializable transaction instead of advisory locks: retries on conflict to write and test. Storing the code before the send: a refused send would have invalidated the earlier code (FR-002).

## R6. Time zones

**Decision.** The browser sends `Intl.DateTimeFormat().resolvedOptions().timeZone` at sign-in. The core accepts it when `TimeZoneInfo.TryFindSystemTimeZoneById` finds it, which on Linux reads the image's tzdata; otherwise the user gets `UTC` with "not recognized" (FR-009). The runtime image is the default ASP.NET Core 10 image (Ubuntu), never Alpine or Chiseled without `-extra`, never globalization-invariant mode (constraints check, 2.2).

**Alternatives.** NodaTime: a richer time zone library, with no case in this epic; epic 2 may bring one.

## R7. The client's IP address

**Decision.** In production Caddy is the only entry point and sets `X-Forwarded-For` and `X-Forwarded-Proto`. ASP.NET Core's forwarded headers middleware trusts them only from the compose network of the proxy, a fixed subnet in `compose.prod.yaml`. The IP address prefix (data-model) is computed from the resulting remote address. In development and in CI there is no proxy and the remote address is used as is.

**Alternatives.** Trusting `X-Forwarded-For` from anyone: any client could pick its own IP address and bypass the per-IP limit.

## R8. Logs and their retention

**Decision.** In production the application and Caddy log to standard output; the containers use the `journald` logging driver, and a journald drop-in on the server (`ops/journald/habits.conf`) sets `MaxRetentionSec=7day` and `MaxFileSec=1day`, so a journal file older than 7 days is removed within a day (FR-032, FR-033). The application never logs a one-time code in production: only the development-log email sender writes the email, and it is registered only in development (FR-011). Request bodies are not logged.

Checks: an automated test runs the sign-in flow in the Production environment with a capturing logger and asserts that no log line contains the code (SC-005); a CI test asserts the two journald values in `ops/journald/habits.conf`; the developer confirms the journal's retention on the server at step 12.

**Alternatives.** Docker's default `json-file` driver: rotates by size, not by age, so "7 days" cannot be guaranteed. Serilog with a rolling file sink and a retention limit: a dependency and a log volume, where the server's journal already deletes by age.

## R9. Request timing metrics (FR-039)

**Decision.** A middleware in the host measures each request and adds it to an in-memory histogram per endpoint route template (or `static` for files) and latency bucket. Once a minute the host upserts the counts into `operations.request_timings` per hour. The rows hold no IP address and no user id. Bucket upper bounds in ms: 10, 25, 50, 75, 100, 150, 200, 250, 300, 400, 500, 750, 1000, 2000, 5000, and +∞. With a bound at 300 ms, "95% answered within 300 ms" is read exactly as the share of requests in buckets up to 300. The 95th percentile is reported as the upper bound of the bucket that holds it.

`ops/latency-p95.sh <since>` prints the share within 300 ms and the 95th-percentile bucket since a given instant; the developer runs it on the server after the branch deployment (SC-008) and for each calendar month. An automated test makes a request and asserts that its timing reaches `operations.request_timings`, aggregated, with no IP address and no user id.

**Alternatives.** OpenTelemetry with Prometheus and Grafana: three services to run and secure to keep one histogram. Computing the percentile from logs: logs are kept only 7 days.

## R10. Backups and the restore check (FR-035, FR-036)

**Decision.**

- `ops/backup.sh` runs `pg_dump --format=custom` through the `db` container into a timestamped directory under `/var/backups/habits/` and writes `manifest.tsv` next to it. The manifest is taken in the same repeatable-read snapshot as the dump (`pg_export_snapshot`) and holds, for every table in schemas `identity` and `operations`, the row count and `md5(string_agg(row::text, E'\n' ORDER BY row::text))`. The script then deletes backup directories older than 7 days.
- A systemd timer on the server runs it daily (`ops/systemd/habits-backup.timer`); the deployment workflow runs it before the migration bundle.
- `ops/restore-check.sh` creates a temporary database next to the live one, restores the newest backup into it with `pg_restore`, computes the same manifest from it, compares every table, prints `PASS` or `FAIL` with the comparison and the time from the start of the restore to the passed check, and drops the temporary database. It never connects to the live database.
- A CI job runs both scripts against a seeded database: a restore of an untouched backup passes; a manifest altered by the test fails; a backup directory dated 8 days back is deleted.

**Rationale.** `pg_dump` and `pg_restore` come with the PostgreSQL image; the checksum is plain SQL. The manifest is taken from the same snapshot as the dump, so a write between the two cannot make a correct restore fail.

**Alternatives.** pgBackRest or WAL-G: continuous archiving, for an RPO of 24 hours that a daily dump meets. Hetzner's server backups: they hold the whole disk and need a payment option, and they were set aside by the constraints check.

## R11. Frontend

**Decision.** Angular 22 (Node 24 LTS), standalone components, zoneless, signals; plain CSS with the system font stack, so no font file and no third-party request (FR-027). No component library.

- **Translations: Transloco 8.4** with `@jsverse/transloco-messageformat` 8.4 (ICU MessageFormat, plural categories from CLDR through `Intl.PluralRules`), switching the language in place without a reload (FR-022), as the constraints check (2.1) requires. Translation files: `frontend/public/i18n/<code>.json`, one per language, each with `language.name`. The list of languages comes from `GET /api/locales`, which reads the same files, so a new file is a new language with no code change (FR-020). The backend composes the code email from the `email.*` keys of the same files; those keys use only simple `{placeholder}` substitution, so the backend needs no MessageFormat parser.
- **Language before sign-in:** `navigator.languages` matched against the available codes, else `en`; the switcher's choice in `localStorage` (FR-021). After sign-in the user's locale wins and the switcher calls `PUT /api/me/locale` (FR-023).
- **Content Security Policy** `default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'`, set by the host. Angular's critical-CSS inlining is turned off, since its `onload` handler would need `'unsafe-inline'` in `script-src`; `'unsafe-inline'` for styles covers the component styles Angular injects and allows no other server.

**Alternatives.** Angular's built-in i18n: compile-time, a reload per language (constraints check 2.1). `@ngx-translate/core` with a MessageFormat compiler: equivalent; Transloco's plugin is maintained by the same project as the library. Angular Material: a component library for four simple pages.

## R12. Tests: tools and commands

**Decision.**

| Layer | Tool | Runs in |
|---|---|---|
| Identity core unit tests, parameterized from the spec's example tables | xUnit v3, `FakeTimeProvider`, hand-written fakes of the ports | fast |
| Adapters and API: EF Core storage, endpoints, cookie, cross-user tests (FR-017), log and metrics tests | xUnit v3, `WebApplicationFactory`, Testcontainers PostgreSQL 18 | fast |
| Frontend units: translation key parity across files (SC-012), plural forms of 1, 2 and 5 in `pl` and `ru`, components | Vitest (the Angular 22 default) | fast |
| End-to-end at 360 px: sign-in with the code read from the development log, sign out, sign out everywhere with two browser contexts, language switch without reload, no third-party requests (SC-011, SC-013), cookie expiry in a fresh context (SC-004) | Playwright 1.64, Chromium | full |
| Backup and restore check | `ops/` scripts against compose | full |
| HTTPS only: Caddy with `tls internal` in front of the app; `http://` answers with a redirect and no page (SC-004) | `curl` against compose | full |
| Load test (R14) | k6 | own command, every pull request |

Commands, added to `CLAUDE.md` by the task that creates them:

- **Fast** `scripts/test-fast.sh`: `dotnet test backend/HabitsTracker.slnx` and `npm --prefix frontend test`. About 2 minutes; Testcontainers needs Docker, which every developer machine and GitHub runner here has.
- **Full** `scripts/test-full.sh`: fast, then the end-to-end tests, the backup and restore check, the HTTPS check.
- **Load** `scripts/load-test.sh`.

No mocking library and no assertion library: fakes implement the core's ports, and xUnit's assertions suffice.

**Alternatives.** Only unit tests with an in-memory database: unique index, advisory locks and `cidr` would go untested. A shared PostgreSQL started by hand before tests: the fast command would depend on state outside it.

## R13. Delivery: images, workflows, server, secrets

**Decision.**

- **Image.** One multi-stage `Dockerfile`: Node builds the Angular app; the .NET SDK publishes the host and builds the migration bundle; the runtime stage is the default ASP.NET Core 10 image with the app, the static files and the translation files. Pushed to GitHub Container Registry, tagged with the commit SHA (free for public images).
- **Compose.** `compose.yaml` runs `db` (PostgreSQL 18, volume at `/var/lib/postgresql`, the PostgreSQL 18 image's mount point) and `app` (development mode) with one command (constitution, Additional Constraints). `compose.prod.yaml` on the server adds `caddy` (2.11, volumes for `/data` and `/config`, never recreated by a deployment, constraints check 2.4) and runs `app` in production from the GHCR image.
- **Workflows** (FR-034; process document, "Epic 1"):
  - `pr-checks.yml` on `pull_request`: backend, frontend, end-to-end, load test, backup and restore check, HTTPS check, static analysis, test integrity (R15).
  - `deploy-branch.yml` on `pull_request` `labeled` with `deploy`, only for branches of this repository: calls `deploy.yml`.
  - `deploy-main.yml` on `push` to `main`: calls `deploy.yml`.
  - `deploy.yml` (`workflow_call`, environment `production`): build and push the image; over SSH run `ops/backup.sh`, the migration bundle, `docker compose up -d`; then the availability check once (R16).
  - `availability.yml` (R16).
  - GitHub-hosted runners only, no `pull_request_target` (constraints check 2.5).
- **Server (manual).** Docker with compose, a deploy user with key-only SSH, the Hetzner Cloud Firewall open on 22, 80, 443, the journald drop-in, the backup timer, `/opt/habits` with `compose.prod.yaml` and `.env`.
- **Secrets**, in GitHub environment `production` and the server's `.env`, never in the repository (principle VIII): `RESEND_API_KEY`, `IDENTITY_CODE_HASH_KEY`, `POSTGRES_PASSWORD`, `DEPLOY_SSH_KEY`, `DEPLOY_HOST`, `DEPLOY_KNOWN_HOSTS`. The contact email and the developer's full name are page text in the translation files (FR-028), not secrets.
- **The GitHub repository** is created public by the developer before the first workflow runs.

**Alternatives.** Building on the server: the build would compete with the running app for 2 vCPU. nginx with certbot: two tools for what Caddy does alone. A self-hosted runner: unsafe for a public repository.

## R14. Load test (FR-040)

**Decision.** k6 (`grafana/k6` image, v2.3) against `compose.yaml` with `compose.loadtest.yaml`:

- **Limits.** Limits the app to 1 vCPU and 2 GB and the database to 1 vCPU and 2 GB: together the server's 2 vCPU and 4 GB. Split limits are stricter than a shared pool: any schedule that fits the split also fits the shared pool, so passing the split passes "limited together".
- **Per-IP limit.** Raises the per-IP code request limit through configuration (FR-003), since every load-test user signs in from the runner's one address.
- **Sign-in before measuring.** `scripts/load-test.sh` first signs in 100 users: it requests a code for each, reads the codes from the development log, enters them, and writes the session tokens to a JSON file that k6 reads in its init context.
- **Measured run.** 100 virtual users for 10 minutes. Each sleeps a uniformly random 1 to 5 seconds before each request. Every 10th request switches the language (`PUT /api/me/locale`). The others open the main screen: `GET /` and `GET /api/me`, as one request each *(agent's call: the API call and the page shell are what a browser with a warm cache requests)*.
- **Threshold.** `http_req_duration` p(95) < 300 ms over the measured run; k6 exits non-zero when it is missed, which fails the check (SC-006).

**Alternatives.** NBomber (.NET): a NuGet dependency where k6 runs as a container without entering the build. A hand-written C# load generator: percentile and pacing code to write and test.

## R15. Static analysis and test integrity

**Decision.**

- **Static analysis** starts at zero warnings, so the ratchet's baseline is zero and any new warning fails:
  - Backend: `TreatWarningsAsErrors`, `AnalysisLevel` `latest-recommended`, nullable reference types, and CA1502 (cyclomatic complexity, threshold 25) switched on, in `backend/Directory.Build.props`.
  - Frontend: `ng lint` with angular-eslint and ESLint's `complexity` rule (20), run with `--max-warnings 0`.
- **Duplication rules** (process step 11) come from plugins of the same linters, so no separate tool runs: `SonarAnalyzer.CSharp` with S4144 (methods with identical implementations) and S1192 (duplicated string literals), switched on in `backend/.globalconfig`; `eslint-plugin-sonarjs` with `no-identical-functions` and `no-duplicate-string` in `frontend/eslint.config.js`. Every other rule of both plugins is switched off. Neither finds copied blocks of code; that stays with review.
- **Test integrity** (`scripts/test-integrity.sh`, in `pr-checks.yml`). Fails when either holds:
  - an added line in the diff carries a skip or focus marker: `Skip =`, `.skip(`, `.only(`, `xit(`, `xdescribe(`, `test.fixme(`;
  - the number of tests listed by `dotnet test --list-tests`, Vitest and `playwright test --list` drops below that of `main`.

  The developer allows an exception with the pull request label `test-exception`, which only collaborators can add.
- **No baseline on `main` yet.** Epic 1 compares against an empty `main`.

**Alternatives.** SonarQube or SonarCloud: a separate service, excluded by the process; its copy-paste detection runs only there. No duplication rule, leaving duplication to review: the process asks for the rule. `SonarAnalyzer.CSharp` is under the SONAR Source-Available License v1.0 (not OSI), which allows its use as a build analyzer; `eslint-plugin-sonarjs` is LGPL-3.0.

## R16. Availability check (FR-038)

**Decision.**

- **Scheduled check.** `availability.yml` on `schedule` every 5 minutes, the shortest interval GitHub allows, plus `workflow_dispatch`: `curl --fail --max-time 10 https://habits.cardlane.eu/health`. A timeout, a TLS error or a non-success status fails the run, and GitHub notifies the user who created the workflow of a failed scheduled run.
- **Monthly value.** `ops/availability-month.sh YYYY-MM` lists the month's runs through the GitHub REST API and prints passed / ran; a month with no runs prints "no value".
- **Before merge.** The branch deployment runs the same `curl` once after the deployment.
- **Not covered.** Delayed or dropped runs, and the 60-day pause of schedules in a quiet public repository, are the accepted risks of the constraints check.

## R17. Development mode and the development log

**Decision.** `scripts/dev.sh` takes a lock on `.dev/dev.lock` (`flock`); if another instance holds it, it exits with an error and starts nothing. Otherwise it:

- starts `db` from `compose.yaml`;
- runs the host with `dotnet watch` in the Development environment;
- runs `ng serve` with a proxy for `/api` and `/health`, so the page and the API share the origin `http://localhost:4200`;
- appends every line of both to `.dev/app.log`.

`.dev/` is git-ignored; `CLAUDE.md` points to the log. In development the code email sender writes the recipient, the locale, the subject and the body, code included, to the log (FR-010).

## Dependencies and their cases

Everything here beyond ASP.NET Core, Angular, PostgreSQL and docker compose; each needs the developer's approval at the technical plan review (constitution, Development Workflow).

| Dependency | Where | Case: what breaks without it |
|---|---|---|
| EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.x, `Microsoft.EntityFrameworkCore.Design`, `dotnet-ef` (local tool) | backend adapters, build | versioned migrations that the deployment applies after the backup (principle VI, FR-035); access to PostgreSQL at all needs at least Npgsql (R2) |
| Transloco 8.4 + `@jsverse/transloco-messageformat` 8.4 | frontend | switching the language without a reload (FR-022) and Polish and Russian plurals from the translation files (FR-020, SC-012) (R11) |
| xUnit v3 | backend tests | every backend test (principle II) |
| `Microsoft.AspNetCore.Mvc.Testing` 10.0.x | backend tests | API tests in process: the cross-user tests of FR-017 and the cookie tests of SC-004 |
| `Testcontainers.PostgreSql` 4.16 | backend tests | tests against real PostgreSQL in the fast command: the unique email, advisory locks, `cidr` (R12) |
| `Microsoft.Extensions.TimeProvider.Testing` 10.x | backend tests | tests of 10 minutes, 24 hours and 30 days without waiting (FR-002, FR-003, FR-013) |
| angular-eslint 22 | frontend build | the static analysis check of every pull request (FR-034) |
| `SonarAnalyzer.CSharp` 10.35, `eslint-plugin-sonarjs` 4.2 | backend build, frontend build | the duplication rules of the static analysis ratchet (process step 11); only those rules are on (R15) |
| Playwright 1.64 | frontend tests | the end-to-end tests at 360 px and the no-third-party check (FR-019, SC-011, SC-013) |
| k6 2.3 (container image) | CI | the load test (FR-040, SC-006) |
| Caddy 2.11 (container image) | server | HTTPS only with automatic Let's Encrypt certificates (FR-016) |
| Resend (service) | production | the code email (FR-006), chosen in the email service design |
| GitHub Actions, GitHub Container Registry (services) | CI/CD | the three delivery workflows (FR-034) and the availability check (FR-038) |

Considered and not taken: Resend .NET SDK, ASP.NET Core Identity, cookie authentication with a ticket store, ASP.NET Core rate limiter, Serilog, OpenTelemetry/Prometheus/Grafana, NodaTime, an architecture-test library, mocking and assertion libraries, Angular Material, `@ngx-translate`, NBomber, pgBackRest, nginx with certbot, SonarQube.

## Versions

Pinned as read on 2026-10-30; the implementing task takes the latest patch of the same minor.

| Component | Version |
|---|---|
| .NET SDK / runtime | 10.0.403 / 10.0.14 (LTS, supported to 2028-11-14) |
| EF Core | 10.0.14 |
| Npgsql EF Core provider | 10.0.3 |
| PostgreSQL (image `postgres`) | 18.6 |
| Angular / Angular CLI | 22.2.4 / 22.2.5 |
| Node.js | 24 LTS (Angular 22 accepts `^24.15.0`) |
| Transloco / transloco-messageformat | 8.4.0 / 8.4.0 |
| Playwright | 1.64.1 |
| angular-eslint | 22.6.0 |
| SonarAnalyzer.CSharp / eslint-plugin-sonarjs | 10.35.0.4138 / 4.2.2 |
| xUnit v3 (`xunit.v3`) | 4.0.2 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.14 |
| Testcontainers.PostgreSql | 4.16.0 |
| Microsoft.Extensions.TimeProvider.Testing | 10.11.0 |
| k6 (image `grafana/k6`) | 2.3.0 |
| Caddy (image `caddy`) | 2.11.7 |
