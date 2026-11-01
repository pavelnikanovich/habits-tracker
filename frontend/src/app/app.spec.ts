import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideTransloco, TranslocoService } from '@jsverse/transloco';
import { provideTranslocoMessageformat } from '@jsverse/transloco-messageformat';
import { firstValueFrom } from 'rxjs';
import { FileTranslationLoader, translationCodes } from '../testing/translation-files';
import { AppShell } from './app';
import { routes } from './app.routes';

describe('AppShell', () => {
  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTransloco({ config: { defaultLang: 'en', availableLangs: translationCodes() }, loader: FileTranslationLoader }),
        provideTranslocoMessageformat(),
      ],
    });
    await firstValueFrom(TestBed.inject(TranslocoService).load('en'));
  });

  it('every route shows the switcher and the Privacy and Terms links', async () => {
    const pages: [string, string][] = [
      ['/sign-in', 'Sign in'],
      ['/', 'Habits'],
      ['/privacy', 'Privacy policy'],
      ['/terms', 'Terms of use'],
      ['/no-such-page', 'Page not found'],
    ];
    const fixture = TestBed.createComponent(AppShell);
    const page = fixture.nativeElement as HTMLElement;

    for (const [url, heading] of pages) {
      await TestBed.inject(Router).navigateByUrl(url);
      await fixture.whenStable();

      expect(page.querySelectorAll('h1').length, url).toBe(1);
      expect(page.querySelector('h1')?.textContent?.trim(), url).toBe(heading);
      const switcher = page.querySelector('header select');
      expect(switcher, url).not.toBeNull();
      expect(page.querySelector(`label[for="${switcher?.id}"]`)?.textContent?.trim(), url).toBe('Language');
      expect(page.querySelector('footer a[href="/privacy"]')?.textContent?.trim(), url).toBe('Privacy');
      expect(page.querySelector('footer a[href="/terms"]')?.textContent?.trim(), url).toBe('Terms');
    }
  });
});
