import { HttpClient } from '@angular/common/http';
import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { ann, setUpSignIn } from '../../testing/sign-in-setup';
import { CurrentUserService } from './current-user.service';

describe('notSignedInInterceptor', () => {
  let http: HttpTestingController;
  let users: CurrentUserService;

  beforeEach(async () => {
    ({ http } = await setUpSignIn('en'));
    users = TestBed.inject(CurrentUserService);
  });

  afterEach(() => http.verify());

  function answer(status: number, code: string): void {
    TestBed.inject(HttpClient).get('/api/habits').subscribe({ error: () => undefined });
    http.expectOne('/api/habits').flush({ code }, { status, statusText: 'x' });
  }

  it('not-signed-in after a successful sign-in forgets the user and opens /sign-in', async () => {
    users.signedIn(ann);

    answer(401, 'not-signed-in');
    await TestBed.inject(Router).navigateByUrl('/sign-in').catch(() => undefined);

    expect(users.user()).toBeNull();
    expect(TestBed.inject(Router).url).toBe('/sign-in');
  });

  it('another refusal changes nothing', () => {
    users.signedIn(ann);

    answer(401, 'code-incorrect');

    expect(users.user()).toEqual(ann);
  });
});
