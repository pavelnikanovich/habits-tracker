import { Translation } from '@jsverse/transloco';
import { readTranslation, translationCodes } from '../../testing/translation-files';

// Every user-visible string lives in each translation file (FR-020, SC-012).
function texts(translation: Translation, prefix = ''): Map<string, unknown> {
  const flat = new Map<string, unknown>();
  for (const [key, value] of Object.entries(translation)) {
    if (value !== null && typeof value === 'object') {
      for (const [inner, text] of texts(value as Translation, `${prefix}${key}.`)) flat.set(inner, text);
    } else {
      flat.set(`${prefix}${key}`, value);
    }
  }
  return flat;
}

describe('translation files', () => {
  const files = new Map(translationCodes().map((code) => [code, texts(readTranslation(code))]));

  it('every key exists in every file', () => {
    expect([...files.keys()]).toEqual(expect.arrayContaining(['en', 'pl', 'ru']));
    const englishKeys = [...files.get('en')!.keys()].sort();

    for (const [code, file] of files) {
      expect([...file.keys()].sort(), code).toEqual(englishKeys);
      for (const [key, text] of file) {
        expect(typeof text === 'string' && text.trim() !== '', `${code}: ${key} is empty`).toBe(true);
      }
    }
  });

  it('every file names its language', () => {
    const names = Object.fromEntries([...files].map(([code, file]) => [code, file.get('language.name')]));

    expect(names).toEqual(expect.objectContaining({ en: 'English', pl: 'Polski', ru: 'Русский' }));
  });
});
