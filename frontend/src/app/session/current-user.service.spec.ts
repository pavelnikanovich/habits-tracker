import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { ann } from '../../testing/sign-in-setup';
import { FileTranslationLoader, translationCodes } from '../../testing/translation-files';
import { provideIcuTranspiler } from '../i18n/icu-transpiler';
import { LanguageService } from '../i18n/language.service';
import { CurrentUserService } from './current-user.service';

const NO_CONTENT = { status: 204, statusText: 'No Content' };
const LOCALE_URL = '/api/me/locale';

describe('CurrentUserService', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTransloco({ config: { defaultLang: 'en', availableLangs: translationCodes() }, loader: FileTranslationLoader }),
        provideIcuTranspiler(),
      ],
    });
  });

  afterEach(() => {
    localStorage.clear();
    TestBed.inject(HttpTestingController).verify();
  });

  it('the locale stored with the user wins over the language this browser chose', async () => {
    const language = TestBed.inject(LanguageService);
    await language.use('en'); // the browser's choice, kept in localStorage
    const users = TestBed.inject(CurrentUserService);

    await users.signedIn({ ...ann, locale: 'pl' });
    expect(language.current()).toBe('pl');

    // A later sign-in of a user whose locale is 'ru' follows the user too.
    await users.signedIn({ ...ann, locale: 'ru' });
    expect(language.current()).toBe('ru');
  });

  // Review H1: the user's locale is shown, not stored as this browser's choice, so after sign-out the next visitor on
  // the device does not inherit it.
  it("the user's locale is not stored as this browser's choice", async () => {
    const language = TestBed.inject(LanguageService);
    await language.use('en');
    const users = TestBed.inject(CurrentUserService);

    await users.signedIn({ ...ann, locale: 'pl' });

    expect(language.current()).toBe('pl');
    expect(localStorage.getItem('locale')).toBe('en');
  });

  it("GET /api/me shows the user's locale and stores nothing", async () => {
    const users = TestBed.inject(CurrentUserService);

    const loading = users.load();
    TestBed.inject(HttpTestingController).expectOne('/api/me').flush({ ...ann, locale: 'ru' });
    await loading;

    expect(TestBed.inject(LanguageService).current()).toBe('ru');
    expect(localStorage.getItem('locale')).toBeNull();
  });

  describe('changeLocale', () => {
    const put = (code: string) => TestBed.inject(CurrentUserService).changeLocale(code);

    beforeEach(async () => {
      localStorage.clear();
      await TestBed.inject(CurrentUserService).signedIn({ ...ann, locale: 'en' });
    });

    it("PUT /api/me/locale, then shows the language and stores nothing as this browser's choice", async () => {
      const http = TestBed.inject(HttpTestingController);

      const changing = put('ru');
      const request = http.expectOne(LOCALE_URL);
      expect(request.request.method).toBe('PUT');
      expect(request.request.body).toEqual({ locale: 'ru' });
      request.flush(null, NO_CONTENT);

      expect(await changing).toBeNull();
      expect(TestBed.inject(LanguageService).current()).toBe('ru');
      expect(localStorage.getItem('locale')).toBeNull();
    });

    it('a refused change keeps the page and returns the problem', async () => {
      const http = TestBed.inject(HttpTestingController);

      const changing = put('ru');
      http.expectOne(LOCALE_URL).flush({ code: 'invalid-locale' }, { status: 400, statusText: 'Bad Request' });

      expect(await changing).toEqual({ code: 'invalid-locale', retryAt: undefined, traceId: undefined });
      expect(TestBed.inject(LanguageService).current()).toBe('en');
    });

    // R-86, R-89: the PUTs are sent one at a time, so the server applies them in the order of the choices; a choice made
    // while one is in flight waits, and only the latest waiting one is sent. The page ends in the latest choice.
    it('sends the locale PUTs one at a time and only the latest waiting choice', async () => {
      const http = TestBed.inject(HttpTestingController);
      const tick = () => new Promise((resolve) => setTimeout(resolve));

      const first = put('pl');
      const second = put('en'); // dropped: a later choice overtakes it while the first is in flight
      const third = put('ru');
      const sent = http.match(LOCALE_URL);
      expect(sent.map((request) => request.request.body)).toEqual([{ locale: 'pl' }]);

      sent[0].flush(null, NO_CONTENT);
      await tick();
      const next = http.match(LOCALE_URL);
      expect(next.map((request) => request.request.body)).toEqual([{ locale: 'ru' }]);
      expect(TestBed.inject(LanguageService).current(), 'an overtaken choice shows nothing').toBe('en');

      next[0].flush(null, NO_CONTENT);
      expect(await Promise.all([first, second, third])).toEqual([null, null, null]);
      expect(TestBed.inject(LanguageService).current()).toBe('ru');
    });
  });
});
