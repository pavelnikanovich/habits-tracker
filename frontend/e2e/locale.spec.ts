import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';
import { newBrowser } from './support/contexts';
import { heading, switchLanguage } from './support/shell';
import { projectOrigin, signIn } from './support/sign-in';
import { translation } from './support/translations';

// The signed-in user's locale (US-1.8, FR-023): kept on the server, shown at the next load of any browser.
const ru = translation('ru');

test.use({ locale: 'en-US' });

test("switching the language changes the user's locale and another browser shows it at its next load", async ({ page, browser }) => {
  const email = await signIn(page);
  const other = await newBrowser(browser);
  const otherPage = await other.newPage();
  await signIn(otherPage, email);

  await switchLanguage(page, 'ru');

  await expect(heading(page)).toHaveText(ru.main.title);
  expect((await (await page.request.get('/api/me')).json()).locale).toBe('ru');
  await otherPage.reload();
  await expect(otherPage.locator('html')).toHaveAttribute('lang', 'ru');
  await expect(heading(otherPage)).toHaveText(ru.main.title);
  await other.close();
});

test("after sign-in the user's locale wins over the browser's choice", async ({ browser }) => {
  const email = `e2e-${randomUUID()}@example.com`;
  const russian = await newBrowser(browser, { locale: 'ru-RU' });
  const russianPage = await russian.newPage();
  await signIn(russianPage, email, false, projectOrigin(), 'ru'); // signed up in ru: the page's language is stored with the user
  await russian.close();

  const polish = await newBrowser(browser, { locale: 'pl-PL' });
  const polishPage = await polish.newPage();
  await polishPage.goto('/sign-in');
  await expect(polishPage.locator('html')).toHaveAttribute('lang', 'pl');
  await signIn(polishPage, email, false, projectOrigin(), 'pl');

  await expect(polishPage.locator('html')).toHaveAttribute('lang', 'ru');
  await expect(heading(polishPage)).toHaveText(ru.main.title);
  await polish.close();
});
