import { expect, Locator, Page } from '@playwright/test';

const maxTabs = 50;

/**
 * The focused element shows the focus outline of contracts/ui.md: a computed `outline-width` of 3 px. A browser
 * computes 0 when `outline-style` is `none`, so 3 px also means the outline is drawn.
 */
export async function expectVisibleFocus(page: Page): Promise<void> {
  const focused = await page.evaluate(() => {
    const element = document.activeElement;
    if (element === null || element === document.body) return { element: 'nothing', outlineWidth: 'none' };
    return { element: element.outerHTML.slice(0, 120), outlineWidth: getComputedStyle(element).outlineWidth };
  });
  expect(focused.outlineWidth, `the focus outline of ${focused.element}`).toBe('3px');
}

/** Presses Tab, at most 50 times, until `target` has the focus; fails if it never gets it. */
export async function pressTabUntil(page: Page, target: Locator): Promise<void> {
  const focused = () => target.evaluate((element) => element === document.activeElement);
  if (await focused()) return;
  for (let presses = 0; presses < maxTabs; presses++) {
    await page.keyboard.press('Tab');
    if (await focused()) return;
  }
  throw new Error(`${target} did not get the focus within ${maxTabs} presses of Tab`);
}
