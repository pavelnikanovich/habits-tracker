import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageSwitcher } from './i18n/language-switcher';
import { ThemeSwitcher } from './theme/theme-switcher';

/** The frame of every route: the language and theme switchers, the page, and the links to Privacy and Terms. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, TranslocoPipe, LanguageSwitcher, ThemeSwitcher],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header>
      <app-language-switcher />
      <app-theme-switcher />
    </header>
    <main>
      <router-outlet />
    </main>
    <footer>
      <a routerLink="/privacy">{{ 'nav.privacy' | transloco }}</a>
      <a routerLink="/terms">{{ 'nav.terms' | transloco }}</a>
    </footer>
  `,
})
export class AppShell {}
