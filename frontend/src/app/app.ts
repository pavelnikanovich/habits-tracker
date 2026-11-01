import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageSwitcher } from './i18n/language-switcher';

/** The frame of every route: the language switcher, the page, and the links to Privacy and Terms. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, TranslocoPipe, LanguageSwitcher],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header>
      <app-language-switcher />
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
