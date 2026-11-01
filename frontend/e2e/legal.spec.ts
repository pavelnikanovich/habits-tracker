import { expect, Page, test } from '@playwright/test';
import { collectForeignRequests } from './support/network';
import { expectVisibleFocus, pressTabUntil } from './support/keyboard';
import { expectControlsInViewport, expectNoHorizontalScroll } from './support/layout';
import { heading, languageSwitcher } from './support/shell';
import { signIn } from './support/sign-in';
import { forEachTheme, themeSwitcher } from './support/themes';
import { translation } from './support/translations';

// The Privacy and Terms pages (FR-024, FR-025, FR-027; contracts/ui.md, "Privacy and Terms").
const en = translation('en');
const sentence = 'By signing in you accept the Terms and acknowledge the Privacy notice';
const pages = [
  { url: '/privacy', title: en.privacy.title, link: en.nav.privacy },
  { url: '/terms', title: en.terms.title, link: en.nav.terms },
];
const SECTIONS = { '/privacy': 15, '/terms': 12 } as Record<string, number>;

test.use({ locale: 'en-US' });

const footerLink = (page: Page, name: string) => page.locator('footer').getByRole('link', { name });
const acceptance = (page: Page) => page.locator('p.acceptance');

test('a visitor opens both pages from the sign-in page', async ({ page }) => {
  for (const { url, title, link } of pages) {
    await page.goto('/sign-in');
    await acceptance(page).locator(`a[href="${url}"]`).click();
    await expect(page).toHaveURL(url);
    await expect(heading(page)).toHaveText(title);
    await expect(page.getByRole('heading', { level: 2 })).toHaveCount(SECTIONS[url]);

    await page.goto('/sign-in');
    await footerLink(page, link).click();
    await expect(page).toHaveURL(url);
  }
});

test('a signed-in user opens both pages from the main screen', async ({ page }) => {
  await signIn(page);

  for (const { url, title, link } of pages) {
    await footerLink(page, link).click();
    await expect(page).toHaveURL(url);
    await expect(heading(page)).toHaveText(title);
    await page.goto('/');
    await expect(heading(page)).toHaveText(en.main.title);
  }
});

test('the sign-in page states the acceptance sentence with both links and has no acceptance checkbox', async ({ page }) => {
  await page.goto('/sign-in');

  await expect(acceptance(page)).toHaveText(`${sentence}.`);
  await expect(acceptance(page).getByRole('link', { name: 'Terms' })).toHaveAttribute('href', '/terms');
  await expect(acceptance(page).getByRole('link', { name: 'Privacy notice' })).toHaveAttribute('href', '/privacy');
  const checkboxes = page.getByRole('checkbox');
  await expect(checkboxes).toHaveCount(1);
  await expect(checkboxes).toHaveAccessibleName(en.signIn.rememberMe);
});

test('both pages at every width in both themes @width', async ({ page }) => {
  await page.goto('/privacy');
  await forEachTheme(page, async () => {
    for (const { url } of pages) {
      await page.goto(url);
      await expect(heading(page)).toBeVisible();
      await expectNoHorizontalScroll(page);
      await expectControlsInViewport(page);
      await expect(languageSwitcher(page)).toBeInViewport();
      await expect(themeSwitcher(page)).toBeInViewport();
      // The pages are longer than the viewport: the footer is reached by scrolling down to it.
      await page.locator('footer').scrollIntoViewIfNeeded();
      await expect(footerLink(page, en.nav.privacy)).toBeInViewport();
      await expect(footerLink(page, en.nav.terms)).toBeInViewport();
    }
  });
});

test('both pages by keyboard alone', async ({ page }) => {
  for (const { url, title, link } of pages) {
    await page.goto('/sign-in');
    const target = acceptance(page).locator(`a[href="${url}"]`);
    await pressTabUntil(page, target);
    await expectVisibleFocus(page);
    await page.keyboard.press('Enter');

    await expect(page).toHaveURL(url);
    await expect(heading(page)).toHaveText(title);
    await expect(heading(page)).toBeFocused();
    expect(link).toBeTruthy();
  }
});

test('both pages load nothing from other servers', async ({ page }) => {
  const foreign = collectForeignRequests(page);

  for (const { url } of pages) {
    await page.goto(url);
    await expect(heading(page)).toBeVisible();
    await page.waitForLoadState('networkidle');
  }

  expect(foreign).toEqual([]);
});
