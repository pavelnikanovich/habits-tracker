import { DOCUMENT } from '@angular/common';
import { inject, Injectable, signal } from '@angular/core';

export type Theme = 'dark' | 'light';

const storageKey = 'theme';

// Each theme's --bg of tokens.css, for <meta name="theme-color">, so a phone browser's toolbar matches the page. A
// unit test compares them with tokens.css.
const themeColours: Record<Theme, string> = { dark: '#0a0f0b', light: '#f3f7f2' };

/**
 * The page's theme (FR-042, contracts/ui.md, "Themes"): dark until the person chooses, whatever the browser prefers.
 * Creating the service applies the choice kept in `localStorage`; a value other than `light` or `dark`, or storage
 * that throws, leaves the dark theme. The choice never goes to the server.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly theme = signal<Theme>('dark');

  readonly current = this.theme.asReadonly();

  constructor() {
    this.apply(this.storedChoice() ?? 'dark');
  }

  /** Switches the page at once, without a reload, and keeps the choice. */
  use(theme: Theme): void {
    this.apply(theme);
    try {
      localStorage.setItem(storageKey, theme);
    } catch {
      // Storage refused (blocked site data): the page still switches, the choice lasts until the page closes.
    }
  }

  private storedChoice(): Theme | null {
    try {
      const stored = localStorage.getItem(storageKey);
      return stored === 'dark' || stored === 'light' ? stored : null;
    } catch {
      return null; // Storage refused: as if nothing was chosen.
    }
  }

  private apply(theme: Theme): void {
    this.document.documentElement.dataset['theme'] = theme;
    this.document.querySelector('meta[name="theme-color"]')?.setAttribute('content', themeColours[theme]);
    this.theme.set(theme);
  }
}
