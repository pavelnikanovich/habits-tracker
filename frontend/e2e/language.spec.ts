import { expect, test } from '@playwright/test';
import { expectNoHorizontalScroll } from './support/layout';
import { collectForeignRequests } from './support/network';
import { heading, switchLanguage } from './support/shell';
import { translation } from './support/translations';

// The page <title> is not translated yet, so each test reads the visible page heading (h1), whose text comes from
// the translation file named in the test.
const en = translation('en');
const pl = translation('pl');
const ru = translation('ru');
const shellRoutes = ['/sign-in', '/privacy', '/terms'];

test.describe('a browser preferring Polish', () => {
  test.use({ locale: 'pl-PL' });

  test("page follows the browser's preferred language", async ({ page }) => {
    await page.goto('/sign-in');

    await expect(page.locator('html')).toHaveAttribute('lang', 'pl');
    await expect(heading(page)).toHaveText(pl.signIn.title); // the h1 text from pl.json
  });

  test('without the list of languages the page is in English', async ({ page }) => {
    await page.route('**/api/locales', (route) => route.abort());

    await page.goto('/sign-in');

    await expect(heading(page)).toHaveText(en.signIn.title);
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
  });
});

test.describe('a browser preferring German', () => {
  test.use({ locale: 'de-DE' });

  test('a browser preferring no available language gets English', async ({ page }) => {
    await page.goto('/sign-in');

    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect(heading(page)).toHaveText(en.signIn.title); // the h1 text from en.json
  });
});

test.describe('a browser preferring English', () => {
  test.use({ locale: 'en-US' });

  test('switching the language changes the page without a reload', async ({ page }) => {
    await page.goto('/sign-in');
    await expect(heading(page)).toHaveText(en.signIn.title);
    await page.evaluate(() => Object.assign(window, { __marker: 'kept' }));

    await switchLanguage(page, 'ru');

    await expect(heading(page)).toHaveText(ru.signIn.title); // the h1 text from ru.json
    await expect(page.locator('html')).toHaveAttribute('lang', 'ru');
    expect(await page.evaluate(() => (window as unknown as { __marker?: string }).__marker)).toBe('kept');
  });

  test('a switch whose file fails to load keeps the language and says so', async ({ page }) => {
    await page.route('**/i18n/ru.json', (route) => route.abort());
    await page.goto('/sign-in');
    await expect(heading(page)).toHaveText(en.signIn.title);

    await switchLanguage(page, 'ru');

    await expect(page.getByRole('alert')).toHaveText(en.error.network);
    await expect(heading(page)).toHaveText(en.signIn.title);
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect(page.getByRole('combobox', { name: en.language.switcher })).toHaveValue('en');
  });

  test('the chosen language is kept for a later visit', async ({ page, context }) => {
    await page.goto('/sign-in');
    await switchLanguage(page, 'ru');
    await expect(heading(page)).toHaveText(ru.signIn.title);

    const later = await context.newPage();
    await later.goto('/sign-in');

    await expect(later.locator('html')).toHaveAttribute('lang', 'ru');
    await expect(heading(later)).toHaveText(ru.signIn.title); // the h1 text from ru.json
  });

  test('shell actions are reachable at every width @width', async ({ page }) => {
    for (const route of shellRoutes) {
      await page.goto(route);
      await expect(heading(page)).toBeVisible();

      await expect(page.getByRole('combobox', { name: en.language.switcher }), route).toBeInViewport();
      await expect(page.locator('footer').getByRole('link', { name: en.nav.privacy }), route).toBeInViewport();
      await expect(page.locator('footer').getByRole('link', { name: en.nav.terms }), route).toBeInViewport();
      await expectNoHorizontalScroll(page);
    }
  });

  test('the language switch works in WebKit @main-path', async ({ page }) => {
    await page.goto('/sign-in');
    await expect(heading(page)).toHaveText(en.signIn.title);

    await switchLanguage(page, 'ru');

    await expect(heading(page)).toHaveText(ru.signIn.title); // the h1 text from ru.json
  });

  test('pages load nothing from other servers', async ({ page }) => {
    const foreign = collectForeignRequests(page);

    for (const route of shellRoutes) {
      await page.goto(route);
      await expect(heading(page)).toBeVisible();
    }

    expect(foreign).toEqual([]);
  });
});
