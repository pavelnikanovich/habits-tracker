import { readdirSync, readFileSync } from 'node:fs';

// Cross-site scripting (research R19, T2): Angular escapes what it renders, and no text reaches the page as HTML.
// This file names the two words it looks for, so it leaves itself out.
const self = 'app/no-html-from-strings.spec.ts';

describe('templates', () => {
  it('no template renders HTML from a string', () => {
    const offenders = readdirSync('src', { recursive: true, encoding: 'utf8' })
      .filter((file) => /\.(html|ts)$/.test(file) && file !== self)
      .filter((file) => /innerHTML|bypassSecurityTrust/.test(readFileSync(`src/${file}`, 'utf8')));

    expect(offenders).toEqual([]);
  });
});
