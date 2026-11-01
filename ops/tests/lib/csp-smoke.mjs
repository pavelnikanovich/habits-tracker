// Smoke check of a built page under the host's CSP (task 20): opens <base url>/sign-in in headless Chromium, as a
// browser set to English, and exits 0 only when the heading "Sign in" renders (the translation file was fetched and
// applied, so the bundle ran) and the browser reported no Content-Security-Policy violation. A bundle that evaluates
// code (eval, new Function) or loads from another origin would break here, which a grep of the bundle cannot promise.
// Playwright and its Chromium are those of the end-to-end tests: frontend/node_modules and `playwright install`.
// Usage: node csp-smoke.mjs <base url>
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const baseUrl = process.argv[2];
if (!baseUrl) {
  console.error('usage: csp-smoke.mjs <base url>');
  process.exit(2);
}
const frontend = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'frontend');
const { chromium } = createRequire(join(frontend, 'package.json'))('@playwright/test');

const browser = await chromium.launch();
let failure = '';
try {
  const context = await browser.newContext({ locale: 'en-US' });
  const page = await context.newPage();
  const messages = [];
  page.on('console', (message) => messages.push(message.text()));
  page.on('pageerror', (error) => messages.push(error.message));
  await page.goto(`${baseUrl}/sign-in`);
  try {
    await page.getByRole('heading', { level: 1, name: 'Sign in' }).waitFor({ timeout: 15000 });
  } catch {
    failure = 'the heading "Sign in" did not render';
  }
  const violations = messages.filter((text) => /content security policy|content-security-policy|unsafe-eval/i.test(text));
  if (violations.length > 0) failure = `CSP violations: ${violations.join(' | ')}`;
  else if (failure) failure += `; console: ${messages.join(' | ')}`;
} finally {
  await browser.close();
}
if (failure) {
  console.error(`csp-smoke: ${failure}`);
  process.exit(1);
}
console.log('csp-smoke: ok');
