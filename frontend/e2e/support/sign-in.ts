import { expect, Locator, Page, test } from '@playwright/test';
import { logMark, readCode } from './dev-log';
import { uniqueEmail } from './emails';
import { translation } from './translations';

const en = translation('en');

/** The project's origin: http://localhost:4200, or Caddy's https://localhost:8443 for WebKit (R-66). */
export const projectOrigin = (): string => test.info().project.use.baseURL!;

// The sign-in pages' controls, named in English.
export const emailField = (page: Page): Locator => page.getByLabel(en.signIn.email);
export const rememberMe = (page: Page): Locator => page.getByLabel(en.signIn.rememberMe);
export const getCode = (page: Page): Locator => page.getByRole('button', { name: en.signIn.requestCode });
export const codeField = (page: Page): Locator => page.getByLabel(en.signIn.code, { exact: true });
export const signInButton = (page: Page): Locator => page.getByRole('button', { name: en.signIn.signIn, exact: true });

/** Requests a code for `email` on the email step; returns the log mark taken before the request (R-56). */
export async function requestCode(page: Page, email: string, remember = false): Promise<number> {
  const mark = await logMark();
  await emailField(page).fill(email);
  if (remember) await rememberMe(page).check();
  await getCode(page).click();
  await expect(page).toHaveURL('/sign-in/code');
  return mark;
}

/** Signs `email` in with the code from the development log; the page is left on the main screen at `origin`. */
export async function signIn(
  page: Page,
  email: string = uniqueEmail(),
  remember = false,
  origin: string = projectOrigin(),
): Promise<string> {
  await page.goto('/sign-in');
  const mark = await requestCode(page, email, remember);
  await codeField(page).fill(await readCode(email, mark));
  await signInButton(page).click();
  await expect(page).toHaveURL(`${origin}/`);
  return email;
}
