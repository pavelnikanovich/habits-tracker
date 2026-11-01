import { HttpClient } from '@angular/common/http';
import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { ann, settle, setUpSignIn } from '../../testing/sign-in-setup';
import { CurrentUserService } from './current-user.service';

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
    TestBed.inject(HttpClient).get('/api/habits').subscribe({ error: () => undefined });
    http.expectOne('/api/habits').flush({ code }, { status, statusText: 'x' });
    await settle(harness);
  }

  it('not-signed-in after a successful sign-in forgets the user and opens /sign-in', async () => {
    await answer(401, 'not-signed-in');

    expect(users.user()).toBeNull();
    expect(TestBed.inject(Router).url).toBe('/sign-in');
  });

  it('another refusal changes nothing', async () => {
    await answer(401, 'code-incorrect');

    expect(users.user()).toEqual(ann);
    expect(TestBed.inject(Router).url).toBe('/privacy');
  });
});
