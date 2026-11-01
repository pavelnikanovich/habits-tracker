import { provideHttpClient } from '@angular/common/http';
import { ApplicationConfig, inject, isDevMode, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, TitleStrategy } from '@angular/router';
import { provideTransloco, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { routes } from './app.routes';
import { provideIcuTranspiler } from './i18n/icu-transpiler';
import { LanguageService } from './i18n/language.service';
import { TranslocoHttpLoader } from './i18n/transloco-loader';
import { provideHeadingFocus } from './shell/focus-heading';
import { TranslatedTitleStrategy } from './shell/translated-title.strategy';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    { provide: TitleStrategy, useClass: TranslatedTitleStrategy },
    provideHeadingFocus(),
    provideHttpClient(),
    // The languages are not listed here: GET /api/locales lists the translation files (FR-020).
    provideTransloco({
      config: { defaultLang: 'en', reRenderOnLangChange: true, prodMode: !isDevMode() },
      loader: TranslocoHttpLoader,
    }),
    provideIcuTranspiler(),
    // The page renders once the current language's translation file has loaded, so no key ever shows.
    provideAppInitializer(async () => {
      const language = inject(LanguageService);
      const transloco = inject(TranslocoService);
      await language.start();
      await firstValueFrom(transloco.load(language.current()));
    }),
  ],
};
