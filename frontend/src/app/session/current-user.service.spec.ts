import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
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

    await users.signedIn({ email: 'ann@example.com', timeZone: 'UTC', timeZoneRecognized: true, locale: 'pl' });
    expect(language.current()).toBe('pl');

    // The first sign-in's `use` stored 'pl'; a later sign-in of a user whose locale is 'ru' still follows the user.
    await users.signedIn({ email: 'ann@example.com', timeZone: 'UTC', timeZoneRecognized: true, locale: 'ru' });
    expect(language.current()).toBe('ru');
  });
});
