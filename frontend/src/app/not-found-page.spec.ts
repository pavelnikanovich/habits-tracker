import { Location } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { setUpSignIn } from '../testing/sign-in-setup';
import { NotFoundPage } from './not-found-page';

// contracts/ui.md, "Not found (any other route)": the heading `notFound.title` and the link `notFound.home` to `/`.
describe('Not found page', () => {
  it('shows its heading and a link home, which opens /', async () => {
    const { harness } = await setUpSignIn('en');
    expect(await harness.navigateByUrl('/no-such-page', NotFoundPage)).toBeInstanceOf(NotFoundPage);
    harness.detectChanges();
    const page = harness.routeNativeElement as HTMLElement;

    expect(page.querySelector('h1')?.textContent?.trim()).toBe('Page not found');
    const home = page.querySelector<HTMLAnchorElement>('a[href="/"]');
    expect(home?.textContent?.trim()).toBe('Go to the main screen');

    home?.click();
    await harness.fixture.whenStable();
    // `/` is for a signed-in user: the visitor of setUpSignIn is not, so the guard sends them on to the sign-in.
    expect(TestBed.inject(Location).path()).toBe('/sign-in');
  });
});
