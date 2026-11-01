import { expect, test } from '@playwright/test';
import { expectControlsInViewport, expectNoHorizontalScroll } from './support/layout';
import { uniqueEmail } from './support/emails';
import { logMark, readCodeEmail } from './support/dev-log';
import { expectVisibleFocus, pressTabUntil } from './support/keyboard';
import { heading, switchLanguage } from './support/shell';
import { emailField, getCode, rememberMe, requestCode } from './support/sign-in';
import { forEachTheme } from './support/themes';
import { translation } from './support/translations';

const en = translation('en');
const ru = translation('ru');

test('requesting a code opens the code step with the remaining count @main-path', async ({ page }) => {
  const email = uniqueEmail();
  await page.goto('/sign-in');

  await requestCode(page, email);

  await expect(page.getByText(`Code sent to ${email}`)).toBeVisible();
  await expect(page.getByText('4 of 5 left')).toBeVisible();
  const resend = page.getByRole('button', { name: en.signIn.resendCode });
  await resend.click();
  await expect(page.getByText('3 of 5 left')).toBeVisible();
  await expect(page.getByRole('button', { name: en.signIn.otherEmail })).toBeVisible();
});

test('the email never appears in the URL', async ({ page }) => {
  const email = uniqueEmail();
  const urls: string[] = [];
  page.on('framenavigated', (frame) => {
    if (frame === page.mainFrame()) urls.push(frame.url());
  });
  await page.goto('/sign-in');
  await rememberMe(page).check();

  await requestCode(page, email);
  await page.getByRole('button', { name: en.signIn.resendCode }).click();
  await expect(page.getByText('3 of 5 left')).toBeVisible();
  await page.getByRole('button', { name: en.signIn.otherEmail }).click();
  await expect(page).toHaveURL('/sign-in');
  await requestCode(page, email);
  await page.goBack();
  await expect(page).toHaveURL('/sign-in');

  urls.push(page.url());
  expect(urls.length).toBeGreaterThan(3);
  for (const url of urls) {
    expect(url, 'the email is in no URL').not.toContain('@');
    expect(url.toLowerCase(), 'the email is in no URL').not.toContain('%40');
    expect(url, 'the email is in no URL').not.toContain(email);
  }
});

test('Back from the code step returns to the email step with the email filled in', async ({ page }) => {
  const email = uniqueEmail();
  await page.goto('/sign-in');
  await rememberMe(page).check();
  await requestCode(page, email);

  await page.goBack();

  await expect(page).toHaveURL('/sign-in');
  await expect(emailField(page)).toHaveValue(email);
  await expect(rememberMe(page)).toBeChecked();
});

test('reloading the code step opens the email step', async ({ page }) => {
  await page.goto('/sign-in');
  await requestCode(page, uniqueEmail());

  await page.reload();

  await expect(page).toHaveURL('/sign-in');
  await expect(emailField(page)).toHaveValue('');
  await expect(rememberMe(page)).not.toBeChecked();
});

test('the code email is in the page language', async ({ page }) => {
  const email = uniqueEmail();
  await page.goto('/sign-in');
  await switchLanguage(page, 'ru');
  await expect(heading(page)).toHaveText(ru.signIn.title);
  const mark = await logMark();

  await page.getByLabel(ru.signIn.email).fill(email);
  await page.getByRole('button', { name: ru.signIn.requestCode }).click();
  await expect(page).toHaveURL('/sign-in/code');

  expect((await readCodeEmail(email, mark)).locale).toBe('ru');
  await expect(page.getByText('Осталось 4 запроса кода из 5')).toBeVisible();
});

test('the email step by keyboard alone', async ({ page }) => {
  await page.goto('/sign-in');

  await pressTabUntil(page, emailField(page));
  await expectVisibleFocus(page);
  await page.keyboard.type('ann');
  await pressTabUntil(page, rememberMe(page));
  await expectVisibleFocus(page);
  await page.keyboard.press('Space');
  await expect(rememberMe(page)).toBeChecked();
  await pressTabUntil(page, getCode(page));
  await expectVisibleFocus(page);
  await page.keyboard.press('Enter');

  const message = page.getByRole('alert').filter({ hasText: en.signIn.emailInvalid });
  await expect(message).toBeVisible();
  await expect(emailField(page)).toBeFocused();
  await expectVisibleFocus(page);

  await page.keyboard.press('Control+A');
  await page.keyboard.type(uniqueEmail());
  await pressTabUntil(page, getCode(page));
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL('/sign-in/code');
});

test('the email step and the code step at every width in both themes @width', async ({ page }) => {
  await page.goto('/sign-in');

  await forEachTheme(page, async () => {
    await page.goto('/sign-in');
    await expectNoHorizontalScroll(page);
    await expectControlsInViewport(page);

    await requestCode(page, uniqueEmail());
    await expectNoHorizontalScroll(page);
    await expectControlsInViewport(page);
  });
});
