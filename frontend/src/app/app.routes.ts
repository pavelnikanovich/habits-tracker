import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { LegalPage } from './legal/legal-page';
import { Main } from './main/main';
import { NotFoundPage } from './not-found-page';
import { signedInGuard, signedOutGuard } from './session/signed-in.guard';
import { CodeStep } from './sign-in/code-step';
import { SignIn } from './sign-in/sign-in';
import { SignInFlow } from './sign-in/sign-in-flow.service';

// The code step is for a person who requested a code in this visit; a reload, or a link, starts at the email step.
const codeRequested: CanActivateFn = () => inject(SignInFlow).email() !== null || inject(Router).parseUrl('/sign-in');


// Each route's title is a translation key; TranslatedTitleStrategy turns it into the page's document title.
export const routes: Routes = [
  { path: 'sign-in', component: SignIn, title: 'signIn.title', canActivate: [signedOutGuard] },
  { path: 'sign-in/code', component: CodeStep, title: 'signIn.title', canActivate: [signedOutGuard, codeRequested] },
  { path: '', pathMatch: 'full', component: Main, title: 'main.title', canActivate: [signedInGuard] },
  { path: 'privacy', component: LegalPage, title: 'privacy.title', data: { page: 'privacy', sections: 15 } },
  { path: 'terms', component: LegalPage, title: 'terms.title', data: { page: 'terms', sections: 12 } },
  { path: '**', component: NotFoundPage, title: 'notFound.title' },
];
