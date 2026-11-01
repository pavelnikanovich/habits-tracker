import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { routes } from '../app/app.routes';
import { provideIcuTranspiler } from '../app/i18n/icu-transpiler';
import { LanguageService } from '../app/i18n/language.service';
import { FileTranslationLoader, readTranslation, translationCodes } from './translation-files';

/** The English texts of one section of the translation file, e.g. `texts('signIn')`. */
export function texts(section: string): Record<string, string> {
  return readTranslation('en')[section] as Record<string, string>;
}

/** The sign-in pages with the real routes, an HTTP client that answers only what the test flushes, and English texts. */
export async function setUpSignIn(pageLanguage = 'pl'): Promise<{ harness: RouterTestingHarness; http: HttpTestingController }> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter(routes),
      provideHttpClient(),
      provideHttpClientTesting(),
      provideTransloco({ config: { defaultLang: 'en', availableLangs: translationCodes() }, loader: FileTranslationLoader }),
      provideIcuTranspiler(),
      // The page's language travels in the request; the texts stay English.
      { provide: LanguageService, useValue: { current: signal(pageLanguage) } },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('en'));
  return { harness: await RouterTestingHarness.create(), http: TestBed.inject(HttpTestingController) };
}

/** Types `value` into the field `selector` of `root`, as a person does. */
export function type(root: HTMLElement, selector: string, value: string): void {
  const input = root.querySelector<HTMLInputElement>(selector)!;
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
}

/** Lets the promises of the page's handlers finish, then the rendering and the navigation they started. */
export async function settle(harness: RouterTestingHarness): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve));
  await harness.fixture.whenStable();
}
