import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

/**
 * The page of any route no other route matches (contracts/ui.md, "Not found"): the route's title key as its heading
 * and a link home to `/`. Like every page's `h1`, it has `tabindex="-1"`, so it can take the focus after a navigation.
 */
@Component({
  selector: 'app-not-found-page',
  imports: [TranslocoPipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 tabindex="-1">{{ heading | transloco }}</h1>
    <p><a routerLink="/">{{ 'notFound.home' | transloco }}</a></p>
  `,
})
export class NotFoundPage {
  protected readonly heading = inject(ActivatedRoute).snapshot.title ?? '';
}
