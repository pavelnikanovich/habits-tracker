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
 * The page changes language only once the language's file has loaded, so a failed load leaves it as it was
 * (contracts/ui.md, "Start of the application").
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly http = inject(HttpClient);
  private readonly transloco = inject(TranslocoService);
  private readonly document = inject(DOCUMENT);
  private readonly currentCode = signal(fallbackLocale);
  private readonly availableLocales = signal<LocaleResponse[]>([]);
  private readonly lastSwitchFailed = signal(false);
  /** Counts the switches; one whose number is no longer the latest when its file arrives was overtaken. */
  private latestSwitch = 0;

  readonly current = this.currentCode.asReadonly();
  readonly available = this.availableLocales.asReadonly();
  /** Whether the latest switch failed to load its file; the switcher then shows `error.network`. */
  readonly switchFailed = this.lastSwitchFailed.asReadonly();

  /**
   * Loads the available locales and the detected language's file, then shows the page in that language; detection
   * stores nothing. Without the list of locales, or without the detected language's file, the page is in English;
   * when English fails to load too, the start fails and the static text of index.html stays.
   */
  async start(): Promise<void> {
    const locales = await this.listLocales();
    const codes = locales?.map((locale) => locale.code) ?? [fallbackLocale];
    // Transloco reads a code it does not list as a scope, not a language.
    this.transloco.setAvailableLangs(codes);

    const detected = this.detect(codes);
    try {
      await this.load(detected);
      this.apply(detected);
    } catch (error) {
      if (detected === fallbackLocale) throw error;
      await this.load(fallbackLocale);
      this.apply(fallbackLocale);
    }
    this.availableLocales.set(locales ?? [{ code: fallbackLocale, name: this.transloco.translate('language.name') }]);
  }

  /**
   * Shows the page in `code`, in place, without a reload (FR-022), once its file has loaded; stores nothing. A failed
   * load keeps the page in its language and sets `switchFailed`. The signed-in user's locale is shown this way: it is
   * not this browser's choice, so the next visitor on the device does not inherit it. A later switch wins over this
   * one while its file loads: this one then changes nothing and reports nothing. True when `code` was applied.
   */
  async show(code: string): Promise<boolean> {
    const turn = ++this.latestSwitch;
    try {
      await this.load(code);
    } catch {
      if (turn === this.latestSwitch) this.lastSwitchFailed.set(true);
      return false;
    }
    if (turn !== this.latestSwitch) return false;
    this.lastSwitchFailed.set(false);
    this.apply(code);
    return true;
  }

  /** The person chose `code` in the switcher: shows it, then keeps the choice once its file has loaded and applied. */
  async use(code: string): Promise<void> {
    if (!(await this.show(code))) return;
    try {
      localStorage.setItem(storageKey, code);
    } catch {
      // Storage refused (blocked site data): the page still switches, the choice lasts until the page closes.
    }
  }

  /** The locales the server has translation files for; undefined when the request fails. */
  private async listLocales(): Promise<LocaleResponse[] | undefined> {
    try {
      return await firstValueFrom(this.http.get<LocaleResponse[]>('/api/locales'));
    } catch {
      return undefined;
    }
  }

  private async load(code: string): Promise<void> {
    await firstValueFrom(this.transloco.load(code));
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
