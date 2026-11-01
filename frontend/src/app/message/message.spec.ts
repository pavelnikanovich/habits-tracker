import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MessageComponent } from './message';

@Component({
  imports: [MessageComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <input aria-describedby="email-error" aria-label="Email" />
    <app-message kind="error" id="email-error">Enter a valid email.</app-message>
    <app-message kind="info" id="loading">Loading…</app-message>
  `,
})
class MessagesPage {}

// One component for every message of every page (contracts/ui.md, "Message").
describe('MessageComponent', () => {
  async function render(): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(MessagesPage);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('an error is announced', async () => {
    const page = await render();

    const error = page.querySelector('#email-error');
    expect(error?.getAttribute('role')).toBe('alert');
    expect(error?.textContent?.trim()).toBe('Enter a valid email.');
  });

  it('an info is a status', async () => {
    const page = await render();

    const info = page.querySelector('#loading');
    expect(info?.getAttribute('role')).toBe('status');
    expect(info?.textContent?.trim()).toBe('Loading…');
  });

  it('the id is set for aria-describedby', async () => {
    const page = await render();

    const described = page.querySelector('input')?.getAttribute('aria-describedby');
    const targets = page.querySelectorAll(`#${described}`);
    expect(targets.length, 'one element carries the id').toBe(1);
    expect(targets[0].getAttribute('role')).toBe('alert');
    expect(targets[0].textContent?.trim()).toBe('Enter a valid email.');
  });
});
