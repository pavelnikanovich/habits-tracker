# Research: Walking skeleton

Phase 0 of `plan.md`. Each section records a decision, why, and what else was considered. Facts were read from primary sources on 2026-10-30 (release notes, package registries, vendor documentation); the binding facts of `docs/superpowers/specs/2026-10-30-external-constraints-check.md`, "For `speckit-plan`", are taken as given and cited as "constraints check". Every library, service or tool outside ASP.NET Core, Angular, PostgreSQL and docker compose carries its case — the function that breaks without it (constitution, principle I) — in "Dependencies and their cases" at the end.

## R1. Backend structure: hexagonal packages

**Decision.** One .NET solution (`backend/HabitsTracker.slnx`, the .NET 10 default format). Identity, the only context of this epic, is two projects:

- `HabitsTracker.Identity.Core` — a plain class library (`Microsoft.NET.Sdk`): value objects, aggregates, the code request policy, the use cases (driving ports) and the driven port interfaces of `data-model.md`. It references nothing but the .NET base library; `TimeProvider` and `System.Security.Cryptography` are part of it.
- `HabitsTracker.Identity.Adapters` — references the core and ASP.NET Core: the HTTP endpoints of `contracts/http-api.md`, the EF Core storage, the Resend and development-log email senders, the translation-file reader, the session cookie handling.

`HabitsTracker.Host` is the composition root: it wires the adapters to the core, serves the Angular build, and holds what belongs to no context — the health endpoint, request timing, the hourly cleanup, the migrations entry point. Habit Tracking and Live Updates get the same two projects in their epics.

The build holds the dependency rule (principle IV): the core's project has no `PackageReference`, `FrameworkReference` or `ProjectReference`, so ASP.NET Core or EF Core types do not compile in it. A test reads the core's `.csproj` and fails if any such reference appears, so the rule cannot be dropped by a one-line edit. A second test holds "adapters meet only in the core" (constitution IV), which one adapter project cannot hold by references: the sources under `Identity.Adapters/Http`, `/Storage`, `/Email` and `/Translations` name no type of another of these folders; only the context's registration names them all.

**Rationale.** Two projects per context is the smallest form that lets the compiler hold the rule; folders inside one project would not. Ports with hand-written fakes make the core testable without a database, an email service or a clock.

**Alternatives.** One project per context with folders: the rule is held only by review. Separate `Domain` and `Application` projects per context (onion style): a third project per context with no rule the compiler needs. An architecture-test library (NetArchTest, ArchUnitNET): a dependency doing what project references already do.

## R2. Data access and migrations

**Decision.** EF Core 10 with `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.x in the adapters; one `DbContext` per context, schema `identity`; the host's own `DbContext` for schema `operations`. Migrations are EF Core migrations, shipped as a migration bundle (`efbundle`) built into the image; the deployment runs the bundle after the backup and before the new version starts. Npgsql's connection pooling is used as it comes. In Development and in tests the host applies both contexts' migrations at start (`Database:MigrateOnStart`); in Production it never does, and only the deployment runs the bundles (`migrate-identity`, `migrate-operations`).

**Rationale.** The branch deployment applies migrations to the one database (principle VI): versioned, ordered migrations that a deployment step can run are needed from the first table. EF Core's documentation recommends bundles for automated deployment and warns against migrating at application start in production. EF Core maps the adapter's storage records without hand-written SQL for every query; the core's factories turn them into aggregates (data-model, "Storage").

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

The timeout of 10 seconds is the agent's call. A `500` is treated as unclear because the email may have left. Not used: `Idempotency-Key` — an unclear result is not retried, it counts as sent (FR-004). A connection that fails before the request is sent (connection refused, name not resolved) is refused as unavailable, not unclear: nothing left the server, so the earlier code stays valid.

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

- **Code.** `RandomNumberGenerator.GetInt32(0, 1_000_000)` formatted as 6 digits. Stored as HMAC-SHA-256 over the lower-cased email and the code, keyed by a 32-byte server secret (`Identity__CodeHashKey`, R13); compared in constant time. Production does not start without the key. Development, when no key is configured, generates a random key at each start, so no key is in the repository (constitution VIII); codes issued before a restart of the development host stop matching, which development can afford. A plain hash of a 6-digit code falls to a million guesses; the key keeps a database copy useless without the server's secret, and binding the email means a code presented for another email never matches.
- **One code per email.** `identity.one_time_codes` is keyed by email; storing a new code replaces the earlier one (FR-002). The sequence in "request a code": take the locks, check the limits, send, and only if the send is sent or unclear, store the code and the code request in the same transaction (FR-004).
- **Limits.** Counted from `identity.code_requests` over the last 24 hours, by email and by IP address prefix, from configuration (`Identity__CodeRequestLimits__PerEmail = 5`, `__PerIpAddress = 10`, `__Window = 24:00:00`; FR-003). The check and the insert run in one transaction holding `pg_advisory_xact_lock` on the email and on the prefix, so concurrent requests cannot both pass the last free slot. The send happens inside that transaction; at most one request per email or prefix waits for it, bounded by the 10-second send timeout.
- **Entry attempts.** "Sign in" increments `entry_attempts` with a row lock (`SELECT … FOR UPDATE`), so two concurrent entries cannot both be counted as the fifth.

**Rationale.** The limits count only what the spec counts (FR-004): sent and unclear requests, never refused ones. That is a rule about stored records, not about request rates.

**Alternatives.** ASP.NET Core's rate limiter middleware: in memory, counts every request including refused ones, and cannot report the moment the oldest counted request leaves the window. A serializable transaction instead of advisory locks: retries on conflict to write and test. Storing the code before the send: a refused send would have invalidated the earlier code (FR-002).

## R6. Time zones

**Decision.** The browser sends `Intl.DateTimeFormat().resolvedOptions().timeZone` at sign-in. The core accepts it when `TimeZoneInfo.TryFindSystemTimeZoneById` finds it and the id is an IANA id (`HasIanaId`): on Linux .NET also finds Windows ids, converting them through ICU, which on Linux reads the image's tzdata; otherwise the user gets `UTC` with "not recognized" (FR-009). The runtime image is the default ASP.NET Core 10 image (Ubuntu), never Alpine or Chiseled without `-extra`, never globalization-invariant mode (constraints check, 2.2).

**Alternatives.** NodaTime: a richer time zone library, with no case in this epic; epic 2 may bring one.

## R7. The client's IP address

**Decision.** In production Caddy is the only entry point and sets `X-Forwarded-For` and `X-Forwarded-Proto`. ASP.NET Core's forwarded headers middleware trusts them only from the compose network of the proxy, a fixed subnet in `compose.prod.yaml`. The IP address prefix (data-model) is computed from the resulting remote address. In development and in CI there is no proxy and the remote address is used as is. The trusted network is set through `ForwardedHeadersOptions.KnownIPNetworks` with `System.Net.IPNetwork`: `KnownNetworks` is obsolete in ASP.NET Core 10 (warning `ASPDEPR005`, an error under `TreatWarningsAsErrors`), and the default loopback entries are cleared.

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
- `ops/restore-check.sh` creates a temporary database next to the live one, restores the newest backup into it with `pg_restore`, computes the same manifest from it, compares every table, prints `PASS` or `FAIL` with the comparison and the time from the start of the restore to the passed check, and drops the temporary database. It never connects to the live database. The live restore of R22 uses the same manifest to check the restored database before it is swapped in.
- A CI job runs both scripts against a seeded database: a restore of an untouched backup passes; a manifest altered by the test fails; a backup directory dated 8 days back is deleted.

**Rationale.** `pg_dump` and `pg_restore` come with the PostgreSQL image; the checksum is plain SQL. The manifest is taken from the same snapshot as the dump, so a write between the two cannot make a correct restore fail.

**Alternatives.** pgBackRest or WAL-G: continuous archiving, for an RPO of 24 hours that a daily dump meets. Hetzner's server backups: they hold the whole disk and need a payment option, and they were set aside by the constraints check.

## R11. Frontend

**Decision.** Angular 22 (Node 24 LTS), standalone components, zoneless, signals; plain CSS on the design tokens of `contracts/ui.md`, with the Nordwand Mono font served by the application itself, so no third-party request (FR-027; R24). No component library.

- **Translations: Transloco 8.4** with `@jsverse/transloco-messageformat` 8.4 (ICU MessageFormat, plural categories from CLDR through `Intl.PluralRules`), switching the language in place without a reload (FR-022), as the constraints check (2.1) requires. Translation files: `frontend/public/i18n/<code>.json`, one per language, each with `language.name`. The list of languages comes from `GET /api/locales`, which reads the same files, so a new file is a new language with no code change (FR-020). The backend composes the code email from the `email.*` keys of the same files; those keys use only simple `{placeholder}` substitution, so the backend needs no MessageFormat parser.
- **Language before sign-in:** `navigator.languages` matched against the available codes, else `en`; the switcher's choice in `localStorage` (FR-021); a stored choice that is not among the available locales is ignored, so a removed language or an edited value falls back to the browser's languages. After sign-in the user's locale wins and the switcher calls `PUT /api/me/locale` (FR-023).
- **Content Security Policy** `default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'`, set by the host. A text with links (the acceptance sentence of FR-025) is composed from translation keys around the links; no translation is rendered as HTML (R19, T2). Angular's critical-CSS inlining is turned off, since its `onload` handler would need `'unsafe-inline'` in `script-src`; `'unsafe-inline'` for styles covers the component styles Angular injects and allows no other server.

**Alternatives.** Angular's built-in i18n: compile-time, a reload per language (constraints check 2.1). `@ngx-translate/core` with a MessageFormat compiler: equivalent; Transloco's plugin is maintained by the same project as the library. Angular Material: a component library for four simple pages.

## R12. Tests: tools and commands

**Decision.**

| Layer | Tool | Runs in |
|---|---|---|
| Identity core unit tests, parameterized from the spec's example tables | xUnit v3, `FakeTimeProvider`, hand-written fakes of the ports | fast |
| Adapters and API: EF Core storage, endpoints, cookie, cross-user tests (FR-017), log and metrics tests | xUnit v3, `WebApplicationFactory`, Testcontainers PostgreSQL 18 | fast |
| Frontend units: translation key parity across files (SC-012), plural forms of 1, 2 and 5 in `pl` and `ru`, components | Vitest (the Angular 22 default) | fast |
| End-to-end as a phone at 360 px: sign-in with the code read from the development log, sign out, sign out everywhere with two browser contexts, language and theme switch without reload, keyboard-only flows, no third-party requests (SC-011, SC-013, SC-015, SC-016), cookie expiry in a fresh context (SC-004); the widths 320 px and 1280 px; the main path in WebKit | Playwright 1.64, Chromium and WebKit | full |
| Backup and restore check | `ops/` scripts against compose | full |
| HTTPS only: Caddy with `tls internal` in front of the app; `http://` answers with a redirect and no page (SC-004) | `curl` against compose | full |
| Load test (R14) | k6 | own command, every pull request |

Commands, added to `CLAUDE.md` by the task that creates them:

- **Fast** `scripts/test-fast.sh`: `dotnet test backend/HabitsTracker.slnx` and `npm --prefix frontend test`. Its time is measured once Task 1 creates it and written next to the command in `CLAUDE.md`; Testcontainers needs Docker, which every developer machine and GitHub runner here has.
- **Full** `scripts/test-full.sh`: fast, then `npm --prefix frontend run lint`, the end-to-end tests, and every shell test under `scripts/tests/` and `ops/tests/` (backup and restore, live restore, deletion on request, HTTPS, latency, availability, test integrity, the development lock, the SubagentStop hook).
- **Load** `scripts/load-test.sh`.

No mocking library and no assertion library: fakes implement the core's ports, and xUnit's assertions suffice.

Test harness:

- One PostgreSQL Testcontainer per test assembly and one new database per test class; the fixture is one source file linked into both test projects that need it, so no test project references another.
- API tests set the client's address with a test-only header, read by a startup filter registered only in the test host.
- Tests pass `TestContext.Current.CancellationToken` to every async call: xUnit v3's analyzer `xUnit1051` warns otherwise, and warnings are errors.
- **Contract tests of the ports** (constitution IV): one suite per driven port, one shared source file each, run against the test fake in the core tests and against every adapter in the adapter tests: storage and the transaction against PostgreSQL, the code email sender against the development log and against Resend behind a stubbed HTTP handler, the locales against the translation files. The in-memory transaction fake rolls back what the in-memory stores wrote when the work throws. The concurrency cases run against PostgreSQL only.
- **Playwright projects** (`frontend/playwright.config.ts`): `phone-360`, Chromium, 360 × 740, `isMobile: true`, `hasTouch: true`, `deviceScaleFactor: 2`, runs every end-to-end test; `phone-320`, the same at 320 × 640, runs the width tests (reflow, every action reachable); `desktop-1280`, Chromium, 1280 × 800, not mobile, runs the width tests; `webkit-360`, WebKit with the settings of `phone-360`, runs the tests tagged `@main-path` (sign in with both steps, the main screen, sign out, switch the language and the theme). `isMobile` makes the browser honour `<meta name="viewport">`: a desktop browser with a narrow window ignores it, so a missing or wrong viewport tag would pass at 360 px and still show the page zoomed out on a phone. WebKit is the engine of every browser on an iPhone; on Linux it approximates Safari, it is not Safari. CI installs both browsers with `npx playwright install --with-deps chromium webkit`. Firefox is not tested in epic 1 *(agent's call: no phone requirement names it, and its engine is the third to cost CI time)*. If WebKit refuses the `Secure` session cookie over `http://localhost`, the `webkit-360` project runs against Caddy with `tls internal`, as the HTTPS check does, and the implementer reports it.
- The end-to-end run starts its own `scripts/dev.sh` with the per-IP limit raised to 1000, since every test requests codes from one address; a developer instance must be stopped first.

**Alternatives.** Only unit tests with an in-memory database: unique index, advisory locks and `cidr` would go untested. A shared PostgreSQL started by hand before tests: the fast command would depend on state outside it.

## R13. Delivery: images, workflows, server, secrets

**Decision.**

- **Image.** One multi-stage `Dockerfile`: Node builds the Angular app; the .NET SDK publishes the host and builds the migration bundles (`migrate-identity`, `migrate-operations`); the runtime stage is the default ASP.NET Core 10 image with the app, the static files and the translation files. Pushed to GitHub Container Registry, tagged with the commit SHA (free for public images).
- **Compose.** `compose.yaml` runs `db` (PostgreSQL 18, volume at `/var/lib/postgresql`, the PostgreSQL 18 image's mount point) and `app` (development mode) with one command (constitution, Additional Constraints). `compose.prod.yaml` on the server adds `caddy` (2.11, volumes for `/data` and `/config`, never recreated by a deployment, constraints check 2.4) and runs `app` in production from the GHCR image.
- **Workflows** (FR-034; process document, "Epic 1"):
  - `pr-checks.yml` on `pull_request`: backend, frontend, end-to-end, load test, the shell tests, static analysis, test integrity (R15), the dependency vulnerability checks (R19, P1).
  - `deploy-branch.yml` on `pull_request` `labeled` with `deploy`, only for branches of this repository: calls `deploy.yml`.
  - `deploy-main.yml` on `push` to `main`: calls `deploy.yml`.
  - `deploy.yml` (`workflow_call`, environment `production`): build and push the image; over SSH run `ops/backup.sh`, the migration bundles, `docker compose up -d`; then the availability check once (R16).
  - `availability.yml` (R16).
  - GitHub-hosted runners only, no `pull_request_target` (constraints check 2.5). Every `uses:` is pinned to a commit SHA; workflows default to `permissions: contents: read` (R19, P2).
- **Server (manual).** Docker with compose, a deploy user with key-only SSH, the Hetzner Cloud Firewall open on 22, 80, 443, the journald drop-in, the backup timer, `/opt/habits` with `compose.prod.yaml` and `.env`.
- **Secrets**, in GitHub environment `production` and the server's `.env`, never in the repository (principle VIII): `RESEND_API_KEY`, `IDENTITY_CODE_HASH_KEY`, `POSTGRES_PASSWORD`, `DEPLOY_SSH_KEY`, `DEPLOY_HOST`, `DEPLOY_KNOWN_HOSTS`. The contact email and the developer's full name are page text in the translation files (FR-028), not secrets.
- **The GitHub repository** is created public by the developer before the first workflow runs.

**Alternatives.** Building on the server: the build would compete with the running app for 2 vCPU. nginx with certbot: two tools for what Caddy does alone. A self-hosted runner: unsafe for a public repository.

**Configuration keys** (the environment form replaces `:` with `__`): `ConnectionStrings:Habits`; `Database:MigrateOnStart`; `Identity:CodeHashKey` (base64 of 32 bytes); `Identity:CodeRequestLimits:PerEmail`, `PerIpAddress`, `Window`; `Resend:ApiKey`; `Translations:Path`; `ForwardedHeaders:KnownNetwork` (the proxy network's CIDR, `172.30.0.0/24` in `compose.prod.yaml`).

**Rollback.** `deploy.yml` also runs by hand (`workflow_dispatch`) with the inputs `image_tag` (the SHA of an image already in GHCR) and `migrate` (`false` for a rollback): it takes the backup, starts that image without migrations and checks `/health` once. Expand-only migrations (constitution VI) let the previous image run on the newer schema. If GitHub is unavailable, the fallback is one command over SSH, in `docs/operations/runbook.md`. GitHub runs `workflow_dispatch` only for a workflow on the default branch, so before merge Task 24 rehearses the SSH path, and the manual workflow run is rehearsed once after merge (constitution XI).

**Blue-green and canary deployment: not used.** Blue-green would run the new version next to the old one behind Caddy and switch only after its health check; canary would send it a share of the traffic first. Neither needs a load balancer here, since Caddy can switch upstreams, and the schema already allows two versions at once (constitution VI). Not taken:

- No case under constitution I: no requirement asks for a deployment without downtime. The availability SLO is 99% a month, about 7 hours; a deployment recreates one container in seconds; checks run every 5 minutes.
- Canary needs metrics per version and enough traffic to compare them; about 100 users give neither.
- Blue-green shows an operations practice. This project demonstrates how a product is built with an agent under a stated process, and adding a deployment technique no requirement asks for would break the project's own principle I.
- Revisit when the spec asks for deployments without downtime, or the availability SLO tightens: as a small change or an epic of its own, starting from the requirement.

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

The log line of a code email is a contract between the sender, the end-to-end tests and the load test: `code-email to=<email> locale=<locale> code=<6 digits> subject=<subject>`, then the body. Ports: the host on `5080`, `ng serve` on `4200`, the compose `app` on `8080`, PostgreSQL on `127.0.0.1:5432`.

The development database has no password: `POSTGRES_HOST_AUTH_METHOD: trust`, bound to `127.0.0.1` only, so no credential is in the repository (constitution VIII). The developer's ruling, 2026-10-31. The server's database keeps its password from `.env`.

## R18. Validation rules shared by the core and the pages

**Decision.** The core defines each rule the pages also check (constitution IX): the email pattern, the one-time code pattern and the locale code pattern, as constants of the value objects, matched with `[GeneratedRegex]` (the source generator ships with the .NET SDK). `frontend/src/app/validation/patterns.ts` holds the same literals and the email's maximum length, `EMAIL_MAX_LENGTH = 254`, since the length is part of the rule. Two tests keep them one rule: a backend test reads `patterns.ts` and compares each literal and the length with the core's constants; `testdata/validation-examples.json` lists inputs and whether each is valid, emails of 254 and 255 characters among them, and both the core tests and Vitest run every example. The patterns use only the syntax both engines read alike: ASCII classes such as `[a-z0-9]`, no `\w`, `\s`, Unicode classes or lookbehind, since .NET and JavaScript differ there. A length check runs before the email pattern, so a long input cannot make the engine work hard.

The email pattern is the HTML standard's "valid e-mail address" (WHATWG), the rule a browser applies to `<input type="email">`:

`` ^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$ ``

**Alternatives.** The pattern in a shared file read at run time: the core reads no files. A source generator producing TypeScript from C#: a build tool for three constants. One literal text only: the engines may still disagree, which the shared examples catch.

## R19. Threat model (constitution X)

STRIDE over the epic. Each threat names what closes it and the test that attempts it.

| # | Threat | Closed by | Test |
|---|---|---|---|
| S1 | guessing a code | 5 entry attempts per code, 5 codes per email a day: at most 25 guesses per email a day | Review Focus 2 |
| S2 | a code presented for another email | the code hash binds the email | Review Focus 5 |
| S3 | a stolen session cookie | `HttpOnly`, `Secure`, `SameSite=Lax`, HSTS; a 256-bit token; only its hash stored | cookie tests |
| S4 | session fixation, and a session that outlives a sign-in | sign-in always issues a new token and never adopts one the client sent; a valid session the request carried is revoked | a sign-in with a `session` cookie already set returns a different token, and the old value is not signed in |
| S5 | someone controls the user's inbox | accepted risk, stated in the Terms (FR-031) | — |
| S6 | a spoofed `X-Forwarded-For` | trusted only from the proxy network (R7) | forwarded header test |
| S7 | timing the comparison of a code | constant-time comparison of code hashes (`CryptographicOperations.FixedTimeEquals`, R5) | review |
| T1 | cross-site request forgery | `SameSite=Lax`, and state-changing endpoints accept only `application/json`, which a cross-site page cannot send without a preflight | `text/plain` and form bodies → `415`, nothing changes |
| T2 | cross-site scripting | Angular escapes output; CSP `script-src 'self'`; no translation is rendered as HTML | header test |
| T3 | clickjacking | `frame-ancestors 'none'` | header test |
| T4 | a redirect to another domain after sign-in | no return-address parameter: sign-in always opens `/`, and a signed-in user opening `/sign-in` goes to `/` | Review Focus 4 |
| T5 | SQL injection | EF Core with parameters; the lock statements are parameterized | review |
| R1 | a user denies an action | accepted: no action in this epic needs proof; logs carry `traceId` | — |
| I1 | finding which emails have users | identical answers | Review Focus 3 |
| I2 | another user's data | no endpoint takes a user id | cross-user tests |
| I3 | codes, emails or tokens in logs | R20 | production log test |
| I4 | a copy of the database | codes under a keyed hash, tokens only hashed | hash tests |
| I5 | error details | `500` answers with `internal-error` and `traceId` only | error test |
| I6 | personal answers in a shared cache | `Cache-Control: no-store` on `/api` (R21) | header test |
| I7 | secrets reached from a fork's pull request | deployment runs only for branches of this repository; no `pull_request_target` | workflow review |
| D1 | code emails flooding someone's inbox | 5 per email a day; the rest is an accepted risk of the spec | — |
| D2 | the email service's daily quota used up | accepted risk of the spec | — |
| D3 | a flood of requests to `POST /api/sessions` and `POST /api/code-requests` | **accepted risk** (the developer's ruling, 2026-10-31): requests refused by a limit and wrong codes are not limited by rate; each is one indexed read, and a rate limit in the application on one server only moves the point where 2 vCPU saturate. Visible through the availability check (FR-038); revisit if the SLO suffers, as a requirement of the spec | — |
| D4 | a large request body | bodies under `/api` are limited to 16 KB → `413` with `invalid-request` | body size test |
| D5 | a pattern that makes the regex engine work hard | the length check runs first; generated regexes with a timeout | email examples |
| E1 | gaining rights | there are no roles; the `deploy` label is limited to collaborators | — |
| P1 | a dependency with a known vulnerability | backend: NuGet Audit at restore, part of the SDK, with `NuGetAuditMode=all` and `NuGetAuditLevel=high` in `Directory.Build.props`; a high or critical vulnerability raises NU1903 or NU1904, which `TreatWarningsAsErrors` turns into a failed build, in every build including the fast command (`dotnet list package --vulnerable` is not used: it exits 0 even when it finds one). Frontend: `npm audit --audit-level=high` in the pull request checks. A newly published vulnerability fails the build until the package is updated, also in a pull request that did not touch it; that is intended | the check itself |
| P2 | a changed GitHub Action | every `uses:` is pinned to a commit SHA; workflows default to `permissions: contents: read`, and only the image build gets `packages: write` | workflow review |

## R20. Logs and the error id (constitution X)

**Decision.** Production logs hold only the personal data the Privacy page names, IP addresses (FR-030).

- The application writes time, level, category, message, method and route, status, duration and `traceId`, and exceptions with their stack. It never writes an email, a user id, a session token, a one-time code or a request or response body; `EnableSensitiveDataLogging` stays off outside Development.
- Caddy writes its access log with the client's IP address; credential headers are redacted, its default.
- Every error response carries `traceId`, the W3C trace id of the request, which `AddProblemDetails` adds. The console logger runs with `IncludeScopes: true`, so the same id is on every log line of the request. The page shows the id only for `internal-error`, so a user can send it to the contact email.
- The production log test (SC-005) also checks that no email and no session token reaches the logs, and that a `500` and its log line carry the same `traceId`.

**Alternatives.** No access log at Caddy: fewer personal data, but requests that never reach the application leave no trace. The user id in every log line: easier to search, but it is personal data the Privacy page does not name, a change to the spec.

## R21. HTTP caching

**Decision.** `index.html` and the application fallback: `Cache-Control: no-cache`, so the browser checks with the server each time and a deployment reaches it at once. Build files with a content hash in their name: `public, max-age=31536000, immutable`. Translation files under `/i18n/`, whose names carry no hash: `no-cache`. Every response under `/api` and `/health`: `no-store`. A host test checks each of the four.

**Alternatives.** No explicit headers: after a deployment some browsers keep the previous frontend, and personal answers may be cached. Caching `/api/locales` for an hour: one small request saved, at the cost of a second rule and a new language appearing late.

## R22. Recovery and manual duties (constitution XI)

**Decision.** The procedures live in `docs/operations/runbook.md`, a living document outside the epic's folder.

- **Rolling back a deployment:** `deploy.yml` by hand with the previous image's SHA and `migrate=false` (R13). Rehearsed in Task 24 by the SSH path: deploy the branch, deploy a second commit, roll back to the first, check `/health` and the running image's tag, deploy the newest again; the manual workflow run is rehearsed once after merge (R13).
- **Restoring the live database:** `ops/restore-live.sh <backup directory>` never overwrites in place. It restores into a new database `habits_restored` and checks it against the backup's manifest; only on `PASS` it stops `app`, renames `habits` to `habits_broken_<time>` and `habits_restored` to `habits`, starts `app`, checks `/health` and prints the time; the site answers `502` for the seconds of the swap, and the availability check may record one failed run. The broken database stays for analysis, and swapping back is two statements. It asks for the database name before it changes anything. When a migration did the damage, the runbook rolls the image back first and then restores the backup taken before the migration. A CI test on compose shows that data changed after a backup equals the backup after the restore, and that `habits_broken_*` keeps the changed data. After a restore, every deletion on request answered since the backup was taken is run again, so a restore never brings back data a person was told is deleted (runbook). Rehearsed in Task 24, while the server holds only test data.
- **Deleting a user's data on request** (FR-030, until account deletion in epic 8): the request is accepted only from the address of the account; `ops/delete-user.sh <email>` deletes, in one transaction, the user's sessions, the user, and the one-time code and code requests of that email, asks for the email again before it runs, and prints the rows it deleted. The reply goes from the contact email within a month of the request (GDPR art. 12(3)) and says that backups lose their copy within 7 days and the email service its copy within 30, as the Privacy page states. The script bypasses the core: it is an interim procedure until the account deletion use case. A CI test shows that no row of schema `identity` keeps that email or user id afterwards and that another user's data is untouched.

**Alternatives.** `pg_restore --clean` into the live database: simpler, but it erases the broken state, and a failed restore leaves a half-empty database. Commands in the runbook without scripts: a dozen commands typed by hand during an outage. A host command for deletion through the core: the account deletion use case of epic 8, brought forward without a requirement.

## R23. The domain model in C#

How constitution IX and the error model of `data-model.md` are written in C#; the type model of the task plan follows it.

- Value objects are `sealed record` classes with a private constructor and get-only properties: no positional syntax, `init` or `required`, so neither `with` nor an object initializer bypasses the factory. Factories: `Create(...)` for input, `FromStorage(...)` for loading (not `Restore`, a term of Habit Tracking). Both raise `DomainException`.
- Aggregates are `sealed class`es with `private set` properties, a private constructor and static factories; they do not override `Equals`, and code compares them by id.
- Results are `abstract record`s with `sealed record` cases; a `switch` over a result ends with `_ => throw new UnreachableException()`.
- The storage adapter keeps its own record classes for EF Core and maps them through `FromStorage`; no core type carries a constructor or setter for EF Core.
- `DomainException : Exception` with `DomainError Error`; one `IExceptionHandler` in the adapters turns it into problem details with the codes of the error model.
- Background work runs on `new PeriodicTimer(period, timeProvider)`, so `FakeTimeProvider` drives it.
- The core neither logs nor reads the request context; the acting user and session come in as parameters.

**Alternatives.** `TryParse`-style factories returning a flag: replaced by the developer's rule that an invalid value raises a domain exception. Mapping the domain classes with EF Core directly: the domain would carry constructors for EF Core, and loading would bypass the factories' checks.

## R24. The user interface baseline

Decided when step 5 was reopened for the user interface (process document, step 5 checklist, "User interface"); the result is `contracts/ui.md`.

**Decision.**

- **One baseline, created once.** Task 6 of the task plan creates the tokens (`frontend/src/styles/tokens.css`), the global rules, the page frame, the switchers and the message component; every later page task only uses them. Without it, five page tasks run by separate implementers would each choose their own spacing, buttons and error display, and no single task review would see the drift.
- **Token-only rule, held by a test.** A Vitest test reads every component CSS file under `frontend/src/app/` and fails on a colour literal (`#…`, `rgb(`, `hsl(`, a CSS colour name) or a font size in `px`. It reads files; no dependency.
- **Accessibility as a named subset of WCAG 2.2 AA** (FR-041): keyboard use, visible focus, labels, announced errors, contrast, reflow at 320 px, each with a test (`contracts/ui.md`, "Accessibility checks"). Contrast is computed in a unit test by the WCAG relative luminance formula over the token pairs of both themes; no library.
- **Style: a minimalist neon text terminal**, chosen by the developer. Monospace text, square corners, 1 px borders, a `>` prompt before each heading, a static glow in the dark theme, no motion.
- **Two themes, dark by default** (FR-042), chosen by the developer. `data-theme` on `<html>`, `dark` in `index.html`, so the default does not depend on `prefers-color-scheme`. The choice is kept in `localStorage["theme"]`, the same way as the language before sign-in (R11): a user-chosen interface setting, exempt from consent (legal pages design, "Languages"; WP194, "user-interface customisation"), listed on the Privacy page (FR-030). It is not stored with the user: no column beyond `data-model.md`, no endpoint.
- **Font: Nordwand Mono 1.5.0** (`github.com/tywr/Nordwand-Mono`, SIL Open Font License 1.1, no Reserved Font Name), chosen by the developer. It covers Latin with Polish letters and Cyrillic (`fc-query`: `pl`, `ru`, `en`), and the punctuation of the three translations (`«»`, `„“`, `—`). Only Regular and Bold are used, 37 052 and 36 964 bytes as WOFF2.
- **The font is fetched at build time, not kept in the repository.** `frontend/scripts/fetch-fonts.sh` downloads `https://github.com/tywr/Nordwand-Mono/releases/download/1.5.0/NordwandMono-WOFF2.zip` and `https://raw.githubusercontent.com/tywr/Nordwand-Mono/1.5.0/LICENSE-FONT.txt`, checks each against its pinned SHA-256, and stops the build on a mismatch; it extracts `NordwandMono-Regular.woff2` and `NordwandMono-Bold.woff2` into `frontend/src/assets/fonts/` and the license into `frontend/public/fonts/`, both git-ignored. `npm run build`, `npm start` and the frontend tests run it first, so it runs on a developer machine, in CI and in the image build. `styles.css` refers to the fonts with relative `url()` paths, so the Angular build copies them with a content hash in the name and they get the immutable cache of R21; the license is served at `/fonts/LICENSE-FONT.txt`, as the license asks of a font distributed with software. An end-to-end test checks that the font is applied (`document.fonts.check('16px "Nordwand Mono"')`); SC-013's test still finds no request to another server.

  | File | SHA-256 |
  |---|---|
  | `NordwandMono-WOFF2.zip` (release 1.5.0) | `10fac057fcc90e99b50ad9cced0b3aa9ba97e62cc174f320082b254ef2f79141` |
  | `LICENSE-FONT.txt` (tag 1.5.0) | `e0e493edae714ad5bb243a346f3fd45ecdd94a00584118297f2d05e0f6187d38` |
  | `NordwandMono-Regular.woff2` | `23e1721165a9c770f98afbe202fd92b3ddb022fdaf0393850ea03124fa06ebc3` |
  | `NordwandMono-Bold.woff2` | `77a855a23ecf34bb06dafdbd62dbf8080b0b8a5b80b302d99b06a7e7dfad4223` |

- **Screen states** (`contracts/ui.md`, "Screens and states"), with three decisions of the developer: a button is disabled while its request runs, so a second press sends nothing; the code step offers "Send a new code" and "Use another email", so a mistyped address or a lost email needs no page reload; a session that ends while a page is open brings the sign-in page with "Your session has ended" (spec, Edge Cases). Four more, asked after the baseline: an explanation next to "Remember me", so the consent to the persistent cookie is informed (FR-014); sign out everywhere confirmed on the page (FR-015); the code step at its own route `/sign-in/code`, so the browser's Back returns to the email step, with the email kept in memory, never in the URL; a moment such as `retryAt` shown as an absolute time in the browser's time zone, rounded up to the minute, since a relative time goes stale while the page is open.
- **Widths:** 320 px (reflow), 360 px (FR-019), 1280 px (a desktop).

**Alternatives.**

- Loading the font in the browser from GitHub or a CDN: the release holds only zip archives, which a browser cannot use as a font; a CDN copy of the repository has no built font files; either breaks FR-027 and the content security policy, and sends every visitor's IP address to a third party, which the Privacy page would have to list. Keeping the two WOFF2 files in the repository: no network at build time, at the cost of binary files in the history; the developer chose the build-time fetch. The system monospace stack alone: no file at all, at the cost of the look the developer chose.
- Following `prefers-color-scheme` for the default theme: the developer chose dark by default. Keeping the theme with the user on the server: it would follow the user across devices, at the cost of a column, an endpoint and still a browser value before sign-in.
- The whole WCAG 2.2 AA with `@axe-core/playwright`: a new dependency, and an automated audit finds only part of the failures, so a test could not prove the whole standard.
- Checking the token-only rule by review alone: the rule would hold only as long as each reviewer remembers it (constitution II).

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
| Nordwand Mono 1.5.0 (font, SIL OFL 1.1), fetched at build time | frontend build | the look the developer chose for the interface (R24); nothing functional breaks without it: the fallback is the system monospace stack |
| Playwright 1.64, with its Chromium and WebKit | frontend tests | the end-to-end tests as a phone at 320 and 360 px, on a desktop at 1280 px and in Safari's engine, and the no-third-party check (FR-019, FR-041, SC-011, SC-013, SC-015) |
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
| Nordwand Mono | 1.5.0 (exact: the build checks SHA-256 sums, R24) |
| Playwright | 1.64.1 |
| angular-eslint | 22.6.0 |
| SonarAnalyzer.CSharp / eslint-plugin-sonarjs | 10.35.0.4138 / 4.2.2 |
| xUnit v3 (`xunit.v3`) | 4.0.2 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.14 |
| Testcontainers.PostgreSql | 4.16.0 |
| Microsoft.Extensions.TimeProvider.Testing | 10.11.0 |
| k6 (image `grafana/k6`) | 2.3.0 |
| Caddy (image `caddy`) | 2.11.7 |
