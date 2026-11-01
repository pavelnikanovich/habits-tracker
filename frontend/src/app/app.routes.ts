import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { PlaceholderPage } from './placeholder-page';
import { CodeStep } from './sign-in/code-step';
import { SignIn } from './sign-in/sign-in';
import { SignInFlow } from './sign-in/sign-in-flow.service';

// The code step is for a person who requested a code in this visit; a reload, or a link, starts at the email step.
const codeRequested: CanActivateFn = () => inject(SignInFlow).email() !== null || inject(Router).parseUrl('/sign-in');


// Each route's title is a translation key; TranslatedTitleStrategy turns it into the page's document title.
export const routes: Routes = [
  { path: 'sign-in', component: SignIn, title: 'signIn.title' },
  { path: 'sign-in/code', component: CodeStep, title: 'signIn.title', canActivate: [codeRequested] },
  { path: '', pathMatch: 'full', component: PlaceholderPage, title: 'main.title' },
  { path: 'privacy', component: PlaceholderPage, title: 'privacy.title' },
  { path: 'terms', component: PlaceholderPage, title: 'terms.title' },
  { path: '**', component: PlaceholderPage, title: 'notFound.title' },
];
