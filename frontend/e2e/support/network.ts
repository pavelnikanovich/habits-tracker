import { Page } from '@playwright/test';

/**
 * Starts recording every request whose origin is not the page's (FR-027); call it before the first navigation.
 * The page's origin is the one its latest main-frame navigation went to. The returned array fills as the page runs.
 */
export function collectForeignRequests(page: Page): string[] {
  const foreign: string[] = [];
  let pageOrigin: string | undefined;

  page.on('request', (request) => {
    const url = new URL(request.url());
    if (request.isNavigationRequest() && request.frame() === page.mainFrame()) {
      pageOrigin = url.origin;
      return;
    }
    if (url.protocol === 'data:' || url.protocol === 'blob:') return; // made by the page itself, no server involved
    if (url.origin !== pageOrigin) foreign.push(request.url());
  });

  return foreign;
}
