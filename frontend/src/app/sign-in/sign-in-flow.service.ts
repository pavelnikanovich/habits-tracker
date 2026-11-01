import { computed, inject, Injectable, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoService } from '@jsverse/transloco';
import { Problem, problemOf, problemText } from '../api/problem';
import { LanguageService } from '../i18n/language.service';
import { CurrentUserService } from '../session/current-user.service';
import { CodeRequestAccepted, SignInApi } from './sign-in-api';

/**
 * The sign-in's state between its two steps, in the application's memory only (contracts/ui.md, "Sign-in, code step"):
 * the email, the "Remember me" choice and the remaining count never go into the URL, where the browser history and the
 * proxy's log would keep them. It also sends the code request, which both steps do, and words a refusal.
 */
@Injectable({ providedIn: 'root' })
export class SignInFlow {
  private readonly api = inject(SignInApi);
  private readonly users = inject(CurrentUserService);
  private readonly language = inject(LanguageService);
  private readonly transloco = inject(TranslocoService);
  private readonly activeLanguage = toSignal(this.transloco.langChanges$);

  /** The email as sent (trimmed); null until a code was requested in this visit. */
  readonly email = signal<string | null>(null);
  readonly rememberMe = signal(false);
  readonly remaining = signal(0);
  readonly limit = signal(0);
  readonly sending = signal(false);
  readonly signingIn = signal(false);
  readonly refusal = signal<Problem | null>(null);

  /** The refusal in the page's language (`problemText`), worded again when the language changes. */
  readonly refusalText = computed(() => {
    this.activeLanguage();
    const refusal = this.refusal();
    if (refusal === null) return null;
    return problemText(refusal, (key, params) => this.transloco.translate(key, params), this.language.current(), new Date());
  });

  /** A code was sent to `email`: the code step shows it, and Back to the email step fills it in again. */
  begin(email: string, rememberMe: boolean, count: CodeRequestAccepted): void {
    this.email.set(email);
    this.rememberMe.set(rememberMe);
    this.remaining.set(count.remaining);
    this.limit.set(count.limit);
  }

  /** A sign-in succeeded or the visit starts over: nothing of the flow is kept. */
  clear(): void {
    this.email.set(null);
    this.rememberMe.set(false);
    this.remaining.set(0);
    this.limit.set(0);
    this.refusal.set(null);
  }

  clearRefusal(): void {
    this.refusal.set(null);
  }

  /**
   * Requests a code for `email` (already trimmed and checked) in the page's language. True when the code was sent
   * (`202`), with the flow holding the new count; false when it was refused or got no answer, with the refusal kept
   * for the step to show. A second call while one is running sends nothing.
   */
  async requestCode(email: string, rememberMe: boolean): Promise<boolean> {
    if (this.sending()) return false;

    this.sending.set(true);
    this.refusal.set(null);
    try {
      const accepted = await this.api.requestCode(email, this.language.current());
      this.begin(email, rememberMe, accepted);
      return true;
    } catch (error) {
      this.refusal.set(problemOf(error));
      return false;
    } finally {
      this.sending.set(false);
    }
  }

  /**
   * Signs in with a code already stripped and checked. True when the session opened and the user is known (the
   * step then calls `clear()` once it has left); false when it was refused or got no answer, with the refusal kept for the step to show. A second call
   * while one is running sends nothing.
   */
  async signIn(code: string): Promise<boolean> {
    if (this.signingIn()) return false;

    this.signingIn.set(true);
    this.refusal.set(null);
    try {
      const user = await this.api.signIn({
        email: this.email() ?? '',
        code,
        rememberMe: this.rememberMe(),
        timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone || null,
        locale: this.language.current(),
      });
      await this.users.signedIn(user);
      return true;
    } catch (error) {
      this.refusal.set(problemOf(error));
      return false;
    } finally {
      this.signingIn.set(false);
    }
  }
}
