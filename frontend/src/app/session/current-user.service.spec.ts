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

    // R-86: the last choice sent wins, on the server and on the page, whichever PUT answers last.
    it('two switches whose first PUT answers last end in the latest choice', async () => {
      const http = TestBed.inject(HttpTestingController);

      const first = put('pl');
      const second = put('ru');
      const [firstRequest, secondRequest] = http.match(LOCALE_URL);
      expect(firstRequest.request.body).toEqual({ locale: 'pl' });
      expect(secondRequest.request.body).toEqual({ locale: 'ru' });
      secondRequest.flush(null, NO_CONTENT);
      await second;
      firstRequest.flush(null, NO_CONTENT);
      await first;

      expect(TestBed.inject(LanguageService).current()).toBe('ru');
    });
  });
});
