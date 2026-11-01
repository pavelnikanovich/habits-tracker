# habits-tracker

## Where things are

- The process is `docs/superpowers/specs/2026-10-28-development-process-design.md`. Every prompt names a stage and step of it; read only that step and the sections it points to. If a prompt names no step, ask which step the task belongs to before starting.
- `docs/domain/ubiquitous-language.md` is the only source of domain terms. Use its terms as written in specs, code, tests, the API and the ledger; if a concept has no term, ask instead of coining one.
- Only the current epic's artifacts are current. `specs/` folders of earlier epics are history; standing decisions live in the code and in `.specify/memory/constitution.md`.

## Commands

- Fast: `scripts/test-fast.sh` (`dotnet test backend/HabitsTracker.slnx`, `npm --prefix frontend test`), about 12 s on the developer machine (431 tests, 2026-11-01); it needs Docker running, since Testcontainers starts PostgreSQL, and the first frontend build or test needs access to `github.com` for the font. Frontend lint: `npm --prefix frontend run lint`.
- Full: `scripts/test-full.sh` (fast, lint, `npm --prefix frontend run e2e`, every `*.test.sh` under `scripts/tests/` and `ops/tests/`). The end-to-end run starts its own `scripts/dev.sh` (database, host on 5080, `ng serve` on 4200, log `.dev/app.log`) and Caddy on 8443 for WebKit (image `caddy:2.11`, pulled once), so stop yours first. Browsers once: `npx --prefix frontend playwright install --with-deps chromium webkit`.

## Session start

Read the last 10 commits and the current epic's artifacts in `specs/NNN-<epic>/`, then run the fast test command. If it is red, report the failing tests before any new work. In a developer-owned task, red tests the developer is writing are expected: report them, do not fix them.

## Task statement

Before acting, restate the task in your own words (outcome, files, check command) and wait for the developer's OK. If the same misunderstanding has been corrected twice in one session, do not attempt a third correction: write the task as one consolidated statement for the developer to confirm, and the work continues in a fresh session.

## Evidence

Quote evidence verbatim, as you received it: the command and the tail of its output, regardless of the output style. RTK compresses shell output but keeps failing tests and summary lines; when in doubt, rerun with `rtk proxy <command>` for the raw output.

## Boundaries

Never:
- Do not weaken, skip or delete a test to make a run green: tests encode the spec, so a failing test means the code is wrong. Fix the code, or report BLOCKED if the test contradicts the spec. Applies to backend and frontend tests.
- When executing a task plan, do not edit speckit artifacts (`spec.md`, `plan.md`, `data-model.md`, `contracts/`, `research.md`, the constitution). If one looks wrong, stop and report where and why.

Ask first, because each changes something the developer approved:
- a new dependency (package, service or tool): propose it with the function that breaks without it;
- a database schema change beyond `data-model.md`: propose the change to `data-model.md` first;
- a file outside the task's boundaries: name the file and the reason;
- a divergence from the spec: report it instead of choosing a reading.

In subagent-driven execution the implementer asks the controller, which rules and records the ruling in the SDD progress ledger. A new dependency, a schema change and a spec divergence still stop execution and go to the developer.

Always:
- TDD, backend and frontend: a failing test first, then the code.
- Before reporting a task done, run the fast test command and quote it with its fresh output.
- Copy values from the spec and `data-model.md` (limits, lengths, lifetimes, enumerations) verbatim.
- Commit structural changes (renames, moves, extractions) separately from behavioral ones, so each diff carries one kind of risk.

Secrets, force pushes and pushes to `main` are blocked by deny rules in `.claude/settings.json`.

## Developer-owned code

The domain model, habit and streak logic and OTP authentication are written by the developer. There, propose code in chat and let the developer write it or accept it line by line. Bugs there go through `superpowers:systematic-debugging`: propose hypotheses and how to test them; the developer writes or accepts the fix.

## Ledgers

`docs/ledger.md` is the project ledger. It is written only in stage 1 commits, in the ledger commit (step 13) and in a small change's ledger line; one-line records that arise earlier (deviations, spikes) are collected and written at step 13. The SDD progress ledger lives in `.superpowers/sdd/` and is not committed.

## Language

Repository artifacts are in English: documents, specs, plans, the ledger, commits, code comments. The developer writes in Russian; answer in Russian and translate an artifact into Russian only on request.

## Rules in this file

Write rules calmly, with the reason; name what to do instead of what is forbidden; use concrete thresholds. Keep the file under about 60 lines: before adding a line, check whether a hook or a deny rule can hold it, and what can go.
