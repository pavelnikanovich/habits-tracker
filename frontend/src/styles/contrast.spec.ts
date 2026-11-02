import { themeColours } from '../testing/tokens';

// Contrast (FR-041, contracts/ui.md, "Contrast pairs"): the WCAG 2.2 relative luminance formula, computed here.
function luminance(colour: string): number {
  const channels = [1, 3, 5].map((start) => Number.parseInt(colour.slice(start, start + 2), 16) / 255);
  const [red, green, blue] = channels.map((value) => (value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4));
  return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
}

function contrast(foreground: string, background: string): number {
  const [lighter, darker] = [luminance(foreground), luminance(background)].sort((a, b) => b - a);
  return (lighter + 0.05) / (darker + 0.05);
}

// [foreground token, background token, threshold], every pair of contracts/ui.md, "Contrast pairs".
const pairs: [string, string, number][] = [
  ['text', 'bg', 4.5],
  ['text', 'surface', 4.5],
  ['muted', 'bg', 4.5],
  ['muted', 'surface', 4.5],
  ['accent', 'bg', 4.5],
  ['accent', 'surface', 4.5],
  ['bg', 'accent', 4.5],
  ['error', 'bg', 4.5],
  ['error', 'surface', 4.5],
  ['focus', 'bg', 3],
  ['focus', 'surface', 3],
  ['border', 'bg', 3],
  ['border', 'surface', 3],
];

describe('contrast', () => {
  it('the formula matches known values', () => {
    expect(contrast('#000000', '#ffffff')).toBe(21);
    expect(contrast('#777777', '#ffffff').toFixed(2)).toBe('4.48');
  });

  it('every pair meets its threshold in both themes', () => {
    const themes = themeColours();
    expect(Object.keys(themes).sort()).toEqual(['dark', 'light']);

    for (const [theme, colours] of Object.entries(themes)) {
      for (const [foreground, background, threshold] of pairs) {
        const pair = `${theme}: --${foreground} on --${background}`;
        expect(colours[foreground], pair).toMatch(/^#[0-9a-f]{6}$/i);
        expect(colours[background], pair).toMatch(/^#[0-9a-f]{6}$/i);
        expect(contrast(colours[foreground], colours[background]), pair).toBeGreaterThanOrEqual(threshold);
      }
    }
  });
});
