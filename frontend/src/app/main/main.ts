import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  Injector,
  signal,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { Problem, problemText } from '../api/problem';
import { LanguageService } from '../i18n/language.service';
import { MessageComponent } from '../message/message';
import { CurrentUserService } from '../session/current-user.service';

/** `main.loading` appears after this long without an answer from `GET /api/me`; a quicker answer shows no flicker. */
const LOADING_DELAY_MS = 1000;

/**
 * The main screen (contracts/ui.md, "Main screen"): no habits yet, the time zone notice, the loading and failure states
 * of `GET /api/me`, and the sign-out actions. Sign out everywhere asks on the page, not in a dialog, and moves the focus
 * to "Cancel", so a second stray `Enter` signs nothing out.
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
  private readonly injector = inject(Injector);
  private readonly activeLanguage = toSignal(this.transloco.langChanges$);
  private readonly actionProblem = signal<Problem | null>(null);
  private readonly everywhereButton = viewChild<ElementRef<HTMLButtonElement>>('everywhereRef');
  private readonly cancelButton = viewChild<ElementRef<HTMLButtonElement>>('cancelRef');

  /** Whether sign out everywhere waits for its confirmation. */
  protected readonly confirming = signal(false);
  /** Whether a sign-out request runs; the buttons are `aria-disabled` then, never `disabled`, so the focus stays. */
  protected readonly signingOut = signal(false);

  /** Why `GET /api/me` gave no user, in the page's language (`problemText`), worded again when the language changes. */
  protected readonly loadError = computed(() => this.worded(this.users.problem()));
  /** Why a sign-out failed, worded the same way. */
  protected readonly actionError = computed(() => this.worded(this.actionProblem()));

  constructor() {
    const timer = setTimeout(() => this.slow.set(true), LOADING_DELAY_MS);
    inject(DestroyRef).onDestroy(() => clearTimeout(timer));

    // The guard lets the screen open while the answer is awaited; a visitor whose first answer is not-signed-in is
    // sent on when it comes. Every other way out of the screen has its own owner (one per transition): a session that
    // ended while the page was open is the interceptor's, which navigates with the reason; sign out is this class's
    // own, below. A navigation here would supersede theirs and lose the reason.
    let signedIn = false;
    effect(() => {
      if (this.users.user() !== null) signedIn = true;
      else if (this.users.status() === 'signed-out' && !signedIn) void this.router.navigateByUrl('/sign-in');
    });
  }

  protected signOut(): Promise<void> {
    return this.end(() => this.users.signOut());
  }

  protected askSignOutEverywhere(): void {
    if (this.signingOut()) return;
    this.confirming.set(true);
    afterNextRender(() => this.cancelButton()?.nativeElement.focus(), { injector: this.injector });
  }

  protected cancel(): void {
    if (this.signingOut()) return;
    this.confirming.set(false);
    afterNextRender(() => this.everywhereButton()?.nativeElement.focus(), { injector: this.injector });
  }

  protected signOutEverywhere(): Promise<void> {
    return this.end(() => this.users.signOutEverywhere());
  }

  // The user is forgotten by the service before the page navigates (see CurrentUserService); a second activation while
  // the request runs is ignored. A failure is worded on the page and the buttons work again.
  private async end(request: () => Promise<Problem | null>): Promise<void> {
    if (this.signingOut()) return;
    this.signingOut.set(true);
    this.actionProblem.set(null);
    const problem = await request();
    if (problem === null) {
      await this.router.navigateByUrl('/sign-in');
      return;
    }

    this.signingOut.set(false);
    // A session that ended is the interceptor's business: it moves the page on and says so.
    if (problem.code !== 'not-signed-in') this.actionProblem.set(problem);
    if (this.confirming()) this.cancel();
  }

  private worded(problem: Problem | null): string | null {
    this.activeLanguage();
    if (problem === null) return null;
    return problemText(problem, (key, params) => this.transloco.translate(key, params), this.language.current(), new Date());
  }
}
