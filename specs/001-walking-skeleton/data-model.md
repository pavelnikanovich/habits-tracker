# Data model: Walking skeleton

Epic 1 touches one bounded context, Identity. Terms are those of `docs/domain/ubiquitous-language.md`, Identity; every entity below is a term there (constitution, principle III). The model is marked in DDD terms (principle IV): aggregates, entities, value objects, and the invariants each aggregate guards.

Habit Tracking and Live Updates get no code in this epic. The context map rows that start in Identity (time zone changed, locale changed, session revoked or expired, account deleted) are published in epics 3 and 8; this epic raises no domain events.

Values below are copied from `spec.md`; values the spec does not fix are marked *(agent's call)*.

## Identity core

### Value objects

| Value object | Holds | Rules |
|---|---|---|
| **Email** | the address, trimmed and lower-cased | Compared case-insensitively (FR-008): two emails are equal when their lower-cased forms are equal. Must contain one `@` with text on both sides and be at most 254 characters long *(agent's call: the SMTP path limit)*. |
| **Time zone** | an IANA time zone id, and whether it came from the browser | Created from the browser's id at the first sign-in (FR-009). An id the system does not know, or none, gives `UTC` with "not recognized" set; the main screen says so while it stands. Known means: found in the server's IANA database (the container image's tzdata, research R6). |
| **Locale** | a language code: `en`, `pl` or `ru` | Valid when a translation file exists for it (FR-020: a new language is a new file, no code change). The set comes from the translation files through the Locales port, not from an enumeration in code. |
| **IP address prefix** | the address a code request counts against | IPv4: the whole address. IPv6: the /64 prefix (FR-003). An IPv4-mapped IPv6 address counts as its IPv4 address *(agent's call)*. |
| **One-time code value** | 6 decimal digits, leading zeros kept | Drawn uniformly from `000000`–`999999` by a cryptographic random generator (FR-002). Exists only in memory and in the code email; never stored, never logged in production (FR-011). |
| **Code hash** | a keyed hash of the email and the code | HMAC-SHA-256 with a server secret over the email and the code *(agent's call, research R5)*. Binding the email in means a code presented for another email never matches. |
| **Session token** | 32 random bytes, base64url in the cookie | Only the cookie carries the token (FR-012); the server stores its SHA-256 hash as the session id *(agent's call, research R4)*. |
| **Code request limits** | per-email limit, per-IP limit, window | 5 per email, 10 per IP address prefix, sliding window of 24 hours (FR-003). Configuration, so tests can set them (FR-003). |

### Aggregate: User

Root entity **User**. Identity: a generated user id (UUID).

| Field | Type | Notes |
|---|---|---|
| id | UUID | |
| email | Email | unique among users |
| time zone | Time zone | set at the first sign-in; changed only in epic 8 |
| locale | Locale | set at the first sign-in; changed by the language switcher |
| created at | instant (UTC) | |

Invariants:

- One user per email (FR-008). A uniqueness rule across aggregates: the database's unique index on the lower-cased email holds it, and a second concurrent first sign-in for the same email signs in to the user the first one created.
- The time zone is always a valid IANA id; "not recognized" is true only when UTC was stored because the browser's zone was missing or unknown (FR-009).
- The locale is always one the translation files provide.

Behavior: **sign up** (email, browser time zone, current locale, now) — the first successful sign-in (FR-008, FR-009); **change locale** (FR-023). A later sign-in changes neither the time zone nor the locale (spec, Assumptions).

### Aggregate: One-time code

Root entity **One-time code**. Identity: the Email. At most one one-time code exists per email; the code that replaces it is the invalidation of the earlier one (FR-002).

| Field | Type | Notes |
|---|---|---|
| email | Email | key |
| code hash | Code hash | the code itself is never stored |
| issued at | instant | |
| expires at | instant | issued at + 10 minutes |
| entry attempts | integer, 0–5 | every entry counts, correct or not |

Invariants:

- Valid for 10 minutes: an entry at or after `expires at` is refused (FR-002).
- At most 5 entry attempts: once 5 entries have been used, the code no longer signs in, even if correct; a new code has to be requested (FR-002; US-1.2, scenario 4).
- Used once: a correct entry deletes the code (FR-002).
- Replaced only once the new code is sent or the result is unclear; a refused send leaves the earlier code valid (FR-002, FR-004).

State transitions:

```text
            send succeeds or result unclear
  (none) ─────────────────────────────────▶ Valid
  Valid ── correct entry, attempt ≤ 5, before expiry ──▶ Used (deleted)
  Valid ── wrong entry, attempts < 5 ──▶ Valid (attempts + 1)
  Valid ── 5th entry wrong ──▶ Exhausted
  Valid ── now ≥ expires at ──▶ Expired
  Valid | Exhausted | Expired ── new code sent or unclear ──▶ Valid (replaced)
  Exhausted | Expired ── retention cleanup (FR-033) ──▶ (deleted)
```

Verification answers one of: signed in; incorrect (the code stays usable); a new code is required (expired, used, exhausted, or no code for this email).

### Aggregate: Code request

Root entity **Code request**, immutable. Identity: a generated id. Only counted requests are stored (FR-004): a request refused by a limit or by the email service leaves no record.

| Field | Type | Notes |
|---|---|---|
| id | integer | |
| email | Email | kept 24 hours (FR-033) |
| IP address prefix | IP address prefix | kept 24 hours (FR-033) |
| requested at | instant | |

Domain service **Code request policy**, over the counted code requests of the last 24 hours for an email and for an IP address prefix (FR-003, FR-005):

| Counted for the email | Counted for the IP prefix | Outcome |
|---|---|---|
| < 5 | < 10 | allowed; after the send, remaining = 5 − (counted for the email + 1) |
| 5 | any | refused by the per-email limit; next code possible when the oldest request for the email leaves the window |
| < 5 | 10 | refused by the per-IP limit; next code possible when the oldest request for the prefix leaves the window; no remaining count |

When both limits are reached, the per-email refusal is reported *(agent's call)*.

Invariant across records: concurrent code requests for the same email or the same IP address prefix are serialized, so a race cannot exceed a limit. The core states the rule; the database adapter holds it with transaction-scoped advisory locks on the email and the prefix *(agent's call, research R5)*.

### Aggregate: Session

Root entity **Session**. Identity: the hash of its Session token.

| Field | Type | Notes |
|---|---|---|
| id | Session token hash | |
| user id | UUID | the User it belongs to |
| remembered | boolean | true for a Remembered session |
| created at | instant | |
| last activity at | instant | |

Invariants:

- Sliding expiry: a session with no request by the user for 30 days has expired; any request by the user sets `last activity at` to now (FR-013).
- Revocation is deletion: sign out deletes this session, sign out everywhere deletes every session of the user; both take effect on the next request (FR-015).
- A session belongs to one user and authorizes only that user's data (FR-017).
- The cookie of a session that is not a Remembered session has no expiry; the cookie of a Remembered session persists, and every response that extends the session renews its expiry to 30 days from now (FR-014) *(agent's call: renewing keeps the cookie alive as long as the session)*.

State transitions:

```text
  (none) ── correct code entered ──▶ Active
  Active ── request by the user ──▶ Active (last activity at = now)
  Active ── 30 days without a request ──▶ Expired ── cleanup ──▶ (deleted)
  Active ── sign out / sign out everywhere ──▶ (deleted)
```

### Visitor

A term with no stored state: the system keeps no record of a visitor (spec, Key Entities). The visitor's language choice is kept in the browser's local storage (FR-021).

## Use cases (driving ports of the Identity core)

| Use case | Spec | Result |
|---|---|---|
| Request a code (email, locale, IP address) | FR-001–FR-005, FR-007 | sent with remaining count; refused by the per-email limit with the next time; refused by the per-IP limit with the next time; refused by the email service with its reason and, if known, the next time |
| Sign in (email, code, remember me, browser time zone, locale) | FR-008, FR-009, FR-012–FR-014 | a new session and its token; incorrect; a new code is required |
| Authenticate a session (token) | FR-013, FR-015 | the user and the session, extended; or not signed in |
| Get the current user | FR-009, FR-018 | email, time zone, "not recognized", locale |
| Change the locale | FR-023 | |
| Sign out | FR-015 | |
| Sign out everywhere | FR-015 | |
| Delete expired records (now) | FR-033 | deletes codes past expiry, code requests older than 24 hours, sessions 30 days without activity |

## Driven ports of the Identity core

| Port | Adapters |
|---|---|
| Users, One-time codes, Code requests, Sessions (storage) | PostgreSQL through EF Core |
| Code email sender: send (email, code, locale) → sent, refused (reason, next time if known) or unclear | Resend over HTTP (production); the development log (development) (FR-010) |
| Locales: the available locales | the translation files |
| Time | `TimeProvider` from the .NET base library; a fake in tests |

The text of the code email is composed by the adapter from the translation files (FR-007, FR-020); the core passes the code and the locale.

## Storage (PostgreSQL)

One database. Schema `identity` belongs to the Identity package; schema `operations` belongs to the host and holds no domain data. Migrations only expand the schema (principle VI); the first migration creates both schemas.

### Schema `identity`

| Table | Columns | Keys and indexes |
|---|---|---|
| `users` | `id uuid`, `email text`, `time_zone text`, `time_zone_recognized boolean`, `locale text`, `created_at timestamptz` | PK `id`; unique `email` (stored lower-cased) |
| `one_time_codes` | `email text`, `code_hash bytea`, `issued_at timestamptz`, `expires_at timestamptz`, `entry_attempts smallint` | PK `email`; index `expires_at` |
| `code_requests` | `id bigint identity`, `email text`, `ip_prefix cidr`, `requested_at timestamptz` | PK `id`; index (`email`, `requested_at`); index (`ip_prefix`, `requested_at`); index `requested_at` |
| `sessions` | `id bytea`, `user_id uuid`, `remembered boolean`, `created_at timestamptz`, `last_activity_at timestamptz` | PK `id`; FK `user_id` → `users.id`; index `user_id`; index `last_activity_at` |

### Schema `operations` (not domain)

Operational records of the host; none of them is an entity of the domain, and none holds personal data.

| Table | Columns | Purpose |
|---|---|---|
| `request_timings` | `hour timestamptz`, `endpoint text`, `bucket_upper_ms integer`, `count bigint`; PK (`hour`, `endpoint`, `bucket_upper_ms`) | FR-039: request timing aggregated per hour, per endpoint route template and per latency bucket; no IP address, no user id. Kept 13 months *(agent's call: "at least a month", and a year to compare months)*. Bucket bounds in research R9. Primary key (`hour`, `endpoint`, `bucket_upper_ms`), so the host adds each minute's counts with one upsert; the +∞ bucket is stored as `bucket_upper_ms = 2147483647`. This table stands in for a metrics stack (Prometheus with Grafana dashboards and alerts; research R9, "Alternatives"): one PostgreSQL table keeps the API latency SLO measurable without three more services. `ops/latency-p95.sh` reads the SLO from it; there is no automatic latency alert: the developer reads the value after each deployment and for each calendar month. |

Each `DbContext` keeps its EF Core migrations history table in its own schema; the backup manifest (FR-035) is a file next to each backup, not a table (research R10).

## Retention (FR-033)

| Record | Deleted when | By |
|---|---|---|
| One-time code | expired (10 minutes after issue) or exhausted | the hourly cleanup *(agent's call: hourly is well within "at most one day")*; a used code at once |
| Code request | 24 hours after `requested_at` | the hourly cleanup |
| Session | 30 days without activity; at once on sign out or sign out everywhere | the hourly cleanup; the sign-out use cases |
| Logs | 7 days | the log store's retention (research R8) |
| Request timings | 13 months | the hourly cleanup |
| Backups | 7 days | the backup script (research R10) |
