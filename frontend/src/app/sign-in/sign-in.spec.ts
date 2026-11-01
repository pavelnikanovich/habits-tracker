import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { readTranslation } from '../../testing/translation-files';
import { settle, setUpSignIn, texts, type } from '../../testing/sign-in-setup';
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
    // The clock is pinned, in local time, so the day the moment falls on does not depend on when the suite runs. The
    // page's language is Polish (setUpSignIn): the moment is worded in it (contracts/ui.md, "Times").
    const noon = new Date(2026, 10, 10, 12, 0);
    const lateEvening = new Date(2026, 10, 10, 23, 30);
    const inAnHour = (now: Date) => new Date(now.getTime() + 3_600_000).toISOString();
    const cases: { status: number; code: string; now?: Date; retryAt?: string; shown?: string; traceId?: string }[] = [
      { status: 429, code: 'email-limit', retryAt: inAnHour(noon), shown: '13:00' },
      { status: 429, code: 'ip-limit', retryAt: inAnHour(noon), shown: '13:00' },
      { status: 503, code: 'email-service-limit', retryAt: inAnHour(noon), shown: '13:00' },
      { status: 429, code: 'email-limit', now: lateEvening, retryAt: inAnHour(lateEvening), shown: '11 listopada, 00:30' },
      { status: 503, code: 'email-service-limit' },
      { status: 503, code: 'email-service-unavailable' },
      { status: 400, code: 'invalid-request', traceId: hidden },
      { status: 400, code: 'invalid-email', traceId: hidden },
      { status: 400, code: 'invalid-locale', traceId: hidden },
      { status: 500, code: 'internal-error', traceId: '4bf92f3577b34da6a3ce929d0e0e4736' },
    ];

    afterEach(() => vi.useRealTimers());

    for (const refusal of cases) {
      const when = refusal.now === undefined ? ' on the same day' : ' on the next day';
      it(`${refusal.code}${refusal.retryAt ? ` with retryAt${when}` : ''}`, async () => {
        vi.useFakeTimers({ toFake: ['Date'] });
        vi.setSystemTime(refusal.now ?? noon);
        await submit(email);
        http.expectOne(endpoint).flush(
          { code: refusal.code, retryAt: refusal.retryAt, traceId: refusal.traceId },
          { status: refusal.status, statusText: 'Refused' },
        );
        await settle(harness);

        const expected = errors[refusal.code].replace('{traceId}', refusal.traceId ?? '');
        const text = alert()?.textContent?.trim();
        expect(text).toBe(refusal.shown ? `${expected} ${signIn['retryAt'].replace('{time}', refusal.shown)}` : expected);
        expect(text?.includes(hidden)).toBe(false);
        expect(field().value, 'the field keeps its value').toBe(email);
        expect(button().getAttribute(ariaDisabled)).not.toBe('true');
        expect(TestBed.inject(Router).url).toBe('/sign-in');
      });
    }
  });

  it('a refusal follows a language switch', async () => {
    await submit(email);
    http.expectOne(endpoint).flush({ code: 'invalid-email' }, { status: 400, statusText: 'Refused' });
    await settle(harness);
    const transloco = TestBed.inject(TranslocoService);
    await firstValueFrom(transloco.load('ru'));

    transloco.setActiveLang('ru');
    await settle(harness);

    const ru = readTranslation('ru')['error'] as Record<string, string>;
    expect(alert()?.textContent?.trim()).toBe(ru['invalid-email']);
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

// contracts/ui.md, "Session ended": the reason travels in the router's navigation state, never in the URL.
describe('SignIn, arriving after a session ended', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;

  beforeEach(async () => {
    ({ harness, http } = await setUpSignIn());
  });

  afterEach(() => http.verify());

  const info = () => (harness.routeNativeElement as HTMLElement).querySelector('app-message[role="status"]');

  it('shows signIn.sessionEnded above the form, and the URL stays /sign-in', async () => {
    await TestBed.inject(Router).navigateByUrl('/sign-in', { state: { reason: 'session-ended' } });
    await settle(harness);

    const page = harness.routeNativeElement as HTMLElement;
    expect(info()?.textContent?.trim()).toBe(signIn['sessionEnded']);
    expect(info()!.compareDocumentPosition(page.querySelector('form')!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(TestBed.inject(Router).url).toBe('/sign-in');
  });

  it('shows nothing of it on a plain visit', async () => {
    await harness.navigateByUrl('/sign-in', SignIn);

    expect(info()).toBeNull();
  });
});
