# HTTP API contract: Walking skeleton

The Angular application and the API are served from one origin, `https://habits.cardlane.eu` (FR-016, FR-027). Every API path starts with `/api`; `/health` is outside it. Bodies are JSON (`application/json`); errors are RFC 9457 problem details (`application/problem+json`) with three extension members:

- `code` — a machine-readable reason from the closed list of each endpoint; the page maps it to a translated message (FR-020).
- `retryAt` — an ISO 8601 UTC instant, present only where the spec says the user is told when to try again.
- `traceId` — the W3C trace id of the request, on every error response; the same id is on the request's log lines (research R20). The page shows it only for `internal-error`.

`title` is the HTTP reason phrase and `type` is left at its default (`about:blank`) *(agent's call)*; the page shows its own translated text for `code` (FR-020). Every instant in a body is an ISO 8601 UTC instant *(agent's call)*.

Codes every endpoint under `/api` can answer, besides those listed under it (`/health` answers plain text, below):

| Status | `code` | When |
|---|---|---|
| `400` | `invalid-request` | the body is not JSON, a field is missing or of the wrong type |
| `413` | `invalid-request` | the body is over 16 KB (research R19, D4) |
| `415` | `invalid-request` | a body with a content type other than `application/json` (research R19, T1) |
| `404` | `not-found` | a path under `/api` that no endpoint serves *(agent's call)* |
| `405` | `invalid-request` | a path an endpoint serves, with another method *(agent's call)* |
| `500` | `internal-error` | an unexpected failure; no details beyond `traceId` |

The error format is the same for every endpoint of every epic. The API changes only by expansion (constitution VI): a field, a value or an endpoint is added, never renamed or given a new meaning.

## Authentication

- Cookie `session`: the Session token (data-model, "Session token"), `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`, no `Domain` (FR-012). No `Expires` or `Max-Age` for a session that is not a Remembered session; `Max-Age` of 30 days for a Remembered session, renewed on every response that extends it (FR-014).
- An endpoint marked **signed in** answers `401` with `code: "not-signed-in"` when the cookie is missing, malformed, unknown, expired or revoked, and clears the cookie. A request that passes authentication extends the session (FR-013).
- State-changing requests are `POST` or `PUT` with a JSON body, or `DELETE` with no body. With `SameSite=Lax` a cross-site page cannot send any of them with the cookie; a body other than `application/json` is answered `415`, and `PUT` and `DELETE` need a CORS preflight that the host does not allow (research R4).
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
| `503` | problem, `code: "email-service-limit"`, `retryAt` if known | the email service's daily or monthly limit is used up; `retryAt` for the daily one only (FR-005, research R3) |
| `503` | problem, `code: "email-service-unavailable"` | the email service refused for another reason or is unavailable (FR-005) |

The client's IP address is taken from the reverse proxy's `X-Forwarded-For`, trusted only from the proxy (research R7).

### `POST /api/sessions` — sign in with a one-time code (US-1.2)

Public. Request:

```json
{ "email": "Ann@example.com", "code": "042917", "rememberMe": false, "timeZone": "Europe/Warsaw", "locale": "pl" }
```

Every field is required; `timeZone` may be `null`; a missing field is `invalid-request`. `timeZone` is the browser's IANA id, or `null` if the browser reports none. `timeZone` and `locale` are used only when this sign-in creates the user (FR-009). A sign-in whose request carries a valid `session` cookie revokes that session once the new one opens, so a session this browser no longer holds does not outlive the sign-in *(agent's call)*.

| Status | Body | When |
|---|---|---|
| `200` | the current user (below); `Set-Cookie: session=…` with a new token, never one the request carried (research R19, S4) | the code is correct, unexpired and unused (FR-008) |
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
| `/sign-in` | visitor | US-1.1 |
| `/sign-in/code` | visitor, after a code request on `/sign-in` in this tab; otherwise sent to `/sign-in` (contracts/ui.md) | US-1.2 |
| `/` | signed in; a visitor is sent to `/sign-in` | US-1.6 |
| `/privacy` | anyone | US-1.7 (FR-024, FR-026, FR-030) |
| `/terms` | anyone | US-1.7 (FR-024, FR-026, FR-031) |

The sign-in page states under the sign-in button "By signing in you accept the Terms and acknowledge the Privacy notice", with links to both pages and no acceptance checkbox (FR-025); every signed-in screen links to both pages (FR-024).

A signed-in user opening `/sign-in` or `/sign-in/code` is sent to `/` *(agent's call)*. The application takes no return-address parameter: after sign-in it always opens `/`, whatever the sign-in URL carries (research R19, T4).

## Response headers

Caching (research R21): `index.html` and the application fallback `Cache-Control: no-cache`; build files with a content hash in their name `public, max-age=31536000, immutable`; files under `/i18n/` `no-cache`; every response under `/api` and `/health` `no-store`.

Every response: `Strict-Transport-Security: max-age=31536000` (set by the reverse proxy in production), `Content-Security-Policy: default-src 'self'` with the additions the Angular build needs (research R11), `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`. The content security policy is how FR-027 is held in the browser; SC-013's end-to-end test checks it.
