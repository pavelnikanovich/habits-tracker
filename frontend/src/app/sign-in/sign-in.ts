import { ChangeDetectionStrategy, Component, computed, ElementRef, inject, signal, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { MessageComponent } from '../message/message';
import { SESSION_ENDED } from '../session/not-signed-in.interceptor';
import { EMAIL_MAX_LENGTH, EMAIL_PATTERN } from '../validation/patterns';
import { SignInFlow } from './sign-in-flow.service';

/**
 * The email step of the sign-in (contracts/ui.md, "Sign-in, email step"). The page checks the email itself before it
 * sends it (research R18); a refusal of the server shows under the form and leaves the field as it is. A sent code
 * opens the code step, and the email, the checkbox and the count go there through `SignInFlow`, never the URL.
 */
@Component({
  selector: 'app-sign-in',
  imports: [TranslocoPipe, RouterLink, MessageComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sign-in.html',
  styleUrl: './sign-in.css',
})
export class SignIn {
  protected readonly flow = inject(SignInFlow);
  private readonly router = inject(Router);
  /** Arriving after a request answered not-signed-in for a signed-in user: the reason is in the navigation state, not the URL. */
  protected readonly sessionEnded = this.router.currentNavigation()?.extras.state?.['reason'] === SESSION_ENDED;
  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('emailField');

  // Back from the code step, and "Use another email", come back with what the person had entered.
  protected readonly email = signal(this.flow.email() ?? '');
  protected readonly rememberMe = signal(this.flow.rememberMe());
  protected readonly invalid = signal(false);
  protected readonly describedBy = computed(() => (this.invalid() ? 'email-error' : null));

  constructor() {
    // The code step sends a person back here with `new-code-required`; its message stays until the next request.
    if (this.flow.refusal()?.code !== 'new-code-required') this.flow.clearRefusal();
  }

  protected typeEmail(event: Event): void {
    this.email.set((event.target as HTMLInputElement).value);
  }

  protected chooseRememberMe(event: Event): void {
    this.rememberMe.set((event.target as HTMLInputElement).checked);
  }

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.flow.sending()) return;

    const email = this.email().trim();
    this.invalid.set(!(email.length <= EMAIL_MAX_LENGTH && EMAIL_PATTERN.test(email)));
    if (this.invalid()) {
      this.flow.clearRefusal();
      this.field().nativeElement.focus();
      return;
    }

    if (await this.flow.requestCode(email, this.rememberMe())) await this.router.navigateByUrl('/sign-in/code');
  }
}
