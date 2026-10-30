# habits-tracker

A web habit tracker for one person on several devices at once, built to show agentic development: how a developer leads an AI agent from product goals to a working application on their own server.

## What is different

The design started from eight existing habit trackers (Loop, HabitKit, Everyday, Streaks, Habitify, Habitica, TickTick, Way of Life). Two things were not found among them:

- **Instant multi-device sync with documented conflict rules.** A change on one device appears on every open device within 2 seconds, and concurrent edits converge to one state by fixed rules.
- **Trust or strictness, chosen per habit.** A Routine is kept on trust: past days can be fixed. A Commitment is kept strictly: only yesterday can be fixed, and leaving a Commitment takes 7 days and never rewrites the past.

## Status

Design approved; implementation not started. Eight epics are planned, the first being a walking skeleton that reaches the server with one-time-code sign-in. Target: `habits.cardlane.eu`.

Stack: C# / .NET 10 with ASP.NET Core, Angular, PostgreSQL, docker compose on a Linux VPS.

## Where to read

- `docs/superpowers/specs/2026-10-28-product-design.md` — goals, non-goals, requirements, SLOs, epics.
- `docs/domain/ubiquitous-language.md` — bounded contexts and the domain terms used everywhere: specs, code, tests.
- `docs/superpowers/specs/2026-10-28-development-process-design.md` — how the agent is led: stages, human reviews, task plans, agent boundaries.
- `docs/ledger.md` — for every stage, what the agent did, what the developer decided, and what it cost in tokens.
