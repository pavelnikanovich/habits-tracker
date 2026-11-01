import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { MessageComponent } from '../message/message';
import { LanguageService } from './language.service';

/**
 * The language switcher: each language under its own name, from `GET /api/locales`. A switch whose file fails to load
 * keeps the language, shows it in the list again and says `error.network` under the list (contracts/ui.md, "Start of
 * the application").
 */
@Component({
  selector: 'app-language-switcher',
  imports: [TranslocoPipe, MessageComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <label for="language-switcher">{{ 'language.switcher' | transloco }}</label>
    <select
      id="language-switcher"
      [attr.aria-describedby]="language.switchFailed() ? 'language-switcher-error' : null"
      (change)="choose($event)"
    >
      @for (locale of language.available(); track locale.code) {
        <option [value]="locale.code" [selected]="locale.code === language.current()" [attr.lang]="locale.code">
          {{ locale.name }}
        </option>
      }
    </select>
    @if (language.switchFailed()) {
      <app-message kind="error" id="language-switcher-error">{{ 'error.network' | transloco }}</app-message>
    }
  `,
})
export class LanguageSwitcher {
  protected readonly language = inject(LanguageService);

  protected async choose(event: Event): Promise<void> {
    const select = event.target as HTMLSelectElement;
    await this.language.use(select.value);
    // After a failed switch the list would still show the language that did not load.
    select.value = this.language.current();
  }
}
