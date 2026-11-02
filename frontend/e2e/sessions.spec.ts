import { expect, Page, test } from '@playwright/test';
import { newBrowser } from './support/contexts';
import { uniqueEmail } from './support/emails';
import { expectVisibleFocus, pressTabUntil } from './support/keyboard';
import { expectControlsInViewport, expectNoHorizontalScroll } from './support/layout';
import { heading, languageSwitcher, switchLanguage } from './support/shell';
import { projectOrigin as origin, signIn } from './support/sign-in';
import { forEachTheme } from './support/themes';
import { translation } from './support/translations';

// Sign out and sign out everywhere (US-1.4, US-1.5, FR-015; contracts/ui.md, "Main screen").
const en = translation('en');

test.use({ locale: 'en-US' });

const signOut = (page: Page) => page.getByRole('button', { name: en.main.signOut, exact: true });
// The button and the confirmation's button read alike; only one of them is on the page at a time.
const signOutEverywhere = (page: Page) =>
  page.getByRole('button', { name: en.main.signOutEverywhere, exact: true });
const cancel = (page: Page) => page.getByRole('button', { name: en.main.cancel, exact: true });

test("sign out ends only this browser's session @main-path", async ({ page, browser }) => {
  const email = await signIn(page);
  const other = await newBrowser(browser);
  const otherPage = await other.newPage();
  await signIn(otherPage, email);

  await signOut(page).click();

  await expect(page).toHaveURL(`${origin()}/sign-in`);
  await page.goto('/');
  await expect(page).toHaveURL(`${origin()}/sign-in`);
  await otherPage.reload();
  await expect(otherPage.getByText(en.main.noHabits)).toBeVisible();
  expect(otherPage.url()).toBe(`${origin()}/`);
  await other.close();
});

test('sign out everywhere ends every session', async ({ page, browser }) => {
  const email = await signIn(page);
  const other = await newBrowser(browser);
  const otherPage = await other.newPage();
  await signIn(otherPage, email, true);
  const stored = await other.storageState();

  await signOutEverywhere(page).click();
  await expect(page.getByText(en.main.signOutEverywhereConfirm)).toBeVisible();
  await page.getByRole('button', { name: en.main.confirm, exact: true }).click();

  await expect(page).toHaveURL(`${origin()}/sign-in`);
  await otherPage.goto('/');
  await expect(otherPage).toHaveURL(`${origin()}/sign-in`);
  // A browser restored from the other session's storage is signed out too.
  const restored = await newBrowser(browser, { storageState: stored });
  const restoredPage = await restored.newPage();
  await restoredPage.goto('/');
  await expect(restoredPage).toHaveURL(`${origin()}/sign-in`);
  await restored.close();
  await other.close();
});

test('a session ended while the page is open says so', async ({ page, browser }) => {
  const email = await signIn(page);
  const other = await newBrowser(browser);
  const otherPage = await other.newPage();
  await signIn(otherPage, email);
  await signOutEverywhere(otherPage).click();
  await otherPage.getByRole('button', { name: en.main.confirm, exact: true }).click();
  await expect(otherPage).toHaveURL(`${origin()}/sign-in`);

  // This page still shows the main screen; its next request is refused.
  await expect(page.getByText(en.main.noHabits)).toBeVisible();
  await switchLanguage(page, 'ru');

  await expect(page).toHaveURL(`${origin()}/sign-in`);
  expect(page.url()).toBe(`${origin()}/sign-in`);
  await expect(page.getByText(en.signIn.sessionEnded)).toBeVisible();
  await other.close();
});

test('main screen actions by keyboard alone @main-path', async ({ page }) => {
  await signIn(page, uniqueEmail());

  await pressTabUntil(page, signOutEverywhere(page));
  await expectVisibleFocus(page);
  await page.keyboard.press('Enter');
  await expect(page.getByText(en.main.signOutEverywhereConfirm)).toBeVisible();
  await expect(cancel(page)).toBeFocused();
  await expectVisibleFocus(page);

  await page.keyboard.press('Escape');
  await expect(page.getByText(en.main.signOutEverywhereConfirm)).toBeHidden();
  await expect(signOutEverywhere(page)).toBeFocused();
  await expectVisibleFocus(page);

  await page.keyboard.press('Enter');
  await expect(cancel(page)).toBeFocused();
  await page.keyboard.press('Shift+Tab');
  await expect(page.getByRole('button', { name: en.main.confirm, exact: true })).toBeFocused();
  await expectVisibleFocus(page);
  await page.keyboard.press('Enter');

  await expect(page).toHaveURL(`${origin()}/sign-in`);
});

// SC-015: plain sign out by keyboard alone, as sign out everywhere above.
test('sign out by keyboard alone', async ({ page }) => {
  await signIn(page, uniqueEmail());

  await pressTabUntil(page, signOut(page));
  await expectVisibleFocus(page);
  await page.keyboard.press('Enter');

  await expect(page).toHaveURL(`${origin()}/sign-in`);
  await expect(heading(page)).toHaveText(en.signIn.title);
});

test('main screen actions at every width in both themes @width', async ({ page }) => {
  await signIn(page);
  await expect(signOut(page)).toBeVisible();

  await forEachTheme(page, async () => {
    for (const confirming of [false, true]) {
      if (confirming) await signOutEverywhere(page).click();
      await expect(signOut(page)).toBeInViewport();
      await expect(confirming ? cancel(page) : signOutEverywhere(page)).toBeInViewport();
      await expect(languageSwitcher(page)).toBeInViewport();
      await expect(page.locator('app-theme-switcher button')).toBeInViewport();
      await expect(page.locator('footer').getByRole('link', { name: en.nav.privacy })).toBeInViewport();
      await expect(page.locator('footer').getByRole('link', { name: en.nav.terms })).toBeInViewport();
      await expectNoHorizontalScroll(page);
      await expectControlsInViewport(page);
      if (confirming) await page.keyboard.press('Escape');
    }
  });
});
