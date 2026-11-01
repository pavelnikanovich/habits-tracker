import { TestBed } from '@angular/core/testing';
import { provideTransloco, Translation, TranslocoLoader, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom, Observable, of } from 'rxjs';
import { provideIcuTranspiler } from './icu-transpiler';

const texts: Record<string, Translation> = {
  en: { files: '{count, plural, one {# file} other {# files}}', plain: 'Sign in', tag: 'Press <b>{key}</b>' },
  ru: { files: '{count, plural, one {# файл} few {# файла} many {# файлов} other {# файла}}', plain: 'Войти', tag: '' },
};

class FixedLoader implements TranslocoLoader {
  getTranslation(code: string): Observable<Translation> {
    return of(texts[code]);
  }
}

// ICU messages are interpreted, never compiled to code, so they render under `script-src 'self'` (research R19, T2).
describe('IcuTranspiler', () => {
  let transloco: TranslocoService;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideTransloco({ config: { defaultLang: 'en', availableLangs: ['en', 'ru'] }, loader: FixedLoader }),
        provideIcuTranspiler(),
      ],
    });
    transloco = TestBed.inject(TranslocoService);
    await Promise.all(['en', 'ru'].map((code) => firstValueFrom(transloco.load(code))));
  });

  it('formats a plural in the active language', () => {
    expect(transloco.translate('files', { count: 1 })).toBe('1 file');
    expect(transloco.translate('files', { count: 3 })).toBe('3 files');

    transloco.setActiveLang('ru');

    expect([1, 2, 5].map((count) => transloco.translate('files', { count }))).toEqual(['1 файл', '2 файла', '5 файлов']);
  });

  it('returns a text without arguments as it is', () => {
    expect(transloco.translate('plain')).toBe('Sign in');
  });

  it('keeps a tag as text', () => {
    expect(transloco.translate('tag', { key: 'Enter' })).toBe('Press <b>Enter</b>');
  });
});
