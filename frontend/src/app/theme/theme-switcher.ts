import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ThemeService } from './theme.service';

/**
 * The theme switcher: one button whose text names the theme it switches to. The button stays in place across a
 * switch, so the focus stays on it (contracts/ui.md, "Theme switcher").
 */
@Component({
  selector: 'app-theme-switcher',
  imports: [TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<button type="button" (click)="switch()">{{ label() | transloco }}</button>`,
})
export class ThemeSwitcher {
  private readonly theme = inject(ThemeService);

  protected readonly label = computed(() => (this.theme.current() === 'dark' ? 'theme.toLight' : 'theme.toDark'));

  protected switch(): void {
    this.theme.use(this.theme.current() === 'dark' ? 'light' : 'dark');
  }
}
