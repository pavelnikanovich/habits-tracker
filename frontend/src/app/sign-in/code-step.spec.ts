import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { ann, LanguageRecord, settle, setUpSignIn, texts, type } from '../../testing/sign-in-setup';
import { CodeStep } from './code-step';
import { SignInFlow } from './sign-in-flow.service';

const signIn = texts('signIn');
const errors = texts('error');
const endpoint = '/api/code-requests';
const email = 'ann@example.com';
const codeStepUrl = '/sign-in/code';
const codeSelector = 'input[autocomplete="one-time-code"]';
const alertSelector = 'app-message[role="alert"]';
const emailSelector = 'input[type="email"]';
const sessions = '/api/sessions';
const unauthorized = { status: 401, statusText: 'Unauthorized' };

describe('CodeStep', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;
  let language: LanguageRecord;

  beforeEach(async () => {
    ({ harness, http, language } = await setUpSignIn());
  });

  afterEach(() => http.verify());

  async function openCodeStep(rememberMe = false): Promise<HTMLElement> {
    TestBed.inject(SignInFlow).begin(email, rememberMe, { remaining: 4, limit: 5 });
    expect(await harness.navigateByUrl(codeStepUrl, CodeStep)).toBeInstanceOf(CodeStep);
    return harness.routeNativeElement as HTMLElement;
  }

  const buttonNamed = (page: HTMLElement, text: string) =>
    [...page.querySelectorAll('button')].find((button) => button.textContent?.trim() === text)!;

  const field = (page: HTMLElement) => page.querySelector<HTMLInputElement>(codeSelector)!;
  const form = (page: HTMLElement) => page.querySelector('form')!;
  const alert = (page: HTMLElement) => page.querySelector(alertSelector);

  async function submit(page: HTMLElement, entered: string): Promise<void> {
    type(page, codeSelector, entered);
    form(page).dispatchEvent(new Event('submit', { cancelable: true }));
    await settle(harness);
  }

  it('the code field has the focus when the step opens', async () => {
    const page = await openCodeStep();
    await settle(harness);

    expect(document.activeElement).toBe(field(page));
    expect(field(page).getAttribute('inputmode')).toBe('numeric');
    expect(page.querySelector(`label[for="${field(page).id}"]`)?.textContent?.trim()).toBe(signIn['code']);
  });

  it('sends the browser time zone, remember me and the page language', async () => {
    const page = await openCodeStep(true);

    await submit(page, '042917');

    const request = http.expectOne(sessions);
    expect(request.request.body).toEqual({
      email,
      code: '042917',
      rememberMe: true,
      timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone,
      locale: 'pl',
    });
    request.flush(ann);
  });

  it('strips whitespace from the code and refuses one that is not six digits', async () => {
    const page = await openCodeStep();

    await submit(page, ' 042 917\t');
    const request = http.expectOne(sessions);
    expect(request.request.body.code).toBe('042917');
    request.flush(ann);
    await settle(harness);

  });

  it('refuses a code that is not six digits without a request', async () => {
    const page = await openCodeStep();
    await settle(harness);

    for (const entered of ['', '12345', '1234567', '12a456', '12-456']) {
      await submit(page, entered);

      http.expectNone(sessions);
      expect(alert(page)?.textContent?.trim(), entered).toBe(errors['invalid-code-format']);
      expect(field(page).getAttribute('aria-describedby')).toBe(alert(page)?.id);
      expect(field(page).getAttribute('aria-invalid')).toBe('true');
      expect(document.activeElement).toBe(field(page));
    }
  });

  it('is not sent by itself at the sixth digit', async () => {
    const page = await openCodeStep();

    type(page, codeSelector, '123456');
    await settle(harness);

    http.expectNone(sessions);
  });

  it('the button is disabled while signing in', async () => {
    const page = await openCodeStep();
    const button = page.querySelector<HTMLButtonElement>('form button[type="submit"]')!;
    type(page, codeSelector, '042917');
    button.focus();

    form(page).dispatchEvent(new Event('submit', { cancelable: true }));
    await settle(harness);
    form(page).dispatchEvent(new Event('submit', { cancelable: true }));
    button.click();
    await settle(harness);

    // R-69: aria-disabled, never the disabled attribute, so the focus stays on the button.
    expect(button.getAttribute('aria-disabled')).toBe('true');
    expect(button.hasAttribute('disabled')).toBe(false);
    expect(button.textContent?.trim()).toBe(signIn['signingIn']);
    expect(document.activeElement).toBe(button);
    http.expectOne(sessions).flush(ann);
  });

  it('a correct code signs in, forgets the flow, applies the user language and opens /', async () => {
    const page = await openCodeStep();

    await submit(page, '042917');
    http.expectOne(sessions).flush(ann);
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe('/');
    expect(TestBed.inject(SignInFlow).email()).toBeNull();
    expect(language.used).toEqual(['pl']);
  });

  it('code-incorrect clears the field, focuses it and stays on the step', async () => {
    const page = await openCodeStep();
    await submit(page, '000000');

    http.expectOne(sessions).flush({ code: 'code-incorrect' }, unauthorized);
    await settle(harness);

    expect(alert(page)?.textContent?.trim()).toBe(errors['code-incorrect']);
    expect(field(page).value).toBe('');
    expect(document.activeElement).toBe(field(page));
    expect(TestBed.inject(Router).url).toBe(codeStepUrl);
    expect(page.querySelector('form button[type="submit"]')?.getAttribute('aria-disabled')).toBeNull();
  });

  it('a new code clears the field and focuses it', async () => {
    const page = await openCodeStep();
    type(page, codeSelector, '123456');

    buttonNamed(page, signIn['resendCode']).click();
    http.expectOne(endpoint).flush({ remaining: 3, limit: 5 }, { status: 202, statusText: 'Accepted' });
    await settle(harness);

    expect(field(page).value).toBe('');
    expect(document.activeElement).toBe(field(page));
  });

  it('new-code-required returns to /sign-in with the email and its message', async () => {
    const page = await openCodeStep();
    await submit(page, '042917');

    http.expectOne(sessions).flush({ code: 'new-code-required' }, unauthorized);
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe('/sign-in');
    const signInPage = harness.routeNativeElement as HTMLElement;
    expect(signInPage.querySelector<HTMLInputElement>(emailSelector)?.value).toBe(email);
    expect(alert(signInPage)?.textContent?.trim()).toBe(errors['new-code-required']);
  });

  it('shows where the code went and the remaining count', async () => {
    const page = await openCodeStep();

    expect(page.querySelector('h1')?.textContent?.trim()).toBe(signIn['title']);
    expect(page.textContent).toContain('Code sent to ann@example.com');
    expect(page.textContent).toContain('4 of 5 left');
  });

  it('send a new code requests again for the same email and shows the new count', async () => {
    const page = await openCodeStep();

    buttonNamed(page, signIn['resendCode']).click();
    const request = http.expectOne(endpoint);
    expect(request.request.body).toEqual({ email: email, locale: 'pl' });
    await settle(harness);
    expect(buttonNamed(page, signIn['requesting']).disabled).toBe(true);
    request.flush({ remaining: 3, limit: 5 }, { status: 202, statusText: 'Accepted' });
    await settle(harness);

    expect(page.textContent).toContain('3 of 5 left');
    expect(page.querySelector(alertSelector)).toBeNull();
    expect(TestBed.inject(Router).url).toBe(codeStepUrl);
  });

  it('a refused new code shows the refusal on this step', async () => {
    const page = await openCodeStep();

    buttonNamed(page, signIn['resendCode']).click();
    http.expectOne(endpoint).flush({ code: 'email-limit', retryAt: '2030-01-01T00:00:00Z' }, { status: 429, statusText: 'Too Many Requests' });
    await settle(harness);

    expect(page.querySelector(alertSelector)?.textContent).toContain(errors['email-limit']);
    expect(TestBed.inject(Router).url).toBe(codeStepUrl);
    expect(page.textContent).toContain('4 of 5 left');
  });

  it('use another email returns to /sign-in with the email and the checkbox filled', async () => {
    const page = await openCodeStep(true);

    buttonNamed(page, signIn['otherEmail']).click();
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe('/sign-in');
    const signInPage = harness.routeNativeElement as HTMLElement;
    expect(signInPage.querySelector<HTMLInputElement>(emailSelector)?.value).toBe(email);
    expect(signInPage.querySelector<HTMLInputElement>('input[type="checkbox"]')?.checked).toBe(true);
  });

  it('without a requested code /sign-in/code opens /sign-in', async () => {
    await harness.navigateByUrl(codeStepUrl);

    expect(TestBed.inject(Router).url).toBe('/sign-in');
    expect(harness.routeNativeElement?.querySelector(emailSelector)).not.toBeNull();
  });
});
