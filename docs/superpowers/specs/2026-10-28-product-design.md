# habits-tracker product design

Date: 2026-10-28, revised 2026-10-30 by the epic 1 design fork (`docs/superpowers/specs/2026-10-30-email-service-design.md`, `docs/superpowers/specs/2026-10-30-legal-pages-design.md`): the email service, NFR-7, NFR-9, the interface languages, the Privacy and Terms pages; revised again 2026-10-30 by the external constraints check (`docs/superpowers/specs/2026-10-30-external-constraints-check.md`): goal 8, the language switch, NFR-5, NFR-11, the SLO measurements, third-party requests. Stage 1, step 1 of the development process (`docs/superpowers/specs/2026-10-28-development-process-design.md`).

Domain terms in this document are defined in `docs/domain/ubiquitous-language.md`, the only source of domain terms. Items marked *(agent's call)* were proposed by the agent without a separate question and are confirmed by the developer's review of this document.

## Intent

habits-tracker is a web habit tracker for one person who uses it on several devices at the same time. A change made on one device appears on every other open device at once, without reloading the page, and concurrent edits from two devices end in one predictable state everywhere.

The project is a showcase of agentic development: how a developer leads an agent from product goals to a working application on their own server. The product is deliberately small; the parts a reviewer looks at closely are the domain rules (schedules, streaks), the concurrency rules and the path to the server.

Among eight existing habit trackers looked at during design (Loop, HabitKit, Everyday, Streaks, Habitify, Habitica, TickTick, Way of Life), none documents instant multi-device sync or how concurrent edits are resolved. That is the first place where this product differs.

The second is a choice those products make for the user. Almost all of them trust the user: any past day can be fixed, and a habit set aside for a while keeps its streak. The one strict product found (Habitica) is strict for every habit. Here the user decides per habit: a **Routine** is kept on trust, a **Commitment** is kept strictly, and leaving a Commitment is possible but deliberately slow and never rewrites the past. This combination was not found among the eight products researched.

## Users

A single person tracking their own habits. Every user sees only their own data; there is nothing shared between users. The same user typically has the app open on a phone and a laptop at once.

## End state

All eight epics are merged to `main` and run at `habits.cardlane.eu`, and every SLO has a value measured in production. If time runs short, epic 7 is the first to be left out (see "Epics").

## Goals

1. A user signs in with a one-time code sent to their email; anyone with an email address can sign in, and the first sign-in creates the account.
2. A user creates habits with one of two schedules: a Weekday schedule (chosen days of the week, up to daily) or a Weekly target (N times per week on any days).
3. The main screen shows, for every active habit: today's check-in, its history as an activity calendar, and statistics (streak, best streak, completion rate).
4. A user can check in today and fix past days: any past day except a Closed day for a Routine, only yesterday for a Commitment.
5. A change on one device is visible on every other open device of the same user within 2 seconds, and concurrent edits converge to one state by fixed rules.
6. Changing a schedule never rewrites history.
7. A habit can be archived (and restored) or deleted for good.
8. The interface is in English, Polish and Russian; adding a language is adding a translation file (revised 2026-10-30, see "Interface language").
9. Every habit is either a Routine (kept on trust) or a Commitment (kept strictly). A Routine becomes a Commitment at once; a Commitment becomes a Routine only 7 days after the user asks, and days closed before stay closed.
10. A user stays signed in while they keep using the app (across browser restarts if they choose "Remember me"), can sign out on one device or everywhere, can change their time zone, and can delete their account with all their data.

## Non-goals

1. Shared habits and anything social: friends, feeds, competitions.
2. Reminders: email, browser push notifications.
3. Native mobile apps. The web app works on a phone instead.
4. Working offline.
5. Gamification: points, levels, rewards.
6. Habits with quantities ("8 glasses of water") or partial completion. A day is done or not done.
7. "Quit" habits, where the user records a relapse instead of a completion.
8. Import and export of data.
9. Sign-in through Google, GitHub or a password. Only the one-time code.
10. An admin interface for the service owner.

## Anti-goals

What makes the product a failure even if it technically works:

1. Two devices show different states and do not converge without a reload.
2. A check-in is lost silently: the user marked a day, and after a refresh the mark is gone.
3. A streak or completion rate is wrong after a schedule change, a fix to a past day, or a time zone change.
4. A reviewer cannot sign in within a couple of minutes because the code email did not arrive or went to spam.
5. A user sees a single habit or check-in of another user.
6. Technology with no case behind it: microservices, a message broker, a cache that fixes nothing.

## The product

### Sign-in and account

- The user enters an email and receives a 6-digit one-time code. Entering the code signs them in; the first sign-in creates the account.
- A session is a server-side record; the cookie carries only its opaque id. The session is sliding: it expires after 30 days without activity, and any activity extends it. Activity is a request the user makes; an open connection alone is not activity. Revoking it takes effect at once.
- The sign-in form has a "Remember me" checkbox, unchecked by default. Without it the session cookie ends when the browser closes; with it the session is a Remembered session and its cookie persists (see `docs/superpowers/specs/2026-10-30-legal-pages-design.md`, "Remember me").
- After a code is sent, the user sees how many code requests for this email are left; a refusal by a code request limit or by the email service says why and when to try again (see `docs/superpowers/specs/2026-10-30-email-service-design.md`, "What the user sees").
- The user can sign out, or sign out everywhere, which revokes all their sessions and closes every open connection *(agent's call)*.
- The time zone is taken from the browser on the first sign-in and stored with the user; the user can change it. *(agent's call)*
- The user can delete their account; all their data is erased at once, and the copies in backups disappear when those backups expire, within 7 days. *(agent's call)*
- In development mode the code email is written to the log instead of being sent (see the process document, "Epic 1 — walking skeleton").
- A Privacy page, readable before sign-in, says what is stored (email, time zone, habits and check-ins, sessions, and IP addresses in logs and in request limits), which service sends the code email, that backups keep deleted data for up to 7 days and logs are kept for 7 days, and how to have the data deleted: until account deletion exists (epic 8), on request to a contact address on the page (the developer supplies it in epic 1). Its full contents, required by GDPR art. 13, are in `docs/superpowers/specs/2026-10-30-legal-pages-design.md`, "Privacy page".
- A Terms page, readable before sign-in, sets the terms of the service; its contents are in `docs/superpowers/specs/2026-10-30-legal-pages-design.md`, "Terms page". Under the sign-in button the user is told that signing in accepts the Terms, with links to both pages.
- Every page loads everything from `habits.cardlane.eu`: no fonts, scripts, styles or images from third-party servers, since each such request sends the user's IP address to a recipient outside the closed list on the Privacy page (revised 2026-10-30, see `docs/superpowers/specs/2026-10-30-external-constraints-check.md`) *(agent's call)*.

### Routines and commitments

The product follows one principle per habit, chosen by the user, rather than mixing trust and strictness in one set of rules.

- A **Routine** trusts the user. Any past day can be checked in or cleared, except a Closed day left from a time the habit was a Commitment. An archive period that begins while the habit is a Routine is skipped by the streak.
- A **Commitment** is a witness. Only today and yesterday can be checked in or cleared (the Edit window); a day that leaves the window is closed for good. An archive period resets the streak.
- Closing is one date per habit, the Closed-through date: every day on or before it is a Closed day. When the habit becomes a Commitment, it is set to the day before yesterday, or kept if it is already later. It moves forward with every tracking day while the habit stays one, and once more at the start of the day a Release takes effect, so the last yesterday of the Commitment closes too. It never moves back: it is stored with the habit, so a time zone change cannot move it back either.
- **Commit** (Routine to Commitment) takes effect at once, after a warning: days before yesterday close, and leaving will take 7 days. A Commitment period starts on the day of Commit, or on the day a habit is created as a Commitment.
- **Release** (Commitment to Routine) starts as a Release request, after a warning, and takes effect 7 days later; the day it takes effect is fixed when the request is made. Until then the habit stays a Commitment and the request can be cancelled. The Commitment period ends on the day before the Release takes effect.
- No switch is retroactive. The Closed-through date stays where it is after a Release, and a time zone change never moves it back. So switching cannot be used to undo a streak reset, which is the only point of making the way out slow.
- Whether an archive period resets the streak is decided by whether the habit was a Routine or a Commitment when the period begins: it resets the streak if its first day lies in a Commitment period. A later Commit or Release never changes the effect of an earlier archive period.
- The difficulty is time and the absence of a retroactive effect, never friction: there are warnings, no captchas and no "type CONFIRM".
- A Routine or a Commitment can have either schedule kind *(agent's call)*.

### Main screen

One card per active habit, in creation order *(agent's call)*. A card holds:

- the habit name, whether it is a Routine or a Commitment (with a Release request and its date, if any), and a checkbox "done today" (for a Weekday schedule, only on scheduled days; for a Weekly target, every day);
- the streak, the best streak and the completion rate over the window defined below;
- an activity calendar in the style of the GitHub contribution graph: columns are weeks (Monday on top), rows are days of the week, covering the last 20 weeks and ending with the rest of the current week.

Calendar cells of a Weekday schedule habit have four Day statuses: Done, Missed, Unscheduled, Planned. Cells of a Weekly target habit are Done or Empty; a Not met week is marked on its whole column (for example, a red line under the column) *(agent's call)*. A Partial week is never marked. Days before the habit's first schedule period, and days inside an archive period, are Not tracked and look blank. Today's cell is outlined.

On a narrow screen (360 px) the calendar shows 10 weeks and scrolls sideways *(agent's call)*.

Archived habits are listed in a separate Archive view, from which they can be restored.

### Check-ins and past days

- Clicking a past or today's cell sets or clears its check-in. Future cells cannot be checked in, and neither can Unscheduled cells of a Weekday schedule: a flexible plan is what a Weekly target is for.
- A Routine has no limit on how far back a day can be fixed, except Closed days; the interface reaches the days shown in the calendar (the last 20 weeks). A Commitment can fix only yesterday; older days are Closed days and cannot be clicked. Neither takes a check-in before the habit's first schedule period starts *(agent's call)*.
- A check-in for a day that closed while the request was on its way (for example, sent before midnight, received after) is rejected with a message; the server's clock and the user's time zone decide.
- A check-in stores the tracking day (a date in the user's time zone). A later time zone change does not move existing check-ins to other dates. If a zone change makes a stored date lie in the future, the check-in is kept and shown as Done *(agent's call)*; it can be cleared (for a Commitment, only once its date is in the Edit window), and it counts in the streak and the completion rate once its date is no longer in the future.
- "Not done" is the absence of a check-in record: clearing a day deletes its check-in *(agent's call)*.
- Daylight-saving transitions only move the instant at which a tracking day starts. The test for NFR-4 asserts that an action at 23:30 local time on the transition day lands on that local date *(agent's call)*.

### Schedules

- A Weekday schedule names days of the week. A scheduled day that is over without a check-in is Missed; an Unscheduled day is skipped by the streak.
- A Weekly target is N check-ins per week, 1 ≤ N ≤ 7. No single day is Missed; a week has a Week status: Met, Not met, In progress or Not judged. A Partial week (the week the habit was created in, or a week that overlaps an archive period) is Met once its target is reached and is never Not met.
- The schedule kind (Weekday schedule or Weekly target) is fixed when the habit is created. To change it, the user creates a new habit. Streaks therefore never mix days and weeks.
- A change of the weekdays takes effect from tomorrow, or from the day after the habit's latest check-in if that is later (a time zone change can leave a check-in on a future date): today is always judged by the schedule it started with, so a schedule change cannot erase today's miss, and no check-in falls on an Unscheduled day. A change of N takes effect from the next Monday, and the current week is judged by the old N.
- A schedule change never rewrites the past. Earlier dates keep the schedule of their own schedule period, so a change never turns past days into Missed and never resets a streak retroactively. The habit stores its schedule periods; the first one starts on the day the habit is created.
- A week always runs Monday to Sunday.
- **What a streak means.** A streak answers "how long have I kept my word without a break". The word was given in the schedule, so the unit is what the schedule judges: scheduled days for a Weekday schedule, weeks for a Weekly target, shown with the unit ("5 days", "3 weeks"). A unit counts the moment the promise is kept (today once done, the current week once it has N check-ins) and never before; a unit still in progress neither counts nor resets the streak. Check-ins above N add nothing: the tracker measures consistency, not volume. The best streak is the highest value the streak has reached.

Completion rate window *(agent's call)*: for a Weekday schedule, the last 30 tracking days ending today, where today counts only once it is Done; Not tracked days are left out. For a Weekly target, the last 4 complete weeks, plus the current week only once its target is met; a Partial week counts only once met, and weeks left out are not replaced by earlier ones. With nothing to count, the rate is shown as "—".

Exact rules for streak, best streak and completion rate are in the glossary, under Habit Tracking.

### Archive and delete

- Archive moves a habit off the main screen and keeps its history; Restore brings it back. An archived habit takes no check-ins and accepts only a rename, Restore and Delete; Commit, a Release request or its cancellation, and a schedule change need the habit restored first. A Release request made before archiving still takes effect on its day.
- Days inside an archive period are Not tracked: they are not Missed and are left out of the completion rate. An archive period that begins in a Commitment period resets the streak; any other is skipped by the streak. The habit stores its archive periods.
- An archive period runs from the day of archiving to the day before restoring; if the day of archiving, or a later day, already has a check-in, the period starts on the day after the latest check-in, so no check-in is ever hidden *(agent's call)*. A period with no days (archived and restored on the same day, or restored before the day after the latest check-in) has no effect.
- Delete erases the habit with all its check-ins and periods, after a confirmation. It cannot be undone.

### Concurrent edits

Rules differ by type of data:

- **While disconnected**, a device shows that it is offline and takes no changes: cells and edits cannot be used, and nothing is queued *(agent's call)*. A change whose save failed is shown as not saved (NFR-3) and is never retried by itself; after reconnecting and catching up (NFR-2) the user sees the current state and repeats the change if still needed. So a late retry can never overwrite a newer state.
- **Check-ins** are sent as an explicit state ("day D: done" or "day D: not done"), never as a toggle. Two devices sending the same state agree; of two different states the last one to reach the server wins. A check-in never fails because of a concurrent change to another check-in.
- **Habit settings** (name, schedule, archive state, Commit, making or cancelling a Release request) carry a habit version. An edit made against an older version is rejected; the user sees "changed on another device" and the current state, and repeats the edit if still needed.
- **A Release taking effect** is not an edit: it changes no habit version and sends no change notification. Every device derives from the date whether the habit is a Routine or a Commitment, and its Edit window from the date and the stored Closed-through date, and recomputes them when a tracking day starts.
- **Delete** wins over everything: a check-in or edit for a deleted habit is rejected with a message. To tell "deleted" from "never existed", deletion leaves a tombstone holding only the habit id, its owner and the deletion time, no user content; account deletion erases tombstones too *(agent's call)*.
- After any of these, every open connection of the user shows the same final state.

### Interface language

English, Polish and Russian, from epic 1. Every user-visible string, the code email and the Privacy and Terms pages included, comes from translation files, with plural rules handled by the translation format. Adding a language means adding a file, with no code change. Switching the language applies at once, without a page reload (revised 2026-10-30, see `docs/superpowers/specs/2026-10-30-external-constraints-check.md`). Before sign-in the language follows the browser and a switcher on every page; at the first sign-in it becomes the user's locale (see `docs/superpowers/specs/2026-10-30-legal-pages-design.md`, "Languages").

## Non-functional requirements

Numbers in brackets are anti-goals the requirement guards. The Epic column names the epic that first has to meet the requirement.

| # | Requirement | Epic |
|---|---|---|
| NFR-1 | A change is visible on another open device of the same user within 2 seconds: the end-to-end test with two browsers in CI sends at least 20 changes and asserts that the 95th percentile is within 2 seconds. (1) | 3 |
| NFR-2 | A device that lost its connection (sleep, network change) shows the current state within 5 seconds after the network is back, without a page reload: an end-to-end test cuts and restores the network of one browser while another makes changes, and asserts the time. (1) | 3 |
| NFR-3 | A check-in is shown as saved only after the server confirms it; a failed save is visible to the user. (2) | 2 |
| NFR-4 | Streak, best streak and completion rate rules are covered by unit tests, including a schedule change, a fix to a past day, a time zone change, a daylight-saving transition, an archive period of a Routine and of a Commitment, a Closed day, Commit and Release (including the day a Release takes effect), a Partial week, a change of N. (3) | 2; epics 4–8 add their cases |
| NFR-5 | The database is backed up daily and before every migration; backups are kept for 7 days *(agent's call)*. A restore from backup has been done at least once. The backups are kept on the VPS itself: they cover failures that leave the server's disk intact (a failed migration, an application defect, data deleted by mistake), not the loss of the server, which is an accepted risk. The restore is done on the server into a separate temporary database next to the live one: its data is checked against the backup, its time is recorded, and it is then removed; the live database is not touched (revised 2026-10-30, see `docs/superpowers/specs/2026-10-30-external-constraints-check.md`). | 1 |
| NFR-6 | Every request sees only the signed-in user's data. An automated test covers every endpoint: user B can neither read nor change user A's data, and no connection of user B receives a change notification about user A's data. (5) | 1; every epic that adds an endpoint; the live-update channel: 3 |
| NFR-7 | One-time codes: 6 digits, valid 10 minutes, at most 5 entry attempts per code, stored only as a hash; requesting a new code invalidates earlier ones; at most 5 code requests per email and 10 per IP address (per /64 prefix for IPv6) in any 24 hours, a sliding window (revised 2026-10-30, see `docs/superpowers/specs/2026-10-30-email-service-design.md`). | 1 |
| NFR-8 | The code email arrives within 1 minute and lands in the Gmail inbox, not spam: the sending domain has SPF, DKIM and DMARC. (4) | 1 |
| NFR-9 | Sessions are server-side and carried in an `HttpOnly`, `Secure` cookie; a session expires after 30 days without activity; an open connection alone is not activity. The cookie of a session that is not a Remembered session ends when the browser closes; the cookie of a Remembered session persists (revised 2026-10-30, see `docs/superpowers/specs/2026-10-30-legal-pages-design.md`). Revocation (sign out, sign out everywhere, account deletion) and expiry take effect on the next request and close open connections at once. HTTPS only. | 1; closing connections: 3 |
| NFR-10 | In production, one-time codes never appear in logs; logs are kept for 7 days *(agent's call)*. Account deletion erases all the user's data at once; the copies in backups disappear when those backups expire (NFR-5). | 1; account deletion: 8 |
| NFR-11 | Sized for about 100 users on one VPS; no horizontal scaling: a load test with 100 users, two open connections each, keeps the API latency SLO. The load test runs in CI, with the application and database containers limited together to the CPU and memory of the server, a Hetzner Cloud CX23 (2 vCPU, 4 GB) (revised 2026-10-30, see `docs/superpowers/specs/2026-10-30-external-constraints-check.md`). (6) | 1; open connections: 3 |
| NFR-12 | The main screen with 20 habits opens within 2 seconds, measured by an end-to-end test in CI. | 4 |
| NFR-13 | At a viewport width of 360 px every action of the screen is reachable and the page has no horizontal scroll except the calendar: checked by the end-to-end tests at that width. | every epic with a screen |

## Service level objectives

Internal objectives per calendar month. There is no contract with clients, so these are SLOs, not an SLA. 99% is chosen for a single VPS without redundancy.

| SLO | Objective | Measured by | Epic |
|---|---|---|---|
| Availability | 99% (about 7 hours of downtime a month) | an external check of a health endpoint every 5 minutes from a scheduled GitHub Actions workflow, outside the VPS. A check passes when the endpoint answers with success over HTTPS within 10 seconds; the endpoint answers with success only when the application can reach the database, and returns no personal data; a timeout or a TLS error fails the check *(agent's call)*. The monthly value is the share of passed checks among the checks that ran; a month in which no check ran has no value. A failed check notifies the developer through GitHub's notification for a failed workflow run. Scheduled workflows run only from `main`, so the schedule starts once epic 1 is merged; before merge, the branch deployment runs the same check once against the server (revised 2026-10-30) | 1 |
| API latency | 95% of requests answered within 300 ms, server side, live-update connections excluded | request timing kept as metrics for at least a month (logs are kept only 7 days); the metrics are aggregated and hold no IP address and no user id, so they are not personal data (revised 2026-10-30) | 1 |
| Sync latency | 95% of changes reach another open device within 2 seconds | synthetically: an end-to-end test with two browsers signed in as one user, run in CI, and against the server after every deployment and at least once a day. Each run sends at least 20 changes; the monthly value is the 95th percentile over all the month's runs against the server *(agent's call)*. The runs against the server sign in with a Remembered session of a dedicated test account, opened once by the developer and kept in GitHub secrets: they send no code email and use no sign-in bypass (revised 2026-10-30). Telemetry from real clients is added only when a case appears that the test does not catch | 3 |
| Data loss (RPO) | at most 24 hours, for failures that leave the server's disk intact (NFR-5) | the daily backup and the backup before each migration | 1 |
| Recovery (RTO) | at most 4 hours, for failures that leave the server's disk intact (NFR-5) | a restore done once on the server into a separate database (NFR-5), with its time recorded | 1 |

Error budget: if an SLO is missed in a month, the next epic starts with a reliability task before new features. Planned downtime during deployment counts against the budget.

The measuring tools are chosen in `speckit-plan` and follow the rule "no case, no technology"; the availability check is the exception, fixed above by the external constraints check (revised 2026-10-30). The requirement is that every SLO is measured, not only declared.

## Constraints

- Backend: C# on .NET 10 (LTS), ASP.NET Core.
- Frontend: Angular, a single-page application.
- Database: PostgreSQL.
- The whole application starts with one command through docker compose; data survives restarts.
- Deployment target: the developer's Hetzner VPS running Linux, at `habits.cardlane.eu`, HTTPS only.
- One environment, no staging (see the process document).
- The code is a modular monolith: each bounded context is a package, not a service.
- Public GitHub repository; secrets live only in GitHub secrets and on the server.
- Email: the one-time code is sent through a transactional email service, Resend (see `docs/superpowers/specs/2026-10-30-email-service-design.md`), from `mail.cardlane.eu` with its own SPF, DKIM and DMARC records. The DNS zone is managed at OVH. The records of `cardlane.eu` itself (SPF `-all`, DMARC `p=reject` with strict alignment) stay unchanged. The developer creates the service account and the DNS records.

## Domain

Three bounded contexts; terms, rules and the context map are in `docs/domain/ubiquitous-language.md`.

- **Identity** (supporting): accounts, one-time codes, sessions, time zone, locale, account deletion. The one-time code sign-in is written by the developer (see "Division of work" in the process document).
- **Habit Tracking** (core): habits as Routines and Commitments, the Edit window, the Closed-through date and Closed days, schedules and schedule periods, archive periods and Commitment periods, check-ins, Day status and Week status, streaks, completion rate. Written by the developer (see "Division of work" in the process document).
- **Live Updates** (generic): delivers change notifications to every open connection of the user; knows no domain rules.

## Epics

In this order. Each epic is a speckit feature `specs/NNN-<epic>/`; user stories, functional requirements and success criteria are written in its `speckit-specify` step and refer to the NFRs and SLOs above.

1. **Walking skeleton.** An empty application all the way to the server: CI/CD, deployment to the subdomain, one-time code sign-in with the time zone taken from the browser, sign out and sign out everywhere, the translation setup with English, Polish and Russian, the Privacy and Terms pages, an empty "no habits yet" screen, the daily database backup and one restore from it. Measures the availability, API latency, data loss and recovery SLOs. Retires the deployment and authentication risks.
2. **Habits and today.** Weekday schedule; create, rename and delete a habit; today's check-in; the streak. Until epic 6 every habit is a Routine.
3. **Live updates.** Changes appear on every open device; the concurrent edit rules; closing the connections of revoked or expired sessions. Measures the sync latency SLO. It comes right after the first feature because it is the main technical risk after deployment and sign-in, and every later feature is then built with live updates from the start.
4. **History calendar.** The activity calendar with its Day statuses, fixing past days, best streak, completion rate.
5. **Schedule and archive history.** Changing a habit's weekdays with schedule periods; archiving and restoring a habit, the Archive view, archive periods shown as Not tracked and skipped by the streak (until epic 6 every habit is a Routine). Split from epic 4 so that the periods, the hardest part of the domain core, are reviewed on their own. Archive moved here from epic 2 so that the streak never runs for a while without the archive period rule *(agent's call)*.
6. **Commitments.** Routine and Commitment: creating a Commitment, Commit, the Edit window, the Closed-through date and Closed days, Release with its 7-day delay and cancellation, Commitment periods and the archive periods that begin in them resetting the streak. Comes before Weekly target so that the product's own idea lands early: if time runs short, a feature every tracker has is the one left out.
7. **Weekly target.** The second schedule kind, for both Routines and Commitments: Week status and Partial weeks, streaks and completion rate in weeks, the week column marking, changing N from the next Monday.
8. **Account.** Changing the time zone; deleting the account with all its data; the Privacy page pointing to account deletion instead of the contact address.

## Story map

What the user gets from each epic, one line per story. The map checks that the epics cover the whole product. It is frozen: once an epic's `speckit-specify` step has run, that epic's `spec.md` owns the stories (wording, priorities, acceptance scenarios), and this map is not edited to follow it.

**Epic 1 — walking skeleton**

- US-1.1 As a visitor, I want to request a one-time code to my email, so that I can sign in without a password.
- US-1.2 As a visitor, I want to enter the code and be signed in, with an account created on my first sign-in and my time zone taken from the browser, so that I can start right away.
- US-1.3 As a user, I want to stay signed in while I keep using the app, so that I do not sign in every day.
- US-1.4 As a user, I want to sign out on this device.
- US-1.5 As a user, I want to sign out everywhere, so that a lost device loses access at once.
- US-1.6 As a new user, I want to see that I have no habits yet, so that I know where to start.
- US-1.7 As a visitor, I want to read what data the app stores and how to have it deleted, before I sign in.

**Epic 2 — habits and today**

- US-2.1 As a user, I want to create a habit with a Weekday schedule, so that I can track it on the days I plan.
- US-2.2 As a user, I want to rename a habit.
- US-2.3 As a user, I want to check in a habit for today, or clear today's check-in, so that my day is recorded correctly.
- US-2.4 As a user, I want to see each habit's current streak, so that I see how long I have kept it up.
- US-2.5 As a user, I want to delete a habit for good, after a confirmation, so that a habit created by mistake disappears with its data.

**Epic 3 — live updates**

- US-3.1 As a user with the app open on two devices, I want a change made on one to appear on the other without a reload.
- US-3.2 As a user whose device slept or lost the network, I want it to show that it is offline, and to catch up by itself when it is back.
- US-3.3 As a user editing a habit that was changed on another device, I want to be told and shown its current state, so that I do not overwrite a change I have not seen.
- US-3.4 As a user acting on a habit deleted on another device, I want to be told it was deleted.
- US-3.5 As a user who signed out everywhere, I want tabs still open under the revoked sessions to stop receiving my changes at once, so that a lost device sees nothing new.

**Epic 4 — history calendar**

- US-4.1 As a user, I want to see each habit's history as an activity calendar, so that I see at a glance which days I did it, missed it, or was not meant to.
- US-4.2 As a user, I want to check in or clear a past day, so that I can fix a day I forgot to record.
- US-4.3 As a user, I want to see each habit's best streak and completion rate.

**Epic 5 — schedule and archive history**

- US-5.1 As a user, I want to change a habit's weekdays without my past turning into misses.
- US-5.2 As a user, I want to archive a habit and find it in the Archive view, so that it leaves my main screen without losing its history.
- US-5.3 As a user, I want to restore an archived habit.
- US-5.4 As a user who restored a habit, I want the days of its archive period shown as not tracked.

**Epic 6 — commitments**

- US-6.1 As a user, I want to create a habit as a Commitment, or make a Routine a Commitment after a warning, so that a day I did not record in time can no longer be changed.
- US-6.2 As a user with a Commitment, I want to fix yesterday, while older days can no longer be changed.
- US-6.3 As a user, I want to ask to release a Commitment, see the date it becomes a Routine, and cancel the request before then.
- US-6.4 As a user, I want closed days to stay closed after a Release, so that my streak stays an honest record.
- US-6.5 As a user, I want archiving a Commitment to reset its streak, while the streak of an archived Routine skips the archived days.

**Epic 7 — weekly target**

- US-7.1 As a user, I want to create a habit with a Weekly target of N times a week, so that I can track a habit without fixed days.
- US-7.2 As a user, I want to check in a Weekly target habit on any day and see whether this week's target is met.
- US-7.3 As a user, I want weeks that missed the target marked on the calendar, and the streak, best streak and completion rate counted in weeks.
- US-7.4 As a user, I want to change N, with the new N applying from the next Monday.

**Epic 8 — account**

- US-8.1 As a user, I want to change my time zone, so that my days start and end where I live now.
- US-8.2 As a user, I want to delete my account with all my data.
- US-8.3 As a user with a Commitment, I want closed days to stay closed when I change my time zone, so that moving cannot reopen them.

## Left to the builder

- Libraries and frameworks inside the stack, including how live updates are delivered to the browser.
- The email service: decided 2026-10-30, Resend (`docs/superpowers/specs/2026-10-30-email-service-design.md`). The criterion "someone requesting codes for many addresses must not lock other users out" (anti-goal 4) holds against a single source; distributed abuse is an accepted risk recorded there.
- The tools that measure the SLOs, except the availability check (see "Service level objectives").
- The database schema and the API shape.
- Visual design: colors, spacing, components, beyond what "Main screen" fixes.
- How the Archive view and the delete confirmation look.
- The order of a Partial week and an archive reset for a Commitment with a Weekly target (a week that met its target before an archive period began in a Commitment period): decided in epic 7 `speckit-clarify`.
