import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTransloco, Translation, TranslocoLoader, TranslocoService } from '@jsverse/transloco';
import { Observable, of, Subject, throwError } from 'rxjs';
import { readTranslation } from '../../testing/translation-files';
import { LocaleResponse } from '../api/locale-response';
import { LanguageService } from './language.service';

const localesUrl = '/api/locales';
const locales: LocaleResponse[] = [
  { code: 'en', name: 'English' },
  { code: 'pl', name: 'Polski' },
  { code: 'ru', name: 'Русский' },
];

// The translation files from disk, except a file named in `failing`, whose load fails, and one held in `held` until
// the test releases it.
const failing = new Set<string>();
const held = new Map<string, Subject<Translation>>();

class TestLoader implements TranslocoLoader {
  getTranslation(code: string): Observable<Translation> {
    if (failing.has(code)) return throwError(() => new Error(`${code}.json: no connection`));
    return held.get(code) ?? of(readTranslation(code));
  }
}

function failLoading(code: string): void {
  failing.add(code);
  vi.spyOn(console, 'error').mockImplementation(() => undefined); // Transloco reports the failed load
}

function hold(code: string): () => void {
  const subject = new Subject<Translation>();
  held.set(code, subject);
  return () => {
    subject.next(readTranslation(code));
    subject.complete();
  };
}

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
        provideTransloco({ config: { defaultLang: 'en' }, loader: TestLoader }),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    Reflect.deleteProperty(navigator, 'languages');
    localStorage.clear();
    failing.clear();
    held.clear();
    http.verify();
  });

  async function start(): Promise<LanguageService> {
    const service = TestBed.inject(LanguageService);
    const started = service.start();
    http.expectOne(localesUrl).flush(locales);
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

    await service.use('pl');
    expect(service.current()).toBe('pl');
  });

  it('use writes the choice and the document language', async () => {
    browserLanguages(['ru']);
    const service = await start();

    await service.use('pl');

    expect(localStorage.getItem('locale')).toBe('pl');
    expect(document.documentElement.lang).toBe('pl');
    expect(service.current()).toBe('pl');
    expect(TestBed.inject(TranslocoService).getActiveLang()).toBe('pl');
  });

  // Review M7: the page changes language only once the new file is in, so a failed load leaves it as it was.
  it('use changes nothing until the file has loaded', async () => {
    browserLanguages(['ru']);
    const service = await start();
    const release = hold('pl');

    const switching = service.use('pl');

    expect(service.current()).toBe('ru');
    expect(document.documentElement.lang).toBe('ru');
    expect(localStorage.getItem('locale')).toBeNull();
    expect(TestBed.inject(TranslocoService).getActiveLang()).toBe('ru');

    release();
    await switching;

    expect(service.current()).toBe('pl');
    expect(document.documentElement.lang).toBe('pl');
    expect(localStorage.getItem('locale')).toBe('pl');
  });

  // contracts/ui.md, "Start of the application": the switch fails, the page stays in its language and says so.
  it('a switch whose file fails to load keeps the current language', async () => {
    browserLanguages(['ru']);
    const service = await start();
    failLoading('pl');

    await service.use('pl');

    expect(service.current()).toBe('ru');
    expect(document.documentElement.lang).toBe('ru');
    expect(localStorage.getItem('locale')).toBeNull();
    expect(TestBed.inject(TranslocoService).getActiveLang()).toBe('ru');
    expect(service.switchFailed()).toBe(true);
  });

  it('a later switch that loads clears the failure', async () => {
    browserLanguages(['ru']);
    const service = await start();
    failLoading('pl');
    await service.use('pl');

    await service.use('en');

    expect(service.current()).toBe('en');
    expect(service.switchFailed()).toBe(false);
  });

  it('a failed locale list leaves English', async () => {
    localStorage.setItem('locale', 'ru');
    browserLanguages(['pl-PL']);
    const service = TestBed.inject(LanguageService);

    const started = service.start();
    http.expectOne(localesUrl).error(new ProgressEvent('error'));
    await started;

    expect(service.current()).toBe('en');
    expect(TestBed.inject(TranslocoService).getActiveLang()).toBe('en');
    expect(document.documentElement.lang).toBe('en');
    expect(service.available()).toEqual([{ code: 'en', name: 'English' }]);
  });

  it("a detected language whose file fails to load falls back to English", async () => {
    browserLanguages(['ru']);
    failLoading('ru');

    const service = await start();

    expect(service.current()).toBe('en');
    expect(document.documentElement.lang).toBe('en');
    expect(TestBed.inject(TranslocoService).getActiveLang()).toBe('en');
  });

  // Then nothing can be shown in any language: the start fails and index.html's static text stays.
  it('the start fails when even English fails to load', async () => {
    browserLanguages(['ru']);
    failLoading('ru');
    failLoading('en');
    const service = TestBed.inject(LanguageService);

    const started = service.start();
    http.expectOne(localesUrl).flush(locales);

    await expect(started).rejects.toThrow();
  });
});
