# Implementation Plan: Walking skeleton

**Branch**: `001-walking-skeleton` | **Date**: 2026-10-30 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-walking-skeleton/spec.md`; constitution 1.1.0; `docs/superpowers/specs/2026-10-30-external-constraints-check.md`, "For `speckit-plan`"; the email service and legal pages designs of 2026-10-30.

## Summary

An empty application all the way to `habits.cardlane.eu`: one-time code sign-in with server-side sessions, sign out and sign out everywhere, three interface languages switched without a reload, the Privacy and Terms pages, the "no habits yet" main screen, and the operations that measure the epic's SLOs — daily and pre-migration backups with a checked restore, request timing metrics, an external availability check, and a load test on every pull request.

Technically: an ASP.NET Core 10 modular monolith whose only context in this epic, Identity, is a hexagonal package — `Identity.Core` (domain model, use cases, ports; no framework) and `Identity.Adapters` (HTTP, EF Core on PostgreSQL 18, Resend, the development log, translation files) — composed by `Host`, which also serves the Angular 22 single-page application from the same origin. Delivered as one container image through GitHub Actions to the VPS, behind Caddy for HTTPS. Decisions and their alternatives are in [research.md](research.md); records in [data-model.md](data-model.md); the API in [contracts/http-api.md](contracts/http-api.md); how to run and check it in [quickstart.md](quickstart.md).

## Technical Context

**Language/Version**: C# on .NET 10 (SDK 10.0.403, runtime 10.0.14, LTS); TypeScript 6.0 with Angular 22.2 on Node.js 24 LTS.

**Primary Dependencies**: ASP.NET Core 10; EF Core 10 with the Npgsql provider 10.0; Angular 22 with Transloco 8.4 and its MessageFormat plugin; Caddy 2.11 on the server; Resend's HTTP API in production; the duplication rules of `SonarAnalyzer.CSharp` and `eslint-plugin-sonarjs` in the build. Cases for each: research.md, "Dependencies and their cases".

**Storage**: PostgreSQL 18.6 in docker compose, one database; schema `identity` (Identity package) and schema `operations` (host, no domain data); data on a named volume that survives restarts; daily and pre-migration `pg_dump` backups on the server, kept 7 days (research R10).

**Testing**: xUnit v3, `WebApplicationFactory`, Testcontainers PostgreSQL, `FakeTimeProvider` (backend); Vitest (frontend units); Playwright with Chromium at 360 px (end-to-end); k6 (load); shell checks for backup/restore and HTTPS. Commands: `scripts/test-fast.sh`, `scripts/test-full.sh`, `scripts/load-test.sh` (research R12).

**Target Platform**: Linux containers on the developer's Hetzner Cloud CX23 (2 vCPU, 4 GB, Ubuntu 24.04; nothing else runs on it); browsers: current Chromium, Firefox and Safari, phone width from 360 px.

**Project Type**: web application — ASP.NET Core API and host, Angular single-page application, one origin.

**Performance Goals**: 95% of requests within 300 ms server side under 100 users within 2 vCPU and 4 GB (SC-006, SC-008); code email within 1 minute (SC-001); sign-in within 2 minutes (SC-001).

**Constraints**: HTTPS only (FR-016); nothing loaded from other servers (FR-027); one-time codes never in production logs (FR-011); expand-only migrations (constitution VI); no secrets in the repository (constitution VIII); one environment, the server holds one branch at a time.

**Scale/Scope**: about 100 users (NFR-11); 4 pages (sign-in, main, Privacy, Terms); 8 API endpoints; 1 bounded context; 3 languages.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Checked against constitution 1.1.0 before research and again after the design below; both passes give the same result.

| Principle | Result | How the plan meets it |
|---|---|---|
| I. Minimum technology | pass | Every dependency outside ASP.NET Core, Angular, PostgreSQL and docker compose has its case in research.md, "Dependencies and their cases"; rejected alternatives are listed there. No microservice, broker or cache. The developer approved each one at the technical plan review. |
| II. Tests are mandatory | pass | Test-first for backend and frontend (research R12). Every spec value (6 digits, 10 minutes, 5 attempts, 5 per email, 10 per IP prefix, 24 hours, /64, 30 days, 7 days, 300 ms, 95%, 100 users, 360 px) is asserted by a test named in the task plan. Every NFR and SLO of the epic has an automated test or a measurement: table "SLO and NFR checks" below. |
| III. One ubiquitous language | pass | Entities in data-model.md are Identity terms: User, One-time code, Code request, Session (with Remembered session), Email, Time zone, Locale; Visitor has no stored state. Technical value objects (IP address prefix, Code hash, Session token, One-time code value, Code request limits) and the operational table `request_timings` are not domain terms; see "Review decisions", 1. |
| IV. Bounded contexts as hexagonal packages | pass | Identity is `Identity.Core` and `Identity.Adapters`; the core project has no references, guarded by a test (research R1). Aggregates, entities, value objects and invariants are marked in data-model.md. No cross-context dependency in this epic. The Angular frontend is not split. |
| V. Each user sees only their own data | pass | No endpoint takes a user id; every signed-in endpoint acts on the session's user; every endpoint has a cross-user test listed in contracts/http-api.md (FR-017, SC-003). Live updates are absent until epic 3. |
| VI. Expand-only migrations | pass | The first migration creates schemas and tables; the deployment backs up, then runs the migration bundle (research R2, R10, R13). |
| VII. Only the developer's work in the repository | pass | The Resend spike was a `curl` from the server, outside the repository; its result is in research R3. |
| VIII. No secrets in the repository | pass | Secrets are named in research R13 and live in the GitHub environment `production` and the server's `.env`; codes are never logged in production (research R8). |
| Development Workflow | pass | The developer approved the dependencies of research.md at the technical plan review on 2026-10-31; the schema is data-model.md. |

No violation; Complexity Tracking stays empty.

## SLO and NFR checks

| Requirement | Automated check (before merge) | Measurement (server) |
|---|---|---|
| NFR-5, SLO Data loss, SLO Recovery (FR-035, FR-036) | CI job: backup, restore check passes; altered manifest fails; 8-day-old backup deleted | daily timer; `ops/restore-check.sh` once, time to the ledger |
| NFR-6 (FR-017) | cross-user API test per endpoint | — |
| NFR-7 (FR-002–FR-005) | parameterized core tests with `FakeTimeProvider`; API tests of the refusals | — |
| NFR-8 (FR-006) | — | Resend spike (R3); developer's real sign-in at step 12 (SC-001) |
| NFR-9 (FR-012–FR-016) | API tests of cookie attributes, expiry, renewal, revocation; Playwright fresh-context test; HTTPS check against Caddy `tls internal` | `curl` of `http://` and `https://` after deployment |
| NFR-10 (FR-011, FR-032, FR-033) | production-mode log capture test; cleanup tests with `FakeTimeProvider`; journald config test | journal retention read at step 12 |
| NFR-11, SLO API latency (FR-039, FR-040) | k6 load test in every pull request; metrics test | `ops/latency-p95.sh` after deployment, to the ledger |
| NFR-13 (FR-019) | Playwright at 360 px on all four pages | — |
| SLO Availability (FR-037, FR-038) | health endpoint API test (database up and down) | one check by the branch deployment; schedule after merge |

## Division of work

Process document, "Division of work"; each task in `tasks-plan.md` names its executor.

- **developer**: `HabitsTracker.Identity.Core` — the domain model, the code request policy, one-time code issue and verification, sessions, the use cases (OTP authentication and the domain model are developer-owned). The agent writes the code in the main session; the developer accepts it line by line.
- **subagent**: the adapters, the host, the Angular application, the translation files (Polish and Russian drafted by the agent, reviewed by the developer), the Privacy and Terms page texts within FR-030 and FR-031, `docs/legal/record-of-processing.md`, the test scripts, `.claude` hooks and rules.
- **manual** (main session with the developer): the GitHub repository, the server, DNS, Resend, secrets, the workflow files, the backup timer, the first deployment.

## Order of work

The task plan (step 7) refines this; listed here because the foundation decides what the intermediate review covers.

1. **Foundation**:
   - the solution and project skeleton with the reference guard test;
   - `compose.yaml` and the database;
   - the Angular skeleton with Transloco and the three files;
   - `scripts/dev.sh` and `scripts/test-fast.sh`;
   - the `CLAUDE.md` commands and the SubagentStop hook;
   - `pr-checks.yml` with static analysis and test integrity;
   - the public GitHub repository.
2. **Sign-in vertical slices**, each from core to page: request a code; sign in; current user and main screen; sign out; sign out everywhere; locale change.
3. **Pages**: Privacy, Terms, the record of processing.
4. **Operations**: health, metrics, cleanup, backup and restore scripts, load test.
5. **Delivery**: image, server, deployment workflows, availability check, the first branch deployment.

## Project Structure

### Documentation (this feature)

```text
specs/001-walking-skeleton/
├── spec.md
├── plan.md              # this file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   └── http-api.md      # Phase 1
├── checklists/
│   └── requirements.md
└── tasks-plan.md        # step 7, superpowers:writing-plans (not created here)
```

### Source Code (repository root)

```text
backend/
├── HabitsTracker.slnx
├── Directory.Build.props          # warnings as errors, analyzers, CA1502, nullable
├── .globalconfig                  # Sonar: only S4144 and S1192 on
├── Directory.Packages.props       # central package versions
├── .config/dotnet-tools.json      # dotnet-ef
├── src/
│   ├── HabitsTracker.Identity.Core/       # domain model, policy, use cases, ports; no references
│   ├── HabitsTracker.Identity.Adapters/   # HTTP endpoints, EF Core + migrations, Resend, dev log, translations, cookie
│   └── HabitsTracker.Host/                # composition root, SPA hosting, health, metrics, cleanup, migrations bundle entry
└── tests/
    ├── HabitsTracker.Identity.Core.Tests/       # unit, parameterized; reference guard test
    ├── HabitsTracker.Identity.Adapters.Tests/   # storage against Testcontainers PostgreSQL
    └── HabitsTracker.Host.Tests/                # API, cross-user, cookie, logs, metrics, health

frontend/
├── src/app/
│   ├── sign-in/  main/  privacy/  terms/        # the four pages
│   ├── i18n/                                    # Transloco setup, language detection and switcher
│   └── session/                                 # current user, sign out, route guard
├── public/i18n/{en,pl,ru}.json                  # the only translation files, backend included
├── e2e/                                         # Playwright, 360 px
└── eslint.config.js                             # complexity 20; sonarjs: only the two duplication rules

load/
├── main-screen.js                               # k6 scenario
└── compose.loadtest.yaml                        # limits and raised per-IP limit

ops/
├── Caddyfile
├── compose.prod.yaml
├── backup.sh  restore-check.sh  latency-p95.sh  availability-month.sh
├── journald/habits.conf
└── systemd/habits-backup.service  habits-backup.timer

scripts/
├── dev.sh  test-fast.sh  test-full.sh  load-test.sh  test-integrity.sh

.github/workflows/
├── pr-checks.yml  deploy-branch.yml  deploy-main.yml  deploy.yml  availability.yml

.claude/
├── hooks/subagent-stop-tests.sh                 # process document, "Epic 1"
└── rules/backend.md  frontend.md                # with paths: filters

docs/legal/record-of-processing.md               # FR-029
Dockerfile
compose.yaml
```

**Structure Decision**: a web application with `backend/` and `frontend/` side by side, served as one image from one origin. The backend's projects follow constitution IV: two per bounded context plus one host; Habit Tracking and Live Updates add theirs in epics 2 and 3. Operations scripts live in `ops/` because they run on the server, and developer scripts in `scripts/` because they run on a developer machine or CI runner.

## Review decisions

Places the plan settled without a word from the spec; the developer decided each at the technical plan review on 2026-10-31.

1. **Value objects outside the glossary.** IP address prefix, Code hash, Session token, One-time code value, Code request limits are technical value objects, and `operations.request_timings` is an operational table; none is added to `docs/domain/ubiquitous-language.md`, and data-model.md defines their names. Step 8's check covers entities, which are all terms.
2. **Load test "main screen".** One `GET /` and one `GET /api/me` per opening (research R14).
3. **Split resource limits** in the load test, 1 vCPU and 2 GB each for the app and the database, as a stricter reading of "limited together" (research R14). If they fail where a shared pool would pass, a shared cgroup slice replaces them in a task of its own.
4. **Duplication rules** from `SonarAnalyzer.CSharp` and `eslint-plugin-sonarjs`, only those rules on, as process step 11 asks (research R15).
5. **A `500` from Resend counts as an unclear result**, so it invalidates the earlier code and counts against the limits (research R3).
6. **TLS to the recipient's mail server** stays Resend's default, "Opportunistic" (research R3).
7. **The agent's calls in data-model.md** stand as written.
8. **Developer-owned code is written by the agent** in the main session and accepted by the developer line by line; agent-owned code runs through subagents as the process document says ("Division of work").

## Complexity Tracking

No violations of the constitution.
