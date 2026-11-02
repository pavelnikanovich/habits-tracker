import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

/** What a route says about its legal page: the translation section (`privacy` or `terms`) and how many sections it has. */
export interface LegalRouteData {
  page: 'privacy' | 'terms';
  sections: number;
}

/**
 * The Privacy page or the Terms page (FR-024, FR-030, FR-031; contracts/ui.md, "Privacy and Terms"): the controller,
 * the effective date, the sentence that the English version prevails, and the sections `<page>.section.<n>.title` and
 * `.body`. A body is plain text; a blank line starts a new paragraph, and `{name}` and `{email}` take the controller's
 * values from `legal.controllerName` and `legal.contactEmail`, so each value is written once per language. The Terms
 * end with a link to the Privacy page.
 */
@Component({
  selector: 'app-legal-page',
  imports: [TranslocoPipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './legal-page.html',
  styleUrl: './legal-page.css',
})
export class LegalPage {
  private readonly data = inject(ActivatedRoute).snapshot.data as LegalRouteData;
  protected readonly page = this.data.page;
  protected readonly numbers = Array.from({ length: this.data.sections }, (_, index) => index + 1);

  protected paragraphs(text: string): string[] {
    return text.split('\n\n');
  }
}
