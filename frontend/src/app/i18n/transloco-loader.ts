import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Translation, TranslocoLoader } from '@jsverse/transloco';
import { Observable } from 'rxjs';

/** Loads a language's texts from its translation file, served by the application itself (FR-020, FR-027). */
@Injectable({ providedIn: 'root' })
export class TranslocoHttpLoader implements TranslocoLoader {
  private readonly http = inject(HttpClient);

  getTranslation(code: string): Observable<Translation> {
    return this.http.get<Translation>(`/i18n/${code}.json`);
  }
}
