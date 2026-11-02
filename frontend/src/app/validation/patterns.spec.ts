import { readFileSync } from 'node:fs';
import { EMAIL_MAX_LENGTH, EMAIL_PATTERN, ONE_TIME_CODE_PATTERN } from './patterns';

interface Example {
  rule: 'email' | 'one-time-code' | 'locale-code';
  input: string;
  valid: boolean;
}

// The examples the core's tests run too (research R18): the page's copy of the rules must agree with the core.
const examples = JSON.parse(readFileSync('../testdata/validation-examples.json', 'utf8')) as Example[];

describe('the page rules', () => {
  it('every shared example holds', () => {
    const checked = examples.filter((example) => example.rule !== 'locale-code');
    expect(checked.length).toBeGreaterThan(0);

    for (const { rule, input, valid } of checked) {
      const follows =
        rule === 'email'
          ? input.length <= EMAIL_MAX_LENGTH && EMAIL_PATTERN.test(input)
          : ONE_TIME_CODE_PATTERN.test(input);
      expect(follows, `${rule} ${JSON.stringify(input)}`).toBe(valid);
    }
  });
});
