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

/** Every visible control (button, field, select, link) lies inside the viewport's width: each action is reachable. */
export async function expectControlsInViewport(page: Page): Promise<void> {
  const width = page.viewportSize()?.width ?? 0;
  const controls = page.locator('button, input, select, a[href]').filter({ visible: true });
  for (const control of await controls.all()) {
    const box = await control.boundingBox();
    const name = await control.evaluate((element) => element.outerHTML.slice(0, 80));
    expect(box, name).not.toBeNull();
    expect(box!.x, `${name} starts inside the viewport`).toBeGreaterThanOrEqual(0);
    expect(box!.x + box!.width, `${name} ends inside the viewport`).toBeLessThanOrEqual(width);
  }
}
