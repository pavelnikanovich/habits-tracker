# Walking skeleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An empty application all the way to `habits.cardlane.eu`: one-time code sign-in with server-side sessions, sign out and sign out everywhere, three interface languages, the Privacy and Terms pages, the "no habits yet" main screen, and the operations that measure the epic's SLOs.

**Architecture:** An ASP.NET Core 10 modular monolith; Identity is a hexagonal package (`HabitsTracker.Identity.Core` with no references, `HabitsTracker.Identity.Adapters`), composed by `HabitsTracker.Host`, which also serves the Angular 22 single-page application from the same origin. One container image, delivered by GitHub Actions to the VPS behind Caddy.

**Tech Stack:** C# / .NET 10, ASP.NET Core 10, EF Core 10 + Npgsql, PostgreSQL 18.6, Angular 22 + Transloco 8.4 (MessageFormat), the Nordwand Mono 1.5.0 font, xUnit v3, Testcontainers, Vitest, Playwright 1.64 (Chromium and WebKit), k6 2.3, Caddy 2.11, Resend HTTP API, GitHub Actions.

**Spec:** `specs/001-walking-skeleton/spec.md`, with `plan.md`, `data-model.md`, `contracts/http-api.md`, `contracts/ui.md`, `research.md` (R1–R24) and `quickstart.md` of the same folder, as of step 5 reopened for the user interface (2026-11-01). Product-wide interface rules: `docs/superpowers/specs/2026-10-28-product-design.md`, "Interface", NFR-13 to NFR-15. Procedures: `docs/operations/runbook.md`. Domain terms: `docs/domain/ubiquitous-language.md`. Legal page items: `docs/superpowers/specs/2026-10-30-legal-pages-design.md`.

**Execution:** Subagent-driven only (process document, step 8: epic 1). Developer-owned tasks (`Executor: developer`) run in the main session in plan order, never at the same time as a subagent task; the agent proposes the code and the developer accepts it line by line, with the Explanatory or Learning output style and terse styles off. Manual tasks (`Executor: manual`) run in the main session with the developer and skip the per-task reviewer.

## Global Constraints

Copied from the constitution, `plan.md`, `spec.md` and `data-model.md`. Every task's requirements include this section. Each spec or data-model value names the task whose test asserts it.

**Stack and project (constitution, plan.md):**

- C# on .NET 10 (SDK 10.0.403, runtime 10.0.14, LTS); TypeScript 6.0 with Angular 22.2 on Node.js 24 LTS. Versions as in research.md, "Versions"; take the latest patch of the same minor.
- No dependency beyond research.md, "Dependencies and their cases". A new one stops the task and goes to the developer with its case.
- No schema beyond data-model.md, "Storage". A change goes to the developer as a change to data-model.md first.
- `HabitsTracker.Identity.Core` has no `PackageReference`, `FrameworkReference` or `ProjectReference`, and its sources use neither `System.ComponentModel.DataAnnotations` nor `System.Text.Json`; the folders of `HabitsTracker.Identity.Adapters` name no type of one another — Task 1 guards all three.
- Backend dependencies are audited at every restore: `NuGetAuditMode=all`, `NuGetAuditLevel=high`; with `TreatWarningsAsErrors` a high or critical vulnerability fails the build (research R19, P1) — Task 1.
- Backend: `TreatWarningsAsErrors`, `AnalysisLevel` `latest-recommended`, nullable reference types, CA1502 threshold 25; `SonarAnalyzer.CSharp` with only S4144 and S1192 on. Frontend: `ng lint --max-warnings 0`, ESLint `complexity` 20, `eslint-plugin-sonarjs` with only `no-identical-functions` and `no-duplicate-string` on.
- No mocking library, no assertion library: hand-written fakes of the core's ports, xUnit assertions.
- Every user-visible string, the code email and the Privacy and Terms texts included, lives in `frontend/public/i18n/{en,pl,ru}.json`; no other translation files (FR-020).
- Nothing is loaded from another server: no fonts, scripts, styles or images from outside the origin (FR-027). The only font is Nordwand Mono 1.5.0, fetched at build time by `frontend/scripts/fetch-fonts.sh` against the SHA-256 sums of research R24 and served by the application — Task 6.
- Every page follows `contracts/ui.md`: component CSS uses only `var(--…)` tokens; every message goes through `app-message`; no field takes the focus when a page opens; a button is disabled while its request runs; each page's `h1` has `tabindex="-1"` and takes the focus after a navigation — Task 6 creates them, every later page task uses them.
- Migrations and the HTTP API only expand (constitution VI). No secret in the repository (constitution VIII): the code hash key, the Resend API key and the database password come from configuration supplied at deployment; development generates its code hash key at start and its database has no password.
- Logs hold only IP addresses as personal data: never an email, a user id, a session token, a one-time code or a body (research R20).
- Domain names in code are the glossary terms: `User`, `Email`, `OneTimeCode`, `CodeRequest`, `Session`, `TimeZone`, `Locale`; "Remembered session" is `Session.Remembered`. Technical names from data-model.md and research R23: `IpAddressPrefix`, `CodeHash`, `SessionToken`, `OneTimeCodeValue`, `CodeRequestLimits`, `IdentityContextOptions`, `CodeRequestCounts`, `DomainException`, `DomainError`, `FromStorage`.

**Values, each with the task that asserts it:**

- One-time code: 6 decimal digits, leading zeros kept — Task 2.
- One-time code valid 10 minutes; an entry at or after `expires at` is refused — Task 11 (core), Task 12 (API).
- At most 5 entry attempts per code; the 5th wrong entry, and any entry after 5, requires a new code — Task 11, Task 12.
- Code stored only as HMAC-SHA-256 over the email and the code, keyed by a 32-byte server secret — Task 2 (hash), Task 3 (no code column).
- A code is used once — Task 11, Task 12.
- A new code invalidates the earlier one only once sent or unclear; a refused send keeps the earlier code valid — Task 7.
- At most 5 code requests per email and 10 per IP address prefix in any 24 hours, sliding — Task 7 (policy), Task 10 (API with default configuration).
- IP address prefix: IPv4 the whole address (/32), IPv6 the /64 prefix, IPv4-mapped IPv6 counts as its IPv4 address — Task 2.
- A request refused by a limit or by the email service is not stored and does not count; an unclear result counts — Task 7.
- Remaining after a send = 5 − (counted for the email + 1); the response carries `limit: 5` — Task 7, Task 10.
- When both limits are reached, the per-email refusal is reported — Task 7.
- Next possible request (`retryAt`) = oldest counted request in the window + 24 hours — Task 7.
- Resend: `500`, a timeout after 10 seconds, or a connection lost after sending is unclear; `429 daily_quota_exceeded` is refused with `retryAt` next 00:00 UTC — Task 8.
- Code email from `no-reply@mail.cardlane.eu`, text only, in the page's locale — Task 8.
- Email: at most 254 characters, checked first, then the WHATWG pattern of research R18; ASCII only, no whitespace, no trimming; lower-cased — Task 2 (core), Task 10 (the page's copy and its parity).
- Locale code: `^[a-z]{2}$` and a translation file; a translation file with another name stops the start — Task 2, Task 4.
- Code hash key: exactly 32 bytes — Task 2; required outside Development, generated at start in Development — Task 10.
- User id: a version 7 UUID — Task 2.
- Request bodies under `/api`: at most 16 KB (`413`), `application/json` only (`415`), both `invalid-request`; unknown path `404` `not-found`; wrong method `405` `invalid-request`; unexpected failure `500` `internal-error`; every problem carries `traceId` — Task 9, Task 12 (`traceId` in the logs).
- Session token: 32 random bytes, base64url without padding (43 characters); session id = SHA-256 of the token — Task 2.
- Cookie `session`: `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`, no `Domain` — Task 12.
- A session that is not a Remembered session: cookie without `Expires` and `Max-Age`; a Remembered session: `Max-Age=2592000` (30 days), renewed on every response that extends the session — Task 12.
- Session expires after 30 days without a request by the user (expired at exactly 30 days); any request extends it — Task 11 (core), Task 12 (API).
- Sign out and sign out everywhere take effect on the next request — Task 14, Task 15.
- Retention: codes deleted once expired or exhausted, code requests 24 hours after `requested at`, sessions 30 days after last activity, request timings after 13 months; cleanup runs hourly — Task 14 (core), Task 18 (hourly job).
- Logs 7 days: journald `MaxRetentionSec=7day`, `MaxFileSec=1day` — Task 18.
- Latency bucket upper bounds (ms): 10, 25, 50, 75, 100, 150, 200, 250, 300, 400, 500, 750, 1000, 2000, 5000, +∞ — Task 17.
- Backups daily and before every migration, kept 7 days; a backup 8 days old is deleted — Task 19 (script), Task 24 (timer and deployment).
- Restore from backup at most 4 hours, measured once on the server — Task 24 (measurement, recorded at step 13).
- `GET /health`: `200` `Healthy` when the database is reachable, `503` `Unhealthy` otherwise — Task 1.
- Availability check every 5 minutes, `curl --fail --max-time 10` — Task 25.
- Load test: 100 users, signed in before measuring, 10 minutes, a uniformly random 1–5 s pause before each request, every 10th request switches the language, p95 of `http_req_duration` < 300 ms; app 1 vCPU / 2 GB and database 1 vCPU / 2 GB — Task 21.
- Widths: every screen as a phone with touch at 320 px and 360 px and on a desktop at 1280 px, every action reachable, no horizontal scroll, in both themes; the main path also in WebKit at 360 px — Task 5 (projects), Task 6 (shell), Task 10 and Task 13 (sign-in and main), Task 15 (main actions), Task 16 (Privacy and Terms).
- Themes: dark until the person chooses, whatever the browser prefers; `localStorage["theme"]` `light` or `dark`, any other value ignored, a storage failure leaves the page working — Task 6; the choice stays after sign-in — Task 13.
- Contrast: text at least 4.5:1, controls, input borders and the focus outline at least 3:1, in both themes, with the palette of `contracts/ui.md` — Task 6.
- Focus outline 3 px with a 2 px offset on every interactive element; buttons and input fields at least 44 px high; input text at least 16 px — Task 6.
- Keyboard alone: sign in (both steps), switch the language and the theme, sign out and sign out everywhere, open Privacy and Terms — Task 6 (shell), Task 10, Task 13, Task 15, Task 16.
- "Remember me" explained under the checkbox, linked by `aria-describedby` (FR-014) — Task 10.
- The code step at `/sign-in/code`: the email, the checkbox and the remaining count in memory only, never in the URL; Back returns to `/sign-in` with them filled in; opened without them, `/sign-in` — Task 10.
- `retryAt` in the browser's time zone and the page's language, the time alone when it is today, else the date and the time, rounded up to the next minute — Task 10.
- Sign out everywhere only after an on-page confirmation, the focus on "Cancel", `Escape` cancels (FR-015) — Task 15.
- "Session ended" shown only when a request after a successful `GET /api/me` answers `not-signed-in` — Task 15.
- Locales `en`, `pl`, `ru`; before sign-in the browser's preferred languages, falling back to `en` — Task 4.
- Polish and Russian plural forms for 1, 2 and 5 — Task 4.
- Response headers: `Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer` — Task 4; `Strict-Transport-Security: max-age=31536000` — Task 20.
- `http://` serves no page, only a redirect to `https://` — Task 20.
- Caching: `index.html`, the application fallback and `/i18n/*` `no-cache`; hashed build files `public, max-age=31536000, immutable`; `/api/*` and `/health` `no-store` — Task 4.
- A sign-in that carries a valid session cookie revokes that session and issues a new token — Task 11, Task 12.
- Privacy page items 1–15 and Terms page items 1–12 of the legal pages design; complaints answered within 14 days; deleted data stays in backups up to 7 days; the email service keeps the code email 30 days — Task 16.

## Review Focus

The inputs where an error costs most in this epic (process document, "Task plan format"). Each is a negative test that attempts the attack, in the task named.

1. **An expired or reused code.** A code entered at exactly 10 minutes after issue, or a second time after a successful sign-in, must not open a session — Task 11 (table rows), Task 12 (`Reused_code_requires_a_new_code`, `Code_at_ten_minutes_requires_a_new_code`).
2. **Code guessing.** After 5 entries the correct code must not sign in; 10 concurrent wrong entries must count as at most 5 attempts — Task 11 (rows), Task 3 (`Concurrent_attempt_updates_are_not_lost`), Task 12 (`Correct_code_after_five_wrong_entries_requires_a_new_code`).
3. **Email enumeration.** The answers to a code request, and to a code entry, must be the same for an email that has a user and one that has none — Task 10 (`Code_request_answer_does_not_reveal_whether_a_user_exists`), Task 12 (`Sign_in_answer_does_not_reveal_whether_a_user_exists`).
4. **Redirect after sign-in to another domain.** The application has no return-address parameter: after sign-in it always opens `/`, whatever the sign-in URL carries — Task 13 (`sign-in ignores a return address to another domain`).
5. **A code presented for another email.** A code sent to A, entered with email B, must not sign in either user, and must leave A's code valid — Task 2 (`Hash_binds_the_email`), Task 11 (row), Task 12 (`Code_of_another_email_does_not_sign_in`).

Also attacked by tests, though not on the process list (research R19, R24): an email in the URL of the code step (Task 10); a font file whose sum differs from the pinned one (Task 6); a stored theme value other than `light` or `dark` (Task 6); a spoofed `X-Forwarded-For` from outside the proxy network (S6, Task 10); a session cookie set before sign-in (S4, Task 12); a form or `text/plain` post standing in for cross-site request forgery (T1, Task 9); an oversized body (D4, Task 9); a row written past the model (Task 3); a cookie of user B never reading or changing user A's data (Tasks 12 and 15); a Windows time zone id passed as a browser time zone (Task 2).

## Entity and type model

One definition per type for the whole epic, written from `data-model.md` ("Domain model rules" and the aggregates) and research R23. Tasks name these types; a task that needs a change to a signature stops and asks the controller, who updates this section before the next task is dispatched. A design decision a signature does not settle goes back to the step 5 artifacts (process document, step 7).

### Identity core (`backend/src/HabitsTracker.Identity.Core`, namespace `HabitsTracker.Identity.Core`)

How the rules of constitution IV and IX are written here (research R23); the reasons are in `data-model.md`:

- Value objects are `sealed record` classes with a private constructor and get-only properties (no positional syntax, `init` or `required`). `Create(...)` takes input, `FromStorage(...)` takes a stored value; both raise `DomainException`. Not `Restore`: that is a term of Habit Tracking.
- Aggregates are `sealed class`es with `private set`, a private constructor and static factories, `FromStorage` included; they do not override `Equals`, and code compares them by `Id` (a `byte[]` id with `SequenceEqual`).
- Results are `abstract record`s with `sealed record` cases; every `switch` over one ends with `_ => throw new UnreachableException()`.
- A programming error is not a domain error: a `null` argument raises `ArgumentNullException`, an instant whose offset is not zero `ArgumentException`, a method called in a state that forbids it `InvalidOperationException`.
- `OneTimeCodeValue`, `SessionToken` and `CodeHash` override `ToString()` to return `***`.
- Every class of the core is `sealed`. The core has no attribute of `System.ComponentModel.DataAnnotations` or `System.Text.Json`, writes no log and reads no request context (Task 1 guards the attributes).
- In other namespaces `System.TimeZone` collides with `TimeZone`: use `using TimeZone = HabitsTracker.Identity.Core.TimeZone;`.
- `TimeZone` reads the machine's IANA database through `TimeZoneInfo`, and the generators use `RandomNumberGenerator`; neither is a port, so core tests of `TimeZone` rely on the machine's tzdata.

Error model (`data-model.md`, "Error model"):

- `enum DomainError { InvalidEmail, InvalidLocale, InvalidOneTimeCode, InvalidSessionToken, CorruptRecord, InvalidConfiguration }`
- `sealed class DomainException : Exception` with `DomainError Error`; the message names the value, never its content.

Value objects:

| Type | Members |
|---|---|
| `Email` | `const int MaxLength = 254`; `const string Pattern` = the WHATWG pattern of research R18; `static Email Create(string? input)` (`InvalidEmail` unless the length is at most `MaxLength`, checked first, and `Pattern` matches; no trimming); `static Email FromStorage(string value)` (`CorruptRecord` on the same rules or a value not lower-cased); `string Value` (`ToLowerInvariant`) |
| `TimeZone` | `static TimeZone FromBrowser(string? ianaId)` (never throws: a known IANA id is kept, anything else gives `UTC` with `Recognized == false`; known means `TimeZoneInfo.TryFindSystemTimeZoneById` finds it and `HasIanaId` is true); `static TimeZone FromStorage(string id, bool recognized)` (`CorruptRecord` unless `id` is known, and unless `id` is `UTC` when `recognized` is false); `string Id`; `bool Recognized` |
| `Locale` | `const string Pattern = "^[a-z]{2}$"`; `static Locale Create(string? code, ILocales locales)` (`InvalidLocale` unless `Pattern` matches and `locales.IsAvailable(code)`); `static Locale FromStorage(string code)` (`CorruptRecord` unless `Pattern` matches; does not ask `ILocales`); `string Code` |
| `IpAddressPrefix` | `static IpAddressPrefix From(IPAddress address)` (IPv4 → `/32`, IPv6 → `/64`, IPv4-mapped IPv6 → its IPv4 `/32`); `static IpAddressPrefix FromStorage(IPNetwork network)` (`CorruptRecord` unless an IPv4 `/32` or an IPv6 `/64`); `System.Net.IPNetwork Network`; `ToString()` → `"203.0.113.7/32"` |
| `OneTimeCodeValue` | `const string Pattern = "^[0-9]{6}$"`; `static OneTimeCodeValue Generate()` (`RandomNumberGenerator.GetInt32(0, 1_000_000)`, `D6`); `static OneTimeCodeValue Create(string? input)` (`InvalidOneTimeCode` unless `Pattern` matches; no trimming); `string Digits`; `ToString()` → `***` |
| `CodeHash` | `static CodeHash Compute(ReadOnlySpan<byte> key, Email email, OneTimeCodeValue code)` (HMAC-SHA-256 over UTF-8 `email.Value + "\n" + code.Digits`); `static CodeHash FromStorage(byte[] bytes)` (copies; `CorruptRecord` unless 32 bytes); `ReadOnlyMemory<byte> Bytes`; `byte[] ToArray()` (a copy); `bool Matches(CodeHash other)` (`CryptographicOperations.FixedTimeEquals`); `Equals` and `GetHashCode` by content; `ToString()` → `***` |
| `SessionToken` | `static SessionToken Generate()`; `static SessionToken Create(string? cookieValue)` (`InvalidSessionToken` unless 43 characters of the base64url alphabet that decode to 32 bytes); `string Value`; `byte[] ComputeId()` (SHA-256 of the 32 bytes, a new array each call); `ToString()` → `***` |
| `CodeRequestLimits` | `static CodeRequestLimits Create(int perEmail, int perIpAddress, TimeSpan window)` (`InvalidConfiguration` unless all three are above zero); `int PerEmail`, `int PerIpAddress`, `TimeSpan Window`; `static CodeRequestLimits Default` = `Create(5, 10, TimeSpan.FromHours(24))` |
| `IdentityContextOptions` | `static IdentityContextOptions Create(byte[] codeHashKey, CodeRequestLimits limits)` (`InvalidConfiguration` unless the key is 32 bytes; copies it); `ReadOnlyMemory<byte> CodeHashKey`; `CodeRequestLimits CodeRequestLimits`. Not `IdentityOptions`, a type of ASP.NET Core Identity |

Aggregates:

| Type | State | Behavior |
|---|---|---|
| `User` | `Guid Id`, `Email Email`, `TimeZone TimeZone`, `Locale Locale`, `DateTimeOffset CreatedAt` | `static User SignUp(Email, TimeZone, Locale, DateTimeOffset now)` (`Id = Guid.CreateVersion7(now)`); `static User FromStorage(Guid id, Email, TimeZone, Locale, DateTimeOffset createdAt)` (`CorruptRecord` on an empty id); `void ChangeLocale(Locale)` |
| `OneTimeCode` | `Email Email`, `CodeHash CodeHash`, `DateTimeOffset IssuedAt`, `DateTimeOffset ExpiresAt`, `int EntryAttempts`, `bool IsUsed` (in memory only) | `static readonly TimeSpan Lifetime` = 10 min; `const int MaxEntryAttempts = 5`; `static OneTimeCode Issue(Email, CodeHash, DateTimeOffset now)`; `static OneTimeCode FromStorage(Email, CodeHash, DateTimeOffset issuedAt, DateTimeOffset expiresAt, int entryAttempts)` (`CorruptRecord` unless `expiresAt == issuedAt + Lifetime` and `0 ≤ entryAttempts ≤ 5`); `CodeVerification Verify(CodeHash entered, DateTimeOffset now)` (Task 11) |
| `CodeRequest` | `long Id` (0 until stored), `Email Email`, `IpAddressPrefix IpAddressPrefix`, `DateTimeOffset RequestedAt` | `static CodeRequest Create(Email, IpAddressPrefix, DateTimeOffset now)`; `static CodeRequest FromStorage(long id, Email, IpAddressPrefix, DateTimeOffset requestedAt)`; immutable |
| `Session` | `byte[] Id`, `Guid UserId`, `bool Remembered`, `DateTimeOffset CreatedAt`, `DateTimeOffset LastActivityAt` | `static readonly TimeSpan IdleTimeout` = 30 days; `static (Session Session, SessionToken Token) Open(Guid userId, bool remembered, DateTimeOffset now)`; `static Session FromStorage(byte[] id, Guid userId, bool remembered, DateTimeOffset createdAt, DateTimeOffset lastActivityAt)` (`CorruptRecord` unless the id is 32 bytes and `lastActivityAt >= createdAt`); `bool IsExpired(DateTimeOffset now)` (`now >= LastActivityAt + IdleTimeout`); `void RecordActivity(DateTimeOffset now)` (`InvalidOperationException` on an expired session) |

Invariants held outside one aggregate (`data-model.md`): one user per email — the unique index and `IUsers.AddOrGetAsync`; the code request limits under concurrency — `ICodeRequests.LockAsync` with `CodeRequestPolicy`; revocation — `SignOut`, `SignOutEverywhere` and `SignIn` delete sessions. A code is used once inside `OneTimeCode` (`IsUsed`); `IOneTimeCodes.SaveAsync` deletes a used code.

Results, decisions and the read model:

- `enum CodeVerification { SignIn, Incorrect, NewCodeRequired }`
- `sealed record CodeRequestCounts(int ForEmail, DateTimeOffset? OldestForEmail, int ForIpAddressPrefix, DateTimeOffset? OldestForIpAddressPrefix)` — the read model "Code request counts" of `data-model.md`.
- `abstract record CodeRequestDecision` with `Allowed(int RemainingAfterSend)`, `RefusedByEmailLimit(DateTimeOffset RetryAt)`, `RefusedByIpLimit(DateTimeOffset RetryAt)`; `static class CodeRequestPolicy { CodeRequestDecision Decide(CodeRequestCounts counts, CodeRequestLimits limits); }` — the domain service; a count at or above a limit without its oldest moment raises `DomainException(CorruptRecord)`.
- `enum EmailServiceRefusal { DailyLimit, Limit, Unavailable }`
- `abstract record CodeEmailOutcome` with `Sent`, `Unclear`, `Refused(EmailServiceRefusal Reason, DateTimeOffset? RetryAt)`
- `abstract record CodeRequestResult` with `Sent(int Remaining, int Limit)`, `RefusedByEmailLimit(DateTimeOffset RetryAt)`, `RefusedByIpLimit(DateTimeOffset RetryAt)`, `RefusedByEmailService(EmailServiceRefusal Reason, DateTimeOffset? RetryAt)`
- `abstract record SignInResult` with `SignedIn(User User, Session Session, SessionToken Token)`, `CodeIncorrect`, `NewCodeRequired`
- `sealed record AuthenticatedSession(User User, Session Session)`
- `sealed record DeletedRecords(int OneTimeCodes, int CodeRequests, int Sessions)`

Driven ports (all methods take a trailing `CancellationToken ct`):

| Port | Methods |
|---|---|
| `IUsers` | `Task<User?> FindByEmailAsync(Email)`; `Task<User?> FindByIdAsync(Guid)`; `Task<User> AddOrGetAsync(User)` (inserts; on a concurrent insert of the same email returns the stored user); `Task SaveAsync(User)` |
| `IOneTimeCodes` | `Task<OneTimeCode?> FindExclusiveAsync(Email)` (exclusive access to the code until the transaction ends, so concurrent entries are counted one after another); `Task ReplaceAsync(OneTimeCode)`; `Task SaveAsync(OneTimeCode)` (deletes a used code, else stores its attempts); `Task<int> DeleteExpiredOrExhaustedAsync(DateTimeOffset now, int maxEntryAttempts)` (codes with `ExpiresAt <= now` or `EntryAttempts >= maxEntryAttempts`) |
| `ICodeRequests` | `Task LockAsync(Email, IpAddressPrefix)` (transaction-scoped; `InvalidOperationException` outside a transaction); `Task<CodeRequestCounts> CountAsync(Email, IpAddressPrefix, DateTimeOffset windowStart)` (counts `RequestedAt > windowStart`); `Task AddAsync(CodeRequest)`; `Task<int> DeleteRequestedAtOrBeforeAsync(DateTimeOffset cutoff)` |
| `ISessions` | `Task<Session?> FindAsync(byte[] id)`; `Task AddAsync(Session)`; `Task SaveAsync(Session)` (updates an existing session and never inserts one, so a session deleted meanwhile stays deleted); `Task DeleteAsync(byte[] id)`; `Task DeleteAllOfUserAsync(Guid userId)`; `Task<int> DeleteInactiveAtOrBeforeAsync(DateTimeOffset cutoff)` |
| `ICodeEmailSender` | `Task<CodeEmailOutcome> SendAsync(Email, OneTimeCodeValue, Locale)` |
| `ILocales` | `bool IsAvailable(string code)` |
| `IIdentityTransaction` | `Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)` (commits when `work` returns, rolls back when it throws) |
| time | `System.TimeProvider` |

Contract suites of the driven ports (constitution IV; research R12), in `backend/tests/Shared/PortContracts/`: `UsersContract`, `OneTimeCodesContract`, `CodeRequestsContract`, `SessionsContract`, `TransactionContract`, `CodeEmailSenderContract`, `LocalesContract`. Each is an abstract xUnit class with an abstract factory of the implementation under test; the files are linked into `Identity.Core.Tests` (subclasses over the fakes) and `Identity.Adapters.Tests` (subclasses over the adapters). Concurrency cases stay in the adapter tests.

Use cases (driving ports; application services: one class each, constructor-injected with ports, `TimeProvider` and `IdentityContextOptions`; one public method `ExecuteAsync`; no domain rule of their own):

| Class | `ExecuteAsync` | Task |
|---|---|---|
| `RequestCode` | `(Email, Locale, IpAddressPrefix, ct) → CodeRequestResult` | 6 |
| `SignIn` | `(Email, OneTimeCodeValue, bool rememberMe, string? browserTimeZone, Locale, SessionToken? heldSession, ct) → SignInResult` (a valid held session is deleted once the new one opens) | 9 |
| `AuthenticateSession` | `(SessionToken, ct) → AuthenticatedSession?` (extends the session) | 9 |
| `GetCurrentUser` | `(Guid userId, ct) → User` (`InvalidOperationException` when there is none: users are never deleted in this epic) | 9 |
| `ChangeLocale` | `(Guid userId, Locale, ct) → Task` | 12 |
| `SignOut` | `(byte[] sessionId, ct) → Task` | 12 |
| `SignOutEverywhere` | `(Guid userId, ct) → Task` | 12 |
| `DeleteExpiredRecords` | `(ct) → DeletedRecords` (computes every cutoff and passes `OneTimeCode.MaxEntryAttempts`) | 12 |

### Adapters and host

| Name | Where | What |
|---|---|---|
| `IdentityDbContext` | `Identity.Adapters/Storage` | default schema `identity`; history table `identity.__EFMigrationsHistory`; maps only the record classes |
| `UserRecord`, `OneTimeCodeRecord`, `CodeRequestRecord`, `SessionRecord` | `Identity.Adapters/Storage/Records` | plain classes with one property per column of data-model.md; the stores turn them into aggregates through `FromStorage` and back |
| `OperationsDbContext` | `Host/Operations` | default schema `operations`; history table `operations.__EFMigrationsHistory` |
| `TranslationFiles` | `Identity.Adapters/Translations` | implements `ILocales`; `IReadOnlyList<LocaleResponse> List()`; `string Text(Locale, string key)`; reads `*.json` in `Translations:Path`; a file whose name does not match `Locale.Pattern` stops the start |
| `DevelopmentLogCodeEmailSender`, `ResendCodeEmailSender` | `Identity.Adapters/Email` | `ICodeEmailSender` (Task 8) |
| `SessionAuthenticationHandler` | `Identity.Adapters/Http` | scheme `session`; claim `sub` = user id, claim `sid` = base64url session id; a malformed cookie (`DomainException(InvalidSessionToken)`) is not signed in (Task 12) |
| `DomainExceptionHandler` | `Identity.Adapters/Http` | `IExceptionHandler`: `DomainException` → problem details with the status and `code` of data-model.md, "Error model"; anything else → `500` `internal-error` (Task 9) |
| `Problems` | `Identity.Adapters/Http` | `Problems.Create(int status, string code, DateTimeOffset? retryAt)`; every problem carries `traceId` (Task 9) |
| `IdentityContextRegistration.AddIdentityContext(IServiceCollection, IConfiguration, IHostEnvironment)` | `Identity.Adapters` | every Identity registration, and the only choice of an adapter by environment; not `AddIdentity`, which ASP.NET Core Identity defines |
| `IdentityEndpoints.MapIdentityEndpoints(IEndpointRouteBuilder)` | `Identity.Adapters/Http` | the endpoints of `contracts/http-api.md` |

Adapters meet only in the core: the folders `Http`, `Storage`, `Email` and `Translations` of `Identity.Adapters` name no type of one another; only `IdentityContextRegistration` names them all (Task 1 guards it). A driving adapter (an endpoint, the authentication handler, the hourly cleanup) calls only use cases, never resolves a driven port and never changes an aggregate it received; `FromStorage` is called only by the storage adapter. The task reviewer checks what the guard test cannot.

Configuration keys (research R13): `ConnectionStrings:Habits`; `Database:MigrateOnStart` (`true` in Development and tests, absent in Production); `Identity:CodeHashKey` (base64 of 32 bytes; required outside Development; in Development, when absent, a random key is generated at each start, research R5); `Identity:CodeRequestLimits:PerEmail` / `PerIpAddress` / `Window`; `Resend:ApiKey`; `Translations:Path`; `ForwardedHeaders:KnownNetwork`. Environment-variable form uses `__`, e.g. `Identity__CodeRequestLimits__PerIpAddress`.

### Frontend (`frontend/src/app`)

| Name | File | What |
|---|---|---|
| `LanguageService` | `i18n/language.service.ts` | `current: Signal<string>`, `available: Signal<LocaleResponse[]>`, `use(code: string): void`; `localStorage` key `locale` before sign-in; a stored value that is not available is ignored |
| `CurrentUserService` | `session/current-user.service.ts` | `user: Signal<CurrentUser \| null>`, `load(): Promise<CurrentUser \| null>`, `signOut()`, `signOutEverywhere()`, `changeLocale(code)` |
| `signedInGuard` | `session/signed-in.guard.ts` | `/` only; a visitor goes to `/sign-in` |
| `notSignedInInterceptor` | `session/not-signed-in.interceptor.ts` | a `401` with `code: "not-signed-in"` navigates to `/sign-in` (Task 13); with the session-ended state when a user was loaded (Task 15) |
| `AppShell` | `app.ts` | header with the language switcher (Task 4) and the theme switcher (Task 6), `<main>`, footer with links to `/privacy` and `/terms`, on every route |
| `ThemeService` | `theme/theme.service.ts` | `current: Signal<'dark' \| 'light'>`, `use(theme)`; `localStorage` key `theme`; sets `data-theme` and `theme-color` (Task 6) |
| `MessageComponent` | `message/message.ts` | `app-message`, inputs `kind: 'error' \| 'info'`, `id`; `role="alert"` or `role="status"` (Task 6) |
| `TranslatedTitleStrategy` | `shell/translated-title.strategy.ts` | route `title` = translation key; `document.title` = `<page title> — <app.name>` (Task 6) |
| `SignInFlow` | `sign-in/sign-in-flow.service.ts` | in memory only: `email`, `rememberMe`, `remaining`, `limit` signals; `clear()` (Task 10) |
| `formatMoment` | `time/format-moment.ts` | `formatMoment(moment: Date, now: Date, locale: string, timeZone: string): string` (Task 10) |
| `EMAIL_PATTERN`, `EMAIL_MAX_LENGTH`, `ONE_TIME_CODE_PATTERN` | `validation/patterns.ts` | copies of `Email.Pattern`, `Email.MaxLength` and `OneTimeCodeValue.Pattern` (research R18; Task 10) |

### Shared conventions

- **Development log.** `scripts/dev.sh` appends every line of the host and of `ng serve` to `.dev/app.log`. The development code email sender logs one line per code email, then the body:
  `code-email to=<email> locale=<locale> code=<6 digits> subject=<subject>`
  End-to-end tests and the load test read the newest `code=` for an email from this line (`.dev/app.log`; `docker compose logs app` in the load test). Research R17.
- **Ports and the development database** (research R17): host `http://localhost:5080`; `ng serve` `http://localhost:4200` proxying `/api` and `/health` (`frontend/proxy.conf.json`); compose `app` `http://localhost:8080`; database `127.0.0.1:5432`, database `habits`, user `habits`, `POSTGRES_HOST_AUTH_METHOD: trust` bound to localhost only.
- **Migrations** (research R2): in Development and tests the host applies both contexts' migrations at start; in Production only the deployment runs `migrate-identity` and `migrate-operations`, after the backup.
- **Backend test harness** (research R12). `backend/tests/Shared/PostgresFixture.cs`, linked into `Identity.Adapters.Tests` and `Host.Tests`: one `postgres:18.6` Testcontainer per test assembly (xUnit v3 assembly fixture), `Task<string> CreateDatabaseAsync()` returns the connection string of a new empty database. `backend/tests/HabitsTracker.Host.Tests/HostFactory.cs`: `WebApplicationFactory<Program>` with a fresh database, `FakeTimeProvider Time` replacing `TimeProvider`, environment `Development` unless set, `Translations:Path` pointing at `frontend/public/i18n`, and a startup filter that sets the connection's remote address from the test header `Test-Remote-Address` (default `127.0.0.1`). Tests pass `TestContext.Current.CancellationToken` to every async call (`xUnit1051`).
- **Shared validation examples** (research R18): `testdata/validation-examples.json`, an array of `{ "rule": "email" | "one-time-code" | "locale-code", "input": "…", "valid": true | false }`, run by the core tests (Task 2) and by Vitest (Task 10).
- **Timers.** Background work (Tasks 17, 18) runs on `new PeriodicTimer(period, timeProvider)`.
- **Frontend test harness.** Vitest through `ng test`; Playwright in `frontend/e2e/` with the four projects of research R12: `phone-360` (Chromium, 360 × 740, `isMobile`, `hasTouch`, `deviceScaleFactor: 2`) runs every test; `phone-320` (the same at 320 × 640) and `desktop-1280` (Chromium, 1280 × 800) run the tests tagged `@width`; `webkit-360` (WebKit with the settings of `phone-360`) runs the tests tagged `@main-path`. `webServer` runs `scripts/dev.sh` with `Identity__CodeRequestLimits__PerIpAddress=1000` and `reuseExistingServer: false` (a running developer instance must be stopped first; the e2e run tells so). Helpers in `frontend/e2e/support/`: `readCode(email)`, `expectNoHorizontalScroll(page)`, `collectForeignRequests(page)`, `uniqueEmail()` (`e2e-<uuid>@example.com`).
- **Commands.** Fast: `scripts/test-fast.sh` (`dotnet test backend/HabitsTracker.slnx`, `npm --prefix frontend test`). Full: `scripts/test-full.sh` (fast, `npm --prefix frontend run lint`, `npm --prefix frontend run e2e`, every shell test under `scripts/tests/` and `ops/tests/`). Load: `scripts/load-test.sh`. Each task that adds one writes it into `CLAUDE.md`, "Commands"; Task 1 writes the fast command's measured time next to it.
- **Commits.** One behavioral commit per task, plus a separate structural commit where a task moves or renames existing code. Messages in English, imperative.

## Task order

| # | Task | Executor | Depends on | Foundation |
|---|---|---|---|---|
| 1 | Backend solution, test harness and health | subagent (Opus) | — | yes |
| 2 | Identity core model: value objects, aggregates, error model, ports | developer | 1 | yes |
| 3 | Identity storage | subagent (Opus, effort high) | 2 | yes |
| 4 | Angular application, translations and language before sign-in | subagent (Opus) | 2 | yes |
| 5 | Development loop and end-to-end harness | subagent (Opus) | 4 | yes |
| 6 | Interface baseline: tokens, themes, font and shared components | subagent (Opus) | 4, 5 | yes |
| — | **Intermediate review**: `superpowers:requesting-code-review` over the branch | controller | 1–6 | |
| 7 | Request a code: core | developer | 2, 3, 4 | |
| 8 | Code email senders | subagent | 2, 4, 7 | |
| 9 | Error format for the API | subagent (Opus) | 2, 4 | |
| 10 | Request a code: API and sign-in page | subagent | 3, 5, 7, 8, 9 | |
| 11 | Sign in and authenticate: core | developer | 3, 7 | |
| 12 | Sign in: API, session cookie, current user | subagent (Opus) | 10, 11 | |
| 13 | Sign in: code step, main screen, guard | subagent | 12 | |
| 14 | Sign out, locale, expired records: core | developer | 11 | |
| 15 | Sign out, sign out everywhere, locale change: API and main screen | subagent | 13, 14 | |
| 16 | Privacy and Terms pages, record of processing | subagent | 13 | |
| 17 | Request timing metrics | subagent | 1 | |
| 18 | Hourly cleanup, log retention and deletion on request | subagent | 3, 14, 17 | |
| 19 | Backup, restore check and live restore | subagent (Opus, effort high: the live database) | 1 | |
| 20 | Container image and HTTPS-only proxy | subagent | 15, 16 | |
| 21 | Load test | subagent | 20 | |
| 22 | Test integrity check | subagent | 5 | |
| 23 | GitHub repository and pull request checks | manual | 19–22 | |
| 24 | Server, deployment workflows, first deployment | manual | 23 | |
| 25 | Availability check | manual | 24 | |

Tasks run in this order. Tasks 17, 19 and 22 depend only on foundation tasks, so a ruling may move them earlier; developer tasks never run at the same time as a subagent task.

---

### Task 1: [FR-037] Backend solution, test harness and health endpoint

**Executor:** subagent (Opus) · **Depends on:** — · **Foundation:** yes

**Files:**
- Create: `backend/HabitsTracker.slnx`, `backend/Directory.Build.props`, `backend/.globalconfig`, `backend/Directory.Packages.props`, `backend/.config/dotnet-tools.json`
- Create: `backend/src/HabitsTracker.Identity.Core/HabitsTracker.Identity.Core.csproj`, `backend/src/HabitsTracker.Identity.Adapters/HabitsTracker.Identity.Adapters.csproj`
- Create: `backend/src/HabitsTracker.Host/{HabitsTracker.Host.csproj, Program.cs, appsettings.json, appsettings.Development.json, Properties/launchSettings.json}`, `backend/src/HabitsTracker.Host/Operations/OperationsDbContext.cs`, `backend/src/HabitsTracker.Host/Health/DatabaseHealthCheck.cs`
- Create: `backend/tests/{HabitsTracker.Identity.Core.Tests, HabitsTracker.Identity.Adapters.Tests, HabitsTracker.Host.Tests}/*.csproj`, `backend/tests/Shared/PostgresFixture.cs`, `backend/tests/HabitsTracker.Host.Tests/HostFactory.cs`, `backend/tests/Shared/PortContracts/` (empty; Tasks 3, 4, 7, 8 add the suites)
- Create: `compose.yaml` (service `db` only), `scripts/test-fast.sh`, `.claude/hooks/subagent-start-record.sh`, `.claude/hooks/subagent-stop-tests.sh`, `.claude/rules/backend.md`, `scripts/tests/subagent-stop-hook.test.sh`
- Modify: `.claude/settings.json` (add `hooks.SubagentStart` and `hooks.SubagentStop`; keep every deny rule), `.gitignore` (`bin/`, `obj/`, `.dev/`, `node_modules/`, `load/.sessions.json`), `CLAUDE.md` ("Commands")
- Test: `backend/tests/HabitsTracker.Identity.Core.Tests/CoreReferencesTests.cs`, `backend/tests/HabitsTracker.Identity.Adapters.Tests/AdapterSeparationTests.cs`, `backend/tests/HabitsTracker.Host.Tests/HealthTests.cs`

**Interfaces:**
- Produces: `PostgresFixture.CreateDatabaseAsync()`, `HostFactory` (see "Shared conventions"), `OperationsDbContext`, `public partial class Program`, `scripts/test-fast.sh`.

Decisions: `Directory.Build.props` holds the analysis settings of Global Constraints, NuGet Audit (`NuGetAudit=true`, `NuGetAuditMode=all`, `NuGetAuditLevel=high`) and `SonarAnalyzer.CSharp` as a `PrivateAssets="all"` analyzer for every project; the core's own `.csproj` stays free of references. `DatabaseHealthCheck` calls `OperationsDbContext.Database.CanConnectAsync()`; `/health` uses `MapHealthChecks` with the default writer, no details. `compose.yaml` `db`: image `postgres:18.6`, named volume at `/var/lib/postgresql`, port `127.0.0.1:5432:5432`, `POSTGRES_DB=habits`, `POSTGRES_USER=habits`, `POSTGRES_HOST_AUTH_METHOD=trust`. `.claude/rules/backend.md` (`paths: backend/**`): the core references nothing; time only through `TimeProvider`; invalid values raise `DomainException`; adapters meet only in the core; driving adapters call only use cases; `FromStorage` only in the storage adapter; no email, user id, token or code in a log ("Entity and type model"); tests first; values from the spec copied verbatim.

The hooks: `subagent-start-record.sh` writes `git rev-parse HEAD` to `.dev/subagents/<agent_id>`; `subagent-stop-tests.sh` runs `scripts/test-fast.sh` only when `backend/`, `frontend/`, `scripts/`, `ops/` or `load/` differ from that commit (committed or not, untracked included), and on a red run prints the last 40 lines to stderr and exits 2. Field names of the hook input are read from the Claude Code hooks documentation.

- [ ] **Step 1: Write the failing tests**

```csharp
// CoreReferencesTests
[Fact] public void Core_project_references_nothing()
{
    var csproj = XDocument.Load(PathToRepo("backend/src/HabitsTracker.Identity.Core/HabitsTracker.Identity.Core.csproj"));
    foreach (var element in new[] { "PackageReference", "FrameworkReference", "ProjectReference" })
        Assert.Empty(csproj.Descendants(element));
}
[Fact] public void Core_sources_carry_no_storage_or_serialization_attributes()
    // no *.cs under backend/src/HabitsTracker.Identity.Core contains "System.ComponentModel.DataAnnotations" or "System.Text.Json"

// AdapterSeparationTests
[Fact] public void Adapters_meet_only_in_the_core()
    // for each folder F of Http, Storage, Email, Translations under backend/src/HabitsTracker.Identity.Adapters:
    // no *.cs in F, except IdentityContextRegistration.cs, contains "HabitsTracker.Identity.Adapters.<G>" for another G

// HealthTests
[Fact] public async Task Health_is_healthy_when_the_database_is_reachable()
    // GET /health → 200, body "Healthy"
[Fact] public async Task Health_is_unhealthy_when_the_database_is_unreachable()
    // ConnectionStrings:Habits = "Host=127.0.0.1;Port=1;Database=habits;Username=habits;Timeout=2"
    // GET /health → 503, body exactly "Unhealthy"
```

`scripts/tests/subagent-stop-hook.test.sh`, in a temporary git repository with a copied hook and a stub `scripts/test-fast.sh`: no change under the source folders → exit 0 and the stub was not run; a changed file and a stub exiting 1 → exit 2 with the stub's output on stderr; a changed file and a stub exiting 0 → exit 0.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test backend/HabitsTracker.slnx` and `bash scripts/tests/subagent-stop-hook.test.sh`
Expected: health tests FAIL (no `/health`); hook test FAIL (no hook).

- [ ] **Step 3: Implement the solution, the health endpoint, the fixture, the factory and the hooks**

- [ ] **Step 4: Run to verify they pass**

Run: `scripts/test-fast.sh` and `bash scripts/tests/subagent-stop-hook.test.sh`
Expected: PASS; `dotnet build` with zero warnings.

- [ ] **Step 5: Write "Commands" in `CLAUDE.md`**: the fast command with its time as measured in Step 4, and that it needs Docker for Testcontainers. Keep the file under about 60 lines.

- [ ] **Step 6: Commit** — `Add backend solution, test harness and health endpoint`

---

### Task 2: [US-1.1][US-1.2][FR-002][FR-003][FR-008][FR-009] Identity core model: value objects, aggregates, error model, ports

**Executor:** developer · **Depends on:** 1 · **Foundation:** yes

**Files:**
- Create in `backend/src/HabitsTracker.Identity.Core/`: `DomainError.cs`, `DomainException.cs`, `Email.cs`, `TimeZone.cs`, `Locale.cs`, `IpAddressPrefix.cs`, `OneTimeCodeValue.cs`, `CodeHash.cs`, `SessionToken.cs`, `CodeRequestLimits.cs`, `IdentityContextOptions.cs`, `User.cs`, `OneTimeCode.cs`, `CodeRequest.cs`, `Session.cs`, `Results.cs`, `Ports/{IUsers, IOneTimeCodes, ICodeRequests, ISessions, ICodeEmailSender, ILocales, IIdentityTransaction}.cs`
- Create: `testdata/validation-examples.json`
- Test: `backend/tests/HabitsTracker.Identity.Core.Tests/{EmailTests, TimeZoneTests, LocaleTests, IpAddressPrefixTests, OneTimeCodeValueTests, CodeHashTests, SessionTokenTests, ValueObjectRulesTests, AggregateTests, ValidationExamplesTests}.cs`

**Interfaces:**
- Produces: every type of "Entity and type model", Identity core, with the signatures given there. `OneTimeCode.Verify`, `Session.IsExpired` and `Session.RecordActivity` are declared here and specified by Task 11's tests; until then they may throw `NotImplementedException`.

- [ ] **Step 1: Write the failing parameterized tests**

Each invalid case asserts `DomainException` with the `DomainError` named.

`EmailTests.Creates` (`[Theory]`):

| Input | Result |
|---|---|
| `ann@example.com` | `Value == "ann@example.com"` |
| `Ann@Example.COM` | `Value == "ann@example.com"` |
| `a.b+tag@sub.example.com` | valid |
| `a@b` | valid (the WHATWG pattern needs no dot in the domain) |
| 242 × `a` + `@example.com` (254 characters) | valid |
| 243 × `a` + `@example.com` (255 characters) | `InvalidEmail` |
| 100 000 × `a` | `InvalidEmail`, rejected by the length check before the pattern runs (research R19, D5) |
| `null`, `""`, `"   "` | `InvalidEmail` |
| `"  ann@example.com "`, `"ann@example.com\t"`, `"ann\n@example.com"` | `InvalidEmail` (no trimming, no whitespace anywhere) |
| `ann @example.com`, `ann@exa mple.com` | `InvalidEmail` |
| `ann`, `@example.com`, `ann@`, `ann@b@example.com` | `InvalidEmail` |
| `ann@-example.com`, `ann@example-.com` | `InvalidEmail` |
| `аnn@example.com` (Cyrillic `а`) | `InvalidEmail` (ASCII only) |

Plus `Emails_differing_in_case_are_equal` and `From_storage_rejects_a_value_that_is_not_lower_cased` (`CorruptRecord`).

`TimeZoneTests.From_browser`:

| Browser id | `Id` | `Recognized` |
|---|---|---|
| `Europe/Warsaw` | `Europe/Warsaw` | true |
| `America/Argentina/Buenos_Aires` | same | true |
| `UTC` | `UTC` | true |
| `null`, `""` | `UTC` | false |
| `Mars/Olympus_Mons` | `UTC` | false |
| `Central European Standard Time` (a Windows id) | `UTC` | false |

`LocaleTests.Creates`, with a fake `ILocales` of `en`, `pl`, `ru`: `pl` valid; `de` (no file), `PL`, `pl-PL`, `""`, `null` → `InvalidLocale`. `LocaleTests.From_storage_checks_the_pattern_only`: `FromStorage("de")` succeeds; `FromStorage("PL")` and `FromStorage("")` → `CorruptRecord`.

`IpAddressPrefixTests.From_address`:

| Address | `ToString()` |
|---|---|
| `203.0.113.7` | `203.0.113.7/32` |
| `2001:db8:1:2:3:4:5:6` | `2001:db8:1:2::/64` |
| `2001:db8:1:2:ffff:ffff:ffff:ffff` | `2001:db8:1:2::/64` |
| `::ffff:203.0.113.7` | `203.0.113.7/32` |

Plus `Addresses_in_one_64_prefix_are_equal` and `From_storage_rejects_other_prefix_lengths` (`203.0.113.0/24`, `2001:db8::/48` → `CorruptRecord`).

`OneTimeCodeValueTests`: `Create` accepts `042917` (`Digits == "042917"`) and raises `InvalidOneTimeCode` for `42917`, `0429170`, `04291a`, `" 042917"`, `０４２９１７` (full-width digits); `Generated_codes_are_six_ascii_digits` — 1000 calls all match `^[0-9]{6}$`.

`CodeHashTests`:

```csharp
[Fact] public void Hash_is_hmac_sha256_over_the_email_and_the_code()
    // expected = HMACSHA256.HashData(key, UTF8("ann@example.com\n042917")); Assert.Equal(expected, hash.ToArray())
[Fact] public void Hash_binds_the_email()      // same code, emails A and B → !Matches
[Fact] public void Hash_depends_on_the_key()   // same email and code, two keys → !Matches
```

`SessionTokenTests`: `Generate().Value` has 43 characters of the base64url alphabet and decodes to 32 bytes; two tokens differ; `ComputeId()` equals `SHA256.HashData` of those 32 bytes; `Create` raises `InvalidSessionToken` for `""`, `abc`, a 44-character value and any value with `+`, `/` or `=`.

`ValueObjectRulesTests`:
- `Secrets_do_not_print` — `ToString()` of a `OneTimeCodeValue`, a `SessionToken` and a `CodeHash` is `***` and contains no part of the value.
- `Code_hashes_are_equal_by_content` — two `CodeHash.FromStorage` of equal bytes are equal and have equal hash codes.
- `From_storage_rejects_corrupt_values` — `TimeZone.FromStorage("Mars/Olympus_Mons", true)`, `TimeZone.FromStorage("Europe/Warsaw", false)` and `CodeHash.FromStorage(new byte[31])` → `CorruptRecord`.
- `Configuration_values_are_checked` — `CodeRequestLimits.Create` with a zero in any position and `IdentityContextOptions.Create` with a 31-byte key → `InvalidConfiguration`.
- `Instants_must_be_utc` — `User.SignUp(…, now with offset +02:00)` raises `ArgumentException`.

`AggregateTests`:
- `OneTimeCode.Issue(email, hash, T)` gives `ExpiresAt == T + 10 min`, `EntryAttempts == 0`, `IsUsed == false`; `OneTimeCode.MaxEntryAttempts == 5`.
- `CodeRequestLimits.Default` has `5`, `10`, `TimeSpan.FromHours(24)`.
- `User.SignUp` sets every field and a version 7 id (`Id.Version == 7`); `User.ChangeLocale` changes only the locale.
- `Session.Open(userId, remembered: true, T)` gives `CreatedAt == LastActivityAt == T`, `Id == token.ComputeId()`; `Session.IdleTimeout == TimeSpan.FromDays(30)`.
- `Aggregates_loaded_from_storage_check_their_invariants` — `OneTimeCode.FromStorage` with `expiresAt = issuedAt + 11 min`, or with 6 entry attempts; `Session.FromStorage` with a 31-byte id, or with `lastActivityAt < createdAt`; `User.FromStorage` with an empty id → `CorruptRecord`.

`ValidationExamplesTests.Every_shared_example_holds` — reads `testdata/validation-examples.json` and checks each example against `Email.Create`, `OneTimeCodeValue.Create` or the `Locale.Pattern` regex: valid when `valid` is true, `DomainException` otherwise. The file holds at least the email rows of the table above (including 254, 255 and 100 000 characters), the code rows of `OneTimeCodeValueTests` and the locale codes `pl`, `PL`, `pl-PL`.

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test backend/tests/HabitsTracker.Identity.Core.Tests`
Expected: FAIL — the types do not exist.

- [ ] **Step 3: Write the types** with the signatures of "Entity and type model"; patterns as `[GeneratedRegex]` with a match timeout; ports as interfaces only.

- [ ] **Step 4: Run to verify they pass**

Run: `scripts/test-fast.sh` — Expected: PASS; the guard tests of Task 1 still pass.

- [ ] **Step 5: Commit** — `Add Identity core model: value objects, aggregates, error model and ports`

---

### Task 3: [FR-002][FR-003][FR-008][FR-012] Identity storage

**Executor:** subagent (Opus, effort high: schema and migration) · **Depends on:** 2 · **Foundation:** yes

**Files:**
- Create in `backend/src/HabitsTracker.Identity.Adapters/Storage/`: `IdentityDbContext.cs`, `Records/{UserRecord, OneTimeCodeRecord, CodeRequestRecord, SessionRecord}.cs`, `Configurations/*.cs` (one per record), `EfUsers.cs`, `EfOneTimeCodes.cs`, `EfCodeRequests.cs`, `EfSessions.cs`, `EfIdentityTransaction.cs`, `Migrations/<timestamp>_CreateIdentitySchema.cs`
- Create: `backend/tests/Shared/PortContracts/{UsersContract, OneTimeCodesContract, CodeRequestsContract, SessionsContract, TransactionContract}.cs` (linked into `Identity.Core.Tests` and `Identity.Adapters.Tests`)
- Modify: `backend/src/HabitsTracker.Identity.Adapters/IdentityContextRegistration.cs` (create: registers the context and the five stores, scoped); `backend/src/HabitsTracker.Host/Program.cs` (calls `AddIdentityContext`; migrates `IdentityDbContext` when `Database:MigrateOnStart`)
- Test: `backend/tests/HabitsTracker.Identity.Adapters.Tests/Storage/{SchemaTests, EfContractTests, ConcurrencyTests, LoadTests}.cs`

**Interfaces:**
- Consumes: the ports, aggregates and `FromStorage` factories of Task 2.
- Produces: `IUsers`, `IOneTimeCodes`, `ICodeRequests`, `ISessions`, `IIdentityTransaction` registered by `AddIdentityContext`; the `identity` schema exactly as data-model.md, "Schema `identity`"; the five contract suites, which Task 7 subclasses over the fakes.

Decisions (data-model.md, "Storage"; research R5, R23): EF Core maps only the record classes; each store turns a record into an aggregate through the core's `FromStorage` factories and an aggregate back into a record, so every rule is checked on load and no core type has a constructor for EF Core. Every store shares the request's scoped `IdentityDbContext` and calls `SaveChangesAsync` per write; `EfIdentityTransaction` wraps them in one database transaction. `LockAsync` runs `pg_advisory_xact_lock(hashtextextended('email:' || @email, 0))`, then the same for `'ip:' || @prefix`, always in that order. `FindExclusiveAsync` is `SELECT … FOR UPDATE`. `AddOrGetAsync` is `INSERT … ON CONFLICT (email) DO NOTHING` followed by a select, so a concurrent first sign-in does not abort the transaction. `ISessions.SaveAsync` is an `UPDATE` by id that inserts nothing. `ip_prefix` maps through `NpgsqlCidr` to `System.Net.IPNetwork`. A database failure other than the concurrent first sign-in propagates.

- [ ] **Step 1: Write the failing tests** (each class takes a fresh database from `PostgresFixture` and applies the migration)

- `SchemaTests.Migration_creates_the_tables_of_the_data_model` — from `information_schema.columns`, schema `identity`, exactly:
  `users(id uuid, email text, time_zone text, time_zone_recognized boolean, locale text, created_at timestamptz)`,
  `one_time_codes(email text, code_hash bytea, issued_at timestamptz, expires_at timestamptz, entry_attempts smallint)`,
  `code_requests(id bigint, email text, ip_prefix cidr, requested_at timestamptz)` with `id` an identity column,
  `sessions(id bytea, user_id uuid, remembered boolean, created_at timestamptz, last_activity_at timestamptz)`;
  and from `pg_indexes`: unique `users(email)`, `one_time_codes(expires_at)`, `code_requests(email, requested_at)`, `code_requests(ip_prefix, requested_at)`, `code_requests(requested_at)`, `sessions(user_id)`, `sessions(last_activity_at)`, FK `sessions.user_id → users.id`. No column holds a code in clear text.
- The contract suites, each subclassed in `EfContractTests` over the EF stores:
  - `UsersContract`: `User_round_trips` (a `TimeZone` with `Recognized == false` included); `Add_or_get_returns_the_stored_user_for_the_same_email`.
  - `OneTimeCodesContract`: `Replace_keeps_one_code_per_email`; `Saving_stores_the_attempts`; (`Saving_a_used_code_deletes_it` is added by Task 11, once `Verify` can mark a code used; `EfOneTimeCodes.SaveAsync` deletes a code whose `IsUsed` is true from this task on); `Delete_expired_or_exhausted` — called with `(now, 5)`: expires at `now` → deleted, at `now + 1 s` → kept, attempts 5 → deleted, attempts 4 → kept; called with `(now, 3)`: attempts 3 → deleted.
  - `CodeRequestsContract`: `Count_uses_a_sliding_window` — requests for the email at `T − 24 h`, `T − 23 h 59 min 59 s`, `T − 1 h`, window start `T − 24 h` → `ForEmail == 2`, `OldestForEmail == T − 23 h 59 min 59 s`; `Requests_of_one_64_prefix_count_together`; `Delete_requested_at_or_before` boundary.
  - `SessionsContract`: round trip; `Saving_a_deleted_session_does_not_restore_it`; `Delete_all_of_user` leaves other users' sessions; `Delete_inactive_at_or_before` boundary.
  - `TransactionContract`: `Work_that_returns_is_committed`; `Work_that_throws_is_rolled_back` (a user, a code and a code request written before the exception are all absent afterwards).
- `ConcurrencyTests` (PostgreSQL only): `Concurrent_first_sign_ins_create_one_user` — 10 parallel scopes, each in its own transaction, `AddOrGetAsync` of a new `User` with the same email → one row, all ten results carry its id; `Concurrent_attempt_updates_are_not_lost` — 5 parallel transactions each `FindExclusiveAsync` and `SaveAsync` a copy with one more attempt (`OneTimeCode.FromStorage`) → `entry_attempts == 5`; `Concurrent_code_requests_cannot_pass_the_last_free_slot` — 4 stored, 10 parallel transactions each `LockAsync` → `CountAsync` → `AddAsync` only if `ForEmail < 5` → 5 stored; `Lock_outside_a_transaction_fails`.
- `LoadTests.Corrupt_rows_are_rejected_on_load` — rows written by SQL past the model (`entry_attempts = 7`; `last_activity_at < created_at`; `locale = 'PL'`; a 31-byte `code_hash`) → reading them raises `DomainException(CorruptRecord)`.

- [ ] **Step 2: Run to verify they fail** — `dotnet test backend/tests/HabitsTracker.Identity.Adapters.Tests` — FAIL: no context.

- [ ] **Step 3: Implement the records, the context, the configurations, the stores and the transaction; generate the migration** with `dotnet ef migrations add CreateIdentitySchema` (local tool), schema and names as data-model.md.

- [ ] **Step 4: Run to verify they pass** — `scripts/test-fast.sh` — PASS.

- [ ] **Step 5: Commit** — `Add Identity storage on PostgreSQL`

---

### Task 4: [US-1.8][FR-020][FR-021][FR-022][FR-024][FR-027] Angular application, translations and language before sign-in

**Executor:** subagent (Opus) · **Depends on:** 2 · **Foundation:** yes

**Files:**
- Create: `frontend/` from `ng new` (standalone, zoneless, routing, plain CSS, Vitest), `frontend/proxy.conf.json`, `frontend/eslint.config.js`, `frontend/public/i18n/{en,pl,ru}.json`
- Create in `frontend/src/app/`: `app.ts` (`AppShell`), `app.routes.ts` (`/sign-in`, `/`, `/privacy`, `/terms`, `**` not found; each page a placeholder heading from translations), `app.config.ts`, `i18n/{transloco-loader.ts, language.service.ts, language-switcher.ts}`, `api/locale-response.ts`
- Create: `backend/src/HabitsTracker.Identity.Adapters/Translations/TranslationFiles.cs`, `backend/src/HabitsTracker.Identity.Adapters/Http/IdentityEndpoints.cs` (`GET /api/locales`), `backend/src/HabitsTracker.Host/Web/{SecurityHeaders, CachingHeaders}.cs`, `backend/tests/Shared/PortContracts/LocalesContract.cs`, `.claude/rules/frontend.md`
- Modify: `backend/src/HabitsTracker.Host/Program.cs` (static files from the web root, fallback to `index.html` for any path outside `/api` and `/health`, security headers on every response), `scripts/test-fast.sh` (adds `npm --prefix frontend test`), `CLAUDE.md`
- Test: `frontend/src/app/i18n/{translation-files.spec.ts, plurals.spec.ts, language.service.spec.ts}`, `frontend/src/app/{app.spec.ts, no-html-from-strings.spec.ts}`, `backend/tests/HabitsTracker.Identity.Adapters.Tests/TranslationFilesTests.cs`, `backend/tests/HabitsTracker.Host.Tests/{LocalesEndpointTests, SpaHostingTests, SecurityHeadersTests, CachingHeadersTests}.cs`

**Interfaces:**
- Consumes: `ILocales`, `Locale` (Task 2).
- Produces: `TranslationFiles` (`ILocales`, `List()`, `Text(Locale, key)`); `LanguageService`; `AppShell`; `GET /api/locales`; the key set below, which later tasks extend in all three files.

Decisions: Transloco with `@jsverse/transloco-messageformat`; the loader fetches `/i18n/<code>.json`. Initial keys: `language.name`, `language.switcher`, `nav.privacy`, `nav.terms`, `signIn.title`, `main.title`, `privacy.title`, `terms.title`, `notFound.title`, and `signIn.remaining`, an ICU `plural` over `remaining` with `{limit}` (English reads "4 of 5 left"). `LanguageService` detection: `localStorage["locale"]` if it is one of the available codes (any other stored value is ignored), else the first entry of `navigator.languages` whose primary subtag, lower-cased, is an available code, else `en`; `use(code)` switches Transloco in place, writes `localStorage["locale"]` and sets `<html lang>`. Angular's critical-CSS inlining is off (`optimization.styles.inlineCritical: false`). Host tests point the web root at a temporary folder with a stub `index.html`. Caching headers as research R21. `.claude/rules/frontend.md` (`paths: frontend/**`): every user-visible string through Transloco, in all three files; no translation rendered as HTML: no `innerHTML` and no `bypassSecurityTrust*`; no URL outside the origin; tests first.

- [ ] **Step 1: Write the failing tests**

- `translation-files.spec.ts`: `every key exists in every file` — the key sets of `en`, `pl` and `ru` are equal and no value is empty; `every file names its language` — `language.name` is `English`, `Polski`, `Русский`.
- `plurals.spec.ts`: `Polish and Russian use their plural forms` — render `signIn.remaining` with `limit: 5` and `remaining` 1, 2 and 5 in `pl` and in `ru`; with the digits removed, the three strings of each language are pairwise different.
- `language.service.spec.ts`, table:

| `localStorage.locale` | `navigator.languages` | Detected |
|---|---|---|
| — | `['pl-PL', 'en']` | `pl` |
| — | `['de-DE', 'ru']` | `ru` |
| — | `['de-DE']` | `en` |
| — | `[]` | `en` |
| `ru` | `['pl-PL']` | `ru` |
| `xx` | `['pl-PL']` | `pl` (a stored value that is not available is ignored) |
| `de` | `[]` | `en` |

  plus `use writes the choice and the document language` (`localStorage.locale == 'pl'`, `document.documentElement.lang == 'pl'`).
- `app.spec.ts`: `every route shows the switcher and the Privacy and Terms links`.
- `no-html-from-strings.spec.ts`: `no template renders HTML from a string` (research R19, T2) — no file under `frontend/src/` with the extension `.html` or `.ts` contains `innerHTML` or `bypassSecurityTrust`.
- `TranslationFilesTests`: `Lists_the_files` → `en English`, `pl Polski`, `ru Русский` ordered by code; `A_new_file_is_a_new_language` — a temporary folder with the three files and a `de.json` naming `Deutsch` → `IsAvailable("de")`; `A_file_without_a_language_name_fails_at_load` with the file name in the message; `A_file_whose_name_is_not_a_locale_code_fails_at_load` (`README.json`, `pl-PL.json`). `TranslationFilesLocalesContract` subclasses `LocalesContract` over `TranslationFiles`: `en`, `pl`, `ru` are available; `de`, `PL`, `""` are not.
- `LocalesEndpointTests.Locales_lists_the_translation_files` → `200`, body exactly `[{"code":"en","name":"English"},{"code":"pl","name":"Polski"},{"code":"ru","name":"Русский"}]`.
- `SpaHostingTests`: `GET /privacy` → `200` `text/html` (the stub); `GET /api/unknown` → `404`, not HTML (its body is Task 9's); `GET /health` still the health answer.
- `CachingHeadersTests.Each_kind_of_response_has_its_cache_rule` — `GET /` and `GET /privacy` (fallback): `no-cache`; a hashed file of the stub build (`main-ABC123.js`): `public, max-age=31536000, immutable`; `GET /i18n/en.json`: `no-cache`; `GET /api/locales` and `GET /health`: `no-store`.
- `SecurityHeadersTests.Every_response_carries_the_security_headers` — on `GET /` and `GET /api/locales`: the exact `Content-Security-Policy` of Global Constraints, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`.

- [ ] **Step 2: Run to verify they fail** — `scripts/test-fast.sh` — FAIL.

- [ ] **Step 3: Implement** the application shell, the language service and switcher, the translation files (Polish and Russian drafted by the agent), `TranslationFiles`, the endpoint, the SPA hosting and the headers.

- [ ] **Step 4: Run to verify they pass** — `scripts/test-fast.sh` and `npm --prefix frontend run lint` — PASS, zero lint warnings.

- [ ] **Step 5: Commit** — `Add Angular application with three languages and the locales endpoint`

---

### Task 5: [US-1.8][FR-019][FR-021][FR-022][FR-027] Development loop and end-to-end harness

**Executor:** subagent (Opus) · **Depends on:** 4 · **Foundation:** yes

**Files:**
- Create: `scripts/dev.sh`, `scripts/test-full.sh`, `scripts/tests/dev-lock.test.sh`, `frontend/playwright.config.ts` (the four projects of "Shared conventions"), `frontend/e2e/support/{dev-log.ts, layout.ts, network.ts, emails.ts}`, `frontend/e2e/language.spec.ts`
- Modify: `frontend/package.json` (`e2e` script, Playwright 1.64 as a dev dependency), `CLAUDE.md` (full command; the development log is `.dev/app.log`; stop `scripts/dev.sh` before `npm --prefix frontend run e2e`; browsers once with `npx --prefix frontend playwright install --with-deps chromium webkit`)

**Interfaces:**
- Consumes: `AppShell`, `LanguageService` (Task 4); `/health` (Task 1).
- Produces: `scripts/dev.sh`; `readCode(email: string): Promise<string>` (polls `.dev/app.log` up to 10 s for the newest `code-email to=<email> … code=<6 digits>` line); `expectNoHorizontalScroll(page)` (`document.documentElement.scrollWidth <= window.innerWidth`); `collectForeignRequests(page): string[]` (every request whose origin is not the page's); `uniqueEmail()`; `scripts/test-full.sh`.

Decisions (research R12, R17): if WebKit refuses the `Secure` session cookie over `http://localhost`, the `webkit-360` project runs against Caddy with `tls internal` in front of the development host, and the implementer reports it to the controller. `dev.sh` takes `flock -n .dev/dev.lock`; when held it prints `scripts/dev.sh is already running` and exits 1 having started nothing. Otherwise it starts `db` from `compose.yaml`, runs the host with `dotnet watch` in Development on port 5080, and `ng serve --proxy-config proxy.conf.json` on 4200, appending both outputs to `.dev/app.log`; it passes its environment through to the host, which is how the end-to-end run raises the per-IP limit.

- [ ] **Step 1: Write the failing tests**

`scripts/tests/dev-lock.test.sh`: holding `.dev/dev.lock` with `flock` in the background, `scripts/dev.sh` exits 1 within 5 seconds with the message above, and no `dotnet` or `ng` process was started.

`frontend/e2e/language.spec.ts`:

```ts
test('page follows the browser\'s preferred language')        // context locale 'pl-PL' → <html lang="pl">, title from pl.json
test('a browser preferring no available language gets English') // 'de-DE' → lang "en"
test('switching the language changes the page without a reload') // set window.__marker; switch to ru → title from ru.json; marker still set
test('the chosen language is kept for a later visit')          // switch to ru; new page in the same context → ru
test('shell actions are reachable at every width @width')         // /sign-in, /privacy, /terms: switcher and both links toBeInViewport; expectNoHorizontalScroll
test('the language switch works in WebKit @main-path')              // switch to ru → title from ru.json
test('pages load nothing from other servers')                  // the same routes: collectForeignRequests is empty
```

- [ ] **Step 2: Run to verify they fail** — `bash scripts/tests/dev-lock.test.sh`; `npm --prefix frontend run e2e` — FAIL.

- [ ] **Step 3: Implement** `dev.sh`, the Playwright configuration of "Shared conventions", the helpers and `test-full.sh`.

  Check: `npx --prefix frontend playwright test --list` lists `language.spec.ts` under all four projects, with only the `@width` test under `phone-320` and `desktop-1280` and only the `@main-path` test under `webkit-360`.

- [ ] **Step 4: Run to verify they pass** — `scripts/test-full.sh` — PASS.

- [ ] **Step 5: Commit** — `Add development loop and end-to-end test harness`

---

### Task 6: [US-1.9][FR-027][FR-041][FR-042] Interface baseline: tokens, themes, font and shared components

**Executor:** subagent (Opus) · **Depends on:** 4, 5 · **Foundation:** yes

**Files:**
- Create: `frontend/scripts/fetch-fonts.sh`, `frontend/scripts/fonts.sha256`, `frontend/src/styles/tokens.css`, `frontend/public/icon.svg`, `frontend/src/app/theme/{theme.service.ts, theme-switcher.ts}`, `frontend/src/app/message/message.ts`, `frontend/src/app/shell/{translated-title.strategy.ts, focus-heading.ts}`, `frontend/e2e/support/{keyboard.ts, themes.ts}`, `scripts/tests/fetch-fonts.test.sh`
- Modify: `frontend/src/styles.css` (the two `@font-face` rules with relative `url()` paths, the page frame, controls, focus, `color-scheme` per theme, autofill), `frontend/src/index.html` (`<html data-theme="dark">`, `<meta name="viewport" content="width=device-width, initial-scale=1">`, `<meta name="theme-color" content="#0a0f0b">`, `<link rel="icon" href="/icon.svg" type="image/svg+xml">`, the `<noscript>` text in English, Polish and Russian), `frontend/src/app/app.ts` (`AppShell`: the theme switcher next to the language switcher, `<main>`), `app.config.ts` (the current language's translation file loads before the first render; `TranslatedTitleStrategy`; the heading focus), `app.routes.ts` (each route's `title` is its translation key), `frontend/package.json` (`prebuild`, `prestart` and `pretest` run `scripts/fetch-fonts.sh`), `.gitignore` (`frontend/src/assets/fonts/`, `frontend/public/fonts/`), `frontend/public/i18n/{en,pl,ru}.json` (`app.name`, `theme.toLight`, `theme.toDark`, `error.network`), `frontend/e2e/support/layout.ts`, `.claude/rules/frontend.md`, `CLAUDE.md`
- Test: `frontend/src/styles/{tokens-only.spec.ts, contrast.spec.ts}`, `frontend/src/app/theme/theme.service.spec.ts`, `frontend/src/app/message/message.spec.ts`, `frontend/src/app/shell/translated-title.strategy.spec.ts`, `frontend/e2e/{theme.spec.ts, interface.spec.ts}`, `scripts/tests/fetch-fonts.test.sh`

**Interfaces:**
- Consumes: `AppShell`, `LanguageService`, the Transloco loader (Task 4); the Playwright projects and helpers (Task 5).
- Produces: the tokens of `contracts/ui.md`, "Themes"; `ThemeService` (`current: Signal<'dark' | 'light'>`, `use(theme: 'dark' | 'light'): void`); `ThemeSwitcher`; `MessageComponent` (`app-message`, inputs `kind: 'error' | 'info'` and `id: string`), which every later page uses for every message; `TranslatedTitleStrategy` (a route's `title` is a translation key; `document.title` = `<page title> — <app.name>`, again on a language switch); the heading focus after a navigation, to the page's `h1`, which every page renders with `tabindex="-1"`; e2e helpers `expectVisibleFocus(page)` (the focused element's computed `outline-width` is `3px`), `pressTabUntil(page, locator)` (presses `Tab` at most 50 times until the locator is focused), `forEachTheme(page, fn)` (runs `fn` in the dark theme, then switches with the theme switcher and runs it in the light one); `fetch-fonts.sh`.

Decisions: as `contracts/ui.md` and research R24. `fetch-fonts.sh` reads the expected sums from `frontend/scripts/fonts.sha256` (`sha256sum` format: the release zip, `LICENSE-FONT.txt`, `NordwandMono-Regular.woff2`, `NordwandMono-Bold.woff2`, values of research R24), or from the file named by `FONTS_SHA256_FILE`, which only its test sets; when the two fonts and the license are already in place with their sums, it downloads nothing; otherwise `curl --fail --location` fetches the zip and the license into a temporary directory, checks both sums, extracts only the two fonts, checks their sums and moves the files into place; any mismatch exits 1 with the file's name and leaves nothing in place. `ThemeService` sets `data-theme` and `<meta name="theme-color">` (the theme's `--bg`) together. `.claude/rules/frontend.md` gains: component CSS uses only `var(--…)` tokens; every message through `app-message`; no field takes the focus when a page opens; a button is disabled while its request runs; each page's `h1` has `tabindex="-1"`. `CLAUDE.md`: the first frontend build needs access to `github.com`.

- [ ] **Step 1: Write the failing tests**

`scripts/tests/fetch-fonts.test.sh` (a stub `curl` on `PATH` serves files from a fixture folder; `FONTS_SHA256_FILE` points at the fixture's sums): matching files → exit 0, the two fonts in `src/assets/fonts/` and the license in `public/fonts/`; a second run makes no `curl` call; a zip whose sum differs → exit 1, the message names `NordwandMono-WOFF2.zip`, and neither folder holds a font; `fonts.sha256` holds the four sums of research R24.

`tokens-only.spec.ts`: `component styles use only tokens` — no `.css` file under `frontend/src/app/` contains a `#` colour, `rgb(`, `hsl(`, a CSS named colour, or a `font-size` in `px`; `the check catches a literal` — the same detector flags `color: red`, `background: #fff` and `font-size: 14px` in a sample string.

`contrast.spec.ts`: `the formula matches known values` — `#000000` on `#ffffff` is 21, `#777777` on `#ffffff` is 4.48 to two decimals; `every pair meets its threshold in both themes` — reads `tokens.css`, both `data-theme` blocks, every pair of `contracts/ui.md`, "Contrast pairs", against its threshold.

`theme.service.spec.ts`, table:

| `localStorage.theme` | Applied |
|---|---|
| — | `dark` |
| `light` | `light` |
| `dark` | `dark` |
| `blue` | `dark` |
| `getItem` throws | `dark` |

plus `use sets the attribute, the theme colour and the choice` (`data-theme == 'light'`, `theme-color` is `#f3f7f2`, `localStorage.theme == 'light'`) and `a choice that cannot be stored still applies` (`setItem` throws → `data-theme == 'light'`).

`message.spec.ts`: `an error is announced` (`role="alert"`); `an info is a status` (`role="status"`); `the id is set for aria-describedby`.

`translated-title.strategy.spec.ts`: `the title is the page title and the app name` (`/sign-in` in `en` → `signIn.title` + ` — ` + `app.name`); `the title follows a language switch`.

`frontend/e2e/theme.spec.ts`:

```ts
test('a browser that never chose gets the dark theme @main-path')      // colorScheme 'light' → data-theme 'dark'; body background rgb(10, 15, 11)
test('switching the theme changes the page without a reload @main-path') // window.__marker set; switch → data-theme 'light'; marker still set; focus on the switcher
test('the chosen theme is kept for a later visit')                      // switch to light; new page in the same context → light
test('a browser that keeps nothing still switches the page')            // addInitScript: Storage.prototype.setItem throws → switch works; reload → dark
```

`frontend/e2e/interface.spec.ts`:

```ts
test('a phone gets the page at its own width')                    // phone projects: window.innerWidth equals the viewport width (a missing viewport tag gives 980)
test('the font is applied')                                       // document.fonts.check('16px "Nordwand Mono"') is true; /fonts/LICENSE-FONT.txt answers 200
test('the keyboard reaches every shell control with a visible focus') // /sign-in, /privacy, /terms: pressTabUntil the language switcher, the theme switcher, both links; expectVisibleFocus on each
test('the focus moves to the heading after a navigation')         // activate the Privacy link by keyboard → document.activeElement is the h1
test('every page has its title in the page language')            // /sign-in, /privacy, /terms, an unknown path; in en and in ru
test('shell controls are at least 44 px high')                    // the switchers' bounding boxes
test('the shell at every width in both themes @width')            // forEachTheme: expectNoHorizontalScroll; switchers and links toBeInViewport
test('the page explains itself without JavaScript')               // the raw index.html has <noscript> with the three texts and the icon link; no request for /favicon.ico
```

- [ ] **Step 2: Run to verify they fail** — `bash scripts/tests/fetch-fonts.test.sh`; `scripts/test-fast.sh`; `npm --prefix frontend run e2e -- theme interface` — FAIL.

- [ ] **Step 3: Implement** the font script and its sums, the tokens and global styles, `index.html`, the theme service and switcher, the message component, the title strategy and the heading focus, the helpers.

- [ ] **Step 4: Run to verify they pass** — `scripts/test-full.sh` and `npm --prefix frontend run lint` — PASS, zero lint warnings.

- [ ] **Step 5: Commit** — `Add interface baseline with two themes, the font and shared components`

---

**Intermediate review.** After Task 6 the controller runs `superpowers:requesting-code-review` over the whole branch before Task 7 starts (process document, "Task plan format").

---

### Task 7: [US-1.1][FR-001][FR-002][FR-003][FR-004][FR-005] Request a code: core

**Executor:** developer · **Depends on:** 2, 3, 4

**Files:**
- Create: `backend/src/HabitsTracker.Identity.Core/CodeRequestPolicy.cs`, `backend/src/HabitsTracker.Identity.Core/UseCases/RequestCode.cs`
- Create: `backend/tests/HabitsTracker.Identity.Core.Tests/Fakes/{InMemoryUsers, InMemoryOneTimeCodes, InMemoryCodeRequests, InMemorySessions, InMemoryTransaction, RecordingCodeEmailSender, FixedLocales}.cs`, `backend/tests/Shared/PortContracts/CodeEmailSenderContract.cs`
- Test: `backend/tests/HabitsTracker.Identity.Core.Tests/{CodeRequestPolicyTests, RequestCodeTests, FakeContractTests}.cs`

**Interfaces:**
- Consumes: Task 2 types.
- Produces: `CodeRequestPolicy.Decide`, `RequestCode.ExecuteAsync`, and the fakes, which Tasks 11 and 14 reuse; `CodeEmailSenderContract`, which Task 8 subclasses over the adapters. `InMemoryCodeRequests.CountAsync` counts `RequestedAt > windowStart`; `RecordingCodeEmailSender` returns a configured `CodeEmailOutcome` and records each `(Email, OneTimeCodeValue, Locale)`; the fakes record the order of calls. `InMemoryTransaction` rolls back: each in-memory store registers an undo action for every write with the transaction in progress, and the transaction runs them when the work throws. `CodeEmailSenderContract`: `A_deliverable_message_is_sent` (the outcome is `Sent` or, for a fake set to it, the configured outcome) and `A_refusal_is_an_outcome_not_an_exception`.

The sequence (research R5): inside `IIdentityTransaction.RunAsync`: `LockAsync` → `CountAsync(windowStart: now − Window)` → `Decide` → on refusal return; else generate the code → `SendAsync` → on `Refused` return `RefusedByEmailService`; on `Sent` or `Unclear` `ReplaceAsync(OneTimeCode.Issue(...))` and `AddAsync(CodeRequest)` → `Sent(remaining, limits.PerEmail)`.

- [ ] **Step 1: Write the failing tests**

`CodeRequestPolicyTests.Decides` with `CodeRequestLimits.Default`, `T = 2026-11-02T10:00Z`:

| ForEmail | OldestForEmail | ForIp | OldestForIp | Decision |
|---|---|---|---|---|
| 0 | — | 0 | — | `Allowed(4)` |
| 3 | T − 5 h | 3 | T − 5 h | `Allowed(1)` |
| 4 | T − 5 h | 9 | T − 21 h | `Allowed(0)` |
| 5 | T − 23 h | 5 | T − 24 h | `RefusedByEmailLimit(T + 1 h)` |
| 4 | T − 2 h | 10 | T − 21 h | `RefusedByIpLimit(T + 4 h)` |
| 5 | T − 23 h | 10 | T − 21 h | `RefusedByEmailLimit(T + 1 h)` |

`RequestCodeTests` (fakes, `FakeTimeProvider` at T):

- `Sent_stores_the_code_and_the_request` — sender `Sent` → `Sent(4, 5)`; the stored code's hash matches the sent code for the email; one code request at T with the prefix; the sender got the email and the locale.
- `Unclear_counts_as_sent` — sender `Unclear` → `Sent(4, 5)`, code stored, request stored.
- `Refused_send_keeps_the_earlier_code_and_stores_nothing` — an earlier code exists; sender `Refused(DailyLimit, T + 14 h)` → `RefusedByEmailService(DailyLimit, T + 14 h)`; the earlier code is unchanged; no request stored.
- `A_new_code_replaces_the_earlier_one` — two sends → the stored hash matches the second code, not the first.
- `A_limit_refusal_sends_nothing_and_stores_nothing` — 5 requests stored → `RefusedByEmailLimit`, sender not called, still 5 stored.
- `A_request_exactly_24_hours_old_has_left_the_window` — 5 requests, the oldest at exactly T − 24 h → `Sent(0, 5)`.
- `Locks_before_counting_and_sends_before_storing` — call order `Lock < Count < Send < Replace, Add`, all inside one `RunAsync`.
- `Uses_the_configured_limits` — limits `(2, 3, 1 h)`: the third request for one email is refused.
- `A_refused_send_rolls_back_nothing_it_did_not_write` — `Refused` leaves the stores as before, and a send that throws leaves no code and no code request (the transaction fake rolls back).

`CodeRequestPolicyTests.A_count_at_the_limit_without_its_oldest_moment_is_corrupt` — `ForEmail = 5`, `OldestForEmail = null` → `DomainException(CorruptRecord)`.

`FakeContractTests` subclasses each contract suite of Tasks 3 and 4 over the fakes (`UsersContract`, `OneTimeCodesContract`, `CodeRequestsContract`, `SessionsContract`, `TransactionContract`, `LocalesContract` over `FixedLocales`) and `CodeEmailSenderContract` over `RecordingCodeEmailSender`; every case passes.

- [ ] **Step 2: Run to verify they fail** — `dotnet test backend/tests/HabitsTracker.Identity.Core.Tests` — FAIL.

- [ ] **Step 3: Write `CodeRequestPolicy.Decide` and `RequestCode`.**

- [ ] **Step 4: Run to verify they pass** — `scripts/test-fast.sh` — PASS.

- [ ] **Step 5: Commit** — `Add request a code use case and code request policy`

---

### Task 8: [US-1.1][US-1.8][FR-006][FR-007][FR-010] Code email senders

**Executor:** subagent · **Depends on:** 2, 4, 7

**Files:**
- Create in `backend/src/HabitsTracker.Identity.Adapters/Email/`: `CodeEmailText.cs`, `DevelopmentLogCodeEmailSender.cs`, `ResendCodeEmailSender.cs`
- Modify: `IdentityContextRegistration.cs` (by environment: Development registers only the development log sender; any other environment registers only Resend and fails at start without `Resend:ApiKey`, naming the key), `frontend/public/i18n/{en,pl,ru}.json` (`email.subject`, `email.body` with the placeholder `{code}`)
- Create: `backend/tests/Shared/CapturingLoggerProvider.cs` (linked like `PostgresFixture`)
- Test: `backend/tests/HabitsTracker.Identity.Adapters.Tests/Email/{CodeEmailTextTests, DevelopmentLogCodeEmailSenderTests, ResendCodeEmailSenderTests, SenderContractTests}.cs`, `backend/tests/HabitsTracker.Host.Tests/CodeEmailSenderRegistrationTests.cs`

**Interfaces:**
- Consumes: `ICodeEmailSender`, `CodeEmailOutcome`, `TranslationFiles.Text`, `CodeEmailSenderContract` (Task 7).
- Produces: `CodeEmailText.Compose(Locale, OneTimeCodeValue) → (string Subject, string Body)`; the two senders; `CapturingLoggerProvider` (records formatted messages with level and category).

Decisions: the Resend adapter is the anti-corruption layer of data-model.md: no Resend name or status leaves it. Resend over a typed `HttpClient` with `Timeout = 10 s`, `POST https://api.resend.com/emails`, `Authorization: Bearer <Resend:ApiKey>`, body `{ "from": "no-reply@mail.cardlane.eu", "to": ["<email>"], "subject": …, "text": … }` — no `html`, no tags, no tracking options. The email keys use plain `{code}` substitution, no MessageFormat (research R11). A connection that fails before the request is sent (`HttpRequestError.ConnectionError` or `NameResolutionError`) is `Refused(Unavailable, null)`: nothing left the server; any other transport failure is `Unclear`.

- [ ] **Step 1: Write the failing tests**

`ResendCodeEmailSenderTests.Maps_the_answer` (stub `HttpMessageHandler`, `FakeTimeProvider` at `2026-11-02T10:00Z`):

| Resend answer | Outcome |
|---|---|
| `200 {"id":"…"}` | `Sent` |
| `429` `name: daily_quota_exceeded` | `Refused(DailyLimit, 2026-11-03T00:00Z)` |
| `429` `name: monthly_quota_exceeded` | `Refused(Limit, null)` |
| `429` `name: rate_limit_exceeded`, `retry-after: 2` | `Refused(Unavailable, 2026-11-02T10:00:02Z)` |
| `429` `name: rate_limit_exceeded`, no header | `Refused(Unavailable, null)` |
| `422` `name: validation_error` | `Refused(Unavailable, null)` |
| `503` `name: service_unavailable` | `Refused(Unavailable, null)` |
| `500` `name: application_error` | `Unclear` |
| handler throws `TaskCanceledException` (timeout) | `Unclear` |
| handler throws `HttpRequestException` with `HttpRequestError.ResponseEnded` | `Unclear` |
| handler throws `HttpRequestException` with `HttpRequestError.ConnectionError` | `Refused(Unavailable, null)` |

Plus `Sends_text_from_the_mail_subdomain` (method, URL, bearer key, `from`, `to`, `subject` from `pl.json`, `text` contains the code, no `html` member) and `Times_out_after_ten_seconds` (the resolved client's `Timeout == TimeSpan.FromSeconds(10)`).

`DevelopmentLogCodeEmailSenderTests.Writes_the_email_to_the_log` → returns `Sent`; one log line `code-email to=ann@example.com locale=ru code=042917 subject=<ru subject>` followed by the body containing `042917`.

`CodeEmailTextTests.Composes_in_each_locale` — for `en`, `pl`, `ru`: the subject and body of that file, the body contains the code, no `{code}` left.

`SenderContractTests` subclasses `CodeEmailSenderContract` over `DevelopmentLogCodeEmailSender` and over `ResendCodeEmailSender` behind a stub handler (a `200` for the deliverable message, a `429 daily_quota_exceeded` for the refusal).

`CodeEmailSenderRegistrationTests`: `Development_uses_only_the_log`; `Production_uses_only_Resend` (with `Resend:ApiKey` and `Identity:CodeHashKey` set); `Production_without_a_Resend_key_does_not_start` — the factory throws, the message contains `Resend:ApiKey`.

- [ ] **Step 2: Run to verify they fail** — `scripts/test-fast.sh` — FAIL.

- [ ] **Step 3: Implement** the text composer, both senders and the registration; add the email keys in three languages.

- [ ] **Step 4: Run to verify they pass** — `scripts/test-fast.sh` — PASS.

- [ ] **Step 5: Commit** — `Add Resend and development log code email senders`

---

### Task 9: [FR-005] Error format for the API

**Executor:** subagent (Opus: the error format every endpoint relies on) · **Depends on:** 2, 4

**Files:**
- Create: `backend/src/HabitsTracker.Identity.Adapters/Http/{Problems, DomainExceptionHandler}.cs`, `backend/src/HabitsTracker.Host/Web/ApiRequestRules.cs`
- Modify: `backend/src/HabitsTracker.Host/Program.cs` (`AddProblemDetails`; `DomainExceptionHandler`; request bodies under `/api` limited to 16 KB; a body under `/api` with a content type other than `application/json` refused; a path under `/api` that no endpoint serves answers `not-found`, another method `invalid-request`)
- Modify: `backend/tests/HabitsTracker.Host.Tests/HostFactory.cs` (maps test-only endpoints under `/api/test/`: `POST /api/test/echo` binding a record with two required fields, `GET /api/test/throw` raising `InvalidOperationException("secret detail")`, `GET /api/test/domain/{error}` raising `DomainException` of that `DomainError`)
- Test: `backend/tests/HabitsTracker.Host.Tests/ErrorFormatTests.cs`

**Interfaces:**
- Consumes: `DomainException`, `DomainError` (Task 2); the host and `GET /api/locales` (Task 4).
- Produces: the error format of `contracts/http-api.md` for every endpoint under `/api`: `Problems.Create(int status, string code, DateTimeOffset? retryAt)`, `DomainExceptionHandler` with the mapping of data-model.md, "Error model", `traceId` on every problem, and the codes `invalid-request`, `not-found`, `internal-error`; the test-only endpoints, which Task 12 uses for the log test.

- [ ] **Step 1: Write the failing tests**

`ErrorFormatTests` (the shared table of `contracts/http-api.md`):

- `Malformed_bodies_are_invalid_request` — to `/api/test/echo`: `{}`, a field of the wrong type, and `not json` with `application/json` → `400` `invalid-request`.
- `A_body_over_16_KB_is_refused` — 16 KB + 1 byte → `413` `invalid-request`; exactly 16 KB of valid JSON is read.
- `Only_json_is_accepted` (research R19, T1) — a valid body as `text/plain` and as `application/x-www-form-urlencoded` → `415` `invalid-request`.
- `Unknown_paths_and_methods` — `GET /api/unknown` → `404` `not-found`; `POST /api/locales` → `405` `invalid-request`.
- `Domain_errors_map_to_the_error_model` — `/api/test/domain/{error}`: `InvalidEmail` → `400` `invalid-email`; `InvalidLocale` → `400` `invalid-locale`; `InvalidOneTimeCode` → `400` `invalid-code-format`; `InvalidSessionToken` → `401` `not-signed-in`; `CorruptRecord` → `500` `internal-error`.
- `An_unexpected_failure_is_internal_error` — `/api/test/throw` → `500` `internal-error`, and the body does not contain `secret detail`.
- `Every_problem_carries_a_trace_id` — each case above has `Content-Type: application/problem+json` and a non-empty `traceId` in W3C form (32 hexadecimal characters).

- [ ] **Step 2: Run to verify they fail** — `scripts/test-fast.sh` — FAIL.

- [ ] **Step 3: Implement** `Problems`, `DomainExceptionHandler` and the request rules.

- [ ] **Step 4: Run to verify they pass** — `scripts/test-fast.sh` — PASS.

- [ ] **Step 5: Commit** — `Add the error format of the API`

---

### Task 10: [US-1.1][US-1.3][US-1.8][FR-001][FR-003][FR-004][FR-005][FR-007][FR-014][FR-041] Request a code: API, sign-in email step and code step header

**Executor:** subagent · **Depends on:** 3, 5, 6, 7, 8, 9

**Files:**
- Modify: `IdentityEndpoints.cs` (`POST /api/code-requests`), `IdentityContextRegistration.cs` (`IdentityContextOptions.Create` from `Identity:CodeHashKey` and `Identity:CodeRequestLimits`, defaults `CodeRequestLimits.Default`; outside Development a missing key stops the start; in Development a missing key is replaced by 32 random bytes at each start, research R5), `backend/src/HabitsTracker.Host/Program.cs` (forwarded headers trusted only from `ForwardedHeaders:KnownNetwork`, set through `ForwardedHeadersOptions.KnownIPNetworks` with `System.Net.IPNetwork`, since `KnownNetworks` is obsolete in ASP.NET Core 10 (`ASPDEPR005`), the default loopback entries cleared)
- Create: `backend/tests/HabitsTracker.Host.Tests/CapturingCodeEmailSender.cs`
- Create: `frontend/src/app/sign-in/{sign-in.ts, sign-in.html, sign-in.css}` (email step at `/sign-in`, the states of `contracts/ui.md`, "Sign-in, email step", except "Session ended", which is Task 15's), `frontend/src/app/sign-in/{code-step.ts, code-step.html, code-step.css}` (`/sign-in/code`: where the code went, the remaining count, "Send a new code", "Use another email"; Task 13 adds the code field), `frontend/src/app/sign-in/sign-in-flow.service.ts` (`SignInFlow`), `frontend/src/app/time/format-moment.ts`, `frontend/src/app/api/problem.ts`, `frontend/src/app/validation/patterns.ts`
- Modify: `app.routes.ts` (`/sign-in/code`, title `signIn.title`; without a `SignInFlow` email it redirects to `/sign-in`), `frontend/public/i18n/{en,pl,ru}.json` (`signIn.email`, `signIn.emailInvalid`, `signIn.rememberMe`, `signIn.rememberMeHint`, `signIn.requestCode`, `signIn.requesting`, `signIn.codeSent`, `signIn.resendCode`, `signIn.otherEmail`, `signIn.retryAt` with `{time}`, `error.<code>` for every `code` of the endpoint and of the shared table of `contracts/http-api.md`, with `{traceId}` for `internal-error`; English texts as `contracts/ui.md`)
- Test: `backend/tests/HabitsTracker.Host.Tests/{CodeRequestEndpointTests, ValidationPatternParityTests}.cs`, `frontend/src/app/sign-in/{sign-in.spec.ts, code-step.spec.ts}`, `frontend/src/app/time/format-moment.spec.ts`, `frontend/src/app/validation/patterns.spec.ts`, `frontend/e2e/request-code.spec.ts`

**Interfaces:**
- Consumes: `RequestCode` (Task 7), stores (Task 3), senders (Task 8), the error format (Task 9), `LanguageService` (Task 4), `readCode` (Task 5), `app-message`, `expectVisibleFocus`, `pressTabUntil`, `forEachTheme` (Task 6), `testdata/validation-examples.json` (Task 2).
- Produces: `POST /api/code-requests` exactly as `contracts/http-api.md`; `CapturingCodeEmailSender` (replaces the sender in `HostFactory`; `LastCode(email)`, `Next = <CodeEmailOutcome>`), which Tasks 12–15 use; `patterns.ts`; `SignInFlow`; `formatMoment`; the email step and the code step's header, which Task 13 extends with the code field.

The page sends `locale` = `LanguageService.current()` and the email with surrounding whitespace removed; it refuses to send an email that fails `EMAIL_MAX_LENGTH` or `EMAIL_PATTERN` and shows `signIn.emailInvalid`. `retryAt` goes through `formatMoment` with the browser's time zone (`Intl.DateTimeFormat().resolvedOptions().timeZone`, UTC when it reports none); `traceId` only for `internal-error`; a request with no response shows `error.network`. `formatMoment`: the moment rounded up to the next minute (a moment already on the minute stays); the time with `{ hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }`; when the rounded moment's date in that time zone is not `now`'s, the date with `{ day: 'numeric', month: 'long' }`, a comma and the time. After a `202` the email step stores the email, the checkbox and the count in `SignInFlow` and navigates to `/sign-in/code`; nothing of them goes into the URL. "Send a new code" sends the same request again and, after a `202`, shows the new count; a refusal shows on the code step as on the email step. "Use another email" and the browser's Back return to `/sign-in` with the email and the checkbox from `SignInFlow`.

- [ ] **Step 1: Write the failing tests**

`CodeRequestEndpointTests` (`HostFactory` with `CapturingCodeEmailSender`, `Time` at `2026-11-02T10:00Z`):

- `Sends_a_code_in_the_page_language_and_reports_the_remaining_count` — `{email, locale: "pl"}` → `202` `{"remaining":4,"limit":5}`; the sender got locale `pl`.
- `Default_limits_are_five_per_email_and_ten_per_ip` — five requests for one email → `remaining` 4, 3, 2, 1, 0; the sixth → `429`, `code: "email-limit"`, `retryAt: "2026-11-03T10:00:00Z"`. Ten emails from one `Test-Remote-Address` → the eleventh `429`, `code: "ip-limit"`, `retryAt`, no `remaining` member.
- `Ipv6_addresses_of_one_64_prefix_share_the_limit` — ten from `2001:db8:1:2::<n>` → `2001:db8:1:2:ffff::1` refused, `2001:db8:1:3::1` accepted.
- `A_refused_request_does_not_count` — email A: five requests and a refused sixth from address X; then five other emails from X are accepted; the next is refused by `ip-limit` (10 counted, not 11).
- `An_unclear_send_counts_as_sent` — `Next = Unclear` → `202` `{"remaining":4,"limit":5}`, and the next request reports `remaining: 3`.
- `Email_service_refusals` — `Next = Refused(DailyLimit, t)` → `503`, `email-service-limit`, `retryAt` t; `Refused(Limit, null)` → `503`, `email-service-limit`, no `retryAt`; `Refused(Unavailable, null)` → `503`, `email-service-unavailable`.
- `Invalid_input` — email `ann` and email `" ann@example.com"` → `400` `invalid-email`; locale `de` → `400` `invalid-locale`; `Content-Type: application/problem+json`.
- `Forwarded_for_is_trusted_only_from_the_proxy_network` — `ForwardedHeaders:KnownNetwork = 172.30.0.0/24`: ten requests from remote `198.51.100.1`, each with a different `X-Forwarded-For` → the eleventh refused (`ip-limit`); from remote `172.30.0.5` with `X-Forwarded-For: 203.0.113.9`, ten accepted and the eleventh refused, while `203.0.113.10` through the same proxy is accepted.
- `Malformed_and_non_json_requests_store_nothing` — `{}` and the valid body as `text/plain` → `400` and `415` (Task 9's format); no code is sent and no code request is stored.
- `Code_request_answer_does_not_reveal_whether_a_user_exists` (Review Focus 3) — a seeded user's email and a new email: same status, same body, same header names (`Date` aside).
- `Development_without_a_key_generates_one` — Development with no `Identity:CodeHashKey` starts and sends a code; `Production_without_a_key_does_not_start` — the message names `Identity:CodeHashKey`; `appsettings*.json` hold no `CodeHashKey`.

`ValidationPatternParityTests.The_page_uses_the_cores_rules` — reads `frontend/src/app/validation/patterns.ts`; `EMAIL_PATTERN`, `ONE_TIME_CODE_PATTERN` and `EMAIL_MAX_LENGTH` equal `Email.Pattern`, `OneTimeCodeValue.Pattern` and `Email.MaxLength`.

`patterns.spec.ts`: `every shared example holds` — reads `testdata/validation-examples.json` and checks each email and code example against `EMAIL_MAX_LENGTH`, `EMAIL_PATTERN` and `ONE_TIME_CODE_PATTERN`.

`sign-in.spec.ts`: `"Remember me" is unchecked by default and explained` (the checkbox's `aria-describedby` names the element with `signIn.rememberMeHint`); `sends the email trimmed`; `does not send an invalid email and says why` (`ann`, 255 characters: `signIn.emailInvalid` in an `app-message` of kind `error` linked by `aria-describedby`, the field has `aria-invalid="true"` and the focus); `the button is disabled while sending and a second press sends nothing` (one request recorded, the button reads `signIn.requesting`); `no field has the focus when the page opens`; `shows each refusal` — one case per `code`, `signIn.retryAt` with the formatted time where present, `traceId` shown for `internal-error` only; `no response shows error.network`; `a sent code opens the code step with nothing in the URL` (URL exactly `/sign-in/code`).

`code-step.spec.ts`: `shows where the code went and the remaining count`; `send a new code requests again for the same email and shows the new count`; `a refused new code shows the refusal on this step`; `use another email returns to /sign-in with the email and the checkbox filled`; `without a requested code /sign-in/code opens /sign-in`.

`format-moment.spec.ts` (`now` `2026-11-02T10:00:00Z`, time zone `Europe/Warsaw`):

| `moment` | `en` | `pl` | `ru` |
|---|---|---|---|
| `2026-11-02T13:04:30Z` | `14:05` | `14:05` | `14:05` |
| `2026-11-02T13:04:00Z` | `14:04` | `14:04` | `14:04` |
| `2026-11-03T10:00:00Z` | `November 3, 11:00` | `3 listopada, 11:00` | `3 ноября, 11:00` |
| `2026-11-02T22:59:30Z` | `November 3, 00:00` | `3 listopada, 00:00` | `3 ноября, 00:00` |

`request-code.spec.ts`: `requesting a code opens the code step with the remaining count @main-path` (`4 of 5 left`); `the email never appears in the URL` (the URL of every navigation of the flow contains neither the email nor `@`); `Back from the code step returns to the email step with the email filled in`; `reloading the code step opens the email step`; `the code email is in the page language` (switch to `ru`, request → the log line has `locale=ru`; the remaining count is in Russian); `the email step by keyboard alone` (`pressTabUntil` the field, type, `Space` on "Remember me", `Enter` on the button; `expectVisibleFocus` on each; an invalid email gives a message with `role="alert"`); `the email step and the code step at every width in both themes @width` (`forEachTheme`: `expectNoHorizontalScroll`, every control in viewport).

- [ ] **Step 2: Run to verify they fail** — `scripts/test-fast.sh`; `npm --prefix frontend run e2e -- request-code` — FAIL.

- [ ] **Step 3: Implement** the endpoint, the options, forwarded headers, the patterns and the email step.

- [ ] **Step 4: Run to verify they pass** — `scripts/test-full.sh` — PASS.

- [ ] **Step 5: Commit** — `Add code request endpoint and sign-in email step`

---

### Task 11: [US-1.2][US-1.3][FR-002][FR-008][FR-009][FR-013] Sign in and authenticate: core

**Executor:** developer · **Depends on:** 3, 7

**Files:**
- Modify: `OneTimeCode.cs` (`Verify`), `Session.cs` (`IsExpired`, `RecordActivity`)
- Create: `backend/src/HabitsTracker.Identity.Core/UseCases/{SignIn, AuthenticateSession, GetCurrentUser}.cs`
- Modify: `backend/tests/Shared/PortContracts/OneTimeCodesContract.cs` (adds `Saving_a_used_code_deletes_it`, run against the fake and PostgreSQL)
- Test: `backend/tests/HabitsTracker.Identity.Core.Tests/{OneTimeCodeVerifyTests, SignInTests, SessionExpiryTests, AuthenticateSessionTests}.cs`

**Interfaces:**
- Consumes: Task 2 types, Task 7 fakes.
- Produces: `SignIn`, `AuthenticateSession`, `GetCurrentUser` with the signatures of "Entity and type model".

`SignIn` inside one `RunAsync`: `FindExclusiveAsync` → `Verify` → `SaveAsync` the code (a used code is deleted, a wrong entry's attempt is stored; the transaction commits either way) → on `SignIn`: find the user or `AddOrGetAsync(User.SignUp(email, TimeZone.FromBrowser(browserTimeZone), locale, now))`, `Session.Open`, `AddAsync`, then, if `heldSession` is given and its session exists, `DeleteAsync` it. The three aggregates change in one transaction, the exception data-model.md records.

- [ ] **Step 1: Write the failing tests**

`OneTimeCodeVerifyTests.Verifies` — code issued at `T0`, `EntryAttempts` before the entry:

| Attempts before | Entered | Now | Result | Attempts after |
|---|---|---|---|---|
| 0 | correct | T0 + 9 min 59 s | `SignIn` | 1 |
| 0 | correct | T0 + 10 min | `NewCodeRequired` | 0 |
| 0 | wrong | T0 + 1 min | `Incorrect` | 1 |
| 3 | wrong | T0 + 1 min | `Incorrect` | 4 |
| 4 | wrong | T0 + 1 min | `NewCodeRequired` | 5 |
| 4 | correct | T0 + 1 min | `SignIn` | 5 |
| 5 | correct | T0 + 1 min | `NewCodeRequired` | 5 |

Plus `A_used_code_refuses_a_second_entry` — after a `SignIn`, `IsUsed` is true and a second correct entry gives `NewCodeRequired`; and `An_entry_after_expiry_changes_nothing` (row 2: attempts stay 0, `IsUsed` stays false).

`SignInTests` (fakes, `FakeTimeProvider`):

- `First_sign_in_creates_the_user_with_the_browser_time_zone_and_the_locale` — `Europe/Warsaw`, `pl` → `SignedIn`; user `Europe/Warsaw`, recognized, locale `pl`; session not remembered; the code is gone.
- `An_unknown_browser_time_zone_gives_UTC_not_recognized` — `null` and `Mars/Olympus_Mons`.
- `A_later_sign_in_keeps_the_time_zone_and_the_locale` — user `Europe/Warsaw`, `ru`; sign in with `America/New_York`, `en` → unchanged.
- `Email_case_does_not_matter` — signed up as `Ann@example.com`; a code for `ann@example.com` signs in the same user.
- `Remember_me_opens_a_remembered_session`.
- `A_used_code_requires_a_new_code`; `No_code_requires_a_new_code`; `A_wrong_code_is_counted` (attempts 1 stored).
- `Code_of_another_email_does_not_sign_in` (Review Focus 5) — codes for A and B; A's code entered with B → `CodeIncorrect`, B's attempts 1; A then signs in with its code.
- `A_held_session_is_revoked_by_the_sign_in` — a session S of user A exists; sign in as A (or as B) with `heldSession` = S's token → S is gone, the new session exists; with an unknown `heldSession` the sign-in succeeds and deletes nothing.

`SessionExpiryTests.Expires_after_30_days_without_activity` — last activity `L`:

| Now | `IsExpired` |
|---|---|
| L + 29 d | false |
| L + 30 d − 1 s | false |
| L + 30 d | true |

`SessionExpiryTests.Activity_on_an_expired_session_is_refused` — `RecordActivity(L + 30 d)` throws `InvalidOperationException`.

`AuthenticateSessionTests`: `Activity_extends_the_session` — at L + 29 d returns the user and the session with `LastActivityAt == now`, and again at that + 29 d; `An_expired_session_is_not_signed_in` (L + 30 d → `null`, last activity unchanged); `An_unknown_token_is_not_signed_in`. `GetCurrentUser` returns the stored user, and throws `InvalidOperationException` when there is none (users are never deleted in this epic, so after authentication one always exists).

- [ ] **Step 2: Run to verify they fail** — `dotnet test backend/tests/HabitsTracker.Identity.Core.Tests` — FAIL.
- [ ] **Step 3: Write `Verify`, the session expiry and the three use cases.**
- [ ] **Step 4: Run to verify they pass** — `scripts/test-fast.sh` — PASS.
- [ ] **Step 5: Commit** — `Add sign in and session authentication use cases`

---

### Task 12: [US-1.2][US-1.3][FR-008][FR-011][FR-012][FR-013][FR-014][FR-017] Sign in: API, session cookie, current user

**Executor:** subagent (Opus: authentication design) · **Depends on:** 10, 11

**Files:**
- Create: `backend/src/HabitsTracker.Identity.Adapters/Http/{SessionAuthenticationHandler, SessionCookie}.cs`
- Modify: `IdentityEndpoints.cs` (`POST /api/sessions`, `GET /api/me`), `IdentityContextRegistration.cs`, `Program.cs` (authentication, authorization)
- Modify: `backend/tests/HabitsTracker.Host.Tests/HostFactory.cs` (adds `Task<SignedInClient> SignInAsync(string email, bool rememberMe = false)`; `SignedInClient` = an `HttpClient` sending that session's cookie, and the token)
- Modify: `backend/src/HabitsTracker.Host/appsettings.json` (`Logging:Console:FormatterOptions:IncludeScopes: true`, so `TraceId` is on every log line of a request)
- Test: `backend/tests/HabitsTracker.Host.Tests/{SignInEndpointTests, CurrentUserEndpointTests, SessionCookieTests, ProductionLogTests}.cs`

**Interfaces:**
- Consumes: `SignIn`, `AuthenticateSession`, `GetCurrentUser` (Task 11); `CapturingCodeEmailSender` (Task 10), `Problems` (Task 9).
- Produces: scheme `session` with claims `sub` and `sid`; endpoints marked signed in with `.RequireAuthorization()`; `SessionCookie.Append(HttpResponse, SessionToken, bool remembered)` and `SessionCookie.Clear(HttpResponse)`; `HostFactory.SignInAsync`, which Task 15 uses.

Decisions (research R4, R20): the handler creates the token with `SessionToken.Create` and treats `DomainException(InvalidSessionToken)` as not signed in, calls `AuthenticateSession`, and for a Remembered session appends the cookie again with `Max-Age=2592000` on that response. The challenge answers `401` problem `not-signed-in` and clears the cookie. The cookie is appended by hand, so its attributes are exactly those of Global Constraints. `POST /api/sessions` passes the request's `session` cookie, when it parses, to `SignIn` as `heldSession`, and never reuses its value.

- [ ] **Step 1: Write the failing tests**

`SignInEndpointTests` (a code requested through the API, read from `CapturingCodeEmailSender`):

- `A_correct_code_signs_in` → `200` `{"email":"ann@example.com","timeZone":"Europe/Warsaw","timeZoneRecognized":true,"locale":"pl"}`.
- `A_wrong_code` → `401` `code-incorrect`.
- `Code_at_ten_minutes_requires_a_new_code` (Review Focus 1) — `Time` advanced exactly 10 min → `401` `new-code-required`.
- `Reused_code_requires_a_new_code` (Review Focus 1).
- `Correct_code_after_five_wrong_entries_requires_a_new_code` (Review Focus 2).
- `Ten_concurrent_wrong_entries_leave_the_code_exhausted` (Review Focus 2) — exactly 4 answers are `code-incorrect`, 6 are `new-code-required`; the correct code then gets `new-code-required`.
- `Code_of_another_email_does_not_sign_in` (Review Focus 5) — A's code with email B (B has a code) → `401` `code-incorrect`; A then signs in with it.
- `An_earlier_code_no_longer_signs_in` (US-1.1, scenario 5) — two codes requested; the first → `401` `code-incorrect`; the second signs in.
- `Email_case_does_not_matter` (US-1.2, scenario 3) — signed up as `Ann@example.com`; a code requested and entered for `ann@example.com` signs in the same user.
- `Malformed_input_counts_no_attempt` — `04291a` → `400` `invalid-code-format`; `invalid-email`, `invalid-locale` likewise; `entry_attempts` stays 0.
- `Sign_in_answer_does_not_reveal_whether_a_user_exists` (Review Focus 3) — a wrong code for a seeded user's email and for a new email: same status, body and header names; with no code for either: both `new-code-required`.
- `A_sign_in_with_a_held_cookie_revokes_it_and_issues_a_new_token` (research R19, S4) — sign in, then sign in again (same or another user) sending the first cookie → the new `Set-Cookie` value differs from the first, and a request with the first value → `401` `not-signed-in`; a request carrying a made-up `session` value before sign-in → the issued token is not that value.
- `Every_field_is_required` — a body without `rememberMe` → `400` `invalid-request`; `timeZone: null` is accepted.

`SessionCookieTests`:

- `A_session_cookie_without_remember_me_has_no_expiry` — `Set-Cookie` is `session=<43 chars>; path=/; secure; samesite=lax; httponly` with no `expires`, `max-age` or `domain`.
- `A_remembered_session_cookie_lasts_30_days` — `max-age=2592000`; after `Time` + 1 day a `GET /api/me` answers with the cookie again, `max-age=2592000`.
- `The_cookie_holds_only_the_session_token` — the only cookie set; `identity.sessions.id` equals SHA-256 of the decoded token.
- `A_request_extends_the_session` — `GET /api/me` at + 29 days → `200`; at + 29 more days → `200`; then + 30 days idle → `401`.
- `An_unknown_expired_or_malformed_cookie_is_not_signed_in_and_is_cleared` — an unknown token, an expired session and the value `abc`: each `401` `not-signed-in` and a `Set-Cookie` for `session` with an `expires` in the past.

`CurrentUserEndpointTests`: `Me_requires_a_session` (no cookie → `401` `not-signed-in`); `Me_reports_an_unrecognized_time_zone` (`timeZone: null` → `"UTC"`, `false`); `Me_of_user_B_is_never_user_A` (cross-user, FR-017).

`ProductionLogTests` (SC-005, research R20) — environment `Production` with `CapturingCodeEmailSender` and `CapturingLoggerProvider` at `Trace`, scopes included:
- `No_secret_or_email_reaches_the_production_logs` — request a code, enter a wrong one, enter the right one, call `GET /api/me`; no captured message or scope contains the code as a whole word, the email, or the session token.
- `A_500_and_its_log_line_carry_the_same_trace_id` — the throwing test endpoint of Task 9 (`/api/test/throw`): the `traceId` of the response appears in the scope of the logged exception.

- [ ] **Step 2: Run to verify they fail** — `scripts/test-fast.sh` — FAIL.
- [ ] **Step 3: Implement** the handler, the cookie and the two endpoints.
- [ ] **Step 4: Run to verify they pass** — `scripts/test-fast.sh` — PASS.
- [ ] **Step 5: Commit** — `Add sign in endpoint, session cookie and current user`

---

### Task 13: [US-1.2][US-1.3][US-1.6][US-1.9][FR-009][FR-014][FR-018][FR-019][FR-041][FR-042] Sign in: code step, main screen, guard

**Executor:** subagent · **Depends on:** 12

**Files:**
- Modify: `frontend/src/app/sign-in/code-step.*` (the code field, `inputmode="numeric"`, `autocomplete="one-time-code"`, focused when the step opens; whitespace stripped before sending, then checked against `ONE_TIME_CODE_PATTERN`; sends the email, `rememberMe` from `SignInFlow`, `timeZone` from `Intl.DateTimeFormat().resolvedOptions().timeZone`, `locale`; the states of `contracts/ui.md`, "Sign-in, code step")
- Create: `frontend/src/app/main/{main.ts, main.html, main.css}`, `frontend/src/app/session/{current-user.service.ts, signed-in.guard.ts, not-signed-in.interceptor.ts}`
- Modify: `app.routes.ts`, `app.config.ts`, `frontend/public/i18n/{en,pl,ru}.json` (`signIn.code`, `signIn.signIn`, `signIn.signingIn`, `main.noHabits`, `main.loading`, `main.timeZoneNotRecognized`; the `error.<code>` keys of `POST /api/sessions`, added in Task 10, read as `contracts/ui.md`)
- Test: `frontend/src/app/sign-in/code-step.spec.ts`, `frontend/src/app/main/main.spec.ts`, `frontend/src/app/session/signed-in.guard.spec.ts`, `frontend/e2e/sign-in.spec.ts`

**Interfaces:**
- Consumes: `POST /api/sessions`, `GET /api/me` (Task 12); `LanguageService` (Task 4); `ThemeService`, `app-message`, the e2e helpers (Task 6); `SignInFlow`, the code step (Task 10).
- Produces: `CurrentUserService` (`user`, `load()`), `signedInGuard`, `notSignedInInterceptor`, the main screen, which Task 15 extends with the sign-out actions.

Decisions: the application has no return-address parameter; after sign-in it always navigates to `/`, and the guard sends a visitor to `/sign-in` with no query. A signed-in user opening `/sign-in` or `/sign-in/code` goes to `/`. After `load()`, the user's locale is applied through `LanguageService.use` (spec, Assumptions: the stored locale wins). `new-code-required` returns to `/sign-in` with the email filled in and its message; `code-incorrect` clears the field and returns the focus to it; after "Send a new code" (Task 10) answers `202`, the field is cleared and takes the focus, since the earlier code no longer works (FR-002); the button is disabled and reads `signIn.signingIn` while the request runs. A successful sign-in calls `SignInFlow.clear()`. The main screen shows only the page frame while `GET /api/me` runs, and `main.loading` once it has run 1 second.

- [ ] **Step 1: Write the failing tests**

Units: `code-step.spec.ts` — `the code field has the focus when the step opens`; `sends the browser time zone, remember me and the page language`; `strips whitespace from the code and refuses one that is not six digits`; `is not sent by itself at the sixth digit`; `the button is disabled while signing in`; `code-incorrect clears the field, focuses it and stays on the step`; `a new code clears the field and focuses it`; `new-code-required returns to /sign-in with the email and its message`. `main.spec.ts` — `says there are no habits yet`; `shows the time zone notice only when not recognized`; `says loading only after one second` (fake timers: nothing at 999 ms, `main.loading` at 1000 ms). `signed-in.guard.spec.ts` — `a visitor is sent to /sign-in`.

`frontend/e2e/sign-in.spec.ts`:

```ts
test('a new user signs in and lands on the main screen @main-path') // request, readCode, enter → URL '/', main.noHabits text
test('the browser time zone is stored')                            // context timezoneId 'Europe/Warsaw' → GET /api/me timeZone 'Europe/Warsaw'
test('an unknown browser time zone gives UTC and a notice')        // addInitScript: resolvedOptions().timeZone undefined → notice shown
test('sign-in ignores a return address to another domain')         // Review Focus 4: /sign-in?returnUrl=https://evil.example/&redirect=//evil.example&next=https%3A%2F%2Fevil.example → URL exactly http://localhost:4200/
test('a visitor opening / is sent to /sign-in')                    // URL exactly http://localhost:4200/sign-in
test('a signed-in user opening /sign-in is sent to /')
test('a session without Remember me ends with the browser session') // cookie 'session' expires === -1; a new context from storageState without cookies of expires -1 → '/' goes to /sign-in
test('a remembered session survives a browser restart')            // cookie expires ≈ now + 30 days (±60 s); new context from storageState → stays on '/'
test('sign-in by keyboard alone @main-path')                       // both steps with Tab, typing and Enter only; expectVisibleFocus on each control; lands on '/'
test('the chosen theme stays after sign-in')                       // SC-016: switch to light on /sign-in, sign in → '/' is light
test('the code field offers the code from the email')              // the field's autocomplete is 'one-time-code' and inputmode 'numeric'
test('sign-in and main screen at every width in both themes @width') // forEachTheme: every control in viewport; expectNoHorizontalScroll on both
```

- [ ] **Step 2: Run to verify they fail** — `scripts/test-fast.sh`; `npm --prefix frontend run e2e -- sign-in` — FAIL.
- [ ] **Step 3: Implement** the code step, the main screen, the service, the guard and the interceptor.
- [ ] **Step 4: Run to verify they pass** — `scripts/test-full.sh` — PASS.
- [ ] **Step 5: Commit** — `Add sign-in code step and main screen`

---

### Task 14: [US-1.4][US-1.5][US-1.8][FR-015][FR-023][FR-033] Sign out, locale, expired records: core

**Executor:** developer · **Depends on:** 11

**Files:**
- Create: `backend/src/HabitsTracker.Identity.Core/UseCases/{SignOut, SignOutEverywhere, ChangeLocale, DeleteExpiredRecords}.cs`
- Test: `backend/tests/HabitsTracker.Identity.Core.Tests/{SignOutTests, ChangeLocaleTests, DeleteExpiredRecordsTests}.cs`

**Interfaces:**
- Produces: the four use cases with the signatures of "Entity and type model". `DeleteExpiredRecords` computes every cutoff itself (`now − CodeRequestLimits.Window`, `now − Session.IdleTimeout`) and passes `OneTimeCode.MaxEntryAttempts` to `DeleteExpiredOrExhaustedAsync`; a test with a fake records those arguments.

- [ ] **Step 1: Write the failing tests**

- `SignOutTests`: `Sign_out_deletes_only_this_session`; `Sign_out_everywhere_deletes_every_session_of_the_user_and_no_other`.
- `ChangeLocaleTests.Changes_only_this_users_locale`.
- `DeleteExpiredRecordsTests.Deletes_what_is_past_its_period`, at `N`:

| Record | State | Deleted |
|---|---|---|
| one-time code | expires at N | yes |
| one-time code | expires at N + 1 s | no |
| one-time code | 5 entry attempts, expires at N + 5 min | yes |
| code request | requested at N − 24 h | yes |
| code request | requested at N − 24 h + 1 s | no |
| session | last activity N − 30 d | yes |
| session | last activity N − 30 d + 1 s | no |

  Result `DeletedRecords(2, 1, 1)`.
- `DeleteExpiredRecordsTests.Passes_the_core_rules_to_the_stores` — the fakes record `DeleteExpiredOrExhaustedAsync(N, 5)`, `DeleteRequestedAtOrBeforeAsync(N − 24 h)`, `DeleteInactiveAtOrBeforeAsync(N − 30 d)`.

- [ ] **Step 2: Run to verify they fail** — FAIL.
- [ ] **Step 3: Write the four use cases.**
- [ ] **Step 4: Run to verify they pass** — `scripts/test-fast.sh` — PASS.
- [ ] **Step 5: Commit** — `Add sign out, locale change and expired record use cases`

---

### Task 15: [US-1.4][US-1.5][US-1.8][FR-015][FR-017][FR-018][FR-019][FR-023][FR-041] Sign out, sign out everywhere, locale change: API and main screen

**Executor:** subagent · **Depends on:** 13, 14

**Files:**
- Modify: `IdentityEndpoints.cs` (`DELETE /api/sessions/current`, `DELETE /api/sessions`, `PUT /api/me/locale`), `frontend/src/app/main/*` (sign out, and sign out everywhere with its on-page confirmation, as `contracts/ui.md`, "Main screen"), `current-user.service.ts`, `not-signed-in.interceptor.ts` ("Session ended"), `frontend/src/app/sign-in/sign-in.*` (the `signIn.sessionEnded` info), `language.service.ts` (signed in: `use` also calls `PUT /api/me/locale`), `frontend/public/i18n/{en,pl,ru}.json` (`main.signOut`, `main.signOutEverywhere`, `main.signOutEverywhereConfirm`, `main.confirm`, `main.cancel`, `signIn.sessionEnded`)
- Test: `backend/tests/HabitsTracker.Host.Tests/{SignOutEndpointTests, LocaleEndpointTests, CrossUserTests}.cs`, `frontend/src/app/i18n/language.service.spec.ts`, `frontend/src/app/main/main.spec.ts`, `frontend/src/app/session/not-signed-in.interceptor.spec.ts`, `frontend/e2e/sessions.spec.ts`, `frontend/e2e/locale.spec.ts`

**Interfaces:**
- Consumes: `SignOut`, `SignOutEverywhere`, `ChangeLocale` (Task 14); `HostFactory.SignInAsync` (Task 12).
- Produces: the three endpoints of `contracts/http-api.md`.

Decisions: sign out everywhere replaces its button with `main.signOutEverywhereConfirm` and the buttons `main.confirm` and `main.cancel`, moves the focus to "Cancel", and sends `DELETE /api/sessions` only on "Sign out everywhere"; "Cancel" or `Escape` restores the button and returns the focus to it. Both sign-out buttons are disabled while a sign-out runs. The interceptor navigates to `/sign-in` with the router state `{ reason: 'session-ended' }` only when `CurrentUserService.user()` was not `null` at the `401`; the sign-in page then shows `signIn.sessionEnded` as an info above the form; the reason never goes into the URL. Sign out and sign out everywhere set `CurrentUserService.user` to `null` before they navigate to `/sign-in`, so a late request of this tab that answers `401` never shows "Session ended" to a user who has just signed out.

- [ ] **Step 1: Write the failing tests**

- `SignOutEndpointTests`: `Sign_out_revokes_only_the_current_session` — two sessions of one user; one signs out → `204`, cookie cleared; its next `GET /api/me` → `401`; the other → `200`. `Sign_out_everywhere_revokes_every_session` — two sessions, one remembered → both `401` on the next request.
- `LocaleEndpointTests`: `{locale: "ru"}` → `204`, `GET /api/me` `locale: "ru"`; `{locale: "de"}` → `400` `invalid-locale`.
- `CrossUserTests` (FR-017, SC-003), users A and B: `B_signing_out_leaves_A_signed_in`; `B_signing_out_everywhere_leaves_A_signed_in`; `B_changing_the_locale_leaves_A_unchanged`; `Every_signed_in_endpoint_has_a_cross_user_test` — the endpoints with authorization metadata in `EndpointDataSource` are exactly `GET /api/me`, `PUT /api/me/locale`, `DELETE /api/sessions/current`, `DELETE /api/sessions`.
- `language.service.spec.ts`: `signed in, use() changes the user's locale`.
- `main.spec.ts`: `sign out everywhere asks on the page and sends nothing until confirmed`; `the focus moves to Cancel`; `Cancel and Escape restore the button and its focus`; `both buttons are disabled while signing out`; `plain sign out asks nothing`.
- `not-signed-in.interceptor.spec.ts`: `a 401 after a loaded user brings the session-ended state`; `a 401 with no user loaded brings plain /sign-in`; `own sign out brings plain /sign-in, even if a late request answers 401`.
- `sessions.spec.ts` (two browser contexts): `sign out ends only this browser's session @main-path`; `sign out everywhere ends every session` — A confirms on the page; B remembered; B's next navigation lands on `/sign-in`, also from a new context restored from B's storage state; `a session ended while the page is open says so` — A signs out everywhere; B, still on `/`, switches the language → `/sign-in` with `signIn.sessionEnded`, and the URL is exactly `/sign-in`; `main screen actions by keyboard alone` — sign out everywhere, `Enter` lands on "Cancel", `Escape`, then confirm, with `expectVisibleFocus` on each; `main screen actions at every width in both themes @width` — sign out, sign out everywhere, both switchers and both links in viewport, no horizontal scroll.
- `locale.spec.ts`: `switching the language changes the user's locale and another browser shows it at its next load` (A switches to `ru`; B reloads → `ru`); `after sign-in the user's locale wins over the browser's choice` (signed up in `ru`; a `pl-PL` context signs in → `ru`).

- [ ] **Step 2: Run to verify they fail** — FAIL.
- [ ] **Step 3: Implement** the endpoints and the main screen actions.
- [ ] **Step 4: Run to verify they pass** — `scripts/test-full.sh` — PASS.
- [ ] **Step 5: Commit** — `Add sign out, sign out everywhere and locale change`

---

### Task 16: [US-1.7][FR-019][FR-024][FR-025][FR-026][FR-028][FR-029][FR-030][FR-031][FR-041][FR-042] Privacy and Terms pages, record of processing

**Executor:** subagent · **Depends on:** 13

**Files:**
- Modify: `frontend/src/app/privacy/*`, `frontend/src/app/terms/*`, `frontend/src/app/sign-in/*` (the acceptance sentence under the sign-in button), `frontend/public/i18n/{en,pl,ru}.json`
- Create: `docs/legal/record-of-processing.md`
- Test: `frontend/src/app/legal/legal-pages.spec.ts`, `frontend/e2e/legal.spec.ts`

**Interfaces:**
- Consumes: `AppShell` links (Task 4); the baseline and the e2e helpers (Task 6); sign-in page (Task 13).

Decisions: the page texts are keys `privacy.section.<n>.title` and `.body` for n = 1…15 in the order of the legal pages design, "Privacy page", and `terms.section.<n>.title` and `.body` for n = 1…12 in the order of its "Terms page" table; `legal.englishPrevails` (in English: the Polish and Russian translations are for convenience and this English version prevails); `legal.effectiveDate` = the date of the first deployment; `legal.controllerName` and `legal.contactEmail`, the developer's values (FR-028) below. The acceptance sentence is three keys around two links (`signIn.acceptance.before`, `.middle`, `.after`), so no HTML is rendered from a translation. Section 12 of the Privacy page (the legal pages design, item 12) names the session cookie, the language choice and the theme choice kept in the browser (FR-030). Section headings are `h2`; long words and addresses wrap anywhere (`contracts/ui.md`, "Privacy and Terms"). Polish and Russian texts are drafted by the agent; the developer reviews them and the record of processing before the commit.

**Developer's values (FR-028):** full name `Paweł Nikanowicz`, the same spelling in all three languages; contact email `pavelnikanovich@gmail.com`. Both pages and `docs/legal/record-of-processing.md` use exactly these values.

- [ ] **Step 1: Write the failing tests**

`legal-pages.spec.ts`: `the Privacy page shows 15 sections and the Terms page 12, in every language` (title and body non-empty); `the Polish and Russian pages say the English version prevails`; `both pages name the controller and link the contact email` (in every file `legal.controllerName` is `Paweł Nikanowicz` and `legal.contactEmail` is `pavelnikanovich@gmail.com`; the pages show the name and a `mailto:pavelnikanovich@gmail.com` link); `the Privacy page names Hetzner and Resend as recipients in every language` (section 5 body contains both names). `the pages state the promised periods` — in every language, the Terms section on complaints (item 6) contains `14`, and the Privacy section on retention (item 7) contains `7` and `30`. `the Privacy page names what the browser keeps` — the English section 12 body contains `cookie`, `language` and `theme`.

`legal.spec.ts`: `a visitor opens both pages from the sign-in page`; `a signed-in user opens both pages from the main screen`; `the sign-in page states the acceptance sentence with both links and has no acceptance checkbox` — English text exactly "By signing in you accept the Terms and acknowledge the Privacy notice", the only checkbox is "Remember me"; `both pages at every width in both themes @width` — `forEachTheme`: no horizontal scroll, both switchers and the links in viewport; `both pages by keyboard alone` — from the sign-in page, `pressTabUntil` the Privacy link, `Enter`, the focus is on the `h1`; the same for Terms; `both pages load nothing from other servers`.

- [ ] **Step 2: Run to verify they fail** — FAIL.
- [ ] **Step 3: Write the texts** in three languages from FR-030 and FR-031 and the legal pages design, the two pages and the record of processing (purposes, data, recipients, transfers and retention equal to the Privacy page).
- [ ] **Step 4: Run to verify they pass** — `scripts/test-full.sh` — PASS.
- [ ] **Step 5: Developer review** of the Polish and Russian texts and of `docs/legal/record-of-processing.md`; corrections applied.
- [ ] **Step 6: Commit** — `Add Privacy and Terms pages and the record of processing`

---

### Task 17: [FR-039] Request timing metrics

**Executor:** subagent · **Depends on:** 1

**Files:**
- Create: `backend/src/HabitsTracker.Host/Metrics/{RequestTimingMiddleware, RequestTimingHistogram, RequestTimingFlusher, RequestTiming}.cs`, `backend/src/HabitsTracker.Host/Operations/Migrations/<timestamp>_CreateOperationsSchema.cs`, `ops/latency-p95.sh`, `ops/tests/latency-p95.test.sh`
- Modify: `OperationsDbContext.cs`, `Program.cs` (middleware first in the pipeline; migrates `OperationsDbContext` when `Database:MigrateOnStart`)
- Test: `backend/tests/HabitsTracker.Host.Tests/RequestTimingTests.cs`

**Interfaces:**
- Produces: `operations.request_timings(hour, endpoint, bucket_upper_ms, count)`; `RequestTimingHistogram.Bucket(TimeSpan duration) → int?` (`null` = +∞); `ops/latency-p95.sh <since>`.

Decisions (research R9): `endpoint` is `"<METHOD> <route pattern>"` (`GET /api/me`), `static` for files and the application fallback. The middleware measures with `TimeProvider.GetTimestamp()` and `GetElapsedTime`, so tests drive durations with `FakeTimeProvider`. The flusher runs once a minute on a `PeriodicTimer` over `TimeProvider` and upserts per hour, endpoint and bucket. The upsert uses the primary key (`hour`, `endpoint`, `bucket_upper_ms`) and the +∞ bucket is stored as `bucket_upper_ms = 2147483647` (data-model.md, "Schema `operations`").

`latency-p95.sh <since>` reads the rows with `hour >= date_trunc('hour', <since>)` through `docker compose exec db psql` and prints exactly two lines: `within 300 ms: <share with one decimal>%` and `p95 bucket: <upper bound> ms` (`+inf` for the last bucket).

- [ ] **Step 1: Write the failing tests**

`RequestTimingTests`: `Bucket` table — 0 ms → 10; 10 ms → 10; 10.1 ms → 25; 300 ms → 300; 300.1 ms → 400; 5000 ms → 5000; 5000.1 ms → +∞. `A_request_timing_reaches_the_metrics_without_personal_data` (FR-039) — `GET /api/locales`, `Time` + 1 min → one row: `hour` = the hour of the request, `endpoint` `GET /api/locales`, `count` 1; the table's columns are exactly the four, its primary key is (`hour`, `endpoint`, `bucket_upper_ms`). `Counts_add_up_within_an_hour`. `The_unbounded_bucket_is_stored_as_int_max` — a request of 5000.1 ms (an endpoint mapped only in the test, which advances `Time` by that much) gives a row with `bucket_upper_ms = 2147483647`.

`ops/tests/latency-p95.test.sh` against the compose database with seeded rows: 95 requests ≤ 300 ms and 5 in the 400 bucket → `within 300 ms: 95.0%`, `p95 bucket: 300 ms`; 94 and 6 → `94.0%`, `p95 bucket: 400 ms`.

- [ ] **Step 2: Run to verify they fail** — FAIL.
- [ ] **Step 3: Implement** the middleware, histogram, flusher, migration and script.
- [ ] **Step 4: Run to verify they pass** — `scripts/test-full.sh` — PASS.
- [ ] **Step 5: Commit** — `Add request timing metrics`

---

### Task 18: [FR-030][FR-032][FR-033] Hourly cleanup, log retention and deletion on request

**Executor:** subagent · **Depends on:** 3, 14, 17

**Files:**
- Create: `backend/src/HabitsTracker.Host/Cleanup/HourlyCleanup.cs`, `ops/journald/habits.conf`, `ops/delete-user.sh`, `ops/tests/lib/identity-schema.sh`, `ops/tests/delete-user.test.sh`
- Test: `backend/tests/HabitsTracker.Host.Tests/{HourlyCleanupTests, JournaldRetentionTests}.cs`

**Interfaces:**
- Consumes: `DeleteExpiredRecords` (Task 14), `OperationsDbContext` (Task 17).

Decisions: a `BackgroundService` runs once at start and then every hour on a `PeriodicTimer` over `TimeProvider`; each run calls `DeleteExpiredRecords` and deletes `request_timings` rows with `hour < now − 13 months`. `ops/delete-user.sh <email>` (research R22; runbook, "Delete a user's data on request") asks for the email again, then deletes in one transaction the user's sessions, the user, and the one-time code and code requests of that email, through `docker compose exec db psql` with the compose files in `HABITS_COMPOSE_FILES`, and prints the rows deleted per table. `ops/tests/lib/identity-schema.sh` builds the `migrate-identity` bundle locally and applies it to the compose database, so shell tests run on the real schema.

- [ ] **Step 1: Write the failing tests**

- `HourlyCleanupTests.Cleanup_runs_every_hour` — an expired code, a code request 25 hours old and a session idle 31 days are stored; `Time` + 1 h → all three gone, valid records kept.
- `HourlyCleanupTests.Request_timings_older_than_13_months_are_deleted` — a row 13 months and 1 hour old deleted, one 12 months old kept.
- `JournaldRetentionTests.Logs_are_kept_seven_days` — `ops/journald/habits.conf` has section `[Journal]` with `MaxRetentionSec=7day` and `MaxFileSec=1day`.
- `ops/tests/delete-user.test.sh` (compose, real schema): users A and B, each with two sessions, a code and code requests; deleting A → no row of schema `identity` holds A's email or A's id, B's rows are all there; a wrong email typed at the prompt → nothing deleted, exit 1.

- [ ] **Step 2: Run to verify they fail** — FAIL.
- [ ] **Step 3: Implement** the job, the drop-in and the deletion script.
- [ ] **Step 4: Run to verify they pass** — `scripts/test-fast.sh` and `bash ops/tests/delete-user.test.sh` — PASS.
- [ ] **Step 5: Commit** — `Add hourly cleanup, journal retention and deletion on request`

---

### Task 19: [FR-035][FR-036] Backup, restore check and live restore

**Executor:** subagent (Opus, effort high: the live database) · **Depends on:** 1

**Files:**
- Create: `ops/backup.sh`, `ops/restore-check.sh`, `ops/restore-live.sh`, `ops/tests/backup-restore.test.sh`, `ops/tests/restore-live.test.sh`
- Modify: `scripts/test-full.sh` (if the `ops/tests/` loop does not already pick it up)

**Interfaces:**
- Produces: `ops/backup.sh` (writes `<BACKUP_DIR>/<UTC yyyyMMddTHHmmssZ>/{habits.dump, manifest.tsv}`, default `BACKUP_DIR=/var/backups/habits`; deletes directories whose name dates more than 7 days back); `ops/restore-check.sh` (newest backup into database `habits_restore_check`, prints the per-table comparison, then `PASS` or `FAIL` and `time: <seconds> s`, drops the database; exit 0 on `PASS`, 1 on `FAIL`). Both reach PostgreSQL through `docker compose exec db` with the compose files in `HABITS_COMPOSE_FILES`. Manifest lines: `<schema>.<table>\t<row count>\t<md5>`, for every table of schemas `identity` and `operations`, taken in the dump's snapshot (research R10); `ops/restore-live.sh <backup directory>` (research R22; runbook, "Restore the live database"): asks for the database name, restores into `habits_restored`, compares it with the backup's manifest, and only on `PASS` stops `app`, renames `habits` to `habits_broken_<UTC yyyyMMddTHHmmssZ>` and `habits_restored` to `habits`, starts `app`, checks `/health`, prints the time; on `FAIL` it drops `habits_restored`, leaves the live database and `app` as they were, and exits 1.

- [ ] **Step 1: Write the failing test** — `ops/tests/backup-restore.test.sh` against `compose.yaml`, with tables created and seeded by the test in both schemas: `a restore of an untouched backup passes` (exit 0, `PASS`, a `time:` line); `an altered manifest fails` (one count changed → exit 1, `FAIL`, the table named); `a backup 8 days old is deleted and one 6 days old is kept`; `the restore leaves the live database untouched` (row counts unchanged, `habits_restore_check` no longer exists).
- `ops/tests/restore-live.test.sh` (compose, tables seeded by the test): back up, then change and delete rows; restore-live → the live database equals the backup (the manifest of `habits` matches), `habits_broken_*` holds the changed rows; a backup with an altered manifest → exit 1, the live database still holds the changed rows, no `habits_restored` left; a wrong name typed at the prompt → nothing changes.
- [ ] **Step 2: Run to verify they fail** — FAIL.
- [ ] **Step 3: Implement** the three scripts.
- [ ] **Step 4: Run to verify they pass** — `bash ops/tests/backup-restore.test.sh` and `bash ops/tests/restore-live.test.sh` — PASS.
- [ ] **Step 5: Commit** — `Add database backup, restore check and live restore`

---

### Task 20: [FR-016][FR-027][FR-034] Container image and HTTPS-only proxy

**Executor:** subagent · **Depends on:** 15, 16

**Files:**
- Create: `Dockerfile`, `.dockerignore`, `ops/Caddyfile`, `ops/compose.prod.yaml`, `ops/tests/compose.https-test.yaml`, `ops/tests/https-only.test.sh`, `ops/tests/compose-run.test.sh`
- Modify: `compose.yaml` (service `app`: built from `Dockerfile`, Development, port `8080:8080`, `ConnectionStrings__Habits=Host=db;Database=habits;Username=habits`, after `db` is healthy)

**Interfaces:**
- Produces: the image (`/app` with the host, `wwwroot` with the Angular build and `wwwroot/i18n`, `Translations__Path=wwwroot/i18n`, and the bundles `/app/migrate-identity`, `/app/migrate-operations`); `ops/compose.prod.yaml` with `caddy` (`caddy:2.11.7`, volumes `caddy_data:/data`, `caddy_config:/config`), `db` and `app` (image `ghcr.io/<owner>/habits-tracker:${IMAGE_TAG}`, Production, logging driver `journald`, `ForwardedHeaders__KnownNetwork=172.30.0.0/24`, secrets from `.env`) on network `edge` with subnet `172.30.0.0/24`; every service of `compose.prod.yaml` (`caddy`, `db`, `app`) uses the `journald` logging driver, so the 7-day retention covers all logs (FR-032).

Decisions (research R13, R24): the frontend build stage runs `npm ci` and `npm run build`, whose `prebuild` runs `fetch-fonts.sh`, so the image build needs access to `github.com` and fails on a font whose sum differs. The runtime stage is the default ASP.NET Core 10 image (not Alpine, not Chiseled, no invariant globalization: research R6). One `Caddyfile` for production and test, differing only by environment variables: the site address, and internal certificates in the test. Caddy redirects `http://` to `https://`, sets `Strict-Transport-Security: max-age=31536000`, and writes its access log to standard output with its default redaction of credential headers (research R20).

- [ ] **Step 1: Write the failing tests**

`ops/tests/compose-run.test.sh` (quickstart §1): `docker compose up --build -d`; `curl -s http://localhost:8080/health` → `Healthy`; `curl -s -o /dev/null -w '%{http_code}' http://localhost:8080/fonts/LICENSE-FONT.txt` and `/icon.svg` → `200`; a row inserted into `identity.users` survives `docker compose down` (no `-v`) and `up`; `docker compose run --rm app ./migrate-identity` and `./migrate-operations` exit 0 twice in a row on a fresh database.

`ops/tests/https-only.test.sh` (`compose.yaml` + `ops/tests/compose.https-test.yaml`, Caddy with `tls internal` for `localhost`): `curl -sI http://localhost` → `308`, `Location: https://localhost/`, no HTML body; `curl -sk https://localhost/health` → `Healthy`; `curl -skI https://localhost/` carries `Strict-Transport-Security: max-age=31536000` and the CSP of Global Constraints; a request with `Cookie: session=abc` leaves an access log line with the client address and without the cookie's value. `compose-run.test.sh` also checks that every service of `ops/compose.prod.yaml` sets `logging.driver: journald`.

- [ ] **Step 2: Run to verify they fail** — FAIL.
- [ ] **Step 3: Implement** the image, the compose files and the Caddyfile.
- [ ] **Step 4: Run to verify they pass** — `scripts/test-full.sh` — PASS.
- [ ] **Step 5: Commit** — `Add container image and HTTPS-only reverse proxy`

---

### Task 21: [FR-040] Load test

**Executor:** subagent · **Depends on:** 20

**Files:**
- Create: `load/main-screen.js`, `load/compose.loadtest.yaml`, `scripts/load-test.sh`
- Modify: `CLAUDE.md` (load command, about 12 minutes)

**Interfaces:**
- Consumes: the compose `app` (Task 20); the development log line format.

Decisions (research R14; plan.md, "Review decisions" 2 and 3): `compose.loadtest.yaml` limits `app` and `db` to `cpus: 1`, `mem_limit: 2g` each and sets `Identity__CodeRequestLimits__PerIpAddress=1000`. `load-test.sh` starts compose with the overlay, signs in 100 users with `curl` (codes from `docker compose logs app`), writes their tokens to `load/.sessions.json` (git-ignored), runs `grafana/k6:2.3.0` on the host network, stops compose, and exits with k6's code. Each virtual user owns one token, sent as the `Cookie` header. Each action is preceded by `sleep(1 + Math.random() * 4)`; every 10th action is `PUT /api/me/locale` alternating `en` and `pl`, the others open the main screen as `GET /` then `GET /api/me`. Options: `vus: 100`, `duration: '10m'`, `thresholds: { http_req_duration: ['p(95)<300'] }`; `LOAD_USERS` and `LOAD_DURATION` override users and duration for a short run.

- [ ] **Step 1: Write the scenario and a gate check** — a short run with `LOAD_USERS=5 LOAD_DURATION=30s` and the threshold temporarily overridden to `p(95)<1` (`LOAD_P95_MS=1`) must exit non-zero: this proves a missed threshold fails the command.
- [ ] **Step 2: Run the gate check** — `LOAD_USERS=5 LOAD_DURATION=30s LOAD_P95_MS=1 scripts/load-test.sh` — Expected: exit code ≠ 0, k6 reports the threshold crossed.
- [ ] **Step 3: Run the real test** — `scripts/load-test.sh` — Expected: exit 0; k6 summary `http_req_duration … p(95)=<value under 300 ms>`; about 20,000 measured requests. If it misses SC-006 with split limits, stop and report (plan.md, "Review decisions" 3).
- [ ] **Step 4: Commit** — `Add load test of the main screen with 100 users`

---

### Task 22: [FR-034] Test integrity check

**Executor:** subagent · **Depends on:** 5

**Files:**
- Create: `scripts/test-integrity.sh`, `scripts/tests/test-integrity.test.sh`

**Interfaces:**
- Produces: `scripts/test-integrity.sh <base-ref>` — exit 1 when an added line of `git diff <base-ref>...HEAD` contains `Skip =`, `.skip(`, `.only(`, `xit(`, `xdescribe(` or `test.fixme(` (printing file, line and marker), or when the test count of `HEAD` is below that of `<base-ref>`; exit 0 otherwise. The count is the sum of `dotnet test backend/HabitsTracker.slnx --list-tests`, the Vitest list and `playwright test --list`; the environment variable `TEST_COUNT_COMMAND` replaces the counting command, for the script's own test. A base with no tests counts 0 (research R15: `main` is empty in epic 1). The `test-exception` label is checked by the workflow (Task 23), not here.

- [ ] **Step 1: Write the failing test** — `scripts/tests/test-integrity.test.sh` in a temporary repository: one case per marker in an added line → exit 1 naming it; a marker only in a removed line → exit 0; a stub count lower on `HEAD` → exit 1; equal or higher → exit 0.
- [ ] **Step 2: Run to verify it fails** — FAIL.
- [ ] **Step 3: Implement** the script.
- [ ] **Step 4: Run to verify it passes** — `bash scripts/tests/test-integrity.test.sh` — PASS.
- [ ] **Step 5: Commit** — `Add test integrity check`

---

### Task 23: [FR-034] GitHub repository and pull request checks

**Executor:** manual · **Depends on:** 19–22

**Files:**
- Create: `.github/workflows/pr-checks.yml`

Manual steps (developer): create the public repository `habits-tracker` on GitHub; push `main` (the agent may not push `main`: deny rules); the agent then pushes `001-walking-skeleton`.

`pr-checks.yml` on `pull_request` (GitHub-hosted runners, no `pull_request_target`), jobs: `fast` (`scripts/test-fast.sh`), `lint` (`npm --prefix frontend run lint`; the backend analysers run in `fast`), `e2e` (`npx --prefix frontend playwright install --with-deps chromium webkit`, then `npm --prefix frontend run e2e`: all four projects), `ops` (the shell tests of `scripts/tests/` and `ops/tests/`), `load` (`scripts/load-test.sh`), `audit` (`npm --prefix frontend audit --audit-level=high`; the backend's NuGet Audit runs in every build), `test-integrity` (`scripts/test-integrity.sh origin/main`, skipped when the pull request has the label `test-exception`). Every `uses:` is pinned to a commit SHA with the version in a comment; the workflow sets `permissions: contents: read` (research R19, P1–P2).

- [ ] **Step 1: Write the workflow** with the developer.
- [ ] **Step 2: Open the pull request of step 11 as a draft** — `gh pr create --draft --base main`. Developer's ruling at the step 8 review: GitHub runs `pr-checks.yml` and `deploy-branch.yml` only from a pull request, so Tasks 23–25 check their workflows on this draft; step 11 marks it ready for review, and step 12 deploys the final code again. The deviation is one ledger line at step 13.
- [ ] **Step 3: Check** — `gh pr checks` — Expected: every job green; `grep -E 'uses: [^@]+@[0-9a-f]{40}' .github/workflows/pr-checks.yml` matches every `uses:` line.
- [ ] **Step 4: Commit** — `Add pull request checks workflow`

---

### Task 24: [FR-016][FR-034][FR-035][FR-036] Server, deployment workflows, first deployment

**Executor:** manual · **Depends on:** 23

**Files:**
- Create: `.github/workflows/{deploy.yml, deploy-branch.yml, deploy-main.yml}`, `ops/systemd/habits-backup.service` (`WorkingDirectory=/opt/habits`, `Environment=HABITS_COMPOSE_FILES=/opt/habits/compose.prod.yaml`, runs `ops/backup.sh`), `ops/systemd/habits-backup.timer` (`OnCalendar=*-*-* 03:00:00 UTC`, `Persistent=true`)
- Modify: `docs/operations/runbook.md` if a rehearsal shows a command that differs

Manual steps (developer, research R13): the DNS A record of `habits.cardlane.eu`; on the server Docker with compose, a deploy user with key-only SSH, the Hetzner Cloud Firewall open on 22, 80 and 443, `ops/journald/habits.conf` as a journald drop-in, the backup timer, `/opt/habits` with `compose.prod.yaml`, `Caddyfile` and `.env` (`POSTGRES_PASSWORD`, `RESEND_API_KEY`, `IDENTITY_CODE_HASH_KEY`; the key is made once with `openssl rand -base64 32`, the same value goes into the GitHub environment, and a new key invalidates the codes issued before it); the GitHub environment `production` with `RESEND_API_KEY`, `IDENTITY_CODE_HASH_KEY`, `POSTGRES_PASSWORD`, `DEPLOY_SSH_KEY`, `DEPLOY_HOST`, `DEPLOY_KNOWN_HOSTS`; in the Resend dashboard, tracking off for `mail.cardlane.eu` (FR-007).

Workflows (pinned by SHA, least permissions, as Task 23): `deploy.yml` (`workflow_call`, and `workflow_dispatch` with inputs `image_tag` and `migrate`, environment `production`): unless `image_tag` is given, build and push the image to GHCR tagged with the commit SHA (`packages: write` for this job only); over SSH run `ops/backup.sh`, then, when `migrate` is not `false`, `migrate-identity` and `migrate-operations`, then `docker compose up -d` with that tag; then `curl --fail --max-time 10 https://habits.cardlane.eu/health` once (research R13). `deploy-branch.yml`: on `pull_request` `labeled` with `deploy`, only for branches of this repository, calls `deploy.yml`. `deploy-main.yml`: on `push` to `main`, calls `deploy.yml`.

- [ ] **Step 1: Write the workflows and the timer units** with the developer; do the manual steps.
- [ ] **Step 2: Deploy** — the developer adds the label `deploy` to the draft pull request.
- [ ] **Step 3: Check** — the deployment run is green; `curl -sI http://habits.cardlane.eu` → redirect to `https://`, no page; `curl -s https://habits.cardlane.eu/health` → `Healthy`; `systemctl list-timers habits-backup.timer` shows the next run, and `systemctl start habits-backup.service` leaves a new directory under `/var/backups/habits/`; `/var/backups/habits/` holds the pre-migration backup; `systemd-analyze cat-config systemd/journald.conf` shows the two retention values.
- [ ] **Step 4: The one restore (FR-036)** — on the server `ops/restore-check.sh` → `PASS` and its time, at most 4 hours (SC-010); the time is kept for the ledger commit (step 13).
- [ ] **Step 5: Rehearse the rollback by SSH (constitution XI)** — push a second commit and let the deployment run; then, on the server, the runbook's SSH path with the first SHA (`ops/backup.sh`, `IMAGE_TAG=<first SHA> docker compose -f compose.prod.yaml up -d app`); `docker compose -f compose.prod.yaml ps --format '{{.Image}}' app` shows the first SHA; `/health` → `Healthy`; deploy the newest commit again. The manual run of `deploy.yml` cannot run before merge: GitHub runs `workflow_dispatch` only from the default branch (research R13); it is rehearsed after merge ("After the last task"). Follow the runbook as written and correct it where a command differs.
- [ ] **Step 6: Rehearse the live restore (constitution XI)**, while the server holds only test data — `ops/restore-live.sh` with the newest backup → `PASS`, `Healthy`, the time printed; a sign-in on the site works; drop `habits_broken_*`.
- [ ] **Step 7: Commit** — `Add deployment workflows and backup timer`

---

### Task 25: [FR-038] Availability check

**Executor:** manual · **Depends on:** 24

**Files:**
- Create: `.github/workflows/availability.yml`, `ops/availability-month.sh`, `ops/tests/availability-month.test.sh`

`availability.yml`: `schedule` every 5 minutes (`*/5 * * * *`) and `workflow_dispatch`; one step `curl --fail --max-time 10 https://habits.cardlane.eu/health`; a failed run notifies the developer through GitHub. Schedules run only from `main`, so the scheduled check starts once epic 1 is merged (FR-038); before merge, Task 24's deployment ran the same check. `ops/availability-month.sh YYYY-MM` lists that month's runs of `availability.yml` through `gh api` and prints `passed/ran: <p>/<r> (<share with two decimals>%)`, or `no value` for a month with no runs (research R16).

- [ ] **Step 1: Write the failing test** — `ops/tests/availability-month.test.sh` with a stub `gh` returning fixed run lists: 8,640 runs with 86 failed → `passed/ran: 8554/8640 (99.00%)`; no runs → `no value`.
- [ ] **Step 2: Run to verify it fails** — FAIL.
- [ ] **Step 3: Write the script and the workflow.**
- [ ] **Step 4: Check** — `bash ops/tests/availability-month.test.sh` PASS; the workflow file passes the pull request checks.
- [ ] **Step 5: Commit** — `Add availability check`

---

## After the last task

Process steps 10–12 continue from here; they are not tasks of this plan. Step 11 marks the draft pull request of Task 23 ready for review instead of opening a new one. After merge (step 14), once `deploy.yml` is on `main`, the developer rehearses the rollback by a manual workflow run once: `gh workflow run deploy.yml -f image_tag=<previous SHA> -f migrate=false`, then deploys the newest again (constitution XI; research R13). At step 12, after the branch deployment, the developer: signs in on `https://habits.cardlane.eu` with a real Gmail address — code email within 1 minute, in the inbox, main screen within 2 minutes (SC-001, SC-014); signs in once more from their own phone — the page fills the phone's width, both themes switch, the code field offers the code from the email, sign out works (SC-014); looks at every page in both themes at the phone's width and on a desktop, the one check of the look no test makes; runs `ops/latency-p95.sh <deployment time>` on the server and keeps the 95th percentile for the ledger (SC-008); confirms `curl -sI http://habits.cardlane.eu` serves no page (SC-004).
