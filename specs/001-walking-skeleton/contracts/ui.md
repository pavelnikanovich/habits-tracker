# UI contract: Walking skeleton

What every page of epic 1 looks like and how it behaves: the shared visual baseline, the two themes, the shared components, and the states of every screen. The routes are in `contracts/http-api.md`, "Pages"; the requirements are FR-014, FR-015, FR-018, FR-019, FR-021, FR-022, FR-025, FR-027, FR-041 and FR-042 (NFR-13, NFR-14, NFR-15); the decisions and their alternatives are in research R24. Task 6 of the task plan creates the baseline and the shared components; every later page task uses them and adds no colour, size or font of its own.

Every user-visible text below is a translation key (FR-020); the English wording shown is the `en` value. `error.<code>` is the message of a problem `code` of `contracts/http-api.md`.

## Visual baseline

**Files.** Design tokens are CSS custom properties in `frontend/src/styles/tokens.css`; global rules (font, page frame, focus, controls) in `frontend/src/styles.css`. A component's CSS uses only `var(--…)` tokens: no colour literal (`#…`, `rgb(…)`, `hsl(…)`, a colour name) and no font size in `px`. A unit test reads every component CSS file and fails on either (research R24).

**Font.** Nordwand Mono 1.5.0, Regular (400) and Bold (700), WOFF2, served by the application itself (FR-027): `--font-mono: "Nordwand Mono", ui-monospace, "SF Mono", Menlo, Consolas, "Liberation Mono", monospace`, `font-display: swap`, so the text shows in the fallback until the font loads. The files are fetched at build time, not kept in the repository (research R24). No other weight, no italic.

**Type scale** (`--text-sm` … `--text-2xl`): 14 / 16 / 20 / 25 / 31 px, written in `rem` (0.875 / 1 / 1.25 / 1.5625 / 1.9375); body 16 px; input fields 16 px or larger. Line height 1.5 for body text, 1.25 for headings.

**Spacing** (`--space-1` … `--space-7`): 4 / 8 / 12 / 16 / 24 / 32 / 48 px, in `rem`.

**Layout.** One column, at most `40rem` wide, centred, with `--space-4` side padding; the same column on a desktop. No element has a fixed width wider than 320 px.

**Shape.** Square corners, 1 px borders, no shadows, no gradients.

**Terminal mark.** Every page heading (`h1`) is preceded by a `>` prompt in the accent colour, rendered by CSS (`::before`) and hidden from screen readers. No blinking cursor.

**Glow.** Dark theme only: the heading and accent text carry a static `text-shadow: 0 0 6px` in the accent colour. The contrast test uses the colours alone.

**Controls.** Buttons and input fields at least 44 px high. The primary button is outlined: accent text and border on the background; on hover and focus it fills with the accent and its text takes the background colour. A disabled button has the `disabled` attribute, the muted colour for text and border, and no hover fill.

**Focus.** Every interactive element shows `outline: 3px solid var(--focus)` with `outline-offset: 2px` on `:focus-visible`. `outline: none` appears only together with a replacement of equal visibility.

**Motion.** None: no transitions, no animations.

**Long text.** Text that can be long (an email, an address on the legal pages) wraps anywhere (`overflow-wrap: anywhere`), so nothing overflows at 320 px.

**Native controls.** Each theme sets CSS `color-scheme` (`dark` or `light`), so the browser draws the checkbox, the drop-down list and scroll bars in the theme; autofilled fields (`:-webkit-autofill`) keep `--surface` and `--text`. `<meta name="theme-color">` follows the theme (`--bg`), so a phone browser's toolbar matches the page.

## Themes

Two themes, dark and light (FR-042). The theme is the `data-theme` attribute of `<html>`; `index.html` ships `data-theme="dark"`, so a browser that has never chosen a theme shows the dark theme before and after the application starts, whatever `prefers-color-scheme` says.

| Token | Use | Dark (default) | Light |
|---|---|---|---|
| `--bg` | page background | `#0a0f0b` | `#f3f7f2` |
| `--surface` | input fields, the message box | `#101912` | `#e8efe6` |
| `--text` | body text | `#c9f7d3` | `#0d2e17` |
| `--muted` | secondary text, disabled controls | `#7fb58c` | `#3d5e46` |
| `--accent` | links, buttons, headings, the prompt | `#39ff14` | `#0a6b2c` |
| `--focus` | focus outline | `#00e5ff` | `#0050b3` |
| `--error` | error messages, the border of an invalid field | `#ff4d6d` | `#b0002a` |
| `--border` | input field borders | `#3f8a55` | `#5b7d63` |

**Contrast pairs** checked by the unit test in both themes (FR-041), with the value each pair has today:

| Pair | Threshold | Dark | Light |
|---|---|---|---|
| `--text` on `--bg`, on `--surface` | 4.5:1 | 16.32, 15.15 | 13.66, 12.62 |
| `--muted` on `--bg`, on `--surface` | 4.5:1 | 8.19, 7.60 | 6.72, 6.21 |
| `--accent` on `--bg`, on `--surface` | 4.5:1 | 14.26, 13.24 | 6.15, 5.68 |
| `--bg` on `--accent` (filled button) | 4.5:1 | 14.26 | 6.15 |
| `--error` on `--bg`, on `--surface` | 4.5:1 | 6.02, 5.59 | 6.74, 6.22 |
| `--focus` on `--bg`, on `--surface` | 3:1 | 12.57, 11.67 | 6.93, 6.40 |
| `--border` on `--bg`, on `--surface` | 3:1 | 4.58, 4.26 | 4.26, 3.93 |

A colour changed later keeps its hue and changes only its lightness until its pairs pass; the new value is written here.

**Choice.** `ThemeService` reads `localStorage["theme"]` at start: `"light"` or `"dark"` sets `data-theme`; any other value, no value, or storage that throws leaves the dark theme. Switching writes the key and sets the attribute at once, without a reload; a write that throws is ignored, so the switch still applies to the page (spec, Edge Cases). The choice is the same before and after sign-in and never goes to the server. A browser that stored `"light"` shows the dark theme until the application starts, for the time it takes to load *(agent's call: a script in `<head>` that sets the theme before the first paint is a second file holding the same rule)*.

## Shared components

**Page frame** (every page): a header with the language switcher and the theme switcher; `<main>` with one `h1`; a footer with the links Privacy (`nav.privacy`) and Terms (`nav.terms`). At 320 px the header wraps; nothing is hidden.

**Language switcher** (`language.switcher` as its label): a `<select>` of the available locales by `language.name`; a change applies at once (FR-022) and the focus stays on it.

**Theme switcher:** one `<button type="button">` whose text names the theme it switches to: `theme.toLight` "Light theme" in the dark theme, `theme.toDark` "Dark theme" in the light theme. The focus stays on it after a switch *(agent's call: a button that names its result needs no pressed state for a screen reader)*.

**Message** (`app-message`): one component for every message of every page, with `kind` `error` or `info`. An error has `role="alert"`, the `--error` colour and a 1 px `--error` border on `--surface`; an info has `role="status"` and the `--text` colour. A message tied to an input field sits right under it and is linked with `aria-describedby`; the field gets `aria-invalid="true"` and an `--error` border.

**Input field:** a visible `<label>` above the field; no placeholder in place of a label.

**Page title and focus.** Every route sets `document.title` to its page title and `app.name`, from the translations (for example `signIn.title` and `app.name`: "Sign in — Habits"), and again on a language switch. After a navigation between routes the focus moves to the page's `h1` (`tabindex="-1"`), so a screen reader announces the new page. No field takes the focus when a page opens: on a phone the keyboard would cover half the screen; the code step's field takes it because the user's own action opened that step.

**Times.** A moment such as `retryAt` is shown in the browser's time zone and the page's language with `Intl.DateTimeFormat`: the time alone when the moment is today ("Try again after 14:05"), the date and the time otherwise ("Try again after 1 November, 14:05"); it is rounded up to the next minute, so the time shown is never earlier than the moment. A unit test covers today, another day and the rounding, in the three languages.

**Start of the application.** The page renders once the translation file of the current language has loaded, so no key ever shows. A language switch whose file fails to load keeps the current language and shows `error.network`. `index.html` holds a `<noscript>` text in the three languages ("This app needs JavaScript."), since nothing else can show without it, and an SVG icon (`>_` in the dark theme's accent), so the browser does not ask for `/favicon.ico`. No web app manifest.

## Screens and states

### Sign-in, email step (`/sign-in`)

| State | What is shown |
|---|---|
| Initial | `signIn.title`; email field (`signIn.email`, `type="email"`, `autocomplete="email"`); "Remember me" checkbox (`signIn.rememberMe`), unchecked, with `signIn.rememberMeHint` "Stay signed in on this device for up to 30 days without activity. Only on a device you trust." under it in `--muted`, linked by `aria-describedby` (FR-014); button `signIn.requestCode` "Get code"; the acceptance sentence with links (FR-025); the page frame |
| Invalid email (the page's own check, research R18) | `signIn.emailInvalid` under the field; the button stays enabled; the focus returns to the field; no request is sent |
| Sending | the button is disabled and reads `signIn.requesting` "Sending…"; pressing it again sends nothing |
| Sent | `/sign-in/code` opens |
| Refused (`email-limit`, `ip-limit`, `email-service-limit`, `email-service-unavailable`) | `error.<code>` with `{retryAt}` where the response has it; the email step stays, the field keeps its value |
| `invalid-request`, `internal-error`, no response | `error.<code>`, for `internal-error` with `{traceId}`; a request that gets no response shows `error.network` "No connection to the server. Try again." |
| Session ended | arriving after a request answered `not-signed-in` (spec, Edge Cases): info `signIn.sessionEnded` "Your session has ended. Sign in again." above the form; the reason travels in the router's navigation state, never in the URL (research R19, T4) |

### Sign-in, code step (`/sign-in/code`, after a code is sent)

The email, the "Remember me" choice and the remaining count travel from the email step in the application's memory, never in the URL: an email in the address would reach the browser history and the reverse proxy's access log, which may hold only the personal data the Privacy page names (constitution X). The browser's Back returns to `/sign-in` with the email and the checkbox filled in, as "Use another email" does. Opening or reloading `/sign-in/code` without them opens `/sign-in` in its initial state; the code already sent stays valid, but reaching the code step again takes a new code request.

| State | What is shown |
|---|---|
| Initial | `signIn.codeSent` "Code sent to {email}"; `signIn.remaining`; code field (`signIn.code`, `inputmode="numeric"`, `autocomplete="one-time-code"`, 6 digits); button `signIn.signIn` "Sign in" (the code is not sent by itself at the sixth digit); link-styled buttons `signIn.resendCode` "Send a new code" (a new code request for the same email, then this step again with the code field cleared and the new remaining count) and `signIn.otherEmail` "Use another email" (back to `/sign-in` with the email filled in); the focus is in the code field |
| Malformed code (`invalid-code-format`, or not 6 digits by the page's check) | `error.invalid-code-format` under the field; no request when the page's check fails |
| Signing in | the button is disabled and reads `signIn.signingIn` "Signing in…" |
| Wrong code (`code-incorrect`) | `error.code-incorrect` under the field; the field is cleared; the focus returns to it |
| New code needed (`new-code-required`) | back to `/sign-in` with the email filled in and `error.new-code-required` "This code no longer works. Request a new one." |
| Resend refused | as "Refused" of the email step, shown on the code step |
| Signed in | `/` opens |

### Main screen (`/`)

| State | What is shown |
|---|---|
| Loading (`GET /api/me`) | the page frame and an empty `<main>`; after 1 second without an answer, info `main.loading` "Loading…"; no skeleton placeholders |
| Signed in | `main.title`; `main.noHabits` "No habits yet"; buttons `main.signOut` and `main.signOutEverywhere`; when `timeZoneRecognized` is `false`, info `main.timeZoneNotRecognized` |
| Confirming sign out everywhere | in place of `main.signOutEverywhere`: `main.signOutEverywhereConfirm` "Sign out on every device?" and buttons `main.confirm` "Sign out everywhere" and `main.cancel` "Cancel"; the focus moves to "Cancel", so a second stray `Enter` signs nothing out; "Cancel" or `Escape` restores the button and returns the focus to it; no request is sent until "Sign out everywhere" is pressed (FR-015) |
| Signing out | both sign-out buttons are disabled until the answer |
| Session expired or revoked while the page is open (a request after a successful `GET /api/me` answers `not-signed-in`) | `/sign-in` opens in the "Session ended" state |
| Not signed in when the page loads (`GET /api/me` answers `not-signed-in`) | `/sign-in` opens in its initial state: the page cannot tell an ended session from none |
| `internal-error`, no response | `error.internal-error` with `{traceId}`, or `error.network` |

### Privacy (`/privacy`) and Terms (`/terms`)

The page frame; `privacy.title` or `terms.title`; the sections of FR-030 or FR-031 with `h2` headings; the effective date; in Polish and Russian, the sentence that the English version prevails (FR-026). Long words and addresses wrap at 320 px (`overflow-wrap: anywhere`).

### Not found (any other route)

The page frame; `notFound.title` "Page not found"; link `notFound.home` to `/`.

### Any page

Switching the language or the theme changes the page at once, without a reload; the focus stays on the switcher (FR-022, FR-042).

## Widths

Every screen is checked by the end-to-end tests as a phone with touch at 320 px (reflow, FR-041) and 360 px (FR-019), and on a desktop at 1280 px, each with no horizontal scroll and every action reachable; the main path also runs in WebKit, the engine of every browser on an iPhone (research R12). `index.html` carries `<meta name="viewport" content="width=device-width, initial-scale=1">`, with no `maximum-scale` and no `user-scalable=no`, so the page can still be zoomed. On the branch deployment the developer signs in on their own phone (SC-014).

## Accessibility checks (FR-041, SC-015)

| Criterion | Check |
|---|---|
| Keyboard alone | e2e: sign in (both steps), switch the language and the theme, sign out, open Privacy and Terms, with `Tab`, `Shift+Tab`, `Enter` and `Space` only |
| Visible focus | e2e: every element the keyboard reaches has a computed `outline-width` of 3 px when focused |
| Labels | e2e: every input field has an accessible name from its `<label>` (`getByLabel`) |
| Errors announced | e2e: every error message shown in a test has `role="alert"` |
| Contrast | unit test: every pair of the table above, in both themes, by the WCAG relative luminance formula, computed in the test |
| Reflow at 320 px | e2e: `scrollWidth <= clientWidth` and every action reachable, in both themes |
