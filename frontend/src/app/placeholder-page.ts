import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

/** A page's heading until the task that builds the page replaces it: the route's `heading` translation key. */
@Component({
  selector: 'app-placeholder-page',
  imports: [TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h1>{{ heading | transloco }}</h1>`,
})
export class PlaceholderPage {
  protected readonly heading = inject(ActivatedRoute).snapshot.data['heading'] as string;
}
