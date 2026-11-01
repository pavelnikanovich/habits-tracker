import { expect, Locator, Page } from '@playwright/test';

const maxTabs = 50;

/**
 * The focused element shows the focus outline of contracts/ui.md: `outline: 3px solid var(--focus)` with
 * `outline-offset: 2px`. The width alone proves nothing: the initial `outline-width` is `medium`, which computes to
 * 3 px even when `outline-style` is `none`, so the style and the offset are checked as well.
 */
export async function expectVisibleFocus(page: Page): Promise<void> {
  const focused = await page.evaluate(() => {
    const element = document.activeElement;
    if (element === null || element === document.body) return { element: 'nothing', outline: 'none' };
    const style = getComputedStyle(element);
    return {
      element: element.outerHTML.slice(0, 120),
      outline: `${style.outlineWidth} ${style.outlineStyle} offset ${style.outlineOffset}`,
    };
  });
  expect(focused.outline, `the focus outline of ${focused.element}`).toBe('3px solid offset 2px');
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
