import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Every message of every page (contracts/ui.md, "Message"): an error is announced at once (`role="alert"`), an info
 * politely (`role="status"`). The element itself carries the id, so a field names it in `aria-describedby`; the text
 * is the content, already translated by the page.
 */
@Component({
  selector: 'app-message',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.id]': 'id()', '[attr.role]': 'role()', '[class.error]': "kind() === 'error'" },
  template: `<ng-content />`,
  styleUrl: './message.css',
})
export class MessageComponent {
  readonly kind = input.required<'error' | 'info'>();
  readonly id = input.required<string>();

  protected readonly role = computed(() => (this.kind() === 'error' ? 'alert' : 'status'));
}
