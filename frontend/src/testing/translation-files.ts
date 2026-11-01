import { readdirSync, readFileSync } from 'node:fs';
import { Translation, TranslocoLoader } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';

const folder = 'public/i18n';

/** The locale codes of the translation files, as the backend lists them: one per `<code>.json`. */
export function translationCodes(): string[] {
  return readdirSync(folder)
    .filter((name) => name.endsWith('.json'))
    .map((name) => name.slice(0, -'.json'.length))
    .sort();
}

export function readTranslation(code: string): Translation {
  return JSON.parse(readFileSync(`${folder}/${code}.json`, 'utf8')) as Translation;
}

/** Serves the translation files from disk, so a spec renders the real texts without HTTP. */
export class FileTranslationLoader implements TranslocoLoader {
  getTranslation(code: string): Observable<Translation> {
    return of(readTranslation(code));
  }
}
