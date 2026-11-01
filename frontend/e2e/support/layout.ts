import { expect, Page } from '@playwright/test';

/** The page fits its viewport: nothing makes the user scroll sideways (reflow at 320 px). */
export async function expectNoHorizontalScroll(page: Page): Promise<void> {
  const { scrollWidth, innerWidth } = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    innerWidth: window.innerWidth,
  }));
  expect(scrollWidth, `page ${page.url()} is ${scrollWidth} px wide in a ${innerWidth} px viewport`)
    .toBeLessThanOrEqual(innerWidth);
}
