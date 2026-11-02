import { TestBed } from '@angular/core/testing';
import { RouterTestingHarness } from '@angular/router/testing';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { setUpSignIn } from '../../testing/sign-in-setup';
import { readTranslation, translationCodes } from '../../testing/translation-files';
import { LegalPage } from './legal-page';

// The Privacy and Terms pages (FR-024 to FR-031; the legal pages design, "Privacy page", "Terms page").
const PRIVACY_SECTIONS = 15;
const TERMS_SECTIONS = 12;
const CONTROLLER_NAME = 'Paweł Nikanowicz';
const CONTACT_EMAIL = 'pavelnikanovich@gmail.com';
const PAGES = [
  { url: '/privacy', key: 'privacy', sections: PRIVACY_SECTIONS },
  { url: '/terms', key: 'terms', sections: TERMS_SECTIONS },
];
// The sections the promises of the spec live in: retention (Privacy 7), recipients (Privacy 5), the browser (Privacy 12),
// complaints (Terms 6).
const RETENTION = 7;
const RECIPIENTS = 5;
const BROWSER = 12;
const COMPLAINTS = 6;

interface Section {
  title: string;
  body: string;
}
type Legal = Record<string, unknown> & { section: Record<string, Section> };

function legal(code: string, page: string): Legal {
  return readTranslation(code)[page] as Legal;
}

function legalTexts(code: string): Record<string, string> {
  return readTranslation(code)['legal'] as Record<string, string>;
}

async function open(url: string, language: string): Promise<{ harness: RouterTestingHarness; page: HTMLElement }> {
  const { harness } = await setUpSignIn(language);
  const transloco = TestBed.inject(TranslocoService);
  await firstValueFrom(transloco.load(language));
  transloco.setActiveLang(language);
  expect(await harness.navigateByUrl(url, LegalPage)).toBeInstanceOf(LegalPage);
  harness.detectChanges();
  return { harness, page: harness.routeNativeElement as HTMLElement };
}

describe('Privacy and Terms pages', () => {
  it('the Privacy page shows 15 sections and the Terms page 12, in every language', async () => {
    for (const code of translationCodes()) {
      for (const { url, key, sections } of PAGES) {
        const files = legal(code, key).section;
        expect(Object.keys(files), `${code} ${key}`).toHaveLength(sections);
        for (let n = 1; n <= sections; n++) {
          expect(files[n].title?.trim(), `${code} ${key} section ${n} title`).toBeTruthy();
          expect(files[n].body?.trim(), `${code} ${key} section ${n} body`).toBeTruthy();
        }

        TestBed.resetTestingModule();
        const { page } = await open(url, code);
        const headings = [...page.querySelectorAll('h2')].map((heading) => heading.textContent?.trim());
        expect(headings, `${code} ${url}`).toHaveLength(sections);
        expect(headings.every((text) => !!text), `${code} ${url} headings`).toBe(true);
        expect(page.querySelector('h1')?.getAttribute('tabindex')).toBe('-1');
        expect(page.querySelector('h1')?.textContent?.trim()).toBe((readTranslation(code)[key] as { title: string }).title);
      }
    }
  });

  it('the Polish and Russian pages say the English version prevails', async () => {
    expect(legalTexts('en')['englishPrevails']).toMatch(/English version prevails/);
    for (const [code, word] of [['pl', 'angielsk'], ['ru', 'английск']]) {
      expect(legalTexts(code)['englishPrevails'], code).toContain(word);
      for (const { url } of PAGES) {
        TestBed.resetTestingModule();
        const { page } = await open(url, code);
        expect(page.textContent, `${code} ${url}`).toContain(legalTexts(code)['englishPrevails']);
      }
    }
  });

  it('both pages name the controller and link the contact email', async () => {
    for (const code of translationCodes()) {
      expect(legalTexts(code)['controllerName'], code).toBe(CONTROLLER_NAME);
      expect(legalTexts(code)['contactEmail'], code).toBe(CONTACT_EMAIL);
      for (const { url } of PAGES) {
        TestBed.resetTestingModule();
        const { page } = await open(url, code);
        expect(page.textContent, `${code} ${url}`).toContain(CONTROLLER_NAME);
        expect(page.textContent, `${code} ${url} has an unfilled placeholder`).not.toContain('{');
        if (url === '/privacy') {
          const first = page.querySelectorAll('section')[0].textContent;
          expect(first, `${code} privacy section 1`).toContain(CONTROLLER_NAME);
          expect(first, `${code} privacy section 1`).toContain(CONTACT_EMAIL);
        }
        expect(page.querySelector(`a[href="mailto:${CONTACT_EMAIL}"]`), `${code} ${url}`).not.toBeNull();
      }
    }
  });

  it('the Privacy page names Hetzner and Resend as recipients in every language', () => {
    for (const code of translationCodes()) {
      const body = legal(code, 'privacy').section[RECIPIENTS].body;
      expect(body, code).toContain('Hetzner');
      expect(body, code).toContain('Resend');
    }
  });

  it('the pages state the promised periods', () => {
    for (const code of translationCodes()) {
      expect(legal(code, 'terms').section[COMPLAINTS].body, `${code} terms complaints`).toContain('14');
      const retention = legal(code, 'privacy').section[RETENTION].body;
      expect(retention, `${code} privacy retention`).toContain('7');
      expect(retention, `${code} privacy retention`).toContain('30');
    }
  });

  it('the Privacy page names what the browser keeps', () => {
    const body = legal('en', 'privacy').section[BROWSER].body;

    for (const word of ['cookie', 'language', 'theme']) expect(body).toContain(word);
  });

  it('the Terms page links to the Privacy page', async () => {
    const { page } = await open('/terms', 'en');

    expect(page.querySelector('main a[href="/privacy"], a[href="/privacy"]')).not.toBeNull();
  });
});
