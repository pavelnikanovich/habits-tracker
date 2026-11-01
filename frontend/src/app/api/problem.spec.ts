import { readTranslation } from '../../testing/translation-files';
import { knownCodes, problemText } from './problem';

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

describe('problemText', () => {
  // A translation that shows what it was asked: the key and its parameters.
  const translate = (key: string, params: Record<string, string>) =>
    `${key}(${Object.entries(params)
      .map(([name, value]) => `${name}=${value}`)
      .join(',')})`;
  const noon = new Date(2026, 10, 10, 12, 0);

  it('is error.<code> with the trace id', () => {
    expect(problemText({ code: 'internal-error', traceId: 'abc' }, translate, 'en', noon)).toBe('error.internal-error(traceId=abc)');
    expect(problemText({ code: 'network' }, translate, 'en', noon)).toBe('error.network(traceId=)');
  });

  it('adds when to try again, the time alone on the same day', () => {
    const retryAt = new Date(2026, 10, 10, 13, 0).toISOString();

    expect(problemText({ code: 'email-limit', retryAt }, translate, 'pl', noon)).toBe(
      'error.email-limit(traceId=) signIn.retryAt(time=13:00)',
    );
  });

  it('adds when to try again, with the date on another day, in the given language', () => {
    const retryAt = new Date(2026, 10, 11, 0, 30).toISOString();

    expect(problemText({ code: 'email-limit', retryAt }, translate, 'pl', new Date(2026, 10, 10, 23, 30))).toBe(
      'error.email-limit(traceId=) signIn.retryAt(time=11 listopada, 00:30)',
    );
  });
});
