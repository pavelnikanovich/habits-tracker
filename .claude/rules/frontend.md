---
paths:
  - "frontend/**"
---

# Frontend rules

These hold the translation rule of FR-020, the cross-site scripting defence of research R19 (T2) and the no-third-party rule of FR-027; the unit tests guard the first two, review and the end-to-end check the third.

- Every user-visible string goes through Transloco and exists in all three files `public/i18n/{en,pl,ru}.json`: a page then reads in every language, and the key parity test fails on a key one file lacks. Write Polish and Russian plurals with their own forms (`one`, `few`, `many`, `other`).
- Render no translation as HTML: no `innerHTML` and no `bypassSecurityTrust*`. Angular escapes what it renders, so a text with a link is composed from keys around the link. A unit test scans `src/` for both words.
- Load nothing from outside the origin: no font, script, style, image or request to another server. The CSP of the host blocks it in production anyway, so it would only fail there.
- Every page uses the interface baseline (tokens, global styles, shared components), so pages built by separate tasks look and behave as one:
  - Component CSS takes colours and font sizes only from the `var(--…)` tokens of `src/styles/tokens.css`, in a `.css` file next to the component (`styleUrl`): the two themes and the type scale stay in one place, and a unit test reads those files and fails on a literal.
  - Every message goes through `app-message` (`kind` `error` or `info`), so errors are announced and look the same everywhere.
  - No field takes the focus when a page opens: on a phone the keyboard would cover half the screen.
  - A button is disabled while its request runs, so a second press sends nothing.
  - Each page's `h1` has `tabindex="-1"`: the focus moves to it after a navigation, so a screen reader announces the new page.
- Tests first: a failing Vitest spec, then the code.
