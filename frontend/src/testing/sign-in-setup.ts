import { provideHttpClient, withInterceptors } from '@angular/common/http';
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
import { CurrentUser, CurrentUserService } from '../app/session/current-user.service';
import { notSignedInInterceptor } from '../app/session/not-signed-in.interceptor';
import { FileTranslationLoader, readTranslation, translationCodes } from './translation-files';

/** The English texts of one section of the translation file, e.g. `texts('signIn')`. */
export function texts(section: string): Record<string, string> {
  return readTranslation('en')[section] as Record<string, string>;
}

export const ann: CurrentUser = { email: 'ann@example.com', timeZone: 'Europe/Warsaw', timeZoneRecognized: true, locale: 'pl' };

/** What the page asked of the language service: the codes it was told to use. */
export interface LanguageRecord {
  used: string[];
}

/**
 * The pages with the real routes, an HTTP client that answers only what the test flushes, and English texts. The
 * visitor is known not to be signed in, so no `GET /api/me` is waiting; `fresh` leaves the user unknown instead.
 */
export async function setUpSignIn(
  pageLanguage = 'pl',
  options: { fresh?: boolean } = {},
): Promise<{ harness: RouterTestingHarness; http: HttpTestingController; language: LanguageRecord }> {
  const language: LanguageRecord = { used: [] };
  TestBed.configureTestingModule({
    providers: [
      provideRouter(routes),
      provideHttpClient(withInterceptors([notSignedInInterceptor])),
      provideHttpClientTesting(),
      provideTransloco({ config: { defaultLang: 'en', availableLangs: translationCodes() }, loader: FileTranslationLoader }),
      provideIcuTranspiler(),
      // The page's language travels in the request; the texts stay English.
      {
        provide: LanguageService,
        useValue: {
          current: signal(pageLanguage),
          use: async (code: string) => void language.used.push(code),
        },
      },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('en'));
  if (options.fresh !== true) TestBed.inject(CurrentUserService).clear();
  return { harness: await RouterTestingHarness.create(), http: TestBed.inject(HttpTestingController), language };
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
