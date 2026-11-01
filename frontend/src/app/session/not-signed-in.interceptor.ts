import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { problemOf } from '../api/problem';
import { CurrentUserService } from './current-user.service';

/**
 * A request answered `not-signed-in` while a user is signed in means the session ended or was revoked: forget the user
 * and open `/sign-in`. Before `GET /api/me` has answered, that answer is the ordinary one of a visitor and is left to
 * `CurrentUserService`. Task 15 adds the "Session ended" state of the sign-in page.
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
        void router.navigateByUrl('/sign-in');
      },
    }),
  );
};
