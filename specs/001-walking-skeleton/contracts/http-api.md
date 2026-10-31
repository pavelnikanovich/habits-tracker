# HTTP API contract: Walking skeleton

The Angular application and the API are served from one origin, `https://habits.cardlane.eu` (FR-016, FR-027). Every API path starts with `/api`; `/health` is outside it. Bodies are JSON (`application/json`); errors are RFC 9457 problem details (`application/problem+json`) with two extension members:

- `code` — a machine-readable reason from the closed list of each endpoint; the page maps it to a translated message (FR-020).
- `retryAt` — an ISO 8601 UTC instant, present only where the spec says the user is told when to try again.

The error format is the same for every endpoint of every epic.

## Authentication

- Cookie `session`: the Session token (data-model, "Session token"), `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`, no `Domain` (FR-012). No `Expires` or `Max-Age` for a session that is not a Remembered session; `Max-Age` of 30 days for a Remembered session, renewed on every response that extends it (FR-014).
- An endpoint marked **signed in** answers `401` with `code: "not-signed-in"` when the cookie is missing, unknown, expired or revoked, and clears the cookie. A request that passes authentication extends the session (FR-013).
- State-changing requests are `POST` or `PUT` with a JSON body; with `SameSite=Lax`, a cross-site page cannot send them with the cookie (research R4).
- Every signed-in endpoint acts only on the signed-in user; no endpoint takes a user id (FR-017). The cross-user test of each endpoint is listed below.

## Endpoints

### `POST /api/code-requests` — request a one-time code (US-1.1)

Public. Request:

```json
{ "email": "ann@example.com", "locale": "pl" }
```

`locale` is the language of the page; the code email is in it (FR-007).

| Status | Body | When |
|---|---|---|
| `202` | `{ "remaining": 4, "limit": 5 }` | the code was sent, or the result was unclear (FR-004, FR-005) |
| `400` | problem, `code: "invalid-email"` or `"invalid-locale"` | the email or the locale is not valid (data-model, value objects) |
| `429` | problem, `code: "email-limit"`, `retryAt` | the per-email limit is reached (FR-005) |
| `429` | problem, `code: "ip-limit"`, `retryAt` | the per-IP limit is reached; no remaining count (FR-005) |
| `503` | problem, `code: "email-service-limit"`, `retryAt` if known | the email service's daily limit is used up (FR-005) |
| `503` | problem, `code: "email-service-unavailable"` | the email service refused for another reason or is unavailable (FR-005) |

The client's IP address is taken from the reverse proxy's `X-Forwarded-For`, trusted only from the proxy (research R7).

### `POST /api/sessions` — sign in with a one-time code (US-1.2)

Public. Request:

```json
{ "email": "Ann@example.com", "code": "042917", "rememberMe": false, "timeZone": "Europe/Warsaw", "locale": "pl" }
```

`timeZone` is the browser's IANA id, or `null` if the browser reports none. `timeZone` and `locale` are used only when this sign-in creates the user (FR-009).

| Status | Body | When |
|---|---|---|
| `200` | the current user (below); `Set-Cookie: session=…` | the code is correct, unexpired and unused (FR-008) |
| `400` | problem, `code: "invalid-email"`, `"invalid-code-format"` or `"invalid-locale"` | the input is malformed; no entry attempt is counted |
| `401` | problem, `code: "code-incorrect"` | a wrong code; the code stays usable |
| `401` | problem, `code: "new-code-required"` | the code expired, was used, ran out of entry attempts, or none exists for this email |

### `GET /api/me` — the current user (US-1.2, US-1.6) — signed in

```json
{ "email": "ann@example.com", "timeZone": "UTC", "timeZoneRecognized": false, "locale": "pl" }
```

`timeZoneRecognized: false` makes the main screen say that the browser's time zone was not recognized and UTC is used (FR-009).

Cross-user test: with user B's cookie, the response is B's data, never A's.

### `PUT /api/me/locale` — change the locale (US-1.8) — signed in

Request `{ "locale": "ru" }`. Responses: `204`; `400` with `code: "invalid-locale"`.

Cross-user test: B changing the locale leaves A's locale unchanged.

### `DELETE /api/sessions/current` — sign out (US-1.4) — signed in

Responses: `204`, with the cookie cleared.

Cross-user test: B signing out leaves every session of A signed in.

### `DELETE /api/sessions` — sign out everywhere (US-1.5) — signed in

Responses: `204`, with the cookie cleared.

Cross-user test: B signing out everywhere leaves every session of A signed in.

### `GET /api/locales` — the available locales (US-1.8)

Public. `200` with `[{ "code": "en", "name": "English" }, { "code": "pl", "name": "Polski" }, { "code": "ru", "name": "Русский" }]`, read from the translation files, so a new file is a new language with no code change (FR-020).

### `GET /health` — health (FR-037)

Public. `200` with body `Healthy` when the application reaches the database; `503` with body `Unhealthy` otherwise. No personal data, no details of the failure.

## Pages

Routes of the Angular application, all served by the same origin; any other path that is not `/api` or `/health` returns the application, which shows its own "not found".

| Route | Who | Story |
|---|---|---|
| `/sign-in` | visitor | US-1.1, US-1.2 |
| `/` | signed in; a visitor is sent to `/sign-in` | US-1.6 |
| `/privacy` | anyone | US-1.7 |
| `/terms` | anyone | US-1.7 |

## Response headers

Every response: `Strict-Transport-Security: max-age=31536000` (set by the reverse proxy in production), `Content-Security-Policy: default-src 'self'` with the additions the Angular build needs (research R11), `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`. The content security policy is how FR-027 is held in the browser; SC-013's end-to-end test checks it.
