import { TestBed } from '@angular/core/testing';
import { provideTransloco, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { FileTranslationLoader, translationCodes } from '../../testing/translation-files';
import { provideIcuTranspiler } from './icu-transpiler';

// Plural categories come from CLDR through ICU MessageFormat (research R11, SC-012).
describe('plurals', () => {
  let transloco: TranslocoService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideTransloco({ config: { defaultLang: 'en', availableLangs: translationCodes() }, loader: FileTranslationLoader }),
        provideIcuTranspiler(),
      ],
    });
    transloco = TestBed.inject(TranslocoService);
  });

  // As the page does: the active language also sets the plural rules of MessageFormat.
  async function remaining(code: string, count: number): Promise<string> {
    transloco.setActiveLang(code);
    return firstValueFrom(transloco.selectTranslate<string>('signIn.remaining', { remaining: count, limit: 5 }));
  }

  it('Polish and Russian use their plural forms', async () => {
    for (const code of ['pl', 'ru']) {
      const words: string[] = [];
      for (const count of [1, 2, 5]) words.push((await remaining(code, count)).replace(/\d/g, ''));

      expect(new Set(words).size, `${code}: ${words.join(' | ')}`).toBe(3);
    }
  });

  it('English counts what is left', async () => {
    expect(await remaining('en', 4)).toBe('4 of 5 left');
  });
});
