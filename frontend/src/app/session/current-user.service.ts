import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Problem, problemOf } from '../api/problem';
import { LanguageService } from '../i18n/language.service';

/** `GET /api/me` and the answer of `POST /api/sessions`. */
export interface CurrentUser {
  email: string;
  timeZone: string;
  timeZoneRecognized: boolean;
  locale: string;
}

/** `unknown`: not asked yet; `loading`; `signed-in`; `signed-out` (not-signed-in answered); `failed` (no usable answer). */
export type UserStatus = 'unknown' | 'loading' | 'signed-in' | 'signed-out' | 'failed';

/**
 * Who is signed in: the answer of `GET /api/me`, asked once per visit and kept until the user signs out or a request
 * answers `not-signed-in`. The guards and the main screen read it, so the screens never ask twice.
 */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly http = inject(HttpClient);
  private readonly language = inject(LanguageService);
  private readonly state = signal<UserStatus>('unknown');
  private readonly current = signal<CurrentUser | null>(null);
  private readonly failure = signal<Problem | null>(null);
  private pending: Promise<void> | null = null;

  readonly status = this.state.asReadonly();
  readonly user = this.current.asReadonly();
  /** Why `GET /api/me` gave no user although the visitor may be signed in; set while the status is `failed`. */
  readonly problem = this.failure.asReadonly();

  /** Asks `GET /api/me`; a call while one runs waits for that one. */
  load(): Promise<void> {
    this.pending ??= this.ask().finally(() => (this.pending = null));
    return this.pending;
  }

  /** A sign-in succeeded: `user` is the answer of `POST /api/sessions`. */
  async signedIn(user: CurrentUser): Promise<void> {
    this.current.set(user);
    this.failure.set(null);
    this.state.set('signed-in');
    await this.applyLocale(user);
  }

  /** The visitor is known not to be signed in (signed out, or a request answered `not-signed-in`). */
  clear(): void {
    this.current.set(null);
    this.failure.set(null);
    this.state.set('signed-out');
  }

  private async ask(): Promise<void> {
    this.state.set('loading');
    this.failure.set(null);
    try {
      const user = await firstValueFrom(this.http.get<CurrentUser>('/api/me'));
      await this.signedIn(user);
    } catch (error) {
      const problem = problemOf(error);
      if (problem.code === 'not-signed-in') {
        this.clear();
      } else {
        this.failure.set(problem);
        this.state.set('failed');
      }
    }
  }

  // The locale stored with the user wins over this browser's choice (spec, Assumptions): the first sign-in stored the
  // page's language, a later one follows what the user changed it to. A failed load keeps the page as it was.
  private async applyLocale(user: CurrentUser): Promise<void> {
    await this.language.use(user.locale);
  }
}
