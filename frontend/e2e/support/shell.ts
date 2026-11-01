import { Locator, Page } from '@playwright/test';
import { translation } from './translations';

const en = translation('en');

/** The page's `h1`. */
export function heading(page: Page): Locator {
  return page.getByRole('heading', { level: 1 });
}

/** Chooses `code` in the language switcher of the page frame, found by its English label. */
export async function switchLanguage(page: Page, code: string): Promise<void> {
  await page.getByRole('combobox', { name: en.language.switcher }).selectOption(code);
}
