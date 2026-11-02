import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CurrentUserService } from './current-user.service';

/**
 * The main screen is for a signed-in user; a visitor goes to `/sign-in` with no query, since the application takes no
 * return address (a parameter naming another domain could never be followed). While `GET /api/me` has not answered the
 * screen opens and waits itself: it shows the loading and failure states, and sends a visitor on once the answer is in.
 */
export const signedInGuard: CanActivateFn = () => {
  const users = inject(CurrentUserService);
  if (users.status() === 'signed-out') return inject(Router).parseUrl('/sign-in');

  if (users.status() === 'unknown' || users.status() === 'failed') void users.load();
  return true;
};

/**
 * The sign-in pages are for a visitor: a signed-in user opening them goes to `/`. This waits for `GET /api/me`, so the
 * page of a signed-in user never flashes; when no answer comes the page opens, since it can still try to sign in.
 */
export const signedOutGuard: CanActivateFn = async () => {
  const users = inject(CurrentUserService);
  const router = inject(Router); // inject() only works before the first await
  // `loading`: signedInGuard may have started the request already; load() merges the calls.
  if (users.status() !== 'signed-in' && users.status() !== 'signed-out') await users.load();
  return users.status() === 'signed-in' ? router.parseUrl('/') : true;
};
