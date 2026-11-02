# Record of processing activities

Under GDPR art. 30, kept for habits-tracker, epic 1 (walking skeleton). The purposes, data, recipients, transfers and retention below are the same as on the Privacy page (`frontend/public/i18n/en.json`, keys `privacy.section.<n>`); when one changes, the other changes in the same commit. Whether the art. 30(5) exemption applies is doubtful (the processing is not occasional), so the record is kept (`docs/superpowers/specs/2026-10-30-legal-pages-design.md`, "Record of processing").

Effective date: set at the first deployment, like the Privacy page.

## Controller

- Name: Paweł Nikanowicz, a private individual living in Poland.
- Contact: pavelnikanovich@gmail.com
- Data protection officer: none; not required for this service (GDPR art. 37(1)).

## Processing activities

| Activity | Purpose | Legal basis | Data subjects | Personal data | Retention |
|---|---|---|---|---|---|
| Account and habits | Provide the service the user signed up for | Art. 6(1)(b) | Users | Email, time zone, interface language; habits and check-ins | Until the account is deleted; until the app can delete an account, on request to the contact email. Deleted data stays in backups for up to 7 days |
| Sign-in | Provide the service: prove the user owns the email | Art. 6(1)(b) | Users and visitors who request a code | Email, including the email of each code request; the one-time code (stored only as a hash) and the email that carries it | Code: valid 10 minutes. Code requests, with their email: 24 hours. Resend keeps the code email, address and content, for 30 days |
| Sessions and the session cookie | Provide the service: keep the user signed in | Art. 6(1)(b) | Users | Session record and the `HttpOnly` cookie with an opaque session id | Until sign out, sign out everywhere, or 30 days without activity; the cookie ends with the browser unless "Remember me" is ticked |
| Code request limits | Security and abuse prevention | Art. 6(1)(f), the legitimate interest of keeping sign-in secure | Users and visitors who request a code | IP address of each code request | 24 hours |
| Server logs | Security and abuse prevention | Art. 6(1)(f), the legitimate interest of keeping the service secure | Users and visitors | IP addresses; no email, user id, session token or one-time code | 7 days |

Backups are not a separate purpose: they copy the rows above, and deleted data stays in backups for up to 7 days.

The system deletes expired codes, code requests, sessions and logs at most one day after each period ends. A code request for an email that never becomes a user leaves nothing behind after these periods.

The browser also keeps the language choice and the theme choice in local storage. These are interface settings the user chooses, not stored on the server and exempt from consent like the session cookie. The app sets no other cookies and has no analytics and no ads.

## Recipients

The list is closed.

| Recipient | Role | Location | What it receives |
|---|---|---|---|
| Hetzner | Hosting (processor) | Germany, EU | All data of this record, backups and logs included |
| Resend, operated by Plus Five Five, Inc. | Sending the one-time code email (processor) | USA | The email address and the content of the code email |

No one else receives personal data. A tool that would send personal data to anyone else needs the developer's approval, and this record and the Privacy page change in the same epic.

## Transfers outside the EU

Resend, USA: the EU-US Data Privacy Framework adequacy decision (Commission Implementing Decision (EU) 2023/1795; Resend is on the DPF list) and the EU Standard Contractual Clauses in Resend's data processing agreement (https://resend.com/legal/dpa). Transfer to Hetzner: none, the server is in Germany.

## Security measures

Under art. 30(1)(g), as built in epic 1: the site is served over HTTPS only, plain HTTP is redirected to HTTPS and the answers carry HSTS (`ops/Caddyfile`); only the reverse proxy publishes ports (80 and 443), the app and the database none (`ops/compose.prod.yaml`). A one-time code is stored only as an HMAC-SHA-256 under a secret key, bound to its email, and a session token only as its SHA-256 hash, so the database and its backups hold neither in readable form. The session cookie is `HttpOnly`, `Secure` and `SameSite=Lax`. The logs hold no email, user id, session token or one-time code, which a test of the production logs checks; the reverse proxy's access log replaces the values of the cookie and authorization headers with `REDACTED`. Backups stay on the server and are written readable by their owner only (`umask 077`, `ops/backup.sh`).

## Rights, complaints and other statements

- Rights: access, rectification, erasure, restriction, objection to processing under art. 6(1)(f), portability; exercised by writing to the contact email.
- Complaint: to the President of the Personal Data Protection Office (Prezes UODO), ul. Stanisława Moniuszki 1A, 00-014 Warszawa, or to the supervisory authority of the user's own EU country.
- An email is required to sign in; without it there is no account.
- No automated decisions and no profiling.
- A user's habits and check-ins are never shown to another user.
- Users are asked not to put health or other sensitive data in habit names.
