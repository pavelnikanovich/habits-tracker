import { expect, Page, test } from '@playwright/test';
import { logMark, readCode } from './support/dev-log';
import { uniqueEmail } from './support/emails';
import { expectVisibleFocus, pressTabUntil } from './support/keyboard';
import { expectControlsInViewport, expectNoHorizontalScroll } from './support/layout';
import { forEachTheme, themeSwitcher } from './support/themes';
import { translation } from './support/translations';

const en = translation('en');
// The project's origin: http://localhost:4200, or Caddy's https://localhost:8443 for WebKit (R-66).
const origin = (): string => test.info().project.use.baseURL!;

const emailField = (page: Page) => page.getByLabel(en.signIn.email);
const codeField = (page: Page) => page.getByLabel(en.signIn.code, { exact: true });
const getCode = (page: Page) => page.getByRole('button', { name: en.signIn.requestCode });
const signInButton = (page: Page) => page.getByRole('button', { name: en.signIn.signIn, exact: true });

/** Requests a code for `email` on the email step; returns the log mark taken before the request (R-56). */
async function requestCode(page: Page, email: string, remember = false): Promise<number> {
  const mark = await logMark();
  await emailField(page).fill(email);
  if (remember) await page.getByLabel(en.signIn.rememberMe).check();
  await getCode(page).click();
  await expect(page).toHaveURL('/sign-in/code');
  return mark;
}

/** Signs `email` in with the code from the development log; the page is left on the main screen. */
async function signIn(page: Page, email: string = uniqueEmail(), remember = false): Promise<string> {
  await page.goto('/sign-in');
  const mark = await requestCode(page, email, remember);
  await codeField(page).fill(await readCode(email, mark));
  await signInButton(page).click();
  await expect(page).toHaveURL(`${origin()}/`);
  return email;
}

test('a new user signs in and lands on the main screen @main-path', async ({ page }) => {
  await signIn(page);

  await expect(page.getByText(en.main.noHabits)).toBeVisible();
  // The screen above comes from the sign-in's own answer; a reload asks GET /api/me, so it proves the browser kept the
  // Secure session cookie (WebKit may refuse it over http://localhost, research R12).
  await page.reload();
  await expect(page.getByText(en.main.noHabits)).toBeVisible();
  expect(page.url()).toBe(`${origin()}/`);
});

test.describe('in Warsaw', () => {
  test.use({ timezoneId: 'Europe/Warsaw' });

  test('the browser time zone is stored', async ({ page }) => {
    await signIn(page);

    const me = await (await page.request.get('/api/me')).json();
    expect(me.timeZone).toBe('Europe/Warsaw');
    expect(me.timeZoneRecognized).toBe(true);
  });
});

test('an unknown browser time zone gives UTC and a notice', async ({ page }) => {
  await page.addInitScript(() => {
    const resolved = Intl.DateTimeFormat.prototype.resolvedOptions;
    Intl.DateTimeFormat.prototype.resolvedOptions = function () {
      return { ...resolved.call(this), timeZone: undefined as unknown as string };
    };
  });

  await signIn(page);

  await expect(page.getByText(en.main.timeZoneNotRecognized)).toBeVisible();
  const me = await (await page.request.get('/api/me')).json();
  expect(me.timeZone).toBe('UTC');
  expect(me.timeZoneRecognized).toBe(false);
});

test('sign-in ignores a return address to another domain', async ({ page }) => {
  const email = uniqueEmail();
  await page.goto('/sign-in?returnUrl=https://evil.example/&redirect=//evil.example&next=https%3A%2F%2Fevil.example');
  const mark = await requestCode(page, email);
  await codeField(page).fill(await readCode(email, mark));
  await signInButton(page).click();

  await expect(page).toHaveURL(`${origin()}/`);
  expect(page.url()).toBe(`${origin()}/`);
});

test('a visitor opening / is sent to /sign-in', async ({ page }) => {
  await page.goto('/');

  await expect(page).toHaveURL(`${origin()}/sign-in`);
  expect(page.url()).toBe(`${origin()}/sign-in`);
});

test('a signed-in user opening /sign-in is sent to /', async ({ page }) => {
  await signIn(page);

  await page.goto('/sign-in');

  await expect(page).toHaveURL(`${origin()}/`);
  await expect(page.getByText(en.main.noHabits)).toBeVisible();
});

test('a session without Remember me ends with the browser session', async ({ page, context, browser }) => {
  await signIn(page);

  const cookie = (await context.cookies()).find((candidate) => candidate.name === 'session')!;
  expect(cookie.expires).toBe(-1);
  // A restarted browser keeps the cookies that have an expiry and drops the others.
  const state = await context.storageState();
  const restarted = await browser.newContext({
    storageState: { ...state, cookies: state.cookies.filter((candidate) => candidate.expires !== -1) },
  });
  const reopened = await restarted.newPage();
  await reopened.goto(`${origin()}/`);

  await expect(reopened).toHaveURL(`${origin()}/sign-in`);
  await restarted.close();
});

test('a remembered session survives a browser restart', async ({ page, context, browser }) => {
  await signIn(page, uniqueEmail(), true);

  const cookie = (await context.cookies()).find((candidate) => candidate.name === 'session')!;
  const thirtyDays = Date.now() / 1000 + 30 * 24 * 60 * 60;
  expect(Math.abs(cookie.expires - thirtyDays)).toBeLessThanOrEqual(60);
  const state = await context.storageState();
  const restarted = await browser.newContext({
    storageState: { ...state, cookies: state.cookies.filter((candidate) => candidate.expires !== -1) },
  });
  const reopened = await restarted.newPage();
  await reopened.goto(`${origin()}/`);

  await expect(reopened.getByText(en.main.noHabits)).toBeVisible();
  expect(reopened.url()).toBe(`${origin()}/`);
  await restarted.close();
});

test('sign-in by keyboard alone @main-path', async ({ page }) => {
  const email = uniqueEmail();
  await page.goto('/sign-in');

  await pressTabUntil(page, emailField(page));
  await expectVisibleFocus(page);
  await page.keyboard.type(email);
  await pressTabUntil(page, getCode(page));
  await expectVisibleFocus(page);
  const mark = await logMark();
  await page.keyboard.press('Enter');

  await expect(page).toHaveURL('/sign-in/code');
  await expect(codeField(page)).toBeFocused();
  await expectVisibleFocus(page);
  await page.keyboard.type(await readCode(email, mark));
  await pressTabUntil(page, signInButton(page));
  await expectVisibleFocus(page);
  await page.keyboard.press('Enter');

  await expect(page).toHaveURL(`${origin()}/`);
  await expect(page.getByText(en.main.noHabits)).toBeVisible();
});

test('the chosen theme stays after sign-in', async ({ page }) => {
  await page.goto('/sign-in');
  await themeSwitcher(page).click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');

  await signIn(page);

  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
});

test('the code field offers the code from the email', async ({ page }) => {
  await page.goto('/sign-in');
  await requestCode(page, uniqueEmail());

  await expect(codeField(page)).toHaveAttribute('autocomplete', 'one-time-code');
  await expect(codeField(page)).toHaveAttribute('inputmode', 'numeric');
});

test('sign-in and main screen at every width in both themes @width', async ({ page }) => {
  await page.goto('/sign-in');
  await forEachTheme(page, async () => {
    await page.goto('/sign-in');
    await expect(emailField(page)).toBeVisible();
    await expectControlsInViewport(page);
    await expectNoHorizontalScroll(page);

    await requestCode(page, uniqueEmail());
    await expectControlsInViewport(page);
    await expectNoHorizontalScroll(page);
  });

  // The chosen theme is light now; the main screen is checked in light, then in dark.
  await signIn(page);
  for (const theme of ['light', 'dark']) {
    if (theme === 'dark') await themeSwitcher(page).click();
    await expect(page.locator('html')).toHaveAttribute('data-theme', theme);
    await expectControlsInViewport(page);
    await expectNoHorizontalScroll(page);
  }
});
