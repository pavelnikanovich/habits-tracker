import { readFileSync } from 'node:fs';
import { join } from 'node:path';

/** The texts of a translation file that the end-to-end tests compare the page against. */
export interface Translation {
  app: { name: string };
  language: { switcher: string };
  nav: { privacy: string; terms: string };
  theme: { toLight: string; toDark: string };
  signIn: { title: string };
  privacy: { title: string };
  terms: { title: string };
  notFound: { title: string };
}

export function translation(code: string): Translation {
  return JSON.parse(readFileSync(join(__dirname, '..', '..', 'public', 'i18n', `${code}.json`), 'utf8')) as Translation;
}
