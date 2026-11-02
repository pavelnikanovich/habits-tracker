import { Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { filter, ReplaySubject, switchMap } from 'rxjs';

/**
 * The page title (contracts/ui.md, "Page title and focus"): a route's `title` is a translation key, and
 * `document.title` is `<page title> — <app.name>` in the page's language, set again on a language switch.
 */
@Injectable({ providedIn: 'root' })
export class TranslatedTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);
  private readonly pageTitleKey = new ReplaySubject<string | undefined>(1);

  constructor() {
    super();
    // Without a language, selectTranslate follows the active language and waits for its translation file.
    this.pageTitleKey
      .pipe(
        filter((key) => key !== undefined),
        switchMap((key) => this.transloco.selectTranslate<string[]>([key, 'app.name'])),
        takeUntilDestroyed(),
      )
      .subscribe(([page, app]) => this.title.setTitle(`${page} — ${app}`));
  }

  override updateTitle(snapshot: RouterStateSnapshot): void {
    this.pageTitleKey.next(this.buildTitle(snapshot));
  }
}
