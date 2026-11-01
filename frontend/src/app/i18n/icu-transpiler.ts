import { EnvironmentProviders, Injectable, makeEnvironmentProviders } from '@angular/core';
import { DefaultTranspiler, TRANSLOCO_TRANSPILER, TranspileParams } from '@jsverse/transloco';
import { IntlMessageFormat } from 'intl-messageformat';

/**
 * Transloco's transpiler for the ICU messages of the translation files (research R11): `intl-messageformat` interprets
 * each message and never turns it into code, so the texts render under the host's `script-src 'self'` (research R19,
 * T2), which forbids `eval` and `new Function`. A tag such as `<b>` stays text: no translation is rendered as HTML.
 * Objects and arrays go through Transloco's default handling, which calls back here for each string.
 */
@Injectable()
export class IcuTranspiler extends DefaultTranspiler {
  private language = this.config.defaultLang;
  // One parsed message per language and text: parsing is the costly step, and the texts of a page repeat.
  private readonly formats = new Map<string, IntlMessageFormat>();

  override transpile(params: TranspileParams): unknown {
    if (typeof params.value !== 'string') return super.transpile(params);

    const key = `${this.language}\u0000${params.value}`;
    let format = this.formats.get(key);
    if (format === undefined) {
      format = new IntlMessageFormat(params.value, this.language, undefined, { ignoreTag: true });
      this.formats.set(key, format);
    }
    return format.format(params.params);
  }

  onLangChanged(language: string): void {
    this.language = language;
  }
}

export function provideIcuTranspiler(): EnvironmentProviders {
  return makeEnvironmentProviders([{ provide: TRANSLOCO_TRANSPILER, useClass: IcuTranspiler }]);
}
