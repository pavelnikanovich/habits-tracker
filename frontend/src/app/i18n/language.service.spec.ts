import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTransloco, TranslocoService } from '@jsverse/transloco';
import { FileTranslationLoader } from '../../testing/translation-files';
import { LocaleResponse } from '../api/locale-response';
import { LanguageService } from './language.service';

const locales: LocaleResponse[] = [
  { code: 'en', name: 'English' },
  { code: 'pl', name: 'Polski' },
  { code: 'ru', name: 'Русский' },
];

function browserLanguages(languages: string[]): void {
  Object.defineProperty(navigator, 'languages', { configurable: true, get: () => languages });
}

// The language before sign-in (FR-021, research R11).
describe('LanguageService', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTransloco({ config: { defaultLang: 'en' }, loader: FileTranslationLoader }),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    Reflect.deleteProperty(navigator, 'languages');
    localStorage.clear();
    http.verify();
  });

  async function start(): Promise<LanguageService> {
    const service = TestBed.inject(LanguageService);
    const started = service.start();
    http.expectOne('/api/locales').flush(locales);
    await started;
    return service;
  }

  it.each<[string | null, string[], string]>([
    [null, ['pl-PL', 'en'], 'pl'],
    [null, ['de-DE', 'ru'], 'ru'],
    [null, ['de-DE'], 'en'],
    [null, [], 'en'],
    ['ru', ['pl-PL'], 'ru'],
    ['xx', ['pl-PL'], 'pl'], // a stored value that is not available is ignored
    ['de', [], 'en'],
  ])('detects the language: stored %s, browser %j → %s', async (stored, languages, detected) => {
    if (stored !== null) localStorage.setItem('locale', stored);
    browserLanguages(languages);

    const service = await start();

    expect(service.current()).toBe(detected);
    expect(TestBed.inject(TranslocoService).getActiveLang()).toBe(detected);
    expect(document.documentElement.lang).toBe(detected);
    expect(localStorage.getItem('locale'), 'only a choice is stored').toBe(stored);
  });

  it('lists the available locales', async () => {
    browserLanguages([]);

    const service = await start();

    expect(service.available()).toEqual(locales);
    expect(TestBed.inject(TranslocoService).getAvailableLangs()).toEqual(['en', 'pl', 'ru']);
  });

  it('a storage failure leaves the language working', async () => {
    const refuse = () => {
      throw new DOMException('Site data is blocked', 'SecurityError');
    };
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(refuse);
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(refuse);
    browserLanguages(['ru']);

    const service = await start();
    expect(service.current()).toBe('ru');

    service.use('pl');
    expect(service.current()).toBe('pl');
    vi.restoreAllMocks();
  });

  it('use writes the choice and the document language', async () => {
    browserLanguages(['ru']);
    const service = await start();

    service.use('pl');

    expect(localStorage.getItem('locale')).toBe('pl');
    expect(document.documentElement.lang).toBe('pl');
    expect(service.current()).toBe('pl');
    expect(TestBed.inject(TranslocoService).getActiveLang()).toBe('pl');
  });
});
