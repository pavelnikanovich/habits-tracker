import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from './language.service';

/** The language switcher: each language under its own name, from `GET /api/locales`. */
@Component({
  selector: 'app-language-switcher',
  imports: [TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <label for="language-switcher">{{ 'language.switcher' | transloco }}</label>
    <select id="language-switcher" (change)="choose($event)">
      @for (locale of language.available(); track locale.code) {
        <option [value]="locale.code" [selected]="locale.code === language.current()" [attr.lang]="locale.code">
          {{ locale.name }}
        </option>
      }
    </select>
  `,
})
export class LanguageSwitcher {
  protected readonly language = inject(LanguageService);

  protected choose(event: Event): void {
    this.language.use((event.target as HTMLSelectElement).value);
  }
}
