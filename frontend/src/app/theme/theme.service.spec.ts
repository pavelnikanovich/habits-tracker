import { TestBed } from '@angular/core/testing';
import { themeColours } from '../../testing/tokens';
import { Theme, ThemeService } from './theme.service';

const refuse = () => {
  throw new DOMException('Site data is blocked', 'SecurityError');
};

// The theme (FR-042, contracts/ui.md, "Themes"): dark until the person chooses, whatever the browser prefers.
describe('ThemeService', () => {
  let themeColour: HTMLMetaElement;

  beforeEach(() => {
    localStorage.clear();
    // index.html ships data-theme="dark"; without it, the attribute the service sets is what the test sees.
    document.documentElement.removeAttribute('data-theme');
    themeColour = document.createElement('meta');
    themeColour.name = 'theme-color';
    themeColour.content = '#0a0f0b';
    document.head.append(themeColour);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    themeColour.remove();
    localStorage.clear();
  });

  it.each<[string, Theme, string, () => void]>([
    ['nothing stored', 'dark', '#0a0f0b', () => undefined],
    ['light', 'light', '#f3f7f2', () => localStorage.setItem('theme', 'light')],
    ['dark', 'dark', '#0a0f0b', () => localStorage.setItem('theme', 'dark')],
    ['blue', 'dark', '#0a0f0b', () => localStorage.setItem('theme', 'blue')],
    ['getItem throws', 'dark', '#0a0f0b', () => vi.spyOn(Storage.prototype, 'getItem').mockImplementation(refuse)],
  ])('applies the stored choice: %s → %s', (_, applied, colour, store) => {
    store();

    const theme = TestBed.inject(ThemeService);

    expect(theme.current()).toBe(applied);
    expect(document.documentElement.dataset['theme']).toBe(applied);
    expect(themeColour.content).toBe(colour);
  });

  it('use sets the attribute, the theme colour and the choice', () => {
    const theme = TestBed.inject(ThemeService);

    theme.use('light');

    expect(theme.current()).toBe('light');
    expect(document.documentElement.dataset['theme']).toBe('light');
    expect(themeColour.content).toBe('#f3f7f2');
    expect(localStorage.getItem('theme')).toBe('light');
  });

  it('a choice that cannot be stored still applies', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(refuse);
    const theme = TestBed.inject(ThemeService);

    theme.use('light');

    expect(document.documentElement.dataset['theme']).toBe('light');
    expect(theme.current()).toBe('light');
  });

  it("the theme colour is the theme's --bg in tokens.css", () => {
    const tokens = themeColours();
    const theme = TestBed.inject(ThemeService);

    for (const name of ['light', 'dark'] as const) {
      theme.use(name);
      expect(themeColour.content, name).toBe(tokens[name]?.['bg']);
    }
  });
});
