import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideTransloco, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { FileTranslationLoader, readTranslation, translationCodes } from '../../testing/translation-files';
import { ann } from '../../testing/sign-in-setup';
import { LocaleResponse } from '../api/locale-response';
import { provideIcuTranspiler } from './icu-transpiler';
import { CurrentUserService } from '../session/current-user.service';
import { LanguageSwitcher } from './language-switcher';
import { LanguageService } from './language.service';

const MESSAGE = 'app-message';

const locales: LocaleResponse[] = [
  { code: 'en', name: 'English' },
  { code: 'ru', name: 'Русский' },
];

/** A language service whose every switch fails to load its file, as with no connection. */
class OfflineLanguage {
  readonly current = signal('en');
  readonly available = signal(locales);
  readonly switchFailed = signal(false);
  readonly requested: string[] = [];
  readonly shown: string[] = [];

  async use(code: string): Promise<void> {
    this.requested.push(code);
    this.switchFailed.set(true);
  }

  async show(code: string): Promise<boolean> {
    this.shown.push(code);
    return true;
  }
}

// contracts/ui.md, "Start of the application": a switch whose file fails to load keeps the language and says so.
describe('LanguageSwitcher', () => {
  let language: OfflineLanguage;

  beforeEach(async () => {
    language = new OfflineLanguage();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTransloco({ config: { defaultLang: 'en', availableLangs: translationCodes() }, loader: FileTranslationLoader }),
        provideIcuTranspiler(),
        { provide: LanguageService, useValue: language },
      ],
    });
    await firstValueFrom(TestBed.inject(TranslocoService).load('en'));
  });

  it('shows no message before a switch', async () => {
    const fixture = TestBed.createComponent(LanguageSwitcher);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector(MESSAGE)).toBeNull();
  });

  it('a failed switch shows error.network and the current language', async () => {
    const fixture = TestBed.createComponent(LanguageSwitcher);
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const select = page.querySelector('select')!;

    select.value = 'ru';
    select.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(language.requested).toEqual(['ru']);
    const message = page.querySelector(MESSAGE);
    expect(message?.getAttribute('role')).toBe('alert');
    expect(message?.textContent?.trim()).toBe((readTranslation('en')['error'] as Record<string, string>)['network']);
    expect(select.getAttribute('aria-describedby')).toBe(message?.id);
    expect(select.value, 'the select shows the language the page is in').toBe('en');
  });

  // Signed in, the switcher changes the user's locale (FR-023): the server first, then the page; this browser's
  // choice is not touched, and the language service never calls the API.
  it("signed in, the switcher changes the user's locale", async () => {
    localStorage.clear();
    await TestBed.inject(CurrentUserService).signedIn({ ...ann, locale: 'en' });
    language.shown.length = 0; // signing in showed the user's locale
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(LanguageSwitcher);
    await fixture.whenStable();
    const select = (fixture.nativeElement as HTMLElement).querySelector('select')!;

    select.value = 'ru';
    select.dispatchEvent(new Event('change'));
    const request = http.expectOne('/api/me/locale');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ locale: 'ru' });
    request.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();

    expect(language.shown).toEqual(['ru']);
    expect(language.requested).toEqual([]);
    expect(localStorage.getItem('locale')).toBeNull();
    http.verify();
  });

  it('signed in, a refused change says why under the list and keeps the language', async () => {
    await TestBed.inject(CurrentUserService).signedIn({ ...ann, locale: 'en' });
    language.shown.length = 0; // signing in showed the user's locale
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(LanguageSwitcher);
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const select = page.querySelector('select')!;

    select.value = 'ru';
    select.dispatchEvent(new Event('change'));
    http.expectOne('/api/me/locale').flush({ code: 'invalid-locale' }, { status: 400, statusText: 'Bad Request' });
    await fixture.whenStable();

    const message = page.querySelector(MESSAGE);
    expect(message?.getAttribute('role')).toBe('alert');
    expect(message?.textContent?.trim()).toBe((readTranslation('en')['error'] as Record<string, string>)['invalid-locale']);
    expect(select.value).toBe('en');
    expect(language.shown).toEqual([]);
  });
});
