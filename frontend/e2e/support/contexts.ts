import { Browser, BrowserContext, BrowserContextOptions, test } from '@playwright/test';
import { projectOrigin } from './sign-in';

/**
 * Another browser of the same project: a context of the same engine, origin and device as the test's own, with its own
 * cookies and storage, for a test of two browsers (or two devices) of one user.
 */
export async function newBrowser(browser: Browser, options: BrowserContextOptions = {}): Promise<BrowserContext> {
  const { viewport, isMobile, hasTouch, deviceScaleFactor, userAgent } = test.info().project.use;
  return browser.newContext({
    baseURL: projectOrigin(),
    ignoreHTTPSErrors: true,
    viewport,
    isMobile,
    hasTouch,
    deviceScaleFactor,
    userAgent,
    ...options,
  });
}
