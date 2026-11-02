import { readFileSync } from 'node:fs';

export type ThemeName = 'dark' | 'light';

/**
 * The colour tokens of each theme in `src/styles/tokens.css`, from its blocks `[data-theme="<theme>"] { … }`:
 * token name without the leading `--`, mapped to its `#rrggbb` value.
 */
export function themeColours(): Partial<Record<ThemeName, Record<string, string>>> {
  const css = readFileSync('src/styles/tokens.css', 'utf8');
  const themes: Partial<Record<ThemeName, Record<string, string>>> = {};
  for (const [, theme, block] of css.matchAll(/\[data-theme="(dark|light)"\]\s*\{([^}]*)\}/g)) {
    const colours = [...block.matchAll(/--([\w-]+):\s*(#[0-9a-f]{6})\s*;/gi)].map(([, name, value]) => [name, value]);
    themes[theme as ThemeName] = Object.fromEntries(colours);
  }
  return themes;
}
