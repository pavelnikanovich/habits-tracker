import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, GuardResult, Router, RouterStateSnapshot } from '@angular/router';
import { ann, setUpSignIn } from '../../testing/sign-in-setup';
import { CurrentUserService } from './current-user.service';
import { signedInGuard, signedOutGuard } from './signed-in.guard';

const route = {} as ActivatedRouteSnapshot;
const state = {} as RouterStateSnapshot;

describe('the guards of the signed-in screens', () => {
  let http: HttpTestingController;
  let users: CurrentUserService;

  beforeEach(async () => {
    ({ http } = await setUpSignIn('en', { fresh: true }));
    users = TestBed.inject(CurrentUserService);
  });

  afterEach(() => http.verify());

  const run = (guard: typeof signedInGuard) => TestBed.runInInjectionContext(() => guard(route, state));
  const urlOf = (result: GuardResult) => TestBed.inject(Router).serializeUrl(result as ReturnType<Router['parseUrl']>);

  async function visitorLoaded(): Promise<void> {
    const loaded = users.load();
    http.expectOne('/api/me').flush({ code: 'not-signed-in' }, { status: 401, statusText: 'Unauthorized' });
    await loaded;
  }

  it('a visitor is sent to /sign-in', async () => {
    await visitorLoaded();

    expect(urlOf(run(signedInGuard) as GuardResult)).toBe('/sign-in');
  });

  it('a signed-in user passes', () => {
    users.signedIn(ann);

    expect(run(signedInGuard)).toBe(true);
  });

  it('a signed-in user opening the sign-in is sent to /', async () => {
    users.signedIn(ann);

    expect(urlOf((await run(signedOutGuard)) as GuardResult)).toBe('/');
  });

  it('a user who turns out to be signed in is sent from the sign-in to / after GET /api/me answers', async () => {
    const result = run(signedOutGuard) as Promise<GuardResult>;
    http.expectOne('/api/me').flush(ann);

    expect(urlOf(await result)).toBe('/');
  });

  it('a signed-in user who opens /sign-in while GET /api/me is already running is sent to /', async () => {
    run(signedInGuard); // opening / starts the request
    const result = run(signedOutGuard) as Promise<GuardResult>;
    http.expectOne('/api/me').flush(ann);

    expect(urlOf(await result)).toBe('/');
  });

  it('a visitor may open the sign-in; the answer of GET /api/me is awaited', async () => {
    const result = run(signedOutGuard) as Promise<GuardResult>;
    http.expectOne('/api/me').flush({ code: 'not-signed-in' }, { status: 401, statusText: 'Unauthorized' });

    expect(await result).toBe(true);
  });
});
