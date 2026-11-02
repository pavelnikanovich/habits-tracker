import { Locator, Page } from '@playwright/test';
/** The page's `h1`. */
export function heading(page: Page): Locator {
  return page.getByRole('heading', { level: 1 });
}

/** The language switcher of the page frame, whatever language the page is in. */
export function languageSwitcher(page: Page): Locator {
  return page.locator('app-language-switcher select');
}

/** Chooses `code` in the language switcher of the page frame. */
export async function switchLanguage(page: Page, code: string): Promise<void> {
  await languageSwitcher(page).selectOption(code);
}
