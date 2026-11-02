import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { Problem, problemText } from '../api/problem';
import { MessageComponent } from '../message/message';
import { CurrentUserService } from '../session/current-user.service';
import { LanguageService } from './language.service';

/**
 * The language switcher: each language under its own name, from `GET /api/locales`. A switch whose file fails to load
 * keeps the language, shows it in the list again and says `error.network` under the list (contracts/ui.md, "Start of
 * the application"). Signed in, a choice is the user's locale (FR-023), kept on the server first; the server refusing
 * it is worded under the list, in the page's language.
 */
@Component({
  selector: 'app-language-switcher',
  imports: [TranslocoPipe, MessageComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <label for="language-switcher">{{ 'language.switcher' | transloco }}</label>
    <select
      id="language-switcher"
      [attr.aria-describedby]="language.switchFailed() || refusal() ? 'language-switcher-error' : null"
      (change)="choose($event)"
    >
      @for (locale of language.available(); track locale.code) {
        <option [value]="locale.code" [selected]="locale.code === language.current()" [attr.lang]="locale.code">
          {{ locale.name }}
        </option>
      }
    </select>
    @if (refusal(); as text) {
      <app-message kind="error" id="language-switcher-error">{{ text }}</app-message>
    } @else if (language.switchFailed()) {
      <app-message kind="error" id="language-switcher-error">{{ 'error.network' | transloco }}</app-message>
    }
  `,
})
export class LanguageSwitcher {
  protected readonly language = inject(LanguageService);
  private readonly users = inject(CurrentUserService);
  private readonly transloco = inject(TranslocoService);
  private readonly activeLanguage = toSignal(this.transloco.langChanges$);
  private readonly refused = signal<Problem | null>(null);

  /** Why the server refused the user's locale, in the page's language, worded again when the language changes. */
  protected readonly refusal = computed(() => {
    this.activeLanguage();
    const problem = this.refused();
    if (problem === null) return null;
    return problemText(problem, (key, params) => this.transloco.translate(key, params), this.language.current(), new Date());
  });

  protected async choose(event: Event): Promise<void> {
    const select = event.target as HTMLSelectElement;
    if (this.users.user() === null) {
      this.refused.set(null);
      await this.language.use(select.value);
    } else {
      const problem = await this.users.changeLocale(select.value);
      // A session that ended is the interceptor's: the page moves to /sign-in and says so.
      this.refused.set(problem?.code === 'not-signed-in' ? null : problem);
    }
    // After a failed switch the list would still show the language that did not load.
    select.value = this.language.current();
  }
}
