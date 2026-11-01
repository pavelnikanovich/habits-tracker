import { expect, Page } from '@playwright/test';

/**
 * The page fits its viewport: nothing makes the user scroll sideways (reflow at 320 px). The page's width is compared
 * with the layout viewport (`clientWidth`), not with `innerWidth`: a phone browser zooms out to fit a page that is too
 * wide, so `innerWidth` grows with the page and the overflow would pass unseen.
 */
export async function expectNoHorizontalScroll(page: Page): Promise<void> {
  const { scrollWidth, clientWidth } = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }));
  expect(scrollWidth, `page ${page.url()} is ${scrollWidth} px wide in a ${clientWidth} px viewport`)
    .toBeLessThanOrEqual(clientWidth);
}
