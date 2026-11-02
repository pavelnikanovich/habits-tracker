# External constraints check — epic 1

Date: 2026-10-30. Epic 1 (walking skeleton), stage 2 of the development process (`docs/superpowers/specs/2026-10-28-development-process-design.md`), a step inserted before step 2 (`speckit-specify`); the deviation is one ledger line at step 13. Stage 1 checked the design for internal consistency; this check tests it against the outside world: laws, vendors, browsers, runtimes and platform limits. Together with `docs/superpowers/specs/2026-10-30-email-service-design.md` and `docs/superpowers/specs/2026-10-30-legal-pages-design.md`, this document is the input to `speckit-specify` for epic 1.

Domain terms are defined in `docs/domain/ubiquitous-language.md`. Items marked *(agent's call)* were proposed by the agent without a separate question and are confirmed by the developer's review of this document.

## Scope and method

Checked: `docs/superpowers/specs/2026-10-28-product-design.md`, `docs/domain/ubiquitous-language.md`, `.specify/memory/constitution.md`. The email service and the Privacy and Terms pages were checked in their own design documents and are not repeated here.

Two layers: the product (layer 1), then the technology the stack implies (layer 2). Facts come from primary sources — the vendors' documentation, pricing and terms, the statutes, the browser and runtime documentation and source code — read on 2026-10-30 and listed under "Sources". Most pages were read through a fetch tool that returns a summary with quotes; the quotes that decide an outcome were taken from those reads, and the ones that were unclear in them (GitHub plan availability, GitHub's terms for Actions, the EAA recitals) were read in the raw page text.

Every item ends with one outcome:

- **confirmed** — the outside world agrees with the design; nothing changes;
- **revised** — the product design is changed in place, marked "revised 2026-10-30";
- **accepted risk** — the constraint is real, the design keeps its course, and the reason is recorded.

A fact that does not change the product but binds the technical plan is listed under "For `speckit-plan`".

The options that were not taken are listed with their facts, as in the email service design: they are the grounds of an accepted decision, not rejected work (principle VII, the developer's reading).

## Layer 1 — product

### 1.1 Backups outside the VPS against the closed list of recipients

**Found.** NFR-5 asks for a daily backup and one before every migration, kept 7 days; the data loss and recovery SLOs rest on it. The Privacy page names a closed list of recipients (Hetzner, Resend) and says backups stay on the Hetzner server (legal pages design, "Privacy page", item 5). A backup on the server's own disk is lost together with the server. Hetzner Cloud Backups do not help with that: "Backups are server-bound and are removed when the server is deleted", and the documentation does not say where they are stored. A Hetzner Storage Box (BX11, 1 TB, €3.20 a month, Germany or Finland) would keep the list closed, since Hetzner is already a recipient.

**Outcome: accepted risk, revised.** The developer's decision: the backups stay on the VPS. The purpose is to show that the backup requirement exists and is met — a daily backup, a backup before migrations, a restore done once with its time recorded — not to survive the loss of the server. The risk: if the server or its disk is lost, the data and every backup are lost together, and no RPO or RTO holds. The backups cover the failures this project is most likely to meet: a failed migration (principle VI), an application defect that damages data, data deleted by mistake. The recipient list and the Privacy page stay as they are. Revised: NFR-5 says where the backups are and what they cover; the data loss and recovery SLOs are stated for failures that leave the server's disk intact.

With one environment and no staging, a restore over the live database would roll back every user's data, and a restore in CI says nothing about the server. The developer's decision: the one required restore is done on the server into a separate temporary database next to the live one; its data is checked against the backup, its time is recorded for the RTO, and it is then removed. Accepted: switching the application over to a restored database is not exercised. Revised: NFR-5 and the recovery SLO row. If the project ever holds data its users would miss, an off-server copy on a Hetzner Storage Box in Germany is the first step: it adds no recipient.

### 1.2 External availability check

**Found.** The availability SLO is measured by "an external check of a health endpoint once a minute, from outside the VPS", and the project wants no cost and no payment method. Of the free services checked, none offers a one-minute check together with a month of history: UptimeRobot checks every 5 minutes and keeps 3 months; Better Stack every 3 minutes; Checkly every 2 minutes, aggregated data for 30 days; Grafana Cloud every 10 seconds but keeps 14 days; HetrixTools every minute, but its pricing and terms could not be read; cron-job.org calls a URL every minute but keeps only the last 50 runs; Healthchecks.io only receives pings; Freshping has shut down. A scheduled GitHub Actions workflow runs at most every 5 minutes; "The `schedule` event can be delayed during periods of high loads", "some queued jobs may be dropped", and "In a public repository, scheduled workflows are automatically disabled when no repository activity has occurred in 60 days." "Scheduled workflows will only run on the default branch", and "Notifications for scheduled workflows are sent to the user who last modified the cron syntax in the workflow file." GitHub's terms allow Actions for activity related to "the production, testing, deployment, or publication of the software project" and forbid "Any activity that places a burden on our servers, where that burden is disproportionate to the benefits".

**Outcome: revised, accepted risk.** The developer's decision: the check runs every 5 minutes from a scheduled GitHub Actions workflow, so no new service enters the project. The health endpoint returns no personal data, so the check adds no recipient. With about 8,640 checks a month, the resolution is far finer than the 1% error budget (about 7 hours). Accepted:

- delayed or dropped runs: the monthly value counts the checks that ran, so a dropped run neither adds nor removes downtime;
- the 60-day rule: once development stops, the schedule turns itself off and the measurement stops with it; any push counts as activity, and the developer re-enables the workflow if needed;
- the terms: a check of the project's own deployment is related to its testing and deployment; a 5-minute job is a low burden. If GitHub ever objects, the fallback is UptimeRobot's free plan (5 minutes, 3 months of history, an EU company) *(agent's call)*.

Consequences, written into the availability row *(agent's call)*:

- a check passes when the health endpoint answers with success over HTTPS within 10 seconds, and the endpoint answers with success only when the application can reach the database; a timeout or a TLS error fails it; the endpoint returns no personal data, which is what keeps the check off the list of recipients;
- the monthly value is the share of passed checks among the checks that ran; a month in which no check ran has no value;
- a failed check notifies the developer through GitHub's notification for a failed workflow run, so the check also alerts, with no extra tool;
- the schedule starts once epic 1 is merged to `main`. Before merge, the branch deployment runs the same check once against the server, so step 12 sees it pass; this is how epic 1 shows that it "measures" the availability SLO, and the first monthly value exists a month after the merge. The end state ("every SLO has a value measured in production") is unchanged. This scheduled workflow belongs to the product; it is not one of the three workflows the process document's stage 2 relies on.

Revised: the availability row names the interval, the workflow, what passes, how the monthly value is counted, who is notified and when measuring starts; "Service level objectives" and "Left to the builder" say this one tool is no longer chosen in `speckit-plan`.

### 1.3 European Accessibility Act

**Found.** Directive (EU) 2019/882 applies from 28 June 2025 to a closed list of services provided to consumers (art. 2(2)): electronic communications, access to audiovisual media, passenger transport, consumer banking, e-books and e-commerce. "E-commerce services" are services provided "with a view to concluding a consumer contract" (art. 3(30)), and recital 43 says these obligations "should apply to the online sale of any product or service". A "service" is "any self-employed economic activity, normally provided for remuneration" (art. 3(3), referring to Directive 2006/123/EC art. 4(1)). Microenterprises providing services are exempt (art. 4(5)). The Polish implementing act (Dz.U. 2024 poz. 731) follows the directive: it covers "handel elektroniczny" offered to consumers (art. 3 ust. 2 pkt 6) and does not apply to services of microenterprises (art. 4 pkt 1). The Polish act on the digital accessibility of public bodies (Dz.U. 2019 poz. 848) applies only to public entities (art. 2).

**Outcome: confirmed.** A free habit tracker sells nothing and is not an economic activity, so it is none of the listed services; even if it were, a one-person project would fall under the microenterprise exemption. No accessibility requirement is added to the design. Accessibility stays a quality of the interface, not a legal duty; if the project ever sells something, this is revisited.

### 1.4 Hetzner VPS resources under NFR-11

**Found.** The developer's server is a Hetzner Cloud CX23 in Falkenstein (FSN1), Germany: 2 vCPU, 4 GB RAM, 40 GB local disk, 20 TB outbound traffic, €5.49 a month before VAT. NFR-11 asks for a load test with 100 users and two open connections each that keeps the API latency SLO. The email service design runs this test in development mode, where no email is sent, and so not on the server: production has no sign-in bypass. A GitHub-hosted runner for a public repository has 4 vCPU and 16 GB RAM, so a green test there says nothing about the server.

**Outcome: revised, accepted risk.** The developer's decision: the load test runs in CI, with the application and database containers limited together to the server's 2 vCPU and 4 GB. Accepted: the runner's disk and network differ from the server's, so the result approximates the server. The location confirms the Privacy page line "Hetzner (hosting, Germany, EU)". Seven days of database backups on a 40 GB disk are not a constraint at this size *(agent's call)*.

### 1.5 Full pass over the product design

The design was read end to end for anything else the outside world decides.

- **Goal 8 still said "English only".** The legal pages design changed the interface to English, Polish and Russian and revised "Interface language", but not goal 8. **Outcome: revised**: goal 8 names the three languages.
- **Sync latency runs against the server need a sign-in.** The sync latency SLO runs an end-to-end test against the server after every deployment and at least once a day. Signing in with a code each time spends Resend's 100 emails a day and the limit of 5 code requests per email, and production has no sign-in bypass (email service design, "Code request limits"). **Outcome: revised**: the developer's decision: the runs sign in with a Remembered session of a dedicated test account, opened once by the developer and kept in GitHub secrets. The daily run is activity, so the sliding session never expires; if the session ends anyway, the developer signs in again. A leaked cookie opens only the test account. The schedule is under the same 60-day rule as 1.2: accepted risk.
- **Third-party requests from the browser.** A font, script or style loaded from another server sends the user's IP address to that server. The Landgericht München I (3 O 17493/20, 20 January 2022) found that sending a visitor's IP address to Google through Google Fonts without consent breached GDPR, and held that there was no legitimate interest, because the fonts can be used "ohne dass beim Aufruf der Webseite eine Verbindung zu einem Google-Server hergestellt wird". Such a request would also add a recipient outside the closed list. **Outcome: revised**: "Sign-in and account" says every page loads everything from `habits.cardlane.eu` *(agent's call)*.
- **Metrics kept for a month.** The API latency SLO keeps request timing for at least a month, while logs are kept 7 days and the Privacy page lists every retention period. Metrics that held an IP address or a user id would be personal data with an unlisted retention period. **Outcome: revised**: the SLO row says the metrics are aggregated and hold neither *(agent's call)*.
- **The `.eu` domain.** Regulation (EU) 2019/517 art. 3 lets "a Union citizen, independently of their place of residence" or "a natural person who is not a Union citizen and who is a resident of a Member State" register a `.eu` name; the developer lives in Poland. **Outcome: confirmed.**
- **Browser support.** The Terms page requires "a current browser with JavaScript and cookies". Angular 22 supports the "widely available" Baseline: Chrome, Edge, Firefox and Safari released within 30 months. **Outcome: confirmed.**
- **Hetzner outbound ports.** Ports 25 and 465 are blocked on Hetzner Cloud servers; the code email goes over Resend's HTTP API on port 443 (email service design). **Outcome: confirmed.**

## Layer 2 — technology

### 2.1 Angular i18n and the language switch

**Found.** Built-in Angular i18n (`@angular/localize`) translates at compile time: "The CLI writes each locale to its own directory", and "To change the language, change your current location to another subdirectory". A new locale is a translation file plus an entry in `angular.json` and a new build. Its runtime path, `loadTranslations`, "does not provide dynamic language changing without refreshing the browser" (Angular source). Runtime translation libraries switch the language in place and read ICU plurals through a plugin: Transloco 8.4.0 with `@jsverse/transloco-messageformat`, or `@ngx-translate/core` 18.1.0 with `ngx-translate-messageformat-compiler`; both support Angular 22. The product design asked for "adding a language means adding a file, with no code change" and did not say whether switching reloads the page.

**Outcome: revised.** The developer's decision: switching the language applies at once, without a page reload, and adding a language stays one file. "Interface language" says so. This is the case for a runtime translation library in `research.md` (principle I); built-in i18n cannot meet it. The library itself is chosen in `speckit-plan`.

### 2.2 .NET time zones in Docker (NFR-4)

**Found.** On Linux, .NET reads time zones from the operating system: `DefaultTimeZoneDirectory = "/usr/share/zoneinfo/"` (runtime source); there is no time zone data built into .NET. The default `10.0` image of ASP.NET Core is Ubuntu 24.04 ("noble") and includes ICU and tzdata. The Alpine and Ubuntu Chiseled images "do not include `icu` or `tzdata`" and "only work with apps that are configured for globalization-invariant mode"; their `-extra` variants include both. Converting between IANA and Windows ids needs ICU and fails in invariant mode. PostgreSQL "includes its own time zone database", separate from the operating system's unless built otherwise.

**Outcome: confirmed.** IANA time zones from the browser work with the default image, without an extra library. Binding facts for the plan are listed below.

### 2.3 Live updates through a reverse proxy and on mobile browsers (NFR-2)

**Found.**

- The SignalR JavaScript client reconnects only with `withAutomaticReconnect()`, by default after "0, 2, 10, and 30 seconds"; "After four failed attempts, it stops trying to reconnect". The server sends a keep-alive every 15 seconds; the client declares the server lost after 30 seconds without a message.
- Chrome on Android freezes background pages after one minute (Chrome 139: "from five minutes to one minute on Android"); a frozen page should "close any open Web Socket connections". No primary source was found for iOS Safari; a page in the background is assumed to lose its connection there too.
- `navigator.onLine` "is inherently unreliable"; the `online` event "shouldn't be used to determine the availability of a particular website".
- nginx closes a proxied WebSocket that carries no data "within 60 seconds" by default. Caddy proxies WebSockets without extra configuration and obtains certificates by itself.
- Over HTTP/1.1, Server-Sent Events are limited to 6 connections per browser; over HTTP/2 the limit is negotiated, 100 by default.
- Playwright's `setOffline` documentation says offline emulation affects WebSockets. Issue #4910 (2021) showed open WebSocket connections kept working while offline; it was closed as stale, not fixed. Whether an already open connection is cut in current versions is not documented. `routeWebSocket` can close a WebSocket from the test.

**Outcome: confirmed, accepted risk.** NFR-2 can be met, but not with library defaults: after a long sleep the default SignalR client stops reconnecting, and a dead connection is noticed only after 30 seconds. The binding facts go to the epic 3 plan. Accepted risk: the NFR-2 end-to-end test relies on a way to cut an open connection that is not documented; epic 3 `speckit-plan` verifies it with a spike before the test is written, with `routeWebSocket` as the fallback (process document, step 5).

### 2.4 Let's Encrypt

**Found.**

- At most 5 certificates "per exact same set of identifiers every 7 days", with no overrides; 50 per registered domain every 7 days.
- Certificates are valid 90 days now; the default profile moves to 64 days on 10 February 2027 and to 45 days on 16 February 2028.
- Expiration emails ended on 4 June 2025; OCSP ended on 6 August 2025.
- The CAA record of `cardlane.eu` is `0 issue "letsencrypt.org"` (queried 2026-10-30): it applies to subdomains, so only Let's Encrypt can issue for `habits.cardlane.eu`.
- `habits.cardlane.eu` has no A record yet.

**Outcome: confirmed.** Let's Encrypt fits the design. Renewal must be automatic and certificates must survive redeployments; nobody is warned by email before a certificate expires. A failed renewal shows up as failed availability checks once the certificate expires: accepted, since the check of 1.2 fails on the TLS error within 5 minutes and GitHub notifies the developer *(agent's call)*.

### 2.5 Deployment from GitHub Actions to the VPS

**Found.**

- Self-hosted runners "should almost never be used for public repositories", because "any user can open pull requests against the repository and compromise the environment".
- GitHub publishes the IP ranges of its hosted runners, updates them weekly, and does "not recommend that you use these as allowlists".
- Secrets are not passed to workflows triggered from forks; adding a label needs at least the Triage role.
- `pull_request_target` with untrusted code "may lead to security vulnerabilities"; GitHub's advice is to avoid it.
- Environments with required reviewers, deployment branch rules and environment secrets are available for public repositories on GitHub Free.
- The Hetzner Cloud Firewall is free and can restrict traffic by source IP.

**Outcome: confirmed.** The process document's workflows (process document, "Epic 1 — walking skeleton") fit: a GitHub-hosted runner deploys over SSH, and only the developer can add the `deploy` label. The SSH port cannot be limited to GitHub's addresses; it stays open with key-only authentication: accepted, it is the usual setup for a single VPS *(agent's call)*.

### 2.6 GitHub Free limits

**Found.**

- Actions on standard hosted runners are free for public repositories.
- `ubuntu-latest` has 4 vCPU, 16 GB RAM and 14 GB SSD; a job runs at most 6 hours.
- Artifacts and logs are kept 90 days by default, and at most 90 days for public repositories.
- The cache is limited to 10 GB per repository.
- Container image storage and bandwidth in the GitHub Container Registry are free for public packages.
- "Protected branches are available in public repositories with GitHub Free"; "Rulesets are available in public repositories with GitHub Free".
- Scheduled workflows: see 1.2.

**Outcome: confirmed.** Branch protection with required checks after epic 1 merges is available as the process document plans. Run history kept 90 days is enough to count a month of availability checks (1.2).

## For `speckit-plan`

Facts that bind the technical plan without changing the product. Epic 1 unless marked.

- **Translations** (2.1): a runtime translation library with ICU plurals, its case in `research.md`. The code email is translated by the backend.
- **Container images** (2.2): the default ASP.NET Core image (Ubuntu) or a `-extra` variant; never Alpine or Chiseled without `-extra`; never globalization-invariant mode. tzdata comes with the image, so an image rebuild picks up time zone changes.
- **Time zone data** (2.2, epic 2): PostgreSQL stores tracking days as dates and does not convert time zones for them, so its own time zone database never takes part; the server takes its rules from the image's tzdata, the browser from its own. A rule change that reaches one before the other can shift a tracking day near midnight in the affected zone until both are updated: accepted *(agent's call)*.
- **Live updates** (2.3, epic 3):
  - a reconnect policy that never gives up;
  - a reconnect and catch-up triggered by `online`, `visibilitychange` and `pageshow`, not only by the library's timers;
  - the "offline" state taken from the connection, not from `navigator.onLine`;
  - the proxy's idle timeout longer than the keep-alive interval;
  - HTTP/2 if Server-Sent Events are chosen;
  - the spike on cutting an open connection in Playwright.
- **Certificates** (2.4): automatic renewal, ready for 64-day and 45-day lifetimes; the certificate store on a persistent volume that a deployment never recreates, or 5 redeployments in a week lock certificate issuance; only Let's Encrypt is allowed by CAA. The A record of `habits.cardlane.eu` is a manual task.
- **Deployment** (2.5): GitHub-hosted runners only; no `pull_request_target`; the deployment workflows run only for branches of this repository; SSH with key-only authentication.
- **Load test** (1.4): the application and database containers limited together to 2 vCPU and 4 GB in CI.
- **Availability check** (1.2): a scheduled workflow every 5 minutes on `main`, with a 10-second timeout, and the same check run once by the branch deployment; the health endpoint checks the database; the monthly value is computed from the run history. `research.md` records its case (principle I): the facts of 1.2.
- **Restore** (1.1): the restore into a separate temporary database on the server, checked against the backup and timed.
- **No third-party requests** (1.5): fonts, icons and every other asset are served by the application.
- **Metrics** (1.5): request timing aggregated, without IP addresses or user ids.

## For epic 1 `speckit-specify` and `speckit-clarify`

- **Technology in the revised rows.** The spec states the revised NFR and SLO rows as behavior — an external check every 5 minutes, a load test within the server's CPU and memory, and in epic 3 a test account that signs in without a code email — and does not name GitHub, Hetzner or any tool, as the email service design does for the email service. The names bind the plan *(agent's call)*.
- Open for `speckit-clarify`, not settled here:
  - how the restored database is checked and where its time is recorded (1.1);
  - whether switching the language after sign-in reaches the user's other open devices (2.1; the context map carries no locale change);
  - the story map's "visitor" (US-1.1, US-1.7) has no term in `docs/domain/ubiquitous-language.md`; the spec needs a term or other wording.

## Accepted risks

- Losing the server loses the data and every backup; RPO and RTO hold only while the disk survives (1.1).
- The restore is exercised into a separate database; switching the application over to it is not (1.1).
- Scheduled checks can be delayed or dropped, stop after 60 days without repository activity, start measuring only after epic 1 merges, and rest on GitHub's reading of its terms (1.2, 1.5).
- The load test approximates the server: the CPU and memory are the same, the disk and network are not (1.4).
- The NFR-2 test method rests on a spike in epic 3 (2.3).
- A failed certificate renewal is noticed only when the certificate expires (2.4).
- A time zone rule change can reach the server and the browser at different times (2.2).
- The SSH port is open to the internet, with key-only authentication (2.5).

## Changes to other artifacts

- `docs/superpowers/specs/2026-10-28-product-design.md`, revised in place, marked "revised 2026-10-30": goal 8; "Sign-in and account" (no third-party requests); "Interface language" (switching without a reload); NFR-5 (backups on the VPS, the restore into a separate database); NFR-11; the availability, API latency, sync latency, data loss and recovery rows of the SLO table and the sentence on measuring tools under it; "Left to the builder" (the availability check is no longer the plan's choice). The story map stays frozen.
- `docs/domain/ubiquitous-language.md`: no change; "Locale" already says a new language is a new file.
- `.specify/memory/constitution.md`: no change; the product constraints it holds did not change.
- Ledger at step 13: the inserted step; backups on the VPS as an accepted risk; the restore into a separate database on the server; the availability check every 5 minutes from GitHub Actions; the load test in CI with the server's limits; the sync latency runs through a Remembered session of a test account; switching the language without a reload.

## Sources

Read on 2026-10-30.

- Hetzner: [backups and snapshots](https://docs.hetzner.com/cloud/servers/backups-snapshots/overview/), [Regular Performance servers](https://www.hetzner.com/cloud/regular-performance/), [billing FAQ](https://docs.hetzner.com/cloud/billing/faq/), [Storage Box](https://www.hetzner.com/storage/storage-box/), [Object Storage](https://www.hetzner.com/storage/object-storage/), [locations](https://docs.hetzner.com/cloud/general/locations/), [Cost-Optimized servers](https://www.hetzner.com/cloud/cost-optimized/), [server FAQ (ports)](https://docs.hetzner.com/cloud/servers/faq/), [Cloud Firewall](https://docs.hetzner.com/cloud/firewalls/overview/), [data protection](https://docs.hetzner.com/general/company-and-policy/data-protection-at-hetzner/).
- Availability check: [UptimeRobot pricing](https://uptimerobot.com/pricing/), [UptimeRobot terms](https://uptimerobot.com/terms/), [Better Stack pricing](https://betterstack.com/pricing), [HetrixTools](https://hetrixtools.com/), [StatusCake pricing](https://www.statuscake.com/pricing/), [cron-job.org FAQ](https://cron-job.org/en/faq/), [Healthchecks.io pricing](https://healthchecks.io/pricing/), [Checkly pricing](https://www.checklyhq.com/pricing/), [Grafana pricing](https://grafana.com/pricing/), [GitHub: events that trigger workflows](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows), [GitHub terms for additional products](https://docs.github.com/en/site-policy/github-terms/github-terms-for-additional-products-and-features).
- Law: Directive (EU) 2019/882 (http://publications.europa.eu/resource/celex/32019L0882; https://eur-lex.europa.eu/eli/dir/2019/882/oj), Directive 2006/123/EC art. 4(1) (http://publications.europa.eu/resource/celex/32006L0123), Dz.U. 2024 poz. 731 (https://api.sejm.gov.pl/eli/acts/DU/2024/731/text.html), Dz.U. 2019 poz. 848 (https://api.sejm.gov.pl/eli/acts/DU/2019/848/text.html), LG München I 3 O 17493/20 (https://www.gesetze-bayern.de/Content/Document/Y-300-Z-GRURRS-B-2022-N-612), Regulation (EU) 2019/517 (http://publications.europa.eu/resource/celex/32019R0517; https://eur-lex.europa.eu/eli/reg/2019/517/oj).
- Angular: [i18n merge](https://angular.dev/guide/i18n/merge), [i18n deploy](https://angular.dev/guide/i18n/deploy), [`translate.ts`](https://github.com/angular/angular/blob/main/packages/localize/src/translate.ts), [i18n prepare (ICU plurals)](https://angular.dev/guide/i18n/prepare), [versions and browser support](https://angular.dev/reference/versions), [Transloco on npm](https://www.npmjs.com/package/@jsverse/transloco), [Transloco message format](https://jsverse.gitbook.io/transloco/plugins-and-extensions/message-format), [ngx-translate on npm](https://www.npmjs.com/package/@ngx-translate/core), [ngx-translate-messageformat-compiler](https://www.npmjs.com/package/ngx-translate-messageformat-compiler).
- .NET and PostgreSQL: [`TimeZoneInfo.Unix.cs`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/TimeZoneInfo.Unix.cs), [globalization and ICU](https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-icu), [invariant mode](https://github.com/dotnet/runtime/blob/main/docs/design/features/globalization-invariant-mode.md), [ASP.NET Core image tags](https://github.com/dotnet/dotnet-docker/blob/main/README.aspnet.md), [image variants](https://github.com/dotnet/dotnet-docker/blob/main/documentation/image-variants.md), [PostgreSQL build options](https://www.postgresql.org/docs/current/install-make.html).
- Live updates: [SignalR JavaScript client](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client), [SignalR configuration](https://learn.microsoft.com/en-us/aspnet/core/signalr/configuration), [nginx WebSocket proxying](https://nginx.org/en/docs/http/websocket.html), [nginx proxy module](https://nginx.org/en/docs/http/ngx_http_proxy_module.html), [Caddy reverse_proxy](https://caddyserver.com/docs/caddyfile/directives/reverse_proxy), [Caddy automatic HTTPS](https://caddyserver.com/docs/automatic-https), [MDN: Server-sent events](https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events/Using_server-sent_events), [Chrome 139 release notes](https://developer.chrome.com/release-notes/139), [Page Lifecycle API](https://developer.chrome.com/docs/web-platform/page-lifecycle-api), [MDN: `navigator.onLine`](https://developer.mozilla.org/en-US/docs/Web/API/Navigator/onLine), [MDN: `online` event](https://developer.mozilla.org/en-US/docs/Web/API/Window/online_event), [Playwright BrowserContext](https://playwright.dev/docs/api/class-browsercontext), [Playwright issue #4910](https://github.com/microsoft/playwright/issues/4910).
- Let's Encrypt: [rate limits](https://letsencrypt.org/docs/rate-limits/), [from 90 to 45 days](https://letsencrypt.org/2025/12/02/from-90-to-45), [profiles](https://letsencrypt.org/docs/profiles/), [ending expiration emails](https://letsencrypt.org/2025/01/22/ending-expiration-emails/), [OCSP end of life](https://letsencrypt.org/2025/08/06/ocsp-service-has-reached-end-of-life/), [CAA](https://letsencrypt.org/docs/caa/); `dig CAA cardlane.eu`.
- GitHub: [Actions billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions), [hosted runners](https://docs.github.com/en/actions/reference/runners/github-hosted-runners), [limits](https://docs.github.com/en/actions/reference/limits), [artifact retention](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/enabling-features-for-your-repository/managing-github-actions-settings-for-a-repository), [dependency caching](https://docs.github.com/en/actions/reference/workflows-and-actions/dependency-caching), [Packages billing](https://docs.github.com/en/billing/concepts/product-billing/github-packages), [deployments and environments](https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments), [protected branches](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches), [rulesets](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/about-rulesets), [secure use](https://docs.github.com/en/actions/reference/security/secure-use).

external constraints check complete
