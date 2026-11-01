import { readFile } from 'node:fs/promises';
import { join } from 'node:path';

// The development log of scripts/dev.sh (research R17), at the repository root.
const devLog = join(__dirname, '..', '..', '..', '.dev', 'app.log');
const timeoutMs = 10_000;
const pollMs = 200;

/** A code email as the development sender logs it: `code-email to=<email> locale=<locale> code=<6 digits> …`. */
export interface LoggedCodeEmail {
  code: string;
  locale: string;
}

async function readLog(): Promise<string> {
  return readFile(devLog, 'utf8').catch(() => '');
}

/**
 * The newest code email to `email` in `log`, from the line
 * `code-email to=<email> locale=<locale> code=<6 digits> subject=<subject>`; undefined when there is none.
 */
function newestCodeEmail(log: string, email: string): LoggedCodeEmail | undefined {
  const escaped = email.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const line = new RegExp(`code-email to=${escaped} locale=([a-z]{2}) code=(\\d{6})(?!\\d)`, 'g');
  const match = [...log.matchAll(line)].at(-1);
  return match === undefined ? undefined : { locale: match[1], code: match[2] };
}

/**
 * The log's current length: pass it as `after` to read only what is written later, so a second code for the same
 * email is not mistaken for the first.
 */
export async function logMark(): Promise<number> {
  return (await readLog()).length;
}

/** Polls the development log for up to 10 seconds for the newest code email to `email` written after `after`. */
export async function readCodeEmail(email: string, after = 0): Promise<LoggedCodeEmail> {
  const deadline = Date.now() + timeoutMs;
  for (;;) {
    const found = newestCodeEmail((await readLog()).slice(after), email);
    if (found !== undefined) return found;
    if (Date.now() >= deadline) throw new Error(`No code email to ${email} in ${devLog} within ${timeoutMs / 1000} s`);
    await new Promise((resolve) => setTimeout(resolve, pollMs));
  }
}

/** The code of the newest code email to `email` written after `after` (the whole log without it). */
export async function readCode(email: string, after = 0): Promise<string> {
  return (await readCodeEmail(email, after)).code;
}
