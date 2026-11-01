import { HttpClient } from '@angular/common/http';
import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { readTranslation } from '../../testing/translation-files';
import { ann, settle, setUpSignIn, texts } from '../../testing/sign-in-setup';
import { CurrentUserService } from '../session/current-user.service';
import { Main } from './main';

const main = texts('main');
const errors = texts('error');
const signIn = texts('signIn');
const STATUS_MESSAGE = 'app-message[role="status"]';
const NOT_SIGNED_IN_BODY = { code: 'not-signed-in' };
const UNAUTHORIZED = { status: 401, statusText: 'Unauthorized' };
const NO_CONTENT = { status: 204, statusText: 'No Content' };
const CURRENT_URL = '/api/sessions/current';
const SESSIONS_URL = '/api/sessions';
const ARIA_DISABLED = 'aria-disabled';
const alert = (page: HTMLElement) => page.querySelector('app-message[role="alert"]')?.textContent?.trim();

describe('Main', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;

  beforeEach(async () => {
    ({ harness, http } = await setUpSignIn('en', { fresh: true }));
  });

  afterEach(() => {
    vi.useRealTimers();
    http.verify();
  });

  async function openMain(timeZoneRecognized = true): Promise<HTMLElement> {
    TestBed.inject(CurrentUserService).signedIn({ ...ann, timeZoneRecognized });
    expect(await harness.navigateByUrl('/', Main)).toBeInstanceOf(Main);
    return harness.routeNativeElement as HTMLElement;
  }

  it('says there are no habits yet', async () => {
    const page = await openMain();

    expect(page.querySelector('h1')?.textContent?.trim()).toBe(main['title']);
    expect(page.textContent).toContain(main['noHabits']);
  });

  it('shows the time zone notice only when not recognized', async () => {
    expect((await openMain(true)).textContent).not.toContain(main['timeZoneNotRecognized']);

    const page = await openMain(false);
    await settle(harness);

    expect(page.querySelector(STATUS_MESSAGE)?.textContent?.trim()).toBe(main['timeZoneNotRecognized']);
  });

  it('says loading only after one second', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    const page = (await harness.navigateByUrl('/', Main).then(() => harness.routeNativeElement)) as HTMLElement;
    const pending = http.expectOne('/api/me');
    const text = () => {
      harness.detectChanges();
      return page.textContent ?? '';
    };

    expect(text()).not.toContain(main['loading']);
    await vi.advanceTimersByTimeAsync(999);
    expect(text()).not.toContain(main['loading']);
    await vi.advanceTimersByTimeAsync(1);
    expect(text()).toContain(main['loading']);

    pending.flush(ann);
    await vi.advanceTimersByTimeAsync(0);
    expect(text()).not.toContain(main['loading']);
    expect(text()).toContain(main['noHabits']);
  });

  it('a visitor who opens / is sent to /sign-in once GET /api/me answers not-signed-in', async () => {
    await harness.navigateByUrl('/', Main);
    http.expectOne('/api/me').flush(NOT_SIGNED_IN_BODY, UNAUTHORIZED);
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe('/sign-in');
  });

  it('says error.network when GET /api/me gets no answer', async () => {
    const page = (await harness.navigateByUrl('/', Main).then(() => harness.routeNativeElement)) as HTMLElement;
    http.expectOne('/api/me').error(new ProgressEvent('error'));
    await settle(harness);

    expect(alert(page)).toBe(errors['network']);
  });

  // Review M5: the main screen words a problem as the sign-in does, with when to try again.
  it('says when to try again where the answer of GET /api/me gives it', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 10, 10, 12, 0));
    const page = (await harness.navigateByUrl('/', Main).then(() => harness.routeNativeElement)) as HTMLElement;
    http.expectOne('/api/me').flush(
      { code: 'email-service-limit', retryAt: new Date(2026, 10, 10, 13, 0).toISOString() },
      { status: 503, statusText: 'Service Unavailable' },
    );
    await settle(harness);

    expect(alert(page)).toBe(
      `${errors['email-service-limit']} ${signIn['retryAt'].replace('{time}', '13:00')}`,
    );
  });

  it('the failure text follows a language switch', async () => {
    const page = (await harness.navigateByUrl('/', Main).then(() => harness.routeNativeElement)) as HTMLElement;
    http.expectOne('/api/me').error(new ProgressEvent('error'));
    await settle(harness);
    const transloco = TestBed.inject(TranslocoService);
    await firstValueFrom(transloco.load('ru'));

    transloco.setActiveLang('ru');
    await settle(harness);

    const ru = readTranslation('ru')['error'] as Record<string, string>;
    expect(alert(page)).toBe(ru['network']);
  });
});

describe('Main, the sign-out actions', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;
  let page: HTMLElement;

  beforeEach(async () => {
    ({ harness, http } = await setUpSignIn('en', { fresh: true }));
    TestBed.inject(CurrentUserService).signedIn(ann);
    await harness.navigateByUrl('/', Main);
    await settle(harness);
    page = harness.routeNativeElement as HTMLElement;
  });

  afterEach(() => http.verify());

  // Sign out everywhere and its confirmation read alike, so a button is found by the key of its text, through its id.
  const ids = { signOut: 'sign-out', signOutEverywhere: 'sign-out-everywhere', confirm: 'confirm', cancel: 'cancel' };
  const button = (key: keyof typeof ids) => page.querySelector<HTMLButtonElement>(`#${ids[key]}`);
  const press = async (key: keyof typeof ids) => {
    button(key)!.click();
    await settle(harness);
  };
  const focused = () => document.activeElement;
  const router = () => TestBed.inject(Router);

  it('shows both sign-out buttons', () => {
    expect(button('signOut')).not.toBeNull();
    expect(button('signOutEverywhere')).not.toBeNull();
    expect(button('confirm')).toBeNull();
    expect(page.textContent).not.toContain(main['signOutEverywhereConfirm']);
  });

  it('plain sign out asks nothing', async () => {
    await press('signOut');

    const request = http.expectOne(CURRENT_URL);
    expect(request.request.method).toBe('DELETE');
    request.flush(null, NO_CONTENT);
    await settle(harness);

    expect(TestBed.inject(CurrentUserService).user()).toBeNull();
    expect(router().url).toBe('/sign-in');
  });

  it('sign out everywhere asks on the page and sends nothing until confirmed', async () => {
    await press('signOutEverywhere');

    expect(page.textContent).toContain(main['signOutEverywhereConfirm']);
    expect(button('signOutEverywhere')).toBeNull();
    expect(button('confirm')).not.toBeNull();
    expect(button('cancel')).not.toBeNull();
    http.expectNone(SESSIONS_URL);

    await press('confirm');

    const request = http.expectOne(SESSIONS_URL);
    expect(request.request.method).toBe('DELETE');
    request.flush(null, NO_CONTENT);
    await settle(harness);
    expect(TestBed.inject(CurrentUserService).user()).toBeNull();
    expect(router().url).toBe('/sign-in');
  });

  it('the focus moves to Cancel', async () => {
    await press('signOutEverywhere');

    expect(focused()).toBe(button('cancel'));
  });

  it('Cancel and Escape restore the button and its focus', async () => {
    await press('signOutEverywhere');
    await press('cancel');

    expect(button('signOutEverywhere')).not.toBeNull();
    expect(button('confirm')).toBeNull();
    expect(focused()).toBe(button('signOutEverywhere'));

    await press('signOutEverywhere');
    button('cancel')!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    await settle(harness);

    expect(button('signOutEverywhere')).not.toBeNull();
    expect(page.textContent).not.toContain(main['signOutEverywhereConfirm']);
    expect(focused()).toBe(button('signOutEverywhere'));
    http.expectNone(SESSIONS_URL);
  });

  it('both buttons are disabled while signing out', async () => {
    const plain = button('signOut')!;
    plain.focus();
    await press('signOut');
    const request = http.expectOne(CURRENT_URL);

    for (const name of ['signOut', 'signOutEverywhere'] as const) {
      expect(button(name)!.getAttribute(ARIA_DISABLED), name).toBe('true');
      expect(button(name)!.hasAttribute('disabled'), name).toBe(false);
    }
    expect(focused(), 'the focus stays on the button').toBe(plain);

    await press('signOutEverywhere'); // ignored
    await press('signOut'); // ignored: no second request
    expect(page.textContent).not.toContain(main['signOutEverywhereConfirm']);
    http.expectNone(SESSIONS_URL);

    request.flush(null, NO_CONTENT);
    await settle(harness);
    expect(router().url).toBe('/sign-in');
  });

  it('the buttons of the confirmation are disabled too while signing out everywhere', async () => {
    await press('signOutEverywhere');
    const confirm = button('confirm')!;
    confirm.focus();
    await press('confirm');
    const request = http.expectOne(SESSIONS_URL);

    expect(confirm.getAttribute(ARIA_DISABLED)).toBe('true');
    expect(confirm.hasAttribute('disabled')).toBe(false);
    expect(button('cancel')!.getAttribute(ARIA_DISABLED)).toBe('true');
    expect(focused()).toBe(confirm);

    await press('cancel'); // ignored
    await press('confirm'); // ignored
    expect(page.textContent).toContain(main['signOutEverywhereConfirm']);
    http.expectNone(SESSIONS_URL);

    request.flush(null, NO_CONTENT);
    await settle(harness);
  });

  it('a failed sign out says why and enables the buttons again', async () => {
    await press('signOut');
    http.expectOne(CURRENT_URL).error(new ProgressEvent('error'));
    await settle(harness);

    expect(alert(page)).toBe(errors['network']);
    expect(button('signOut')!.getAttribute(ARIA_DISABLED)).toBeNull();
    expect(TestBed.inject(CurrentUserService).user()).toEqual(ann);
    expect(router().url).toBe('/');
  });

  // The interceptor owns the way out of a session that ended: the screen touches nothing while it goes.
  it('a sign out answered not-signed-in leaves the screen to the interceptor', async () => {
    const main = harness.routeDebugElement!.componentInstance as Main;
    await press('signOutEverywhere');
    await press('confirm');

    http.expectOne(SESSIONS_URL).flush(NOT_SIGNED_IN_BODY, UNAUTHORIZED);
    await settle(harness);
    await settle(harness);

    expect(router().url).toBe('/sign-in');
    expect(main['confirming'](), 'the confirmation is left as it was').toBe(true);
    expect((harness.routeNativeElement as HTMLElement).querySelector(STATUS_MESSAGE)?.textContent?.trim()).toBe(
      signIn['sessionEnded'],
    );
  });

  // Design review H4: a request answering not-signed-in while the page is open ends in the sign-in page with its
  // reason, whichever of the main screen and the interceptor navigates first.
  it('a session ended while the page is open brings /sign-in with the session-ended state', async () => {
    TestBed.inject(HttpClient).get('/api/habits').subscribe({ error: () => undefined });
    http.expectOne('/api/habits').flush(NOT_SIGNED_IN_BODY, UNAUTHORIZED);
    await settle(harness);
    await settle(harness);

    expect(router().url).toBe('/sign-in');
    const sign = harness.routeNativeElement as HTMLElement;
    expect(sign.querySelector(STATUS_MESSAGE)?.textContent?.trim()).toBe(signIn['sessionEnded']);
  });
});
