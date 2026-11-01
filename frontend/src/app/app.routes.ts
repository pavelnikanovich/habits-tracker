import { Routes } from '@angular/router';
import { PlaceholderPage } from './placeholder-page';

export const routes: Routes = [
  { path: 'sign-in', component: PlaceholderPage, data: { heading: 'signIn.title' } },
  { path: '', pathMatch: 'full', component: PlaceholderPage, data: { heading: 'main.title' } },
  { path: 'privacy', component: PlaceholderPage, data: { heading: 'privacy.title' } },
  { path: 'terms', component: PlaceholderPage, data: { heading: 'terms.title' } },
  { path: '**', component: PlaceholderPage, data: { heading: 'notFound.title' } },
];
