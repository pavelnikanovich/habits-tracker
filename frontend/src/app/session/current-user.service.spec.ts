import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { ann } from '../../testing/sign-in-setup';
import { FileTranslationLoader, translationCodes } from '../../testing/translation-files';
import { provideIcuTranspiler } from '../i18n/icu-transpiler';
import { LanguageService } from '../i18n/language.service';
import { CurrentUserService } from './current-user.service';

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
});
