import { expect, Page, test } from '@playwright/test';
import { themeSwitcher } from './support/themes';
import { translation } from './support/translations';

// The two themes (FR-042, contracts/ui.md, "Themes"): dark until the person chooses, whatever the browser prefers.
const en = translation('en');
const dark = { attribute: 'dark', background: 'rgb(10, 15, 11)', themeColour: '#0a0f0b' };
const light = { attribute: 'light', background: 'rgb(243, 247, 242)', themeColour: '#f3f7f2' };

test.use({ locale: 'en-US' });

async function open(page: Page): Promise<void> {
  await page.goto('/sign-in');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(en.signIn.title);
}

async function expectTheme(page: Page, theme: typeof dark): Promise<void> {
  await expect(page.locator('html')).toHaveAttribute('data-theme', theme.attribute);
  await expect(page.locator('body')).toHaveCSS('background-color', theme.background);
  await expect(page.locator('meta[name="theme-color"]')).toHaveAttribute('content', theme.themeColour);
}

test.describe('a browser preferring a light colour scheme', () => {
  test.use({ colorScheme: 'light' });

  test('a browser that never chose gets the dark theme @main-path', async ({ page }) => {
    await open(page);

    await expectTheme(page, dark);
  });
});

test('switching the theme changes the page without a reload @main-path', async ({ page }) => {
  await open(page);
  await page.evaluate(() => Object.assign(window, { __marker: 'kept' }));

  // By keyboard: a click does not focus a button in WebKit, and the focus must stay on the switcher.
  await page.getByRole('button', { name: en.theme.toLight }).press('Enter');

  await expectTheme(page, light);
  await expect(page.getByRole('button', { name: en.theme.toDark })).toBeFocused();
  expect(await page.evaluate(() => (window as unknown as { __marker?: string }).__marker)).toBe('kept');
});

test('the chosen theme is kept for a later visit', async ({ page, context }) => {
  await open(page);
  await themeSwitcher(page).click();
  await expectTheme(page, light);

  const later = await context.newPage();
  await open(later);

  await expectTheme(later, light);
});

test('a browser that keeps nothing still switches the page', async ({ page }) => {
  await page.addInitScript(() => {
    Storage.prototype.setItem = () => {
      throw new DOMException('Site data is blocked', 'SecurityError');
    };
  });
  await open(page);

  await themeSwitcher(page).click();
  await expectTheme(page, light);

  await page.reload();
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(en.signIn.title);
  await expectTheme(page, dark);
});
