import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import type { CurrentUser } from '../session/current-user.service';

export interface CodeRequestAccepted {
  remaining: number;
  limit: number;
}

export interface SessionEntry {
  email: string;
  code: string;
  rememberMe: boolean;
  /** The browser's IANA id; null when the browser reports none (the server then uses UTC). */
  timeZone: string | null;
  locale: string;
}

/** The HTTP calls of the sign-in; `SignInFlow` keeps the state and the wording of refusals. */
@Injectable({ providedIn: 'root' })
export class SignInApi {
  private readonly http = inject(HttpClient);

  /** `POST /api/code-requests`; rejects with the `HttpErrorResponse` of a refusal. */
  requestCode(email: string, locale: string): Promise<CodeRequestAccepted> {
    return firstValueFrom(this.http.post<CodeRequestAccepted>('/api/code-requests', { email, locale }));
  }

  /** `POST /api/sessions`; the answer is the current user, and the response sets the `session` cookie. */
  signIn(entry: SessionEntry): Promise<CurrentUser> {
    return firstValueFrom(this.http.post<CurrentUser>('/api/sessions', entry));
  }
}
