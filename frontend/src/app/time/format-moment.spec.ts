import { formatMoment } from './format-moment';

// contracts/ui.md, "Times": the time alone when the moment is today, else the date and the time; rounded up to the
// next minute. Now is 2026-11-02T10:00:00Z; Warsaw is UTC+1 then.
const now = new Date('2026-11-02T10:00:00Z');
const timeZone = 'Europe/Warsaw';

const cases: { moment: string; en: string; pl: string; ru: string }[] = [
  { moment: '2026-11-02T13:04:30Z', en: '14:05', pl: '14:05', ru: '14:05' },
  { moment: '2026-11-02T13:04:00Z', en: '14:04', pl: '14:04', ru: '14:04' },
  { moment: '2026-11-03T10:00:00Z', en: 'November 3, 11:00', pl: '3 listopada, 11:00', ru: '3 ноября, 11:00' },
  { moment: '2026-11-02T22:59:30Z', en: 'November 3, 00:00', pl: '3 listopada, 00:00', ru: '3 ноября, 00:00' },
];

describe('formatMoment', () => {
  for (const { moment, ...expected } of cases) {
    for (const language of ['en', 'pl', 'ru'] as const) {
      it(`${moment} in ${language}`, () => {
        expect(formatMoment(new Date(moment), now, language, timeZone)).toBe(expected[language]);
      });
    }
  }

  it('the time zone decides which day it is', () => {
    expect(formatMoment(new Date('2026-11-02T23:30:00Z'), now, 'en', 'UTC')).toBe('23:30');
    expect(formatMoment(new Date('2026-11-02T23:30:00Z'), now, 'en', timeZone)).toBe('November 3, 00:30');
  });
});
