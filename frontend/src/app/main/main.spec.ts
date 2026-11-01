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

    expect(page.querySelector('app-message[role="status"]')?.textContent?.trim()).toBe(main['timeZoneNotRecognized']);
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
    http.expectOne('/api/me').flush({ code: 'not-signed-in' }, { status: 401, statusText: 'Unauthorized' });
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
