import { afterNextRender, ChangeDetectionStrategy, Component, computed, ElementRef, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ProblemCode } from '../api/problem';
import { MessageComponent } from '../message/message';
import { ONE_TIME_CODE_PATTERN } from '../validation/patterns';
import { SignInFlow } from './sign-in-flow.service';

/** The refusals that belong to the code field: the field names the message in `aria-describedby`. */
const FIELD_REFUSALS = new Set<ProblemCode>(['invalid-code-format', 'code-incorrect']);

/**
 * The code step of the sign-in (contracts/ui.md, "Sign-in, code step"): where the code went, how many requests are
 * left, the code field, a new code and another email. Without a code request in this visit the route redirects to
 * `/sign-in`, so the email is always known here. The code is never sent by itself at the sixth digit: the person
 * presses "Sign in" (or Enter), so a mistyped digit costs no entry attempt.
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
  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('codeField');

  protected readonly code = signal('');
  protected readonly fieldInvalid = computed(() => {
    const refusal = this.flow.refusal();
    return refusal !== null && FIELD_REFUSALS.has(refusal.code);
  });

  constructor() {
    this.flow.clearRefusal();
    // The person came here to type the code, so unlike other pages this one puts the focus in its field.
    afterNextRender(() => this.focusField());
  }

  protected typeCode(event: Event): void {
    this.code.set((event.target as HTMLInputElement).value);
  }

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.flow.signingIn()) return;

    // A code is pasted or autofilled with spaces; they are not part of it.
    const code = this.code().replace(/\s/g, '');
    if (!ONE_TIME_CODE_PATTERN.test(code)) {
      this.flow.refusal.set({ code: 'invalid-code-format' });
      this.focusField();
      return;
    }

    if (await this.flow.signIn(code)) {
      await this.router.navigateByUrl('/');
      this.flow.clear();
      return;
    }

    switch (this.flow.refusal()?.code) {
      case 'code-incorrect':
        this.clearField();
        break;
      case 'new-code-required':
        // The email stays in the flow, so the email step is filled in; the message is shown there.
        await this.router.navigateByUrl('/sign-in');
        break;
      default:
        break;
    }
  }

  protected async resend(): Promise<void> {
    if (this.flow.sending()) return;

    // The earlier code no longer works (FR-002), so the field starts empty.
    if (await this.flow.requestCode(this.flow.email() ?? '', this.flow.rememberMe())) this.clearField();
  }

  protected async useAnotherEmail(): Promise<void> {
    await this.router.navigateByUrl('/sign-in');
  }

  private clearField(): void {
    this.code.set('');
    this.field().nativeElement.value = '';
    this.focusField();
  }

  private focusField(): void {
    this.field().nativeElement.focus();
  }
}
