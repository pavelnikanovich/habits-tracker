import { DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { LocaleResponse } from '../api/locale-response';

const storageKey = 'locale';
const fallbackLocale = 'en';

/**
 * The page's language before sign-in (FR-021, research R11): the switcher's choice kept in `localStorage`, else the
 * first of the browser's preferred languages that has a translation file, else English. A stored value that is not
 * an available locale is ignored, so a removed language or an edited value falls back to the browser's languages.
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly http = inject(HttpClient);
  private readonly transloco = inject(TranslocoService);
  private readonly document = inject(DOCUMENT);
  private readonly currentCode = signal(fallbackLocale);
  private readonly availableLocales = signal<LocaleResponse[]>([]);

  readonly current = this.currentCode.asReadonly();
  readonly available = this.availableLocales.asReadonly();

  /** Loads the available locales and switches to the detected language; detection stores nothing. */
  async start(): Promise<void> {
    const locales = await firstValueFrom(this.http.get<LocaleResponse[]>('/api/locales'));
    const codes = locales.map((locale) => locale.code);
    // Transloco reads a code it does not list as a scope, not a language.
    this.transloco.setAvailableLangs(codes);
    this.availableLocales.set(locales);
    this.apply(this.detect(codes));
  }

  /** Switches the page in place, without a reload (FR-022), and keeps the choice. */
  use(code: string): void {
    this.apply(code);
    try {
      localStorage.setItem(storageKey, code);
    } catch {
      // Storage refused (blocked site data): the page still switches, the choice lasts until the page closes.
    }
  }

  private detect(available: string[]): string {
    const stored = this.storedChoice();
    if (stored !== null && available.includes(stored)) return stored;

    const languages = this.document.defaultView?.navigator.languages ?? [];
    const preferred = languages
      .map((language) => language.split('-')[0].toLowerCase())
      .find((code) => available.includes(code));
    return preferred ?? fallbackLocale;
  }

  private storedChoice(): string | null {
    try {
      return localStorage.getItem(storageKey);
    } catch {
      return null; // Storage refused: as if nothing was chosen.
    }
  }

  private apply(code: string): void {
    this.transloco.setActiveLang(code);
    this.document.documentElement.lang = code;
    this.currentCode.set(code);
  }
}
