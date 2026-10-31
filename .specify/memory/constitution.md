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
- A port is named for what the core needs, not for how an adapter provides it.
- Domain rules live only in the core. An adapter translates between the outside and the core
  and decides nothing; where a port needs a rule's value (a limit, a lifetime, a cutoff), the
  core passes it.
- A driving adapter calls only use cases: it reaches no driven port directly and changes no
  aggregate outside a use case.
- A use case sets its transaction boundary; an adapter does not.
- An external service is reached through an adapter that translates the service's model into
  the core's, so no term or code of the service enters the core (an anti-corruption layer).
- An adapter uses no other adapter; adapters meet only in the core.
- A storage adapter maps between its own records and the core's types through the core's
  factories; the domain model takes no shape for the storage technology's sake.
- Every driven port has one contract test suite that its adapter and its test fake both pass.
- A driven adapter turns the expected failures of its technology into the port's outcomes; any
  other failure propagates and is answered without detail.
- The core neither logs nor reads the ambient request context; it receives the acting user and
  session as parameters.
- The build holds the dependency rule: the core's project references no framework, database
  driver or adapter project, so code that breaks the rule does not compile.

The concrete projects are chosen in epic 1's `speckit-plan`; a context added later follows the
same structure. A context depends on another only along the context map in
`docs/domain/ubiquitous-language.md`, only through a port, and only with what the map says
crosses the boundary. Live Updates knows no domain rules. A dependency the map does not show is
added to the map first, as a design decision. The Angular frontend is not split this way.

Rationale: the boundaries keep each context's core reviewable on its own, without the cost of
separate services; the ports let a test run the core without a database, an email service or a
real clock, and let development mode replace the email service with a log. A contract suite
shared by an adapter and its fake keeps a green core test meaningful against the real technology,
and adapters that meet only in the core leave no path around a domain rule.

### V. Each user sees only their own data

Every request reads and changes only the signed-in user's data, and every change notification
reaches only that user's connections. Every endpoint and the live-update channel are covered by an
automated test in which user B can neither read nor change user A's data and receives no
notification about it. An epic that adds an endpoint adds its test.

Rationale: a user seeing another user's habit is a product failure (anti-goal 5), and the way
queries are bound to the user is decided in the architecture, before the code.

### VI. Expand-only migrations and API

The branch deployment applies migrations to the one real database before merge. A migration that
has reached the server is never edited; a fix is a new migration. Migrations only expand the
schema: add a column instead of renaming one. Removing a column or table is a separate step in a
later epic, once no code reads it. The database is backed up before migrations are applied.

The HTTP API changes the same way: a field, a value or an endpoint is added, never renamed or
given a new meaning; removing one is a separate step in a later epic, once no client uses it.

Rationale: there is one environment and no staging, so a destructive migration on a branch that
is later fixed or abandoned cannot be taken back; and a deployment replaces the API while browser
tabs keep running the previous frontend.

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

### IX. Tactical domain model

Each context's domain model follows DDD tactical rules, so that a rule written once holds
everywhere.

- Value objects are immutable and compared by value; entities are compared by identity. An
  aggregate changes its state only through its own methods and refers to other aggregates by id.
- Value objects and aggregates hold only valid values. Every way of creating one checks its
  rules, loading it from storage included. An invalid value raises the context's domain
  exception, which names the kind of error and carries no transport detail such as an HTTP
  status; the adapters translate it at the edge.
- Expected outcomes reached with valid values (a wrong code, a limit reached, a refused send)
  are results of the use case, not exceptions.
- A rule that belongs to no single aggregate lives in a domain service: stateless, in the core,
  named in the ubiquitous language. A use case is an application service: it loads aggregates,
  calls them and the domain services, saves the result and sets the transaction; it holds no
  domain rule of its own.
- A transaction changes one aggregate. A use case that must change more than one atomically
  records the exception and its reason in `data-model.md`.
- A repository exists only for an aggregate root and loads and saves the aggregate whole.
  Reading past the aggregates is allowed only through a read model named in `data-model.md`.
- Concurrent changes to one aggregate are resolved by the aggregate's version; where another
  rule applies, `data-model.md` states it.
- State that depends on time is computed from the date, not changed by a timer, unless the spec
  requires otherwise.
- A domain event is named in the past tense, as in the context map, and is raised by an
  aggregate or a domain service. It crosses into another context only along the context map and
  through a port. Each epic's `data-model.md` lists the events the epic raises and when each is
  delivered relative to the transaction that raised it.
- An invariant that no single aggregate can hold (uniqueness across aggregates, a limit under
  concurrent requests) is written in `data-model.md` together with what holds it.
- The domain model knows nothing of storage or serialization: it carries no attributes of
  either, and mapping lives in the adapters.
- A validation rule that the interface also checks (the form of an email, the format of a code)
  is defined once, in the core. The interface uses a copy of it, and a test over shared examples
  shows that both accept and reject the same values.
- Each epic's `data-model.md` states its error model: the domain exception, its kinds of error,
  and which value or invariant raises each.

Rationale: a value that is valid from its creation never has to be checked again, and an
invariant held only by the caller is lost the first time a caller forgets it.

### X. Security by design

- Every epic that touches authentication, sessions or user data records a threat model in its
  `research.md`. Each threat it names is closed by a decision and a negative test that attempts
  it, or is recorded as an accepted risk.
- The pull request checks find dependencies with known vulnerabilities, backend and frontend.
  GitHub Actions are pinned to a commit SHA and run with the least permissions their job needs.
- Logs hold only the personal data the Privacy page names. Every error response carries an id
  that finds its entry in the logs.

Rationale: a threat that is named is tested; one that is not is found by an attacker. A public
repository's workflows and dependencies are part of the attack surface.

### XI. Operability

Every recovery path an SLO relies on (rolling back a deployment, restoring the live database) has
a written procedure, and the procedure has been run at least once. Every manual duty the Privacy
or Terms pages promise (deleting a user's data on request) has a written procedure as well.

Rationale: with one environment and no staging, a procedure first written during an outage is a
procedure written under the worst conditions.

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

**Version**: 1.2.0 | **Ratified**: 2026-10-30 | **Last Amended**: 2026-10-31
