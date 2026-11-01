import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { MessageComponent } from '../message/message';
import { SignInFlow } from './sign-in-flow.service';

/**
 * The code step of the sign-in (contracts/ui.md, "Sign-in, code step"): where the code went, how many requests are
 * left, a new code and another email. Task 13 adds the code field. Without a code request in this visit the route
 * redirects to `/sign-in`, so the email is always known here.
 */
@Component({
  selector: 'app-code-step',
  imports: [TranslocoPipe, MessageComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './code-step.html',
  styleUrl: './code-step.css',
})
export class CodeStep {
  protected readonly flow = inject(SignInFlow);
  private readonly router = inject(Router);

  constructor() {
    this.flow.clearRefusal();
  }

  protected async resend(): Promise<void> {
    await this.flow.requestCode(this.flow.email() ?? '', this.flow.rememberMe());
  }

  protected async useAnotherEmail(): Promise<void> {
    await this.router.navigateByUrl('/sign-in');
  }
}
