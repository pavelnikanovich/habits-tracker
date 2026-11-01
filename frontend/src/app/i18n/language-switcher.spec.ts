import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideTransloco, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { FileTranslationLoader, readTranslation, translationCodes } from '../../testing/translation-files';
import { LocaleResponse } from '../api/locale-response';
import { provideIcuTranspiler } from './icu-transpiler';
import { LanguageSwitcher } from './language-switcher';
import { LanguageService } from './language.service';

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

  async use(code: string): Promise<void> {
    this.requested.push(code);
    this.switchFailed.set(true);
  }
}

// contracts/ui.md, "Start of the application": a switch whose file fails to load keeps the language and says so.
describe('LanguageSwitcher', () => {
  let language: OfflineLanguage;

  beforeEach(async () => {
    language = new OfflineLanguage();
    TestBed.configureTestingModule({
      providers: [
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

    expect((fixture.nativeElement as HTMLElement).querySelector('app-message')).toBeNull();
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
    const message = page.querySelector('app-message');
    expect(message?.getAttribute('role')).toBe('alert');
    expect(message?.textContent?.trim()).toBe((readTranslation('en')['error'] as Record<string, string>)['network']);
    expect(select.getAttribute('aria-describedby')).toBe(message?.id);
    expect(select.value, 'the select shows the language the page is in').toBe('en');
  });
});
