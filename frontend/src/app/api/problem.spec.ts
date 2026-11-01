import { readTranslation } from '../../testing/translation-files';
import { knownCodes } from './problem';

describe('known problem codes', () => {
  const texts = readTranslation('en')['error'] as Record<string, string>;

  it('every known code has an error text', () => {
    for (const code of knownCodes) expect(texts[`${code}`], `error.${code}`).toBeTruthy();
  });

  it('every error text except error.network is a known code', () => {
    for (const key of Object.keys(texts)) {
      if (key !== 'network') expect(knownCodes as readonly string[], `error.${key} is a known code`).toContain(key);
    }
  });
});
