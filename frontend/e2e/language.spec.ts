import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { expect, Page, test } from '@playwright/test';
import { expectNoHorizontalScroll } from './support/layout';
import { collectForeignRequests } from './support/network';

// The page <title> is not translated yet, so each test reads the visible page heading (h1), whose text comes from
// the translation file named in the test.
interface Translation {
  language: { switcher: string };
  nav: { privacy: string; terms: string };
  signIn: { title: string };
}

function translation(code: string): Translation {
  return JSON.parse(readFileSync(join(__dirname, '..', 'public', 'i18n', `${code}.json`), 'utf8')) as Translation;
}

const en = translation('en');
const pl = translation('pl');
const ru = translation('ru');
const shellRoutes = ['/sign-in', '/privacy', '/terms'];

function heading(page: Page) {
  return page.getByRole('heading', { level: 1 });
}

async function switchLanguage(page: Page, code: string): Promise<void> {
  await page.getByRole('combobox', { name: en.language.switcher }).selectOption(code);
}

test.describe('a browser preferring Polish', () => {
  test.use({ locale: 'pl-PL' });

  test("page follows the browser's preferred language", async ({ page }) => {
    await page.goto('/sign-in');

    await expect(page.locator('html')).toHaveAttribute('lang', 'pl');
    await expect(heading(page)).toHaveText(pl.signIn.title); // the h1 text from pl.json
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
      await expect(page.getByRole('link', { name: en.nav.privacy }), route).toBeInViewport();
      await expect(page.getByRole('link', { name: en.nav.terms }), route).toBeInViewport();
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
