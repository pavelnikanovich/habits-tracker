import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, Routes } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

/** A page's heading until the task that builds the page replaces it: the route's `heading` translation key. */
@Component({
  selector: 'app-placeholder-page',
  imports: [TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h1>{{ heading | transloco }}</h1>`,
})
class PlaceholderPage {
  protected readonly heading = inject(ActivatedRoute).snapshot.data['heading'] as string;
}

export const routes: Routes = [
  { path: 'sign-in', component: PlaceholderPage, data: { heading: 'signIn.title' } },
  { path: '', pathMatch: 'full', component: PlaceholderPage, data: { heading: 'main.title' } },
  { path: 'privacy', component: PlaceholderPage, data: { heading: 'privacy.title' } },
  { path: 'terms', component: PlaceholderPage, data: { heading: 'terms.title' } },
  { path: '**', component: PlaceholderPage, data: { heading: 'notFound.title' } },
];
