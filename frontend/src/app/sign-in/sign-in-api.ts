import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface CodeRequestAccepted {
  remaining: number;
  limit: number;
}

/** The HTTP calls of the sign-in; `SignInFlow` keeps the state and the wording of refusals. */
@Injectable({ providedIn: 'root' })
export class SignInApi {
  private readonly http = inject(HttpClient);

  /** `POST /api/code-requests`; rejects with the `HttpErrorResponse` of a refusal. */
  requestCode(email: string, locale: string): Promise<CodeRequestAccepted> {
    return firstValueFrom(this.http.post<CodeRequestAccepted>('/api/code-requests', { email, locale }));
  }
}
