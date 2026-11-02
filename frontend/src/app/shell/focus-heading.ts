import { DOCUMENT } from '@angular/common';
import { afterNextRender, EnvironmentProviders, inject, Injector, provideEnvironmentInitializer } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter, skip } from 'rxjs';

/**
 * After a navigation between routes the focus moves to the new page's `h1` (`tabindex="-1"`), so a screen reader
 * announces the new page (contracts/ui.md, "Page title and focus"). The first page of a visit keeps the focus where
 * the browser puts it: no field takes the focus when a page opens. A page that puts the focus inside <main> itself,
 * as the code step does with its field, keeps it.
 */
export function provideHeadingFocus(): EnvironmentProviders {
  return provideEnvironmentInitializer(() => {
    const document = inject(DOCUMENT);
    const injector = inject(Injector);
    inject(Router)
      .events.pipe(
        filter((event) => event instanceof NavigationEnd),
        skip(1),
      )
      .subscribe(() =>
        afterNextRender(
          () => {
            const main = document.querySelector('main');
            if (main?.contains(document.activeElement)) return;
            main?.querySelector<HTMLElement>('h1')?.focus();
          },
          { injector },
        ),
      );
  });
}
