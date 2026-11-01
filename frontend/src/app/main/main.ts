import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { problemText } from '../api/problem';
import { LanguageService } from '../i18n/language.service';
import { MessageComponent } from '../message/message';
import { CurrentUserService } from '../session/current-user.service';

/** `main.loading` appears after this long without an answer from `GET /api/me`; a quicker answer shows no flicker. */
const LOADING_DELAY_MS = 1000;

/**
 * The main screen (contracts/ui.md, "Main screen"). Task 13 builds the frame: no habits yet, the time zone notice and
 * the loading and failure states of `GET /api/me`. Task 15 adds the sign-out actions.
 */
@Component({
  selector: 'app-main',
  imports: [TranslocoPipe, MessageComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './main.html',
  styleUrl: './main.css',
})
export class Main {
  protected readonly users = inject(CurrentUserService);
  protected readonly slow = signal(false);
  private readonly router = inject(Router);
  private readonly language = inject(LanguageService);
  private readonly transloco = inject(TranslocoService);
  private readonly activeLanguage = toSignal(this.transloco.langChanges$);

  /** Why `GET /api/me` gave no user, in the page's language (`problemText`), worded again when the language changes. */
  protected readonly loadError = computed(() => {
    this.activeLanguage();
    const problem = this.users.problem();
    if (problem === null) return null;
    return problemText(problem, (key, params) => this.transloco.translate(key, params), this.language.current(), new Date());
  });

  constructor() {
    const timer = setTimeout(() => this.slow.set(true), LOADING_DELAY_MS);
    inject(DestroyRef).onDestroy(() => clearTimeout(timer));

    // The guard lets the screen open while the answer is awaited; a visitor is sent on when it comes.
    effect(() => {
      if (this.users.status() === 'signed-out') void this.router.navigateByUrl('/sign-in');
    });
  }
}
