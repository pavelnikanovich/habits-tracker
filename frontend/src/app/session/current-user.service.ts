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
  /** Counts the locale changes; one whose number is no longer the latest when its PUT answers was overtaken. */
  private latestLocaleChange = 0;
  private sendingLocale = false;
  private waitingLocale: { code: string; turn: number; done: (problem: Problem | null) => void } | null = null;

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

  /**
   * Changes the signed-in user's locale (`PUT /api/me/locale`, FR-023), then shows it; this browser's choice stays as it
   * was. The PUTs go one at a time, so the server applies them in the order of the choices (R-89): a choice made while
   * one is in flight waits, and only the latest waiting choice is sent; an overtaken choice shows nothing and reports
   * nothing. The page and the server both end in the latest choice. Returns why the change was refused, or null.
   */
  changeLocale(code: string): Promise<Problem | null> {
    const turn = ++this.latestLocaleChange;
    return new Promise((done) => {
      this.waitingLocale?.done(null);
      this.waitingLocale = { code, turn, done };
      if (!this.sendingLocale) void this.sendLocales();
    });
  }

  private async sendLocales(): Promise<void> {
    this.sendingLocale = true;
    while (this.waitingLocale !== null) {
      const { code, turn, done } = this.waitingLocale;
      this.waitingLocale = null;
      try {
        await firstValueFrom(this.http.put('/api/me/locale', { locale: code }));
      } catch (error) {
        done(turn === this.latestLocaleChange ? problemOf(error) : null);
        continue;
      }
      // A choice that waits is newer than this one, so nothing is shown for this one.
      if (turn === this.latestLocaleChange) await this.language.show(code);
      done(null);
    }
    this.sendingLocale = false;
  }

  /** Sign out (US-1.4): revokes this browser's session. Returns why it failed, or null once the user is forgotten. */
  signOut(): Promise<Problem | null> {
    return this.end('/api/sessions/current');
  }

  /** Sign out everywhere (US-1.5): revokes every session of the user. Returns why it failed, or null once the user is forgotten. */
  signOutEverywhere(): Promise<Problem | null> {
    return this.end('/api/sessions');
  }

  // The user is forgotten before the caller navigates, so a late request of this tab that answers `not-signed-in`
  // finds no user and never brings "Session ended" to a person who has just signed out.
  private async end(url: string): Promise<Problem | null> {
    try {
      await firstValueFrom(this.http.delete(url));
    } catch (error) {
      return problemOf(error);
    }
    this.clear();
    return null;
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
  // page's language, a later one follows what the user changed it to. It is shown, not stored as this browser's
  // choice, so after sign-out the next visitor on the device does not inherit it. A failed load keeps the page as it was.
  private async applyLocale(user: CurrentUser): Promise<void> {
    await this.language.show(user.locale);
  }
}
