import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

/**
 * A page's heading until the task that builds the page replaces it: the route's title key. Like every page's `h1`,
 * it has `tabindex="-1"`, so it can take the focus after a navigation.
 */
@Component({
  selector: 'app-placeholder-page',
  imports: [TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h1 tabindex="-1">{{ heading | transloco }}</h1>`,
})
export class PlaceholderPage {
  protected readonly heading = inject(ActivatedRoute).snapshot.title ?? '';
}
