# Feature Specification: Walking skeleton

**Feature Branch**: `001-walking-skeleton`

**Created**: 2026-10-30

**Status**: Draft

**Input**: Epic 1 of `docs/superpowers/specs/2026-10-28-product-design.md`, "Epics": "An empty application all the way to the server: CI/CD, deployment to the subdomain, one-time code sign-in with the time zone taken from the browser, sign out and sign out everywhere, the translation setup with English, Polish and Russian, the Privacy and Terms pages, an empty "no habits yet" screen, the daily database backup and one restore from it. Measures the availability, API latency, data loss and recovery SLOs. Retires the deployment and authentication risks." Also: the product design's "Story map" (epic 1), "Sign-in and account", "Interface language", and the NFR and SLO rows with Epic = 1; `docs/superpowers/specs/2026-10-30-email-service-design.md`; `docs/superpowers/specs/2026-10-30-legal-pages-design.md`; `docs/superpowers/specs/2026-10-30-external-constraints-check.md`.

Domain terms are those of `docs/domain/ubiquitous-language.md`. The NFR and SLO references point to the tables of the product design. The story IDs are those of the product design's story map; this spec owns their wording and priorities from now on.

## Clarifications

### Session 2026-10-30

- Q: How do the stories name a person who is not signed in (a new person, or a user who signed out), given that "visitor" had no term in the glossary? → A: "Visitor" is added to `docs/domain/ubiquitous-language.md`, Identity: a person using the app in a browser without a session; a user with no session in a browser is a visitor in that browser. US-1.1, US-1.2, US-1.7 and US-1.8 use it.
- Q: Does a locale change made on one device reach the user's other open devices, and when? → A: Yes, through Live Updates: the context map gains "Identity → Live Updates: Locale changed", and the open page shows the new language without a reload; that delivery belongs to epic 3. In epic 1 the other devices show the new language at their next page load.
- Q: How is the restored database checked against the backup, and where is its time recorded? → A: Every backup carries the row count and a checksum of the content of every table, taken with it; the restore passes when it ends without errors and both match for every table. Its time runs from the start of the restore to the passed check and is recorded in the project ledger at the ledger commit of epic 1.
- Q: What happens at the first sign-in if the browser reports no time zone, or one the system does not know? → A: The user's time zone is UTC, and the main screen says that the browser's time zone was not recognized and UTC is used, for as long as that UTC stands (changing the time zone is epic 8). `docs/domain/ubiquitous-language.md`, "Time zone" (Identity), says so.
- Q: What does the spec promise about a session that is not a Remembered session when the browser restores session cookies on restart? → A: Its cookie has no expiry and ends with the browser session; a browser that restores its session on restart keeps the cookie, and the session lives until it expires or is revoked. Accepted risk; the Terms already say to sign in with "Remember me" only on trusted devices.
- Q: What does each of the 100 users do in the load test? → A: Each user signs in once with a one-time code before measuring starts; then, for 10 minutes, opens the main screen with a pause of 1 to 5 seconds, chosen at random, before each request, and switches the language on every 10th request. The pause models a person reading the screen: the test checks the expected use of about 100 users (NFR-11), not the server's capacity, which no requirement asks for. Switching the language is the one change a signed-in user can make in this epic, rarer than opening a screen, so it is every 10th request. 10 minutes at an average pause of 3 seconds give about 20,000 measured requests, about 2,000 of them language switches: enough for a stable 95th percentile.
- Q: What checks the API latency SLO (SC-008) before merge, when no monthly value exists yet? → A: An automated test shows that a request's timing reaches the metrics, aggregated, with no IP address and no user id; after the branch deployment, the developer reads from the server's metrics the 95th percentile since the deployment, and the value is recorded in the project ledger at the ledger commit of epic 1.
- Q: What is the stored record of a code request called, given that "code request" and "limit record" had no term in the glossary? → A: "Code request" is added to `docs/domain/ubiquitous-language.md`, Identity: a request for a one-time code for an email, from an IP address. It counts against the limits when the code is sent or the result is unclear; a request refused by a limit or by the email service does not count. Its email and IP address are kept 24 hours. "Limit records" becomes "code requests".
- Q: Is the load test (FR-040) one of the checks of every pull request? → A: Yes: it is a required check of every pull request; a load test that misses SC-006 blocks the pull request as a failing test does.

## User Scenarios & Testing *(mandatory)*

### User Story US-1.1 - Request a one-time code (Priority: P1)

As a visitor, I want to request a one-time code to my email, so that I can sign in without a password.

The sign-in page asks for an email and has a "Remember me" checkbox, a language switcher, and links to the Privacy and Terms pages. After a code is sent, the page shows how many code requests for this email are left; a refusal says why and when to try again.

**Why this priority**: Nobody can use the product without signing in, and the one-time code email is the authentication risk epic 1 retires (anti-goal 4: a reviewer must be able to sign in within a couple of minutes).

**Independent Test**: In development mode, request a code for a new email: the code email appears in the development log, in the language of the page, and the page shows "4 of 5 left". Then request codes until each limit refuses.

**Acceptance Scenarios**:

1. **Given** the sign-in page, **When** an email is entered and a code is requested, **Then** a code email with a 6-digit one-time code is sent to that email in the language of the page, and the page shows how many code requests for this email are left in the current 24-hour window (for example "4 of 5 left").
2. **Given** an email that has had 5 code requests in the last 24 hours, **When** another code is requested for it, **Then** the request is refused, the page says the per-email limit is reached and when the next code can be requested (the moment the oldest request in the window leaves it), no email is sent, and the refused request does not count against either limit.
3. **Given** an IP address (for IPv6, a /64 prefix) that has made 10 code requests in the last 24 hours, **When** another code is requested from it, **Then** the request is refused, the page says the per-IP limit is reached and when the next code can be requested, and no remaining count is shown.
4. **Given** the email service refuses to send (its daily limit is used up, or it is unavailable), **When** a code is requested, **Then** the page says in plain words that the code could not be sent, why, and when to try again if that is known; no new code exists, an earlier code that is still valid stays valid, and the request does not count against either limit.
5. **Given** a valid earlier code, **When** a new code for the same email is sent, **Then** the earlier code no longer signs in.
6. **Given** the email service gives no clear result (a timeout, a lost response), **When** a code is requested, **Then** the request counts as sent: the new code is valid, earlier codes are invalidated, and the request counts against the limits.

---

### User Story US-1.2 - Enter the code and be signed in (Priority: P1)

As a visitor, I want to enter the code and be signed in, with an account created on my first sign-in and my time zone taken from the browser, so that I can start right away.

**Why this priority**: Together with US-1.1 it is the sign-in; every later epic needs a signed-in user.

**Independent Test**: In development mode, request a code for a new email, read it from the development log, enter it: the main screen opens, and the new user has the browser's time zone and the page's language as their locale. Repeat with the same email in other letter case: the same user signs in.

**Acceptance Scenarios**:

1. **Given** a code just sent to an email that no user has, **When** the correct code is entered within 10 minutes, **Then** the account is created, the browser's time zone is stored with the user, the current language becomes the user's locale, a session is opened, and the main screen (US-1.6) is shown.
2. **Given** a code sent to the email of an existing user, **When** the correct code is entered, **Then** a session is opened for that user; their stored time zone and locale do not change.
3. **Given** a user signed up as `Ann@example.com`, **When** a code is requested and entered for `ann@example.com`, **Then** the same user is signed in.
4. **Given** a valid code, **When** a wrong code is entered, **Then** sign-in is refused; **and when** 5 entry attempts of that code have been used, **Then** the code no longer signs in, even if correct, and a new code has to be requested.
5. **Given** a code sent more than 10 minutes ago, or a code already used, **When** it is entered, **Then** sign-in is refused.
6. **Given** the sign-in form, **Then** "Remember me" is unchecked; **When** the user ticks it and signs in, **Then** the session is a Remembered session.
7. **Given** a code sent to an email that no user has, and a browser that reports no time zone or one the system does not know, **When** the correct code is entered, **Then** the account is created with the time zone UTC, and the main screen says that the browser's time zone was not recognized and UTC is used.

---

### User Story US-1.7 - Read the Privacy and Terms pages before signing in (Priority: P1)

As a visitor, I want to read what data the app stores and how to have it deleted, and the terms of the service, before I sign in.

**Why this priority**: The product handles personal data from its first sign-in; the Privacy page (GDPR art. 13) and the Terms page must be in place before sign-in is open to anyone.

**Independent Test**: Without signing in, open both pages from the sign-in page and check every item they must state, in each of the three languages.

**Acceptance Scenarios**:

1. **Given** a visitor, **When** they open the Privacy page or the Terms page from the sign-in page, **Then** the page is shown without signing in and states every item listed in FR-030 or FR-031.
2. **Given** the sign-in page, **Then** under the sign-in button it says "By signing in you accept the Terms and acknowledge the Privacy notice", with links to both pages, and there is no acceptance checkbox.
3. **Given** a signed-in user, **Then** every signed-in screen links to both pages.
4. **Given** either page in Polish or Russian, **Then** it says that the English version prevails if the versions differ.
5. **Given** the Privacy page, **Then** it says how to have the data deleted: on request to the contact email on the page.

---

### User Story US-1.3 - Stay signed in (Priority: P2)

As a user, I want to stay signed in while I keep using the app, across browser restarts if I choose "Remember me", so that I do not sign in every day.

**Why this priority**: Without it every visit spends a code email; it is not needed to prove sign-in works.

**Independent Test**: Sign in with and without "Remember me", restart the browser with a fresh browser session (no session restore), and check whether the session survives; move the clock past 30 days without activity and check that the session has expired.

**Acceptance Scenarios**:

1. **Given** a session opened without "Remember me", **Then** its cookie has no expiry; **When** the browser is closed and opened again with a fresh browser session (no session restore), **Then** the user is no longer signed in.
2. **Given** a Remembered session, **When** the browser is closed and opened again, **Then** the user is still signed in.
3. **Given** a session with a request made by the user 29 days ago, **When** the user makes a request, **Then** the user is still signed in and the 30 days start again.
4. **Given** a session without any request by the user for 30 days, **When** the user makes a request, **Then** the session has expired and the user is asked to sign in.

---

### User Story US-1.4 - Sign out (Priority: P2)

As a user, I want to sign out on this device.

**Why this priority**: Needed on a shared or borrowed device; sign-in works without it.

**Independent Test**: Sign in on two browsers, sign out on one: that browser is signed out, the other still signed in.

**Acceptance Scenarios**:

1. **Given** a signed-in user, **When** they sign out, **Then** the current session is revoked and the next request from that browser is not signed in.
2. **Given** the same user signed in on another browser, **When** they sign out on the first, **Then** the other browser stays signed in.

---

### User Story US-1.5 - Sign out everywhere (Priority: P2)

As a user, I want to sign out everywhere, so that a lost device loses access at once.

**Why this priority**: The defence against a lost device; sign-in works without it.

**Independent Test**: Sign in on two browsers, sign out everywhere on one: the next request from either browser is not signed in.

**Acceptance Scenarios**:

1. **Given** a user signed in on browsers A and B, **When** they sign out everywhere on A, **Then** every session of the user is revoked, and the next request from A or from B is not signed in.
2. **Given** a Remembered session on B, **When** the user signs out everywhere on A, **Then** B is not signed in after a browser restart either.

---

### User Story US-1.6 - See that there are no habits yet (Priority: P2)

As a new user, I want to see that I have no habits yet, so that I know where to start.

**Why this priority**: It is the screen sign-in lands on, and the screen every later epic fills; it carries no domain logic yet.

**Independent Test**: Sign in as a new user: the main screen says there are no habits yet and offers sign out, sign out everywhere, the language switcher and the links to the Privacy and Terms pages.

**Acceptance Scenarios**:

1. **Given** a user who has just signed in, **Then** the main screen says that there are no habits yet.
2. **Given** the main screen at a viewport width of 360 px, **Then** every action is reachable and the page has no horizontal scroll.

---

### User Story US-1.8 - Use the app in my language (Priority: P3)

As a visitor or a user, I want the app in English, Polish or Russian, and to switch the language on any page, so that I can read it in my own language.

This story is new in this spec: the legal pages design made the interface English, Polish and Russian from epic 1, after the story map was frozen.

**Why this priority**: English alone makes every other story usable; the translation setup must exist from epic 1 so that no later epic writes a string outside it.

**Independent Test**: Open the sign-in page with the browser preferring Polish, then switch to Russian and request a code: the page changes without a reload, the code email is in Russian, and the remaining count uses the Russian plural form.

**Acceptance Scenarios**:

1. **Given** a visitor whose browser prefers Polish, **When** they open any page, **Then** it is in Polish; **given** a browser that prefers none of the three languages, **Then** it is in English.
2. **Given** any page, **When** the language is switched on it, **Then** that page shows the new language at once, without a page reload.
3. **Given** a visitor who switched the language, **When** they come back later in the same browser, **Then** the page is in the chosen language.
4. **Given** the page is in Russian, **When** a code is requested, **Then** the code email is in Russian.
5. **Given** a signed-in user, **When** they switch the language, **Then** their locale changes to the chosen language.
6. **Given** counts of 1, 2 and 5 in Polish and Russian, **Then** each is shown with the plural form of that language.
7. **Given** a user signed in on browsers A and B, **When** they switch the language on A, **Then** B shows the new language at its next page load.

---

### Edge Cases

- A new code is requested while an earlier one is valid, and the email service refuses the new send: the earlier code stays valid (US-1.1, scenario 4).
- A code request for an email that never becomes a user leaves nothing behind once the retention periods of FR-033 have passed.
- The same user signs in on several browsers: each sign-in opens its own session; signing out ends only the current one (US-1.4).
- A session expires or is revoked while a page is open: the next request is answered as not signed in, and the page asks the user to sign in.
- The browser reports no time zone, or one that the system does not know, at the first sign-in: the user's time zone is UTC and the main screen says so (FR-009; US-1.2, scenario 7).
- A browser restores its session on restart ("continue where you left off"): the cookie of a session that is not a Remembered session survives, and the session lives until it expires or is revoked. Accepted risk: the product cannot tell a restored browser session from a running one; the Terms say to sign in with "Remember me" only on trusted devices (FR-031).
- The user signs in at a later time on a browser whose language differs from their locale: the interface follows the stored locale (see Assumptions).
- Someone requests 5 codes for another person's email: that email cannot request a code for 24 hours. Accepted risk (email service design, "Accepted risk").
- The email service's daily limit is used up by code requests for many emails from many IP addresses: nobody can sign in until it resets; users who already have a session are not affected. Accepted risk (email service design, "Accepted risk").

## Requirements *(mandatory)*

### Functional Requirements

**Sign-in**

- **FR-001**: Any email MUST be able to request a one-time code; there is no separate registration, and every code request sends an email whether or not a user with that email exists. The sign-in form has a "Remember me" checkbox, unchecked by default.
- **FR-002**: One-time codes MUST be 6 digits, valid 10 minutes, with at most 5 entry attempts per code, stored only as a hash, and used once. Requesting a new code MUST invalidate earlier ones, once the new code is sent. (NFR-7)
- **FR-003**: The system MUST allow at most 5 code requests per email and 10 per IP address (per /64 prefix for IPv6; for IPv4 the address) in any 24 hours, a sliding window. The limits apply in every mode; their values are configuration, so tests can set them. (NFR-7)
- **FR-004**: A request refused by a limit, or refused by the email service, MUST NOT count against either limit. A request with an unclear result (a timeout, a lost response) MUST count as sent: the new code is valid, earlier codes are invalidated, the request counts against the limits. (NFR-7)
- **FR-005**: After a code is sent, the user MUST see how many code requests for this email are left in the current 24-hour window. A refusal by the per-email limit MUST say so and when the next code can be requested (the moment the oldest request in the window leaves it). A refusal by the per-IP limit MUST say so and when the next code can be requested, without a remaining count. A refusal by the email service MUST say that the code could not be sent and why, in plain words, and when to try again if that is known. (NFR-7)
- **FR-006**: The code email MUST be sent from `mail.cardlane.eu`, which has its own SPF, DKIM and DMARC records; the records of `cardlane.eu` itself (SPF `-all`, DMARC `p=reject` with strict alignment) stay unchanged. The code email MUST arrive within 1 minute and land in the Gmail inbox, not spam. (NFR-8)
- **FR-007**: The code email MUST be in the language of the page it was requested from. Open and click tracking MUST be off for the code email.
- **FR-008**: A correct, unexpired, unused code MUST open a session. The first successful sign-in for an email MUST create the user. Emails are compared case-insensitively; one user per email.
- **FR-009**: At the first sign-in, the system MUST store the browser's IANA time zone with the user, and the current language MUST become the user's locale. If the browser reports no time zone, or one the system does not know, the system MUST store UTC, and the main screen MUST say that the browser's time zone was not recognized and UTC is used, for as long as that UTC stands.
- **FR-010**: In development mode, the code email MUST be written to the development log instead of being sent, and no email service is used. Production MUST send real email and MUST have no test bypass of sign-in.
- **FR-011**: In production, one-time codes MUST never appear in logs. (NFR-10)

**Sessions**

- **FR-012**: A session MUST be stored on the server; the `HttpOnly`, `Secure` cookie carries only its opaque id. (NFR-9)
- **FR-013**: A session MUST be sliding: it expires after 30 days without activity, and any activity extends it. Activity is a request the user makes; an open connection alone is not activity. (NFR-9)
- **FR-014**: The cookie of a session that is not a Remembered session MUST have no expiry, so it ends with the browser session; a browser that restores its session on restart keeps it (Edge Cases). The cookie of a Remembered session MUST persist. Ticking "Remember me" is the user's consent to the persistent cookie. (NFR-9)
- **FR-015**: Sign out MUST revoke the current session; sign out everywhere MUST revoke every session of the user. Revocation and expiry MUST take effect on the next request. Closing open connections belongs to epic 3. (NFR-9)
- **FR-016**: The product MUST be served over HTTPS only. (NFR-9)
- **FR-017**: Every request MUST see only the signed-in user's data. An automated test MUST cover every endpoint of this epic: user B can neither read nor change user A's data. The live-update channel belongs to epic 3. (NFR-6)

**Main screen**

- **FR-018**: After sign-in, the main screen MUST say that the user has no habits yet, and offer sign out, sign out everywhere, the language switcher, and links to the Privacy and Terms pages.
- **FR-019**: At a viewport width of 360 px, every action of every screen of this epic (the sign-in page, the main screen, the Privacy page, the Terms page) MUST be reachable, and no page has a horizontal scroll; checked by the end-to-end tests at that width. (NFR-13)

**Interface language**

- **FR-020**: The interface MUST be in English, Polish and Russian. Every user-visible string, the code email and the Privacy and Terms pages included, MUST come from translation files, with plural rules handled by the translation format. Adding a language MUST mean adding a file, with no code change.
- **FR-021**: Before sign-in, the language MUST follow the browser's preferred languages, falling back to English. A language switcher MUST be on every page, the sign-in page included; before sign-in the choice is kept in the browser.
- **FR-022**: Switching the language MUST apply at once, without a page reload, on the page where it is switched.
- **FR-023**: After sign-in, the switcher MUST change the user's locale. The user's other open devices MUST show the new language at their next page load. Delivering the change at once to the user's open connections, without a reload, belongs to epic 3 (`docs/domain/ubiquitous-language.md`, context map, "Locale changed").

**Privacy and Terms**

- **FR-024**: The Privacy page and the Terms page MUST be public and readable before sign-in, and linked from the sign-in page and from every signed-in screen.
- **FR-025**: Under the sign-in button the page MUST say "By signing in you accept the Terms and acknowledge the Privacy notice", with links to both pages; there is no acceptance checkbox.
- **FR-026**: The Polish and Russian versions of each page MUST say that the English version prevails if the versions differ.
- **FR-027**: Every page MUST load everything from `habits.cardlane.eu`: no fonts, scripts, styles or images from other servers.
- **FR-028**: The contact email and the developer's full name on both pages are supplied by the developer in this epic.
- **FR-029**: A record of processing under GDPR art. 30 MUST be kept in the repository at `docs/legal/record-of-processing.md`, with the same purposes, data, recipients, transfers and retention as the Privacy page.
- **FR-030**: The Privacy page MUST state the items of `docs/superpowers/specs/2026-10-30-legal-pages-design.md`, "Privacy page", 1 to 15: the controller (the developer's full name and a contact email); no data protection officer; the purposes and legal basis (the account, habits and check-ins, sessions, the session cookie, the one-time code and its email: art. 6(1)(b); IP addresses in logs and in code request limits: art. 6(1)(f)); the legitimate interest; the closed list of recipients (the hosting provider in Germany, EU, and the email service in the USA, each named on the page); the transfer outside the EU and its safeguards; the retention periods (account data, habits and check-ins until the account is deleted; the periods of FR-033; deleted data stays in backups for up to 7 days; the email service keeps the code email, address and content, for 30 days); the rights and how to use them; the complaint to the President of the Personal Data Protection Office or the supervisory authority of the user's own EU country; that an email is required to sign in; no automated decisions; the session cookie and the language choice kept in the browser, and that there are no other cookies, no analytics, no ads; that habits and check-ins are never shown to another user; the request not to put health or other sensitive data in habit names; the effective date and how changes are announced. It MUST say how to have the data deleted: on request to the contact email, until account deletion exists (epic 8).
- **FR-031**: The Terms page MUST state the items of `docs/superpowers/specs/2026-10-30-legal-pages-design.md`, "Terms page": who provides the service (the developer's full name and contact email, no home address); what the service is (a free habit tracker, a non-commercial showcase project); the technical requirements (a current browser with JavaScript and cookies, an email inbox); that users must not enter unlawful content; how the agreement starts and ends; complaints and reports of unlawful content to the contact email, answered within 14 days; the risks (whoever controls the user's email inbox can sign in; sign in only on trusted devices with "Remember me"); "as is", free, no guarantee of availability or of keeping data beyond the backup policy, liability limited as far as the law allows, never for intentional harm; that sign-in can be unavailable for up to 24 hours if the email service's daily limit is used up; how changes to the Terms are announced, with an effective date; Polish governing law; a link to the Privacy page.
- **FR-032**: Logs MUST be kept for 7 days. (NFR-10)
- **FR-033**: Retention periods MUST be deletion deadlines: sessions until sign out, sign out everywhere, or 30 days without activity; one-time codes valid 10 minutes; code requests, with their email and IP address, 24 hours; logs 7 days. The system MUST delete expired codes, code requests, sessions and logs at most one day after their period ends. (NFR-10)

**Delivery and operations**

- **FR-034**: The product MUST run on the developer's server at `habits.cardlane.eu`, and reach it through the three delivery workflows of the process document, "Epic 1 — walking skeleton": checks of every pull request (tests, the load test of FR-040, static analysis, test integrity), deployment of a pull request branch on the developer's request, with a database backup before migrations, and deployment of the main branch after merge.
- **FR-035**: The database MUST be backed up daily and before every migration; backups MUST be kept for 7 days. Every backup MUST carry the row count and a checksum of the content of every table, taken with it. The backups are kept on the server itself: they cover failures that leave the server's disk intact (a failed migration, an application defect, data deleted by mistake), not the loss of the server, which is an accepted risk. (NFR-5, SLO Data loss)
- **FR-036**: A restore from backup MUST be done at least once, on the server, into a separate temporary database next to the live one, and then removed; the live database is not touched. The restore passes when it ends without errors and, for every table, the row count and the content checksum of the restored database equal those the backup carries (FR-035). Its time runs from the start of the restore to the passed check and is recorded in the project ledger at the ledger commit of epic 1. (NFR-5, SLO Recovery)
- **FR-037**: The product MUST have a health endpoint that answers with success only when the application can reach the database, and returns no personal data. (SLO Availability)
- **FR-038**: Availability MUST be measured by an external check of the health endpoint every 5 minutes, run from outside the server. A check passes when the endpoint answers with success over HTTPS within 10 seconds; a timeout or a TLS error fails it. The monthly value is the share of passed checks among the checks that ran; a month in which no check ran has no value. A failed check notifies the developer. The scheduled check starts once epic 1 is merged; before merge, the branch deployment runs the same check once against the server. (SLO Availability)
- **FR-039**: Request timing MUST be kept as metrics for at least a month, live-update connections excluded. The metrics MUST be aggregated and hold no IP address and no user id; an automated test shows that a request's timing reaches them in that form. After the branch deployment, the developer reads from the server's metrics the 95th percentile since the deployment; the value is recorded in the project ledger at the ledger commit of epic 1. (SLO API latency)
- **FR-040**: A load test with 100 users MUST keep the API latency SLO, run with the application and its database limited together to the server's CPU and memory (2 vCPU, 4 GB). Each user signs in once with a one-time code before measuring starts; then, for 10 minutes, opens the main screen with a pause of 1 to 5 seconds, chosen at random, before each request, and switches the language on every 10th request. The pause models a person reading the screen: the test checks the expected use of about 100 users (NFR-11), not the server's capacity. It is a required check of every pull request (FR-034): missing SC-006 blocks the pull request as a failing test does. It runs in development mode, where no email is sent. Two open connections per user are added in epic 3. (NFR-11, SLO API latency)

### Key Entities *(include if feature involves data)*

- **Visitor**: a person using the app in a browser without a session: not signed up yet, or signed out. The system keeps no record of a visitor; their language choice is kept in the browser.
- **User**: a person signed up to the app, identified by their email; holds the email, the time zone and the locale. Created on the first successful sign-in.
- **Email**: the address a user signs in with; compared case-insensitively, unique among users.
- **Code request**: a request for a one-time code for an email, from an IP address. Counts against the limits of FR-003 when the code is sent or the result is unclear; a request refused by a limit or by the email service does not count (FR-004). Its email and IP address are kept 24 hours.
- **One-time code**: a 6-digit code sent by email; valid 10 minutes, at most 5 entry attempts, stored only as a hash, used once.
- **Session**: the state of being signed in on one browser; stored on the server, sliding, expires after 30 days without activity, revoked by sign out or sign out everywhere.
- **Remembered session**: a session opened with "Remember me" ticked; its cookie persists across browser restarts.
- **Time zone**: the user's IANA time zone, taken from the browser on the first sign-in; UTC if the browser reports none, or one the system does not know.
- **Locale**: the language of the interface and of the one-time code email: English, Polish or Russian.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A person with a Gmail address signs in on the server, from opening the sign-in page to the main screen, within 2 minutes; the code email arrives within 1 minute and lands in the inbox, not spam. Checked by the developer with a real email after the branch deployment. (NFR-8)
- **SC-002**: Every value of FR-002 to FR-005 (6 digits, 10 minutes, 5 entry attempts, 5 code requests per email and 10 per IP address in any 24 hours, the /64 prefix, what counts against the limits) is asserted by an automated test. (NFR-7)
- **SC-003**: For every endpoint of this epic, an automated test shows that user B can neither read nor change user A's data. (NFR-6)
- **SC-004**: Automated tests assert the session rules: the cookie is `HttpOnly` and `Secure` and holds only the session id; the session expires after 30 days without activity and is extended by activity; the cookie of a session that is not a Remembered session has no expiry and is gone in a fresh browser session, the cookie of a Remembered session persists; sign out and sign out everywhere take effect on the next request; plain HTTP serves no page. (NFR-9)
- **SC-005**: An automated test shows that no one-time code is written to the production logs; logs older than 7 days, and expired codes, code requests and sessions, are deleted at most one day after their period ends. (NFR-10)
- **SC-006**: The load test of FR-040, with 100 users within 2 vCPU and 4 GB, keeps 95% of the requests measured in its 10 minutes answered within 300 ms. (NFR-11)
- **SC-007**: Availability is at least 99% per calendar month (about 7 hours of downtime a month), measured as in FR-038; before merge, the one check run by the branch deployment passes. (SLO Availability)
- **SC-008**: 95% of requests are answered within 300 ms, server side, live-update connections excluded, per calendar month, from the metrics of FR-039; before merge, the automated test of FR-039 passes, and the 95th percentile read from the server after the branch deployment is in the project ledger. (SLO API latency)
- **SC-009**: At most 24 hours of data can be lost in a failure that leaves the server's disk intact: there is a backup from the last 24 hours and one taken before each migration. (NFR-5, SLO Data loss)
- **SC-010**: The one restore from backup, done as in FR-036, passes its check (row count and content checksum of every table equal those the backup carries) and takes at most 4 hours from the start of the restore to the passed check; its time is in the project ledger. (NFR-5, SLO Recovery)
- **SC-011**: The end-to-end tests at a viewport width of 360 px reach every action of the sign-in page, the main screen, the Privacy page and the Terms page, with no horizontal scroll. (NFR-13)
- **SC-012**: Every user-visible string of this epic exists in all three languages, checked by an automated test; switching the language changes the page without a reload; Polish and Russian counts use their plural forms.
- **SC-013**: Loading any page of this epic makes no request to a server other than `habits.cardlane.eu`, checked by an end-to-end test.
- **SC-014**: After the branch deployment, the subdomain responds over HTTPS and a real sign-in with a one-time code works on the server.

## Assumptions

- The NFR and SLO rows with Epic = 1 are NFR-5, NFR-6, NFR-7, NFR-8, NFR-9, NFR-10, NFR-11 and the SLOs Availability, API latency, Data loss and Recovery. NFR-13 ("every epic with a screen") is included because epic 1 has screens.
- Parts of those rows that name a later epic are out of scope here: closing open connections on revocation and expiry (NFR-9), the live-update channel (NFR-6) and delivering a locale change to the user's open connections (FR-023) belong to epic 3, two open connections per user in the load test (NFR-11) to epic 3, account deletion (NFR-10) to epic 8.
- Out of scope: habits and everything about them (epics 2 and later), changing the time zone and deleting the account (epic 8), closing open connections (epic 3).
- At a later sign-in the stored locale is kept and the interface follows it; only the first sign-in takes the current language as the locale. Likewise the time zone is taken from the browser only at the first sign-in.
- The developer supplies their full name and a contact email for the Privacy and Terms pages, creates the email service account and the DNS records, and points `habits.cardlane.eu` at the server.
- The Privacy and Terms pages refer to the hosting provider and the email service by name; their names are in the legal pages design and are not repeated here, since this spec names no technology.
- "Gmail" in FR-006 and SC-001 is the reviewer's mailbox that NFR-8 names, not a technology of the product; SPF, DKIM and DMARC are the email authentication standards NFR-8 names.
- "The developer" is the person who runs the service, as in the design documents; it is a project role, not a domain term.
- The end-to-end tests and the load test run in development mode, where the code email goes to the development log; nothing automated signs in on the server in epic 1.
- Polish and Russian texts are drafted by the agent and reviewed by the developer (legal pages design, "Languages").
