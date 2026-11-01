import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { settle, setUpSignIn, texts, type } from '../../testing/sign-in-setup';
import { formatMoment } from '../time/format-moment';
import { SignIn } from './sign-in';

const signIn = texts('signIn');
const errors = texts('error');
const endpoint = '/api/code-requests';
const email = 'ann@example.com';
const codeStepUrl = '/sign-in/code';
const hidden = 'must-not-show';

const ariaDisabled = 'aria-disabled';

describe('SignIn', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;
  let page: HTMLElement;

  beforeEach(async () => {
    ({ harness, http } = await setUpSignIn());
    expect(await harness.navigateByUrl('/sign-in', SignIn)).toBeInstanceOf(SignIn);
    page = harness.routeNativeElement as HTMLElement;
  });

  afterEach(() => http.verify());

  const form = () => page.querySelector('form')!;
  const field = () => page.querySelector<HTMLInputElement>('input[type="email"]')!;
  const button = () => page.querySelector<HTMLButtonElement>('form button[type="submit"]')!;
  const alert = () => page.querySelector('app-message[role="alert"]');

  async function submit(address: string): Promise<void> {
    type(page, 'input[type="email"]', address);
    form().dispatchEvent(new Event('submit', { cancelable: true }));
    await settle(harness);
  }

  it('"Remember me" is unchecked by default and explained', () => {
    const checkbox = page.querySelector<HTMLInputElement>('input[type="checkbox"]')!;

    expect(checkbox.checked).toBe(false);
    expect(page.querySelector(`label[for="${checkbox.id}"]`)?.textContent?.trim()).toBe(signIn['rememberMe']);
    const hint = page.querySelector(`#${checkbox.getAttribute('aria-describedby')}`);
    expect(hint?.textContent?.trim()).toBe(signIn['rememberMeHint']);
  });

  it('labels the field and states the acceptance with links to both pages', () => {
    expect(page.querySelector(`label[for="${field().id}"]`)?.textContent?.trim()).toBe(signIn['email']);
    expect(field().getAttribute('autocomplete')).toBe('email');
    expect(page.querySelector('a[href="/terms"]')).not.toBeNull();
    expect(page.querySelector('a[href="/privacy"]')).not.toBeNull();
    expect(page.querySelector('input[type="checkbox"][id*="accept"]')).toBeNull();
  });

  it('sends the email trimmed, with the language of the page', async () => {
    await submit(`  ${email}  `);

    const request = http.expectOne(endpoint);
    expect(request.request.body).toEqual({ email, locale: 'pl' });
    request.flush({ remaining: 4, limit: 5 }, { status: 202, statusText: 'Accepted' });
  });

  it('does not send an invalid email and says why', async () => {
    for (const entered of ['ann', `${'a'.repeat(243)}@example.com`]) {
      await submit(entered);

      http.expectNone(endpoint);
      const message = alert();
      expect(message?.textContent?.trim(), entered.slice(0, 5)).toBe(signIn['emailInvalid']);
      expect(field().getAttribute('aria-describedby')).toBe(message?.id);
      expect(field().getAttribute('aria-invalid')).toBe('true');
      expect(document.activeElement).toBe(field());
      expect(button().getAttribute(ariaDisabled)).not.toBe('true');
    }
  });

  it('the button is disabled while sending and a second press sends nothing', async () => {
    button().focus();
    await submit(email);
    form().dispatchEvent(new Event('submit', { cancelable: true }));
    button().click();
    await settle(harness);

    // R-69: aria-disabled, never the disabled attribute, so the focus stays on the button.
    expect(button().getAttribute(ariaDisabled)).toBe('true');
    expect(button().hasAttribute('disabled')).toBe(false);
    expect(document.activeElement).toBe(button());
    expect(button().textContent?.trim()).toBe(signIn['requesting']);
    http.expectOne(endpoint).flush({ remaining: 4, limit: 5 }, { status: 202, statusText: 'Accepted' });
  });

  it('no field has the focus when the page opens', () => {
    expect(document.activeElement).toBe(document.body);
  });

  describe('shows each refusal', () => {
    const retryAt = new Date(Date.now() + 3_600_000).toISOString();
    const time = () => formatMoment(new Date(retryAt), new Date(), 'en', Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC');
    const cases: { status: number; code: string; retryAt?: string; traceId?: string }[] = [
      { status: 429, code: 'email-limit', retryAt },
      { status: 429, code: 'ip-limit', retryAt },
      { status: 503, code: 'email-service-limit', retryAt },
      { status: 503, code: 'email-service-limit' },
      { status: 503, code: 'email-service-unavailable' },
      { status: 400, code: 'invalid-request', traceId: hidden },
      { status: 400, code: 'invalid-email', traceId: hidden },
      { status: 400, code: 'invalid-locale', traceId: hidden },
      { status: 500, code: 'internal-error', traceId: '4bf92f3577b34da6a3ce929d0e0e4736' },
    ];

    for (const refusal of cases) {
      it(`${refusal.code}${refusal.retryAt ? ' with retryAt' : ''}`, async () => {
        await submit(email);
        http.expectOne(endpoint).flush(
          { code: refusal.code, retryAt: refusal.retryAt, traceId: refusal.traceId },
          { status: refusal.status, statusText: 'Refused' },
        );
        await settle(harness);

        const expected = errors[refusal.code].replace('{traceId}', refusal.traceId ?? '');
        const text = alert()?.textContent?.trim();
        expect(text).toBe(refusal.retryAt ? `${expected} ${signIn['retryAt'].replace('{time}', time())}` : expected);
        expect(text?.includes(hidden)).toBe(false);
        expect(field().value, 'the field keeps its value').toBe(email);
        expect(button().getAttribute(ariaDisabled)).not.toBe('true');
        expect(TestBed.inject(Router).url).toBe('/sign-in');
      });
    }
  });

  it('no response shows error.network', async () => {
    await submit(email);
    http.expectOne(endpoint).error(new ProgressEvent('error'));
    await settle(harness);

    expect(alert()?.textContent?.trim()).toBe(errors['network']);
  });

  it('a sent code opens the code step with nothing in the URL', async () => {
    await submit(email);
    http.expectOne(endpoint).flush({ remaining: 4, limit: 5 }, { status: 202, statusText: 'Accepted' });
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe(codeStepUrl);
  });
});
