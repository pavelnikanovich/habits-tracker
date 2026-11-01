import { readFile } from 'node:fs/promises';
import { join } from 'node:path';

// The development log of scripts/dev.sh (research R17), at the repository root.
const devLog = join(__dirname, '..', '..', '..', '.dev', 'app.log');
const timeoutMs = 10_000;
const pollMs = 200;

/**
 * The code of the newest code email to `email` in the log, from the line
 * `code-email to=<email> locale=<locale> code=<6 digits> subject=<subject>`; undefined when there is none.
 */
function newestCode(log: string, email: string): string | undefined {
  const escaped = email.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const line = new RegExp(`code-email to=${escaped} .*?\\bcode=(\\d{6})(?!\\d)`, 'g');
  return [...log.matchAll(line)].at(-1)?.[1];
}

/** Polls the development log for up to 10 seconds for the newest code sent to `email`. */
export async function readCode(email: string): Promise<string> {
  const deadline = Date.now() + timeoutMs;
  for (;;) {
    const log = await readFile(devLog, 'utf8').catch(() => '');
    const code = newestCode(log, email);
    if (code !== undefined) return code;
    if (Date.now() >= deadline) throw new Error(`No code email to ${email} in ${devLog} within ${timeoutMs / 1000} s`);
    await new Promise((resolve) => setTimeout(resolve, pollMs));
  }
}
