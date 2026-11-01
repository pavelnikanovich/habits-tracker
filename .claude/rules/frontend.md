---
paths:
  - "frontend/**"
---

# Frontend rules

These hold the translation rule of FR-020, the cross-site scripting defence of research R19 (T2) and the no-third-party rule of FR-027; the unit tests guard the first two, review and the end-to-end check the third.

- Every user-visible string goes through Transloco and exists in all three files `public/i18n/{en,pl,ru}.json`: a page then reads in every language, and the key parity test fails on a key one file lacks. Write Polish and Russian plurals with their own forms (`one`, `few`, `many`, `other`).
- Render no translation as HTML: no `innerHTML` and no `bypassSecurityTrust*`. Angular escapes what it renders, so a text with a link is composed from keys around the link. A unit test scans `src/` for both words.
- Load nothing from outside the origin: no font, script, style, image or request to another server. The CSP of the host blocks it in production anyway, so it would only fail there.
- Tests first: a failing Vitest spec, then the code.
