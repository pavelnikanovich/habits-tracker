import { expect, Locator, Page } from '@playwright/test';

/** The theme switcher of the page frame, whatever the page's language and theme. */
export function themeSwitcher(page: Page): Locator {
  return page.locator('app-theme-switcher button');
}

/**
 * Runs `check` in the dark theme, which a browser that never chose gets, then switches with the theme switcher and
 * runs it again in the light theme. Call it on a loaded page; `check` may navigate, the choice is kept.
 */
export async function forEachTheme(page: Page, check: () => Promise<void>): Promise<void> {
  const html = page.locator('html');
  await expect(html).toHaveAttribute('data-theme', 'dark');
  await check();

  await themeSwitcher(page).click();
  await expect(html).toHaveAttribute('data-theme', 'light');
  await check();
}
