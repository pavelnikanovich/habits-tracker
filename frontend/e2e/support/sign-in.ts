import { expect, Locator, Page, test } from '@playwright/test';
import { logMark, readCode } from './dev-log';
import { uniqueEmail } from './emails';
import { translation } from './translations';

/** The project's origin: http://localhost:4200, or Caddy's https://localhost:8443 for WebKit (R-66). */
export const projectOrigin = (): string => test.info().project.use.baseURL!;

// The sign-in pages' controls, named in the page's language (English unless a test says otherwise).
export const emailField = (page: Page, language = 'en'): Locator => page.getByLabel(translation(language).signIn.email);
export const rememberMe = (page: Page, language = 'en'): Locator => page.getByLabel(translation(language).signIn.rememberMe);
export const getCode = (page: Page, language = 'en'): Locator =>
  page.getByRole('button', { name: translation(language).signIn.requestCode });
export const codeField = (page: Page, language = 'en'): Locator =>
  page.getByLabel(translation(language).signIn.code, { exact: true });
export const signInButton = (page: Page, language = 'en'): Locator =>
  page.getByRole('button', { name: translation(language).signIn.signIn, exact: true });

/** Requests a code for `email` on the email step; returns the log mark taken before the request (R-56). */
export async function requestCode(page: Page, email: string, remember = false, language = 'en'): Promise<number> {
  const mark = await logMark();
  await emailField(page, language).fill(email);
  if (remember) await rememberMe(page, language).check();
  await getCode(page, language).click();
  await expect(page).toHaveURL('/sign-in/code');
  return mark;
}

/**
 * Signs `email` in with the code from the development log; the page, in `language`, is left on the main screen at
 * `origin`.
 */
export async function signIn(
  page: Page,
  email: string = uniqueEmail(),
  remember = false,
  origin: string = projectOrigin(),
  language = 'en',
): Promise<string> {
  await page.goto('/sign-in');
  const mark = await requestCode(page, email, remember, language);
  await codeField(page, language).fill(await readCode(email, mark));
  await signInButton(page, language).click();
  await expect(page).toHaveURL(`${origin}/`);
  return email;
}
