# habits-tracker Constitution

## Core Principles

### I. Minimum technology

A library, service or tool enters the project only with a case: a named function of this product
that breaks without it. The case is written in the epic's `research.md` when the technology is
chosen. Without a case, use what the stack already gives: ASP.NET Core, Angular, PostgreSQL and
docker compose. Microservices, a message broker or a cache are added only on that same condition.

Rationale: every added technology is code to configure, secure and review, and a reviewer reads
restraint as maturity.

### II. Tests are mandatory

Every behavior change, backend and frontend, is written test first: a failing test, then the code
that makes it pass. Every value from the spec and `data-model.md` (limits, lengths, lifetimes,
enumerations) is asserted by a test. Algorithms are specified by example tables and tested as
parameterized tests. Every NFR and SLO is checked by an automated test or a measurement, not only
declared. Do not weaken, skip or delete a test to make a run green: tests encode the spec, so a
failing test means the code is wrong. Fix the code, or report the conflict if the test
contradicts the spec.

Rationale: the agent writes most of the code; a test that was run is the only evidence that the
code does what the spec says.

### III. One ubiquitous language

`docs/domain/ubiquitous-language.md` is the only source of domain terms. A term is spelled the same
in specs, class names, tests, the API and the ledger; one concept has one term, without synonyms.
Every entity in an epic's `data-model.md` is a term in that file. An epic that introduces or
changes a term edits the file in its own branch, and renaming a term is a refactoring everywhere.

Rationale: two words for one concept, or one word for two, is how the domain rules drift apart
between the spec and the code.

### IV. Bounded contexts as hexagonal packages

The application is a modular monolith. Each bounded context (Identity, Habit Tracking,
Live Updates) is a package of its own, not a service. On the backend, each package is
hexagonal: a core and its adapters.

- The core holds the context's domain model and its ports: the interfaces through which the
  core is used (its use cases) and through which it reaches the outside (storage, email, time,
  other contexts). The core depends on no framework, database or external service.
- The domain model in the core is marked in DDD terms: each epic's `data-model.md` names its
  aggregates, entities and value objects, and the invariants each aggregate guards.
- Adapters connect the ports to the outside: the HTTP API, the database, the email service,
  the development log. An adapter depends on the core, never the other way round.
- The build holds the dependency rule: the core's project references no framework, database
  driver or adapter project, so code that breaks the rule does not compile.

The concrete projects are chosen in epic 1's `speckit-plan`; a context added later follows the
same structure. A context depends on another only along the context map in
`docs/domain/ubiquitous-language.md`, only through a port, and only with what the map says
crosses the boundary. Live Updates knows no domain rules. A dependency the map does not show is
added to the map first, as a design decision. The Angular frontend is not split this way.

Rationale: the boundaries keep each context's core reviewable on its own, without the cost of
separate services; the ports let a test run the core without a database, an email service or a
real clock, and let development mode replace the email service with a log.

### V. Each user sees only their own data

Every request reads and changes only the signed-in user's data, and every change notification
reaches only that user's connections. Every endpoint and the live-update channel are covered by an
automated test in which user B can neither read nor change user A's data and receives no
notification about it. An epic that adds an endpoint adds its test.

Rationale: a user seeing another user's habit is a product failure (anti-goal 5), and the way
queries are bound to the user is decided in the architecture, before the code.

### VI. Expand-only migrations

The branch deployment applies migrations to the one real database before merge. A migration that
has reached the server is never edited; a fix is a new migration. Migrations only expand the
schema: add a column instead of renaming one. Removing a column or table is a separate step in a
later epic, once no code reads it. The database is backed up before migrations are applied.

Rationale: there is one environment and no staging, so a destructive migration on a branch that
is later fixed or abandoned cannot be taken back.

### VII. Only the developer's work in the repository

The repository holds development artifacts: specs, plans, code, tests, CI, agent configuration and
the ledger. Learning notes, experiments and rejected options stay outside it. Spike code is
discarded and never merged; what it taught goes into `research.md`. The ledger records accepted
decisions only.

Rationale: the repository is read by an employer's reviewer, who looks at decisions and their
result, not at how they were reached.

### VIII. No secrets in the repository

Secrets live only in GitHub secrets and on the server: no keys, passwords, tokens or connection
strings with credentials in the repository, in CI logs or in application logs. Local secrets are
kept in git-ignored files. In production, one-time codes never appear in logs. Configuration
names its secrets; their values are supplied at deployment.

Rationale: the repository is public, and a committed secret stays in its history.

## Additional Constraints

Taken from the product design (`docs/superpowers/specs/2026-10-28-product-design.md`,
Constraints); this section is their current source.

- Backend: C# on .NET 10 (LTS), ASP.NET Core.
- Frontend: Angular, a single-page application.
- Database: PostgreSQL.
- The whole application starts with one command through docker compose; data survives restarts.
- Deployment target: the developer's Hetzner VPS running Linux, at `habits.cardlane.eu`,
  HTTPS only.
- One environment, no staging.
- Public GitHub repository.
- Email: the one-time code is sent through a transactional email service from `mail.cardlane.eu`,
  with its own SPF, DKIM and DMARC records. The records of `cardlane.eu` itself (SPF `-all`,
  DMARC `p=reject` with strict alignment) stay unchanged.

## Development Workflow

Three kinds of change need the developer's decision before they are made, because each changes
something the developer approved:

- a new dependency (a package, service or tool): it changes the approved stack. Propose it with
  its case (principle I) and wait for the answer.
- a database schema change beyond the epic's `data-model.md`: it becomes irreversible once
  deployed (principle VI). Propose the change to `data-model.md` first.
- a divergence from the spec: the spec is the developer's decision. Stop and report the
  divergence instead of choosing a reading of the spec.

`speckit-plan` checks every technical plan against this constitution in its Constitution Check. A
plan that departs from a principle records the departure and its case under Complexity Tracking;
the developer accepts or rejects it at the technical plan review.

## Governance

This constitution governs the product and its code. The way of working (stages, reviews, the
ledger) is governed by `docs/superpowers/specs/2026-10-28-development-process-design.md`, and the
agent's session rules by `CLAUDE.md`. Where a spec or plan conflicts with this constitution, the
constitution holds until it is amended.

Amendments are the developer's decision. They are made with `speckit-constitution`, in a commit
of their own, on the epic branch that needs them or as a small change, and are recorded in one
ledger line.

Versioning follows semantic versioning: MAJOR for removing or redefining a principle, MINOR for
adding a principle or section or materially expanding one, PATCH for wording and clarifications.

Compliance is checked at the Constitution Check of every `speckit-plan`, at the developer's review
of the technical plan, and at the final branch review of every epic.

**Version**: 1.1.0 | **Ratified**: 2026-10-30 | **Last Amended**: 2026-10-30
