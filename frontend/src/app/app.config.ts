import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, isDevMode, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, TitleStrategy } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { routes } from './app.routes';
import { provideIcuTranspiler } from './i18n/icu-transpiler';
import { LanguageService } from './i18n/language.service';
import { TranslocoHttpLoader } from './i18n/transloco-loader';
import { notSignedInInterceptor } from './session/not-signed-in.interceptor';
import { provideHeadingFocus } from './shell/focus-heading';
import { TranslatedTitleStrategy } from './shell/translated-title.strategy';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    { provide: TitleStrategy, useClass: TranslatedTitleStrategy },
    provideHeadingFocus(),
    provideHttpClient(withInterceptors([notSignedInInterceptor])),
    // The languages are not listed here: GET /api/locales lists the translation files (FR-020).
    provideTransloco({
      config: { defaultLang: 'en', reRenderOnLangChange: true, prodMode: !isDevMode() },
      loader: TranslocoHttpLoader,
    }),
    provideIcuTranspiler(),
    // The page renders once the current language's translation file has loaded, so no key ever shows. When no file
    // loads, the start fails and the static text of index.html stays.
    provideAppInitializer(() => inject(LanguageService).start()),
  ],
};
