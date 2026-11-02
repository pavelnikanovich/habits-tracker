# Privacy and Terms pages — design

Date: 2026-10-30. Epic 1 (walking skeleton), stage 2, step 2 of the development process (`docs/superpowers/specs/2026-10-28-development-process-design.md`). Together with `docs/superpowers/specs/2026-10-30-email-service-design.md`, this document is the input to `speckit-specify` for epic 1.

Domain terms are defined in `docs/domain/ubiquitous-language.md`. Items marked *(agent's call)* were proposed by the agent; the developer asked the agent to follow its recommendations, and the review of this document confirms them.

This is research by the developer and an agent, not legal advice. Statutes were read on 2026-10-30; the sources are listed at the end.

## Intent

The product design already requires a Privacy page readable before sign-in (product design, "The product"). The developer adds a Terms page. Both are short, plain pages that meet Polish and EU law for this project as it is: a free, non-commercial web app run by a private individual living in Poland, with no business, no revenue, no ads and no analytics.

## Which laws apply

- **GDPR applies.** The household exemption (art. 2(2)(c)) does not cover a public app: recital 18 applies the regulation to those "which provide the means for processing personal data for such personal or household activities", and the CJEU reads the exemption narrowly (Ryneš, C-212/13). The developer is the controller. A data protection officer is not required (art. 37(1)).
- **The Polish e-services act (UŚUDE) most likely does not apply.** Its duties fall on a "usługodawca", one "która prowadząc, chociażby ubocznie, działalność zarobkową lub zawodową świadczy usługi drogą elektroniczną" (art. 2 pkt 6); a no-revenue hobby project is neither. No ruling or authority guidance confirms this. The Terms page still covers the art. 8(3) list, because it costs nothing and Civil Code art. 384 needs terms delivered before the contract to make them binding *(agent's call)*. The home address required by art. 5(2) is not published (see "Accepted risk").
- **The Digital Services Act most likely does not apply.** It covers information society services, "normally provided for remuneration" (Directive 2015/1535); the CJEU requires an economic activity (McFadden, C-484/14). Even if it applied, the app would be a hosting service, not an online platform: users' data is never shown to others. The Terms page still includes the cheap DSA items — content rules and a reporting address *(agent's call)*.
- **Consumer law does not apply**: a consumer contracts with a business (Civil Code art. 22¹), and the developer runs none.
- **Cookies**: the Polish electronic communications law, art. 399 ust. 3, exempts storage "konieczne do… dostarczania… usługi świadczonej drogą elektroniczną, żądanej przez… użytkownika końcowego" from consent and prior information. A session cookie that lives until the browser closes is exempt. A persistent login cookie is not clearly exempt: the Article 29 Working Party (WP194, non-binding) says "persistent login cookies… are not exempted" and suggests a "remember me" checkbox.
- **Language**: the Polish language act requires Polish in trade with consumers (art. 7 ust. 1); there is none, so no language is required by law. The developer chose three languages for the whole app (see "Languages").

## Languages (revises the product design, "Language")

The developer's decision: the whole app — the interface, the one-time code email, the Privacy and Terms pages — is in English, Polish and Russian from epic 1.

- Before sign-in the language follows the browser's preferred languages; anything else falls back to English *(agent's call)*.
- A language switcher is on every page, the sign-in page included. Before sign-in the choice is kept in the browser's local storage; this is a user-chosen interface setting, exempt from consent like the session cookie (WP194, "user-interface customisation"), and the Privacy page lists it next to the cookie *(agent's call)*.
- At the first sign-in the current language becomes the user's locale, stored with the account; the switcher changes it. The code email is in the language of the page it was requested from *(agent's call)*.
- Translation files with plural rules (product design, "Language") cover Polish and Russian plurals; adding a language is still adding a file.
- The agent drafts the Polish and Russian texts; the developer reviews them. For the Privacy and Terms pages the English version prevails if the versions differ, and each page says so *(agent's call)*.

This changes the term "Locale" in `docs/domain/ubiquitous-language.md` ("Only English exists now"), edited in branch `001-walking-skeleton`.

## Remember me (revises NFR-9)

The sign-in form gets a "Remember me" checkbox, unchecked by default *(agent's call, chosen by the developer)*.

- **Unchecked**: the session cookie has no expiry and ends when the browser closes. The server-side session also expires after 30 days without activity, as in NFR-9.
- **Checked**: the session cookie persists; the session is sliding and expires after 30 days without activity (NFR-9 unchanged otherwise).
- Ticking the box is the user's consent to the persistent cookie. No cookie banner: the app sets no other cookies.
- The box stays unchecked by default: a pre-ticked box is not valid consent (CJEU Planet49, C-673/17).
- The code request limits of the email service design (5 per email, 10 per IP address in 24 hours) stay. A user who never ticks the box spends one code per browser restart, about one a day; 5 still leave room for mistakes, and a few reviewers behind one NAT still fit in 10 *(agent's call)*.

A session opened with the box ticked is a **Remembered session** (Запомненный сеанс), a new Identity term chosen by the developer; a session opened without it is a session that is not remembered. This changes the term "Session" and adds "Remembered session" in `docs/domain/ubiquitous-language.md` (edited in branch `001-walking-skeleton`, process document step 8). The change to NFR-9 is one ledger line at step 13.

## Privacy page

Required by GDPR art. 13 unless marked. Each item says what the page states.

1. **Controller**: the developer's full name and a contact email (art. 13(1)(a)). The developer supplies both in epic 1 (product design, "The product").
2. **Data protection officer**: none (art. 13(1)(b), "where applicable").
3. **Purposes and legal basis** (art. 13(1)(c)):
   - the account (email, time zone, locale), habits and check-ins, sessions and the session cookie, the one-time code and its email: providing the service the user signed up for, art. 6(1)(b);
   - IP addresses in logs and in code request limits: security and abuse prevention, art. 6(1)(f).
4. **Legitimate interest** (art. 13(1)(d)): keeping the service secure and preventing abuse of sign-in.
5. **Recipients** (art. 13(1)(e)): Hetzner (hosting, Germany, EU) and Resend, Plus Five Five, Inc. (sending the code email, USA). The list is closed: all other personal data, backups and logs included, stays on the Hetzner server. A tool that would send personal data to anyone else (a backup store, a log or metrics service, an uptime check that receives logs) needs the developer's approval, and the Privacy page and the record of processing change in the same epic *(agent's call)*.
6. **Transfer outside the EU** (art. 13(1)(f)): to Resend in the USA, under the EU–US Data Privacy Framework adequacy decision (Commission Implementing Decision (EU) 2023/1795; Resend is on the DPF list), with the EU Standard Contractual Clauses in Resend's DPA as the second safeguard; where to find them (the DPA link).
7. **Retention** (art. 13(2)(a)):
   - account data, habits and check-ins: until the account is deleted; until account deletion exists (epic 8), on request to the contact email (product design, "The product");
   - sessions: until sign-out, sign-out everywhere, or 30 days without activity;
   - one-time codes: valid 10 minutes, stored only as a hash;
   - emails and IP addresses in code request limits: 24 hours; logs: 7 days;
   - backups: deleted data stays in backups for up to 7 days;
   - Resend: the code email (address and content) for 30 days.

   These periods are deletion deadlines, not only "no longer used": the system deletes expired codes, limit records, sessions and logs, at most one day after the period ends; the page may round up by that day *(agent's call)*. A code request for an email that never becomes a user leaves nothing after these periods.
8. **Rights** (art. 13(2)(b)): access, rectification, erasure, restriction, objection to the art. 6(1)(f) processing, portability; how to use them (the contact email).
9. **Complaint** (art. 13(2)(d), art. 77): to the President of the Personal Data Protection Office (Prezes UODO), ul. Stanisława Moniuszki 1A, 00-014 Warszawa, or to the supervisory authority of the user's own EU country.
10. **Is the data required** (art. 13(2)(e)): an email is needed to sign in; without it there is no account.
11. **Automated decisions** (art. 13(2)(f)): none, no profiling.
12. **The session cookie**: one `HttpOnly` cookie holding an opaque session id; its purpose; that it ends with the browser unless "Remember me" is ticked, then 30 days without activity; the language choice and the theme choice kept in the browser's local storage, user-chosen interface settings exempt from consent like the cookie (WP194, "user-interface customisation"; the theme added on 2026-10-31 with the epic 1 spec, FR-042); no other cookies, no analytics, no ads (GDPR transparency, art. 12; WP194).
13. **Privacy of habits**: a user's habits and check-ins are never shown to another user (prudent).
14. **What not to enter**: habit names are free text; the page asks users not to put health or other sensitive data in them (see "Accepted risk") *(agent's call)*.
15. **Effective date** and how changes are announced (prudent).

## Terms page

| Item | Basis |
|---|---|
| Who provides the service: the developer's full name and contact email; no home address | UŚUDE art. 5(2), as a prudent minimum |
| What the service is: a free habit tracker; a non-commercial showcase project | UŚUDE art. 8(3) pkt 1 |
| Technical requirements: a current browser with JavaScript and cookies, an email inbox | UŚUDE art. 8(3) pkt 2a |
| Users must not enter unlawful content | UŚUDE art. 8(3) pkt 2b; DSA art. 14(1) |
| How the agreement starts (the first sign-in creates the account) and ends (the user stops at any time; account deletion — on request by email until epic 8; the developer may delete an account that breaks the Terms, or end the service, with notice to the contact email where possible). Deleting an account for a breach is a manual act of the developer: epic 1 builds no suspension state *(agent's call)* | UŚUDE art. 8(3) pkt 3, art. 7 pkt 2 |
| Complaints and reports of unlawful content: to the contact email, answered within 14 days *(agent's call)* | UŚUDE art. 8(3) pkt 4; DSA art. 16 |
| Risks: whoever controls the user's email inbox can sign in; sign in only on trusted devices with "Remember me" | UŚUDE art. 6 |
| "As is", free, no guarantee of availability or of keeping data beyond the backup policy; liability limited as far as the law allows, never for intentional harm | Civil Code art. 473 § 2 |
| Sign-in can be unavailable for up to 24 hours if the email service's daily limit is used up (see the email service design, "Accepted risk") | transparency |
| Changes to the Terms: announced on the page with an effective date. Epic 1 does not record which version a user accepted; a notice to signed-in users is a later small change if the Terms ever change materially *(agent's call)* | Civil Code art. 384¹ |
| Governing law: Polish | UŚUDE art. 3a(1) |
| Link to the Privacy page | GDPR art. 12 |

## Where the pages are reached

- Both pages are public, readable before sign-in, linked from the sign-in page and from every signed-in screen.
- Under the sign-in button: "By signing in you accept the Terms and acknowledge the Privacy notice", with both links. No acceptance checkbox *(agent's call)*. Terms reached this way are delivered before the contract (Civil Code art. 384 § 1, § 4: a page the user can save and print).
- The page texts live in translation files like every user-visible string (product design, "Language").

## Record of processing

A one-page record of processing under GDPR art. 30 is kept in the repository at `docs/legal/record-of-processing.md`: the same purposes, data, recipients, transfers and retention as the Privacy page *(agent's call)*. The art. 30(5) exemption is doubtful: the processing is not occasional, and whether an individual is an "enterprise or organisation" is unclear. Keeping it costs one page. It is a task of the epic 1 plan, reviewed by the developer like the Privacy page.

The contact email on both pages and in the record is a personal mailbox the developer supplies (product design, "The product"); it needs no DNS change, so the records of `cardlane.eu` stay unchanged *(agent's call)*.

## Accepted risk

- **No home address.** If UŚUDE applies after all, art. 5(2) requires "imię, nazwisko, miejsce zamieszkania i adres", and art. 23 makes the omission a petty offence (a fine). Accepted: the project has no revenue and no business, and publishing a home address on a public showcase has its own cost *(agent's call)*. If the project ever earns money or becomes part of a business, the address is added.
- **Health data in habit names.** A habit named "take medication" may reveal health data (GDPR art. 4(15)), which art. 9(1) prohibits without an exception such as explicit consent. Accepted with a request on the Privacy page not to enter such data *(agent's call)*; no consent flow is added. If the product ever suggests habit names or categories, this is revisited.
- **The pages are written by the developer and an agent, not a lawyer.**

## Changes to other artifacts

- `docs/domain/ubiquitous-language.md`: "Session" (the cookie lifetime with and without "Remember me"), new term "Remembered session", "Locale" (three languages). Edited in branch `001-walking-skeleton`.
- `docs/superpowers/specs/2026-10-28-product-design.md` is revised in place, marked "revised 2026-10-30": goal 10, "Sign-in and account", "Interface language", NFR-9 and the epic 1 summary point to this document. The story map stays frozen, as it says; the epic 1 spec owns the new stories.
- Ledger at step 13: NFR-9 changed by "Remember me"; English only replaced by English, Polish and Russian; the Terms page added to epic 1; the accepted legal risks.

## Left to the builder

- The page wording, within the lists above.
- The layout of the pages and of the links.

## Sources

Polish statutes from the Sejm ELI API; EU texts by their EUR-Lex identifiers.

- UŚUDE, consolidated text Dz.U. 2024 poz. 1513: https://api.sejm.gov.pl/eli/acts/DU/2024/1513/text/T/D20241513L.pdf. The amending act Dz.U. 2026 poz. 1296 (in force 2026-11-06) does not change art. 2 pkt 6, 5 or 8: https://api.sejm.gov.pl/eli/acts/DU/2026/1296/text.pdf
- Civil Code, Dz.U. 2026 poz. 795: https://api.sejm.gov.pl/eli/acts/DU/2026/795/text.pdf
- Prawo komunikacji elektronicznej, Dz.U. 2024 poz. 1221, art. 399: https://api.sejm.gov.pl/eli/acts/DU/2024/1221/text.pdf
- Polish language act, Dz.U. 2026 poz. 81: https://api.sejm.gov.pl/eli/acts/DU/2026/81/text.pdf
- Polish Data Protection Act, art. 34: https://api.sejm.gov.pl/eli/acts/DU/2019/1781/text.pdf; UODO contact: https://uodo.gov.pl/pl/p/kontakt
- GDPR: https://eur-lex.europa.eu/eli/reg/2016/679/oj
- DSA: https://eur-lex.europa.eu/eli/reg/2022/2065/oj; Directive 2015/1535: https://eur-lex.europa.eu/eli/dir/2015/1535/oj
- DPF adequacy decision 2023/1795: https://eur-lex.europa.eu/eli/dec_impl/2023/1795/oj; Resend on the DPF list: https://www.dataprivacyframework.gov/participant/8907
- CJEU Ryneš C-212/13: https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:62013CJ0212; McFadden C-484/14: https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:62014CJ0484; Planet49 C-673/17 (consent "is not validly constituted by way of a pre-ticked checkbox"): https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:62017CJ0673, press release https://curia.europa.eu/jcms/upload/docs/application/pdf/2019-10/cp190125en.pdf
- WP29 Opinion 04/2012 (WP194) on the cookie consent exemption: https://ec.europa.eu/justice/article-29/documentation/opinion-recommendation/files/2012/wp194_en.pdf
