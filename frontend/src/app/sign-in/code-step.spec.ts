import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { settle, setUpSignIn, texts } from '../../testing/sign-in-setup';
import { CodeStep } from './code-step';
import { SignInFlow } from './sign-in-flow.service';

const signIn = texts('signIn');
const errors = texts('error');
const endpoint = '/api/code-requests';
const email = 'ann@example.com';
const codeStepUrl = '/sign-in/code';

describe('CodeStep', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;

  beforeEach(async () => {
    ({ harness, http } = await setUpSignIn());
  });

  afterEach(() => http.verify());

  async function openCodeStep(rememberMe = false): Promise<HTMLElement> {
    TestBed.inject(SignInFlow).begin(email, rememberMe, { remaining: 4, limit: 5 });
    expect(await harness.navigateByUrl(codeStepUrl, CodeStep)).toBeInstanceOf(CodeStep);
    return harness.routeNativeElement as HTMLElement;
  }

  const buttonNamed = (page: HTMLElement, text: string) =>
    [...page.querySelectorAll('button')].find((button) => button.textContent?.trim() === text)!;

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
    expect(page.querySelector('app-message[role="alert"]')).toBeNull();
    expect(TestBed.inject(Router).url).toBe(codeStepUrl);
  });

  it('a refused new code shows the refusal on this step', async () => {
    const page = await openCodeStep();

    buttonNamed(page, signIn['resendCode']).click();
    http.expectOne(endpoint).flush({ code: 'email-limit', retryAt: '2030-01-01T00:00:00Z' }, { status: 429, statusText: 'Too Many Requests' });
    await settle(harness);

    expect(page.querySelector('app-message[role="alert"]')?.textContent).toContain(errors['email-limit']);
    expect(TestBed.inject(Router).url).toBe(codeStepUrl);
    expect(page.textContent).toContain('4 of 5 left');
  });

  it('use another email returns to /sign-in with the email and the checkbox filled', async () => {
    const page = await openCodeStep(true);

    buttonNamed(page, signIn['otherEmail']).click();
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe('/sign-in');
    const signInPage = harness.routeNativeElement as HTMLElement;
    expect(signInPage.querySelector<HTMLInputElement>('input[type="email"]')?.value).toBe(email);
    expect(signInPage.querySelector<HTMLInputElement>('input[type="checkbox"]')?.checked).toBe(true);
  });

  it('without a requested code /sign-in/code opens /sign-in', async () => {
    await harness.navigateByUrl(codeStepUrl);

    expect(TestBed.inject(Router).url).toBe('/sign-in');
    expect(harness.routeNativeElement?.querySelector('input[type="email"]')).not.toBeNull();
  });
});
