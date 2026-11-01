import { Routes } from '@angular/router';
import { PlaceholderPage } from './placeholder-page';

// Each route's title is a translation key; TranslatedTitleStrategy turns it into the page's document title.
export const routes: Routes = [
  { path: 'sign-in', component: PlaceholderPage, title: 'signIn.title' },
  { path: '', pathMatch: 'full', component: PlaceholderPage, title: 'main.title' },
  { path: 'privacy', component: PlaceholderPage, title: 'privacy.title' },
  { path: 'terms', component: PlaceholderPage, title: 'terms.title' },
  { path: '**', component: PlaceholderPage, title: 'notFound.title' },
];
