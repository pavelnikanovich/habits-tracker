import { expect, Page, test } from '@playwright/test';
import { expectVisibleFocus, pressTabUntil } from './support/keyboard';
import { expectNoHorizontalScroll } from './support/layout';
import { forEachTheme } from './support/themes';
import { Translation, translation } from './support/translations';

// The interface baseline (contracts/ui.md, "Visual baseline", "Shared components", "Widths"; FR-027, FR-041).
const en = translation('en');
const ru = translation('ru');
const shellRoutes = ['/sign-in', '/privacy', '/terms'];

test.use({ locale: 'en-US' });

function heading(page: Page) {
  return page.getByRole('heading', { level: 1 });
}

/** The controls of the page frame, in the order the keyboard reaches them, named in English. */
function shellControls(page: Page) {
  return [
    page.getByRole('combobox', { name: en.language.switcher }),
    page.getByRole('button', { name: en.theme.toLight }),
    page.getByRole('link', { name: en.nav.privacy }),
    page.getByRole('link', { name: en.nav.terms }),
  ];
}

test('a phone gets the page at its own width', async ({ page }) => {
  await page.goto('/sign-in');
  await expect(heading(page)).toBeVisible();

  // Without <meta name="viewport"> a phone browser lays the page out 980 px wide and shrinks it.
  expect(await page.evaluate(() => window.innerWidth)).toBe(page.viewportSize()?.width);
});

test('the font is applied', async ({ page }) => {
  await page.goto('/sign-in');
  await expect(heading(page)).toBeVisible();

  const font = await page.evaluate(async () => {
    await document.fonts.ready;
    return {
      check: document.fonts.check('16px "Nordwand Mono"'),
      loaded: [...document.fonts]
        .filter((face) => face.status === 'loaded')
        .map((face) => `${face.family.replace(/["']/g, '')} ${face.weight}`),
    };
  });
  expect(font.check).toBe(true);
  expect(font.loaded).toContain('Nordwand Mono 400');

  const license = await page.request.get('/fonts/LICENSE-FONT.txt');
  expect(license.status()).toBe(200);
  expect(await license.text()).toContain('SIL OPEN FONT LICENSE');
});

test('the keyboard reaches every shell control with a visible focus', async ({ page }) => {
  for (const route of shellRoutes) {
    await page.goto(route);
    await expect(heading(page)).toBeVisible();

    for (const control of shellControls(page)) {
      await pressTabUntil(page, control);
      await expectVisibleFocus(page);
    }
  }
});

test('the focus moves to the heading after a navigation', async ({ page }) => {
  await page.goto('/sign-in');
  await expect(heading(page)).toHaveText(en.signIn.title);

  await pressTabUntil(page, page.getByRole('link', { name: en.nav.privacy }));
  await page.keyboard.press('Enter');

  await expect(page).toHaveURL(/\/privacy$/);
  await expect(heading(page)).toHaveText(en.privacy.title);
  await expect(heading(page)).toBeFocused();
});

test('every page has its title in the page language', async ({ page }) => {
  const titles = (texts: Translation): [string, string][] => [
    ['/sign-in', texts.signIn.title],
    ['/privacy', texts.privacy.title],
    ['/terms', texts.terms.title],
    ['/no-such-page', texts.notFound.title],
  ];

  for (const [route, title] of titles(en)) {
    await page.goto(route);
    await expect(page, route).toHaveTitle(`${title} — ${en.app.name}`);
  }

  await page.getByRole('combobox', { name: en.language.switcher }).selectOption('ru');
  for (const [route, title] of titles(ru)) {
    await page.goto(route);
    await expect(page, route).toHaveTitle(`${title} — ${ru.app.name}`);
  }
});

test('shell controls are at least 44 px high', async ({ page }) => {
  await page.goto('/sign-in');
  await expect(heading(page)).toBeVisible();

  const [language, theme] = shellControls(page);
  for (const control of [language, theme]) {
    const box = await control.boundingBox();
    expect(box?.height, `${control}`).toBeGreaterThanOrEqual(44);
  }
});

test('the shell at every width in both themes @width', async ({ page }) => {
  await page.goto('/sign-in');
  await expect(heading(page)).toBeVisible();

  await forEachTheme(page, async () => {
    for (const route of shellRoutes) {
      await page.goto(route);
      await expect(heading(page)).toBeVisible();

      await expectNoHorizontalScroll(page);
      await expect(page.getByRole('combobox', { name: en.language.switcher }), route).toBeInViewport();
      await expect(page.locator('app-theme-switcher button'), route).toBeInViewport();
      await expect(page.getByRole('link', { name: en.nav.privacy }), route).toBeInViewport();
      await expect(page.getByRole('link', { name: en.nav.terms }), route).toBeInViewport();
    }
  });
});

test.describe('a browser without JavaScript', () => {
  test.use({ javaScriptEnabled: false });

  test('the page explains itself without JavaScript', async ({ page }) => {
    const paths: string[] = [];
    page.on('request', (request) => paths.push(new URL(request.url()).pathname));
    const texts = [
      ['en', 'This app needs JavaScript.'],
      ['pl', 'Ta aplikacja wymaga JavaScriptu.'],
      ['ru', 'Для работы приложения нужен JavaScript.'],
    ];

    const response = await page.goto('/sign-in');

    const html = (await response?.text()) ?? '';
    const noscript = /<noscript>([\s\S]*?)<\/noscript>/.exec(html)?.[1] ?? '';
    for (const [code, text] of texts) {
      expect(noscript, 'the <noscript> of index.html').toContain(text);
      // Playwright's text matching skips <noscript>, so each text is found by its language.
      const shown = page.locator(`noscript p[lang="${code}"]`);
      await expect(shown).toBeVisible();
      await expect(shown).toHaveText(text);
    }
    expect(html).toContain('<link rel="icon" href="/icon.svg" type="image/svg+xml">');
    const icon = await page.request.get('/icon.svg');
    expect(icon.status()).toBe(200);
    expect(icon.headers()['content-type']).toContain('image/svg+xml');
    expect(paths).not.toContain('/favicon.ico');
  });
});
