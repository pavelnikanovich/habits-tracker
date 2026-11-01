import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { problemOf } from '../api/problem';
import { CurrentUserService } from './current-user.service';

/** The navigation state `reason` that makes the sign-in page say the session ended. */
export const SESSION_ENDED = 'session-ended';

/**
 * A request answered `not-signed-in` while a user is signed in means the session ended or was revoked: forget the user
 * and open `/sign-in`. Before `GET /api/me` has answered, that answer is the ordinary one of a visitor and is left to
 * `CurrentUserService`. The reason goes in the router's navigation state, never in the URL, and only for a user who was
 * signed in: the sign-in page then says "Session ended".
 */
export const notSignedInInterceptor: HttpInterceptorFn = (request, next) => {
  const users = inject(CurrentUserService);
  const router = inject(Router);
  return next(request).pipe(
    tap({
      error: (error: unknown) => {
        if (!(error instanceof HttpErrorResponse) || error.status !== 401) return;
        if (users.user() === null || problemOf(error).code !== 'not-signed-in') return;

        users.clear();
        void router.navigateByUrl('/sign-in', { state: { reason: SESSION_ENDED } });
      },
    }),
  );
};
