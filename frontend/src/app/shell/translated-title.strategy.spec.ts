import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, TitleStrategy } from '@angular/router';
import { provideTransloco, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { FileTranslationLoader, readTranslation, translationCodes } from '../../testing/translation-files';
import { routes } from '../app.routes';
import { provideIcuTranspiler } from '../i18n/icu-transpiler';
import { TranslatedTitleStrategy } from './translated-title.strategy';

// Every route sets document.title to its page title and app.name, again on a language switch (contracts/ui.md,
// "Page title and focus").
describe('TranslatedTitleStrategy', () => {
  beforeEach(async () => {
    document.title = '';
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        { provide: TitleStrategy, useClass: TranslatedTitleStrategy },
        provideTransloco({ config: { defaultLang: 'en', availableLangs: translationCodes() }, loader: FileTranslationLoader }),
        provideIcuTranspiler(),
      ],
    });
    await firstValueFrom(TestBed.inject(TranslocoService).load('en'));
  });

  it('the title is the page title and the app name', async () => {
    await TestBed.inject(Router).navigateByUrl('/sign-in');

    await vi.waitFor(() => expect(document.title).toBe('Sign in — Habits'));
  });

  it('the title follows a language switch', async () => {
    await TestBed.inject(Router).navigateByUrl('/sign-in');
    await vi.waitFor(() => expect(document.title).toBe('Sign in — Habits'));

    TestBed.inject(TranslocoService).setActiveLang('ru');

    const ru = readTranslation('ru') as { signIn: { title: string }; app: { name: string } };
    await vi.waitFor(() => expect(document.title).toBe(`${ru.signIn.title} — ${ru.app.name}`));
  });
});
