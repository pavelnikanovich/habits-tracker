import { HttpClient } from '@angular/common/http';
import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { ann, settle, setUpSignIn, texts } from '../../testing/sign-in-setup';
import { Main } from '../main/main';
import { CurrentUserService } from './current-user.service';

const NO_CONTENT = { status: 204, statusText: 'No Content' };
const CURRENT_URL = '/api/sessions/current';
const UNAUTHORIZED = { status: 401, statusText: 'Unauthorized' };
const SIGN_IN = '/sign-in';
const NOT_SIGNED_IN = 'not-signed-in';
const HABITS_URL = '/api/habits';
const infoOf = (harness: RouterTestingHarness) =>
  (harness.routeNativeElement as HTMLElement).querySelector('app-message[role="status"]')?.textContent?.trim();

describe('notSignedInInterceptor', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;
  let users: CurrentUserService;

  beforeEach(async () => {
    ({ harness, http } = await setUpSignIn('en'));
    users = TestBed.inject(CurrentUserService);
    await users.signedIn(ann);
    await harness.navigateByUrl('/privacy');
  });

  afterEach(() => http.verify());

  async function answer(status: number, code: string): Promise<void> {
    TestBed.inject(HttpClient).get(HABITS_URL).subscribe({ error: () => undefined });
    http.expectOne(HABITS_URL).flush({ code }, { status, statusText: 'x' });
    await settle(harness);
  }

  it('not-signed-in after a successful sign-in forgets the user and opens /sign-in', async () => {
    await answer(401, NOT_SIGNED_IN);

    expect(users.user()).toBeNull();
    expect(TestBed.inject(Router).url).toBe(SIGN_IN);
  });

  it('a 401 after a loaded user brings the session-ended state', async () => {
    await answer(401, NOT_SIGNED_IN);

    expect(TestBed.inject(Router).url).toBe(SIGN_IN);
    expect(infoOf(harness)).toBe(texts('signIn')['sessionEnded']);
  });

  it('own sign out brings plain /sign-in, even if a late request answers 401', async () => {
    const late = TestBed.inject(HttpClient).get(HABITS_URL);
    late.subscribe({ error: () => undefined });
    const request = http.expectOne(HABITS_URL);

    const signingOut = users.signOut();
    http.expectOne(CURRENT_URL).flush(null, NO_CONTENT);
    await signingOut;
    await TestBed.inject(Router).navigateByUrl(SIGN_IN);
    request.flush({ code: NOT_SIGNED_IN }, UNAUTHORIZED);
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe(SIGN_IN);
    expect(infoOf(harness)).toBeUndefined();
  });

  it('another refusal changes nothing', async () => {
    await answer(401, 'code-incorrect');

    expect(users.user()).toEqual(ann);
    expect(TestBed.inject(Router).url).toBe('/privacy');
  });
});

describe('notSignedInInterceptor, with no user loaded', () => {
  it('a 401 with no user loaded brings plain /sign-in', async () => {
    const { harness, http } = await setUpSignIn('en', { fresh: true });
    await harness.navigateByUrl('/', Main);

    http.expectOne('/api/me').flush({ code: NOT_SIGNED_IN }, UNAUTHORIZED);
    await settle(harness);
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe(SIGN_IN);
    expect(infoOf(harness)).toBeUndefined();
    http.verify();
  });
});
