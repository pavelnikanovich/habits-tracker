# Email service for one-time codes — design

Date: 2026-10-30. Epic 1 (walking skeleton), stage 2, step 2 of the development process (`docs/superpowers/specs/2026-10-28-development-process-design.md`): the design fork named in `docs/superpowers/specs/2026-10-28-product-design.md`, "Left to the builder". This document is the input to `speckit-specify` for epic 1.

Domain terms are defined in `docs/domain/ubiquitous-language.md`. Items marked *(agent's call)* were proposed by the agent without a separate question and are confirmed by the developer's review of this document.

## Intent

A user signs in with a one-time code sent to their email (product design, goal 1). The code email is sent through a transactional email service from `mail.cardlane.eu`. This document chooses that service, revises the code request limits (NFR-7) to match how the product is actually used, and records the risk and the legal obligations that come with the choice.

## How sign-in is used

- The users are the developer and people who receive a link to the GitHub repository and sign in from the link in its README: single-digit code emails a month. Their emails are not known in advance. NFR-11 sizes the product for about 100 users.
- A session is sliding and expires after 30 days without activity (NFR-9). With "Remember me" ticked, closing the browser does not end it, and a user needs a code only on a new browser, after signing out, or after 30 days of inactivity; without it, the session ends when the browser closes (see `docs/superpowers/specs/2026-10-30-legal-pages-design.md`, "Remember me").
- An ordinary sign-in uses one code, two at most (a delayed email, a mistyped code).
- There is no separate registration: any email can request a code, and the first sign-in creates the user (product design, goal 1 and "The product"). So every code request sends an email, whether or not a user with that email exists.

## Decision

**Resend, free plan.** The application sends the code email through Resend's HTTP API from the domain `mail.cardlane.eu`, with the sending region `eu-west-1` (Ireland).

The HTTP API runs over port 443. Hetzner blocks outbound ports 25 and 465 on new Cloud servers by default, so SMTP is not used.

The epic 1 spec states the behavior without naming the service ("the email service", "its daily limit"). If the step 5 spike or the checks in "Left to verify" rule Resend out, Scaleway TEM is the fallback and this document is revised *(agent's call)*.

## Criteria and comparison

Criteria from the product design: a free tier, DKIM on a subdomain, a delivery log, NFR-8, and what happens when the free tier's daily limit is reached (anti-goal 4). The developer added: free or very cheap; volume is not a constraint.

| Service | Free tier | When the limit is reached | Delivery log | Data | Notes |
|---|---|---|---|---|---|
| **Resend** (chosen) | 3,000/month, 100/day, permanent, no trial | sending stops until the limit resets | 30 days | stored in the USA; sending region selectable | 3 custom domains on the free plan; SPF, DKIM, DMARC on all plans; REST API and SMTP |
| Scaleway TEM | 300/month, then €0.25 per 1,000 | billed pay-as-you-go, no lockout | retention not found | EU (France) | needs a payment method; stricter, unpublished limits for new accounts |
| Postmark | 100/month, permanent | "No overages allowed in this plan" | 45 days | not stated | the tightest cap: one abuser blocks sign-in for the rest of the month |
| Mailjet | 6,000/month, 200/day, no card | not stated for the free plan | not stated | not stated | the free plan appears to carry the Mailjet logo |
| Brevo | 300/day | not checked | not checked | not checked | the free plan always adds "Sent with Brevo" |

Why Resend:

- Free with no payment method; the volume fits with a large margin.
- A delivery log for 30 days, DKIM on a subdomain, an HTTP API.
- No branding added to the email (not mentioned on the pricing page; to confirm at step 5).

What Resend costs:

- A hard limit of 100 emails a day: once it is used up, no code email leaves until the limit resets (see "Accepted risk").
- Account data, including the code email content and the delivery log, is stored in the USA whatever the sending region (see "Legal").

Scaleway TEM was the alternative: pay-as-you-go removes the lockout, and the data stays in the EU. The developer chose Resend to keep the service free without a payment method.

Sources: [Resend pricing](https://resend.com/pricing), [Resend regions](https://resend.com/docs/dashboard/domains/regions), [Scaleway TEM](https://scaleway.com/transactional-email-tem), [Scaleway TEM FAQ](https://www.scaleway.com/en/docs/transactional-email/faq/), [Scaleway TEM limits](https://www.scaleway.com/en/docs/transactional-email/reference-content/tem-capabilities-and-limits.md), [Postmark pricing](https://postmarkapp.com/pricing), [Mailjet pricing](https://www.mailjet.com/pricing/), [Brevo free plan limits](https://help.brevo.com/hc/en-us/articles/208580669). Figures read on 2026-10-30.

## Code request limits (revises NFR-7)

The NFR-7 limits — 3 code requests per email per 15 minutes and 20 per IP address per hour — are generic defaults for a site where people sign in often. With 30-day sessions they allow up to 288 code requests a day for one email and up to 480 code emails a day from one IP address, which is enough to use up the Resend daily limit from a single source.

Revised limits, both over a sliding 24-hour window:

- **Per email: at most 5 code requests in any 24 hours.** An ordinary sign-in uses one or two.
- **Per IP address: at most 10 code requests in any 24 hours** *(agent's call)*. Enough for a reviewers' office behind one NAT; one source cannot use up the daily email limit.

A sliding window needs no time zone, which is unknown before sign-in.

An IPv6 client usually controls a whole /64 prefix, so for IPv6 the per-IP limit counts the /64 prefix; for IPv4 it counts the address *(agent's call)*.

The limits apply in every mode. Their values are configuration, so tests can set them *(agent's call)*. In epic 1 nothing automated signs in on the server: the end-to-end tests and the NFR-11 load test run in development mode, where the code email goes to the log and no email service is used. The developer's check at step 12 signs in with a real email and spends one or two emails. There is no test bypass of sign-in in production.

The rest of NFR-7 is unchanged: 6 digits, valid 10 minutes, at most 5 entry attempts per code, stored only as a hash, requesting a new code invalidates earlier ones.

## What the user sees

- After a code is sent, the user sees how many code requests for this email are left in the current 24-hour window (for example "2 of 5 left"). The count is per email because its purpose is to explain a refusal to that user.
- A refusal by the per-email limit says so and says when the next code can be requested: the moment the oldest request in the window leaves it.
- A refusal by the per-IP limit says so and says when the next code can be requested. It does not show a remaining count: an IP address is often shared with other people *(agent's call)*.
- When the email service refuses to send (its daily limit is used up, or it is unavailable), the user sees that the code could not be sent and why, in plain words, and when to try again if that is known.
- A refused send leaves no new code, and an earlier code that is still valid stays valid: a new code invalidates earlier ones only once it has been sent *(agent's call)*.
- A request the email service refused does not count against the per-email or per-IP limit: the user did nothing that used them up *(agent's call)*.
- A request refused by a limit does not count against either limit: retrying does not extend a refusal *(agent's call)*.
- An unclear result (a timeout, a lost response) counts as sent: the new code is valid, earlier codes are invalidated, the request counts against the limits. If the email does not arrive, the user requests another code *(agent's call)*.
- The remaining count reveals that someone recently requested codes for this email. It does not reveal whether a user with that email exists, since any email can request a code. Accepted *(agent's call)*.

## Accepted risk

Anti-goal 4 holds against abuse from a single source: the per-email and per-IP limits above. Distributed abuse — code requests for many emails from many IP addresses — can use up the 100-a-day limit, after which no one can sign in until it resets. A user who already has a session is not affected. The attacker does not need the codes: each sent email spends the shared limit.

This is accepted. Protection costs more than it is worth for a showcase with single-digit users:

- a paid pay-as-you-go service (Scaleway TEM) removes the lockout but needs a payment method;
- a proof-of-work or third-party captcha adds a dependency, and a botnet absorbs the cost anyway;
- a second email service as a fallback doubles the accounts, DNS records and code;
- reserving part of the daily limit for existing users protects returning users only; considered and not taken;
- an allowlist of emails removes the risk but drops goal 1 ("anyone with an email address can sign in");
- a sign-in that sends no email — through GitHub (OAuth) or with passkeys only — removes every email risk in this section, but replaces goal 1 and the one-time code sign-in that the developer writes; considered and not taken by the developer.

**Targeted lockout of one email.** Anyone who knows a user's email can request 5 codes for it and so block that email from signing in for 24 hours, and repeat this every day to block it for as long as they keep doing it. With the earlier 15-minute window the block lasted at most 15 minutes. This is accepted. The emails of people who sign in from the README are not known in advance; the only public email is the developer's (in the commit history), and the developer stays signed in through a 30-day session, so the block matters only at a rare new sign-in.

A second consequence of distributed abuse: emails to addresses that do not exist bounce, and a high bounce rate can get the Resend account suspended. Also accepted; the delivery log shows it.

This narrows the criterion in the product design ("someone requesting codes for many addresses must not lock other users out"). The narrowing is one ledger line at step 13.

## Legal

Read from Resend's own documents on 2026-10-30; a summary of vendor documents, not legal advice.

- **Roles.** The developer is the controller; Resend is a processor of the recipients' data. Resend's standard DPA applies to every account on any plan: "It's pre-signed and executed when you sign up." The legal entity is Plus Five Five, Inc., San Francisco.
- **Transfer to the USA.** "Resend stores customer data in the United States, including message content, delivery logs, webhook payloads, and account records." The sending region controls "where email is sent from, not where it is stored." Transfers from the EEA rely on the EU SCCs, Module Two (controller to processor); the DPA also states certification under the EU-US Data Privacy Framework.
- **What Resend keeps.** The recipient's email, metadata and message content; "email and log data is retained for 30 days" on the free plan; backups persist for 7 days; data is deleted within 90 days after the account ends. No option to stop storing the message body was found.
- **The code in Resend's store.** The code email body, and so the code, stays at Resend for 30 days. The code is valid for 10 minutes and works once, so a stored code cannot sign anyone in. NFR-10 ("one-time codes never appear in logs") concerns the application's own logs and still holds.
- **Sub-processors.** 22, all in the USA, among them Amazon Web Services. Changes are announced at least 14 days ahead.
- **Open and click tracking** would add IP address, location and device data. It stays off for the code email *(agent's call)*.

The Privacy page (product design, "The product") gains, beyond what it already says:

- the code email is sent by Resend (Plus Five Five, Inc.), a processor in the USA;
- the transfer safeguard: the EU Standard Contractual Clauses and the EU–US Data Privacy Framework (Resend is on the DPF list);
- Resend keeps the email address and the email content for 30 days.

Sources: [Resend DPA](https://resend.com/legal/dpa), [Resend GDPR](https://resend.com/security/gdpr.md), [Resend security](https://resend.com/security), [Resend sub-processors](https://resend.com/legal/subprocessors).

## Constraints

- The sending domain is `mail.cardlane.eu` with its own SPF, DKIM and DMARC records in the OVH zone; the records of `cardlane.eu` stay unchanged. The developer creates the Resend account and the DNS records (product design, "The product").
- The Resend API key lives only in GitHub secrets and on the server.
- In development mode the code email is written to the log instead of being sent (process document, "Epic 1 — walking skeleton"); Resend is used only in production.

## Left to verify

- **Step 5 spike** (`speckit-plan`; the process names this exact unknown): a code email sent through Resend from the VPS arrives in a Gmail inbox, not spam, within 1 minute (NFR-8); whether the free plan adds any branding; how Resend reports a used-up daily limit (error code, reset time) — this decides whether the refusal can name a time.

The [DPF listing of Plus Five Five, Inc.](https://www.dataprivacyframework.gov/participant/8907), read in a browser on 2026-10-30: EU-U.S. Data Privacy Framework and the UK Extension "Active - Re-certification under Review", original certification 02/20/2025, next certification due 03/03/2027, non-HR data; recourse: EU Data Protection Authorities; contact privacy@resend.com. Before the Privacy page is published, the listing is checked once more.

## Resend terms (read 2026-10-30)

From the [terms of service](https://resend.com/legal/terms-of-service) and the [acceptable use policy](https://resend.com/legal/acceptable-use):

- **The free plan** has no restriction on public or non-commercial apps or on the number of users. Resend may change or end it "at any time and without notice". An individual aged 18 or over may hold the account. The terms are governed by California law; the SCCs in the DPA by Irish law.
- **Consent.** "All mail must be sent to recipients who have explicitly opted in"; the customer represents that it has the recipients' consent. A code email goes to the email the person typed into the sign-in form themselves, so the recipient asked for it. A code requested by someone else for another person's email is the abuse case of "Accepted risk".
- **Bounce and complaint thresholds.** "Your complaint rate must be lower than 0.08%." "Your bounce rate must be lower than 4%." Above them "your account may be shut down without warning". At single-digit volume one bounce (a mistyped or invented email) or one spam complaint can cross a threshold. This makes the suspension in "Accepted risk" more likely than abuse alone; it stays accepted, and a suspension shows as refused sends.
- **Nothing in the terms** requires the customer to publish a privacy policy or terms, or to name Resend to recipients. Those obligations come from the law (GDPR), not from Resend.

## Changes to other artifacts

- `docs/domain/ubiquitous-language.md`, term "One-time code": the limits in its rules change to the revised ones. Edited in branch `001-walking-skeleton` (process document, step 8: an epic that changes a term edits the file in its own branch).
- `docs/superpowers/specs/2026-10-28-product-design.md` is revised in place, marked "revised 2026-10-30": NFR-7, the email constraint and the email service line of "Left to the builder" point to this document, so later epics read current requirements.
- `docs/superpowers/specs/2026-10-28-development-process-design.md`, "Open decisions": the email provider row is marked decided.
- Ledger at step 13: NFR-7 revised; the anti-goal 4 criterion narrowed to single-source abuse; targeted lockout of one email accepted; the email service chosen.

## Left to the builder

- The wording of the remaining-count and refusal messages (they live in translation files).
- How limits are stored and counted.
- The Resend client: the official .NET SDK or plain HTTP.
