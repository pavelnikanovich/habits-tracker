import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { Problem, problemOf } from '../api/problem';
import { LanguageService } from '../i18n/language.service';
import { formatMoment } from '../time/format-moment';

interface CodeRequestAccepted {
  remaining: number;
  limit: number;
}

/**
 * The sign-in's state between its two steps, in the application's memory only (contracts/ui.md, "Sign-in, code step"):
 * the email, the "Remember me" choice and the remaining count never go into the URL, where the browser history and the
 * proxy's log would keep them. It also sends the code request, which both steps do, and words a refusal.
 */
@Injectable({ providedIn: 'root' })
export class SignInFlow {
  private readonly http = inject(HttpClient);
  private readonly language = inject(LanguageService);
  private readonly transloco = inject(TranslocoService);
  private readonly activeLanguage = toSignal(this.transloco.langChanges$);

  /** The email as sent (trimmed); null until a code was requested in this visit. */
  readonly email = signal<string | null>(null);
  readonly rememberMe = signal(false);
  readonly remaining = signal(0);
  readonly limit = signal(0);
  readonly sending = signal(false);
  readonly refusal = signal<Problem | null>(null);

  /** The refusal in the page's language: `error.<code>`, then when to try again where the response says it. */
  readonly refusalText = computed(() => {
    this.activeLanguage();
    const refusal = this.refusal();
    if (refusal === null) return null;

    const text = this.transloco.translate(`error.${refusal.code}`, { traceId: refusal.traceId ?? '' });
    if (refusal.retryAt === undefined) return text;

    const time = formatMoment(new Date(refusal.retryAt), new Date(), this.language.current(), browserTimeZone());
    return `${text} ${this.transloco.translate('signIn.retryAt', { time })}`;
  });

  /** A code was sent to `email`: the code step shows it, and Back to the email step fills it in again. */
  begin(email: string, rememberMe: boolean, count: CodeRequestAccepted): void {
    this.email.set(email);
    this.rememberMe.set(rememberMe);
    this.remaining.set(count.remaining);
    this.limit.set(count.limit);
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
      const accepted = await firstValueFrom(
        this.http.post<CodeRequestAccepted>('/api/code-requests', { email, locale: this.language.current() }),
      );
      this.begin(email, rememberMe, accepted);
      return true;
    } catch (error) {
      this.refusal.set(problemOf(error));
      return false;
    } finally {
      this.sending.set(false);
    }
  }
}

function browserTimeZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
}
