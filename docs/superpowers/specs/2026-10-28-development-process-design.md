# habits-tracker development process

Status: approved by the developer. Date: 2026-10-28, revised 2026-10-30.

## Why

habits-tracker is a test habit-tracker project. Its purpose is to show a prospective employer command of agentic development: how a developer leads an agent from product goals to a working application on their own server.

End state: the application is deployed to the developer's VPS, reachable on a subdomain of `cardlane.eu`, and usable after signing in with a one-time password (OTP).

This document describes the way of working, not the product. Product goals, non-goals and requirements are worked out in stage 1.

## Process goals

- Take the project from goals to a working VPS deployment with two skill sets: speckit (what we build) and superpowers (how the code gets written).
- Every stage leaves a checkable artifact in the repository.
- The ledger shows a reviewer, for every stage, what the agent did and what the human decided.
- The main risks — server deployment and authentication — are retired by the first epic, not the last.
- Critical code (domain core, authentication) is written by the developer, not only reviewed.

## Non-goals

- No learning layer in the repository. How the developer learned stays out; the repository holds only the developer's work.
- No other orchestrators (GSD and the like).
- No rejected options in the ledger: it records accepted decisions only.

## Anti-goals

What makes the process a failure even if the application works:

- two sources of tasks or two executors of one plan — task lists drift apart;
- a plan that rewrites the program instead of recording decisions;
- first contact with the server at the end of the project;
- "done" without a check that was actually run;
- requirements handed to the agent piece by piece instead of one full statement.

## Language

All repository artifacts are in English: documents, specs, plans, the ledger, commits, code comments. The one intended exception is the "Russian word" column of the ubiquitous-language glossary. The developer writes to the agent in Russian; the agent writes artifacts in English and translates them into Russian on request.

## Tools

| Tool | Role | Where installed |
|---|---|---|
| superpowers 7.0.0 | brainstorming, writing-plans, execution, TDD, debugging, review | Claude Code plugin, global |
| speckit (`specify` CLI 1.0.7) | constitution, specify, clarify, plan | `.claude/skills/speckit-*`, `.specify/` in the project |
| RTK 0.43.0 | compresses shell command output before it reaches the agent's context | global Claude Code hook |
| caveman | terse style for the agent's status messages; overridden for evidence and in developer-owned sessions | Claude Code plugin, global |
| `ledger-tokens` | token budget and session time per stage for the ledger | local script, outside the repository |
| GitHub, GitHub Actions | repository, CI/CD on a Linux runner | — |
| Hetzner VPS, domain `cardlane.eu` | deployment target | developer's own |

speckit skills **removed from the project**: `speckit-tasks`, `speckit-implement`, `speckit-analyze`, `speckit-converge`, `speckit-taskstoissues`. One reason: the task source is the `writing-plans` plan and the executor is superpowers. These skills operate on `tasks.md`, which this project does not have, and while installed they compete with superpowers for the same cycle. `speckit-checklist` is removed too: no step of the process uses it. If `speckit-tasks` is tried before epic 2, `specify init` restores it.

## Agent configuration

- **`CLAUDE.md`** at the repository root, short. Contents: build and test commands — a fast test command for the task loop (unit tests and the touched area, target about 2 minutes) and a full one (database integration, end-to-end) for step 10 and CI — decisions the agent cannot derive from the code, rules the developer had to repeat. The agent drafts it; the developer edits it for meaning before it is committed. It grows from cases: a correction the developer has to give twice becomes a line in `CLAUDE.md` or a hook. A rule that can be checked automatically lives in a hook or deny rule, not in `CLAUDE.md`: a hook costs the model no attention. `CLAUDE.md` stays under about 60 lines; every addition asks "can a hook do this?" and "what can go?". One line states that evidence (a command and the tail of its output) is quoted verbatim, as the agent received it, regardless of any output style; RTK keeps failing tests and summary lines, and `rtk proxy <command>` gives the raw output when in doubt. Once the stack is confirmed, backend and frontend rules go into `.claude/rules/*.md` with a `paths:` filter instead of the root file. One line points to `docs/domain/ubiquitous-language.md` as the only source of domain terms. One line sets the session start: read the recent commits and the current epic's artifacts, run the fast test command; if it is red, report it before any new work (red tests the developer is writing in a developer-owned task are expected). One line separates the two ledgers: `docs/ledger.md` is the project ledger, written only in stage 1 commits, the ledger commit (step 13) and a small change's ledger line — one-line records that arise earlier (deviations, spikes) are collected and written at step 13; the SDD progress ledger lives in `.superpowers/sdd/`. One line states that only the current epic's artifacts are current: `specs/` folders of earlier epics are history, and standing decisions live in the code and the constitution.
- **Step prompts.** The developer starts each step with the skill the step names (`/speckit-specify`, `/superpowers:writing-plans` and so on) and a prompt that names the stage and step of this document; the session reads only that step and the sections it points to. The developer sets the `ledger-tokens` mark at the start of each stage. `CLAUDE.md` carries one line pointing to this document: a prompt names a stage and step; if it does not, the agent asks which step the task belongs to.
- **Deny rules** in the committed `.claude/settings.json`: the agent may not read `.env` files and other secrets, run `git push --force`, or push to `main`.
- **Branch protection** on GitHub: `main` accepts changes only through a pull request with green required checks.

Agent boundaries, each placed in the strongest layer that can hold it:

| Level | Rules | Layer |
|---|---|---|
| Never | read secrets, push to `main`, `git push --force`; executor edits speckit artifacts; disable or weaken a failing test to get a green run | deny rules, hooks, branch protection where enforceable; otherwise `CLAUDE.md` |
| Ask first | new dependency; database schema or files outside the task; any divergence from the spec | `CLAUDE.md`, constitution |
| Always | TDD; run the fast test command before reporting and quote the command with its fresh output; spec values copied verbatim; structural and behavioral changes in separate commits | `CLAUDE.md`, task plan Global Constraints |

Task-specific limits ("do not touch task 3's files", "plain-text email") go into the task text.

Rules in `CLAUDE.md`, the constitution and `.claude/rules/` are written calmly, with the reason, without capitals or emphasis. Each rule states its breadth ("backend and frontend tests"), each prohibition names what to do instead, and thresholds are concrete ("changes to behavior, security or spec conformance; skip naming and formatting"), not qualitative ("only what matters"). A rule that gets ignored means the file is too long or the rule belongs in a hook — not a louder tone. Example: "Do not weaken, skip or delete a test to make a run green: tests encode the spec, so a failing test means the code is wrong. Fix the code, or report BLOCKED if the test contradicts the spec. Applies to backend and frontend tests."

Anti-patterns of working with the agent, and what in this process counters each:

| Anti-pattern | Countermeasure |
|---|---|
| Requirements handed over piece by piece | one full task statement; consolidated restart instead of a third correction |
| Instruction overload, generated or bloated context file | `CLAUDE.md` under ~60 lines, edited by the developer; hooks instead of lines; path-scoped rules |
| No runnable "done" | "Done" as a command in every statement; SubagentStop hook; evidence quoted verbatim |
| Contradicting instructions | one task source; unused speckit skills removed; prompt audit; only the current epic's artifacts are current |
| Emphasis instead of reasons (capitals, "be thorough", "don't be lazy") | calm rules with the reason; effort level as the lever |
| Qualitative thresholds | concrete thresholds |
| Prohibition without an alternative | every prohibition names what to do instead |
| Tests bent to pass | Never rule; test-integrity check in CI |
| Rubber-stamp review | review digest; plan size guide; mechanical checks |

During subagent-driven execution the controller does not wait on the developer: "Ask first" goes from the implementer to the controller, which decides and records a ruling in the SDD progress ledger. Three exceptions stop execution and go to the developer: a new dependency (it changes the stack approved at step 6); a database schema change beyond `data-model.md` (it becomes irreversible once deployed at step 12); a divergence from the spec (the spec is the developer's decision).

## Where the rules live

No agent reads this document whole; it is the source from which the rules are laid out into layers, each read by whoever needs it.

| Rules | Layer | Read by |
|---|---|---|
| Product and code principles: minimum technology, mandatory tests, the ubiquitous language as the only source of domain terms, contexts as packages, expand-only migrations, only the developer's work in the repository, no secrets | `.specify/memory/constitution.md` | `speckit-specify`, `speckit-clarify`, `speckit-plan` (Constitution Check gate) |
| Agent rules: session start, evidence verbatim, the two ledgers, current epic only, rule style | `CLAUDE.md`, `.claude/rules/` | every session |
| Automatically checkable prohibitions | deny rules, hooks, CI, branch protection | nobody — they fire |
| One step of the process: what to do, where the developer decides, the ledger mark, the next step | this document; the step prompt names the step | the session running that step |
| Task plan format | this document; the step 7 prompt points to it | `writing-plans` |
| Order of steps, human reviews, ledger | this document | the developer and the reviewer |

## Division of work

- **Developer-owned code:** domain model, habit and streak logic, OTP authentication. The developer writes this code in the main session together with the agent, or reads every line the agent proposes before accepting it. TDD applies as usual.
- **Agent-owned code:** scaffolding, wiring, UI, and everything else. Executed by subagents.
- **CI/CD and deployment:** the main session writes the workflow files together with the developer, who handles secrets, DNS and server access (`Executor: manual`). They are covered by the intermediate and final branch reviews and `/security-review`, not by a per-task reviewer, and are left out of the per-task subagent metrics.
- The task plan marks the executor of each task (see "Task plan format").
- Developer-owned tasks run in plan order, in the same worktree, never at the same time as a subagent task. Their sessions use the Explanatory or Learning output style, with terse output styles switched off.
- Bugs in developer-owned code are debugged by the developer: the agent follows `superpowers:systematic-debugging`, proposes hypotheses and ways to test them, and does not fix silently. The developer writes the fix or accepts it line by line.
- Unfamiliar code in the agent's zone: when the PR is opened (step 11), before the developer's server check and merge, the agent explains in chat how it works and why it is built that way, two or three paragraphs per unfamiliar part.
- Parallel subagents are used only for read-only work: research and reviews.

## Models

The controller names the model explicitly for every subagent. Plans are prose decisions, not code, so no role goes below Sonnet; Haiku is not used.

| Role | Epic 1 | Epic 2+ |
|---|---|---|
| Implementer | Sonnet; Opus for foundation tasks and tasks needing design judgment | same |
| Task reviewer | Opus | by diff risk, per the SDD Model Selection rule; Opus for tasks with Review Focus |
| Final branch review, intermediate branch review | Opus | Opus |

Epic 1 keeps every reviewer on Opus: it is the security epic and the baseline for comparison.

Effort: `medium` by default; `high` for brainstorming, `speckit-plan`, `writing-plans`, debugging, database schema and migration tasks, and the final review; `max` is not used.

Native execution (epic 2+): the executing session runs on Sonnet; the intermediate review after foundation tasks, the review of tasks with Review Focus and the final review are fresh Opus subagents. A plan long enough that its last tasks would run on a compacted context goes to SDD, not Native.

## Task statement

Every task starts with one full statement in the first turn. Inside the process the statement points to the artifact: "execute task N of `specs/NNN-<epic>/tasks-plan.md`; check: `<command>`; show the output". Outside the process (small changes, debugging, spikes) the prompt carries the whole frame:

1. Outcome: what becomes true for the user.
2. Why: one sentence, so the agent can judge edge cases.
3. Where: files, functions, reproduction steps, the error text verbatim.
4. Constraints: what not to touch, no new dependencies, which pattern to follow.
5. Scale: a minimal change, or explicitly broader with the additions named.
6. Done: a runnable command; the report quotes the command and its output.
7. Unclear: "ask me first" or "plan first, wait for my OK".

Identifiers, paths, commands, numbers and error texts in a prompt are verbatim — typed or pasted, not dictated. Dictated or scattered input is first assembled into a draft. Before acting, the agent restates the task in its own words and waits for the developer's "OK". The agent-side part of this rule lives in `CLAUDE.md`.

After a break longer than an hour, or when switching to an unrelated task, work continues in a fresh session from the artifacts rather than by resuming the old one.

If the same misunderstanding had to be corrected twice in one session, there is no third correction: the agent consolidates the task into one statement, the developer confirms it, and work continues in a fresh session.

## Stages

The steps are fixed. The agent may propose a deviation (skipping, merging or reordering steps); the developer decides. An accepted deviation is one ledger line: which step and why.

### Stage 1. The product as a whole (once)

1. **`superpowers:brainstorming` — product.** Goals, non-goals, anti-goals, users, end state. Split into epics and their order. Epic 1 is the walking skeleton (see below). Artifact: `docs/superpowers/specs/YYYY-MM-DD-product-design.md`. In the same pass: bounded contexts, the context map and the ubiquitous language per context. Artifact: `docs/domain/ubiquitous-language.md` — a living file, unlike the dated design. Its rules: every term belongs to exactly one context (a word used in two contexts is defined in each); one term per concept, no synonyms; the term is the same in specs, class names, tests, API and the ledger; renaming a term is a refactoring everywhere. Columns: term (EN) | Russian word | definition | rules and invariants. A context is a package in the code (modular monolith), not a service. The stage starts with `ledger-tokens mark 1-product`. Two overrides of the skill's defaults: the design is not committed (there is no repository yet), and the next step is the repository, not `writing-plans`.
2. **Repository.** `git init`, `.gitignore`, a public GitHub repository (a showcase; on GitHub Free, branch protection with required checks needs a public repository; secrets live only in GitHub secrets and on the server). The first commit on `main` contains this document, the product design, `docs/domain/ubiquitous-language.md`, a short `README.md` and `docs/ledger.md` with the rows for stage 0 and steps 1 and 2.
3. **`speckit-constitution`.** Project principles: minimum technology, tests are mandatory, the repository holds only the developer's work, and so on. Artifact: `.specify/memory/constitution.md`. Commit on `main`.
4. **Agent configuration.** `CLAUDE.md` and `.claude/settings.json` with deny rules (see "Agent configuration"). Build and test commands are added once the stack is confirmed in epic 1. One run of `/doctor prompt-audit` checks for conflicts between `CLAUDE.md`, skills and plugins (superpowers, speckit, caveman) — if the installed Claude Code has it; otherwise `/context` shows what loads into the context. Commit on `main`.

Stage 1 runs on `main` without a separate branch: there is nothing to branch from yet. Each step adds its own ledger row in its own commit.

### Stage 2. Each epic (repeated)

An epic is a separate speckit feature with its own folder `specs/NNN-<epic>/` and its own branch `NNN-<epic>`.

1. **Branch** `NNN-<epic>` from `main`.
2. **`speckit-specify`.** User stories with priorities P1, P2…, functional requirements `FR-NNN`, success criteria. No technology. If the epic has a real design fork (for example, choosing the OTP provider), run `superpowers:brainstorming` on that fork before this step; its outcome is the input to `speckit-specify`, which it hands over to instead of `writing-plans`.
3. **`speckit-clarify`.** Up to five clarification questions on gaps in the spec.
4. **Human review: spec.** No approval — no next step.
5. **`speckit-plan`.** Stack, architecture, `data-model.md`, `contracts/`, `research.md`, deployment scheme. Checked against the constitution. If a technical unknown blocks the plan (for example, whether email leaves the VPS through the chosen provider), a throwaway spike comes first: its code is discarded and never merged, what it taught goes into `research.md`, and the ledger records the spike in one line.
6. **Human review: technical plan.**
7. **`superpowers:writing-plans`.** The task plan for the executor. File: `specs/NNN-<epic>/tasks-plan.md`. Requirements are in "Task plan format".
8. **Human review: task plan.** Before it, the agent runs a mechanical check (a `grep`, no model judgment) that every `FR-NNN` and every user story from `spec.md` appears in the task tags of `tasks-plan.md`, and that every entity in `data-model.md` is a term in `docs/domain/ubiquitous-language.md`; a miss sends the plan back. An epic that introduces or changes a term edits the ubiquitous-language file in its own branch. writing-plans requires the human review before execution; the execution mode is chosen here too. The review checks that every cross-cutting concern (database migrations, error format, configuration) has an owning task. Plan depth follows the epic type: risk or security — full depth with Review Focus; tightly coupled domain — the entity and type model section, and Native is considered; loosely coupled tasks — a light plan. A plan of more than about 12 tasks splits the epic in two, so the epic's PR stays reviewable: the epic returns to step 2 as two specs. Epic 1 is exempt — the walking skeleton is one risk unit, and its CI/CD tasks are manual. The threshold is refined after epic 1. Signs of excess depth: the plan is more than twice as long as the spec, or tasks pass with no questions, no findings and trivial diffs. Epic 1 uses Subagent-driven only: the metrics for the pre-epic-2 decision come only from that mode. From epic 2 on, Subagent-driven or Native, by choice.
9. **Execution.** Artifacts of steps 2–8 are committed to branch `NNN-<epic>`. Execution starts in a fresh session that works only from the committed artifacts; anything it has to ask the developer is a gap and is tagged in the metrics like an executor question. The main checkout goes back to `main`; `superpowers:using-git-worktrees` attaches a worktree to the same branch `NNN-<epic>` — no nested branches. Then `superpowers:subagent-driven-development` with `superpowers:test-driven-development` for agent-owned tasks; developer-owned tasks are done in the main session (see "Division of work"). Models per role — see "Models". Execution runs with auto-approval; the deny rules hold the boundaries. The developer watches task 1 end to end (brief, implementer report, review), then checks at natural pauses — the intermediate review after foundation tasks and the final review — and interrupts on drift. Drift signals: a loop (the same fix twice); functionality outside the task, even a reasonable next step; any weakening, disabling or deletion of tests; edits outside the task's boundaries. Bugs go through `superpowers:systematic-debugging`. Task briefs are cut from the plan text, so a ruling that changes anything other tasks rely on (an interface, a value, an entity) is written into `tasks-plan.md` and committed before the next task is dispatched. A defect at the spec level stops execution and returns to the spec with the developer. Override of the SDD Finish section: after its final whole-branch review the controller stops — it does not delete the SDD workspace and does not invoke `finishing-a-development-branch`; the process continues with step 10.
10. **Branch readiness check.** `superpowers:verification-before-completion` with the full test command. The branch code review is SDD's final whole-branch review on Opus (step 9); it is not repeated, and metric 4 refers to it. Epics that touch authentication, sessions or user data also get `/security-review` over the branch.
11. **Pull request to `main`.** CI on the PR: tests and static analysis are required checks. Static analysis works as a ratchet: the PR fails if it adds even one new warning over the baseline, including the complexity and duplication rules of the linters chosen with the stack (no separate tools).
12. **Branch deployment and server check.** The developer adds the `deploy` label to the PR, which runs the branch deployment workflow, and checks: the subdomain responds, the epic's scenarios work (epic 1: OTP sign-in). If not, the fix goes into the same branch; `main` does not change.
13. **Ledger commit** to the branch, including the deployment record and metrics — see "Ledger". Metrics are harvested from the SDD progress ledger (`.superpowers/sdd/<plan>/progress.md`, git-ignored, inside the worktree) before step 14 removes the worktree. The developer reviews every ruling recorded there and decides which parked reviewer findings become small changes. One run of `/usage` (or `/context`, if `/usage` has no per-skill breakdown) checks where the epic's spend went; any skill, plugin or MCP server above 10% is considered, and a change to the environment is one ledger line.
14. **Merge.** The developer merges the PR on GitHub — the merge is a human decision. CI redeploys `main` (same code). Only code checked on the server reaches `main`. The agent then removes the worktree, the local branch and the SDD workspace. `superpowers:finishing-a-development-branch` is not used: its merge option merges locally into `main`, which the deny rules and branch protection forbid.

Before every human review (steps 4, 6, 8) the agent gives a short digest in chat, not in the file: decisions taken, assumptions it made without the developer's word, and 2–3 places worth reading closely — each with a pointer into the document. The developer reviews the digest and spot-reads the document.

There is one environment, no staging: between steps 12 and 14 the server runs the not-yet-merged branch code. Hence one epic at a time: the server holds one branch.

The branch deployment applies database migrations to the real database before merge, so:

- a migration that has reached the server is never edited; a fix is a new migration;
- migrations only expand the schema (add a column rather than rename one); removal is a separate step in a later epic;
- the branch deployment workflow backs up the database before applying migrations.

From epic 2, each epic has a token budget taken from earlier epics. If `ledger-tokens show` reports twice that budget, execution stops and the developer decides whether to continue.

### Epic 1 — walking skeleton

An empty application that goes the whole way to the server: build and tests in GitHub Actions, deployment to the VPS, access on the `cardlane.eu` subdomain, OTP sign-in. After epic 1, every following epic lands on the server immediately.

Epic 1 creates the three workflows stage 2 relies on: PR checks on `pull_request` (tests, static analysis, and a test-integrity check that fails when the diff adds test-skip markers or the test count drops below `main`, unless the developer allows the exception); deployment of a PR branch when the developer adds the `deploy` label (with a database backup before migrations); deployment of `main` on push to `main`. Both PR workflows run from the PR branch itself, so epic 1 needs nothing on `main` beforehand. After epic 1 merges — its PR has run the checks once, so they can be selected — branch protection on `main` is switched on with those checks required; until then the deny rules keep the agent off `main`.

In development mode the application writes the OTP email to the log instead of sending it, so the agent can pass sign-in in end-to-end tests without a real mailbox. Production mode sends real email. Development logs, the OTP email included, go to one fixed git-ignored file that `CLAUDE.md` points to; the dev-server command refuses to start a second instance.

Once the fast test command exists, epic 1 adds a project SubagentStop hook: when an implementer subagent finishes and source files changed, it runs the fast test command, and a red result returns the subagent to work (exit code 2). Main-session work is not gated: in developer-owned tasks and debugging a red test is the expected handover point.

CI/CD and deployment tasks (`Executor: manual`) are written with explicit manual steps (secrets, DNS, server access) and the check "workflow is green + a request to the subdomain responds". They do not go through the "subagent → reviewer" loop: they reach outside the worktree, and the external check is slow.

### Small changes

A change is small if it is one task and adds no new requirements (no new `FR-NNN`, no change to the spec). It skips the speckit cycle:

1. Branch from `main`.
2. Task statement (see above), TDD, `superpowers:verification-before-completion`.
3. PR with required checks; branch deployment if behavior on the server changes.
4. One ledger line in the same branch: "spec cycle skipped: <reason>".

### Before epic 2

Decision: does `writing-plans` stay the only task source, or is `speckit-tasks` tried for simple epics. Decided by epic 1 metrics (see "Metrics").

## Task plan format

The plan is written by `writing-plans` 7.0.0 and records decisions, not code. In addition to the skill's rules:

- Task heading: `### Task N: [US1][FR-003] <name>`. The literal `Task N` is required — subagent-driven-development scripts depend on it.
- `Spec:` field points to `specs/NNN-<epic>/spec.md`, alongside `plan.md` and `data-model.md`.
- `Executor:` field — `subagent`, `developer` or `manual` (see "Division of work").
- Global Constraints: values from the constitution and `plan.md` are copied verbatim. Every value from the spec and `data-model.md` (limits, lengths, lifetimes, enumerations) names the task whose test asserts it, e.g. `OTP TTL = 10 min — Task 3`. A value with no asserting task is a gap the human review at step 8 rejects.
- A section with the entity and type model for the whole epic, so one entity is not defined differently in two tasks.
- Review Focus — inputs and cases where an error costs most (epic 1: expired or reused code, code guessing, email enumeration, redirect after sign-in to a foreign domain, a code presented for another email). Every Review Focus input becomes a negative test in the plan that attempts the attack; it is not left as a note for the reviewer.
- Tasks are sliced by behavior, not by layer. Tasks that would always be accepted or rejected together are merged. Every task ends with wired-in, tested behavior.
- Each task states whether it depends on other tasks. Foundation tasks — those other tasks depend on — are marked. After the last foundation task the controller runs `superpowers:requesting-code-review` over the whole branch before dependent tasks start. An epic without such structure has no intermediate review.
- Algorithms are specified by example tables that become parameterized tests (e.g. streak counting: check-ins, today, expected streak). Code appears in the plan only where examples cannot pin the behavior down.
- The plan writer does not edit speckit artifacts (`spec.md`, `plan.md` and others). If it finds an error in them, it stops and reports.
- Size guide: the plan is about 1.5–2 times longer than the spec. A plan made mostly of code rewrites the program and must be redone.

**Task readiness criterion:** from the task text, the repository and the spec, the executor arrives at exactly one reasonable result and checks it itself with a runnable pass/fail test. The task records decisions that cannot be derived from the repository and the spec, and nothing the executor will find on its own.

## Ledger

File `docs/ledger.md`. The reader is an employer's reviewer; from the entries they see how the developer led the agent.

- One entry per stage. Stage 0: process design — this document, the decisions the developer took and the research behind them, in one row, without links outside the repository; its tokens are counted from the start of the design session (2026-10-27). Stage 1: product design, repository, constitution, agent configuration. Each epic: spec, clarification, technical plan, task plan, execution, deployment.
- Columns: **Stage | Tools | Agent did | Human decided | Artifact | Tokens**. "Agent did" and "Human decided" are one or two lines of substance; details are in commits, which the entry links to.
- Tokens: the stage's budget, main session plus subagents — input (uncached and cache writes), cache reads, output — and session time: the sum of gaps shorter than 10 minutes between consecutive transcript records, which includes time the agent and its subagents ran while the developer was away; it is not the developer's time at the keyboard. Wall-clock time between stage marks is not used: it includes breaks. The developer's own reading time stays a separate, manual metric. Counted from Claude Code transcripts by a local script `ledger-tokens` that is not part of the repository: `ledger-tokens mark <stage-id>` at the start of each stage, `ledger-tokens write` when entries are added or updated. The cell holds a marker `<!--tokens:<stage-id>-->…<!--/tokens-->`, and `write` replaces its contents with current numbers.
- An epic's entries are added in one commit `ledger: <epic>` to the epic branch before merge. The developer decides what goes into the entries at that step.
- Accepted decisions only, no history of rejected options.
- An epic's entry ends with a metrics line (see below).

## Metrics

For each subagent task:

1. did the executor ask for context (`NEEDS_CONTEXT`, questions);
2. how many fix rounds followed review;
3. implementer model and reviewer model.

For each epic:

4. what the final branch review found;
5. how long the developer spent reading the task plan;
6. how many times a correction had to be repeated within one session (a problem signal), and separately how many sessions were restarted from a consolidated statement (the remedy, not a problem);
7. which `CLAUDE.md` rules or hooks were added because of errors, with links to the commits.

Every executor question and every post-review fix is tagged with the stage where the gap originated: goals, spec, technical plan or task plan. The ledger then shows at which level a statement failed its check, not only how many problems there were.

The summary goes into the epic's ledger entry; the "Before epic 2" decision is made from it.

## Open decisions

| Decision | Where it is decided |
|---|---|
| Stack | decided: see the product design, Constraints; libraries confirmed in epic 1 `speckit-plan` |
| OTP / email delivery provider. Outbound ports 25 and 465 are probably blocked on new Hetzner servers — to be checked | decided: Resend over its HTTP API, see `docs/superpowers/specs/2026-10-30-email-service-design.md` |
| Subdomain name on `cardlane.eu` | decided: see the product design, Constraints |
| CI/CD artifacts (what is built, how it is delivered to the VPS) | epic 1, `speckit-plan` |
| `writing-plans` or `speckit-tasks` for epics 2+ | before epic 2, by metrics |

## Revision

Two kinds of rules live in this document. Rules that rest on a mechanism — one full task statement, TDD and pass/fail checks, decisions kept in files, hooks instead of prose, the static-analysis ratchet, fresh-eyes review, the ubiquitous language — need no revision. Rules calibrated to a model — the Models section and effort levels, the numeric thresholds (plan 1.5–2× the spec, ~12 tasks per epic, fast tests ~2 minutes, `CLAUDE.md` ~60 lines), the choice between SDD and Native — hold for Opus 5.5 / Sonnet 5.5 as of 2026-10-28. They are revisited before epic 2, from epic 1 metrics, and whenever the model generation changes.

## Left to the builder

- Concrete branch names and `NNN` numbers — per the speckit scheme.
- Wording of ledger entries and commits — within the rules above.
- Task order inside a plan — within the dependencies the plan states.
- Exact deny rule patterns in `.claude/settings.json`.
