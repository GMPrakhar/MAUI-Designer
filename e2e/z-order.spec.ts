import { test, expect } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

/**
 * Stacking order is not stored as a z-index anywhere -- it comes straight from
 * the order of `children`, which is also the order elements are written to
 * XAML. So the XAML pane is the honest place to assert against: if the emitted
 * document changed order, the canvas and the saved file agree by construction.
 */
async function tagOrder(designer: DesignerPage): Promise<string[]> {
  const xaml = await designer.getXaml();
  return [...xaml.matchAll(/<(Label|Button|Image)\b/g)].map(match => match[1]);
}

/** Adds a control and places it at an absolute position. */
async function addAt(designer: DesignerPage, type: string, x: number, y: number) {
  await designer.addControl(type);
  await designer.setProperty('x', String(x));
  await designer.setProperty('y', String(y));
}

test.describe('Z-order controls', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
    // Spread them out so each one is directly clickable. Stacking order is
    // independent of position, so this does not weaken what is under test.
    await addAt(designer, 'Label', 20, 20);
    await addAt(designer, 'Button', 20, 140);
    await addAt(designer, 'Image', 20, 260);
  });

  test('bring to front moves the element past all of its siblings', async ({ page }) => {
    await designer.selectFirst('Label');
    await page.getByTestId('bring-to-front').click();

    await expect.poll(() => tagOrder(designer)).toEqual(['Button', 'Image', 'Label']);
  });

  test('send to back moves the element behind all of its siblings', async ({ page }) => {
    await designer.selectFirst('Image');
    await page.getByTestId('send-to-back').click();

    await expect.poll(() => tagOrder(designer)).toEqual(['Image', 'Label', 'Button']);
  });

  test('bring forward moves exactly one step', async ({ page }) => {
    await designer.selectFirst('Label');
    await page.getByTestId('bring-forward').click();

    await expect.poll(() => tagOrder(designer)).toEqual(['Button', 'Label', 'Image']);
  });

  test('send backward moves exactly one step', async ({ page }) => {
    await designer.selectFirst('Image');
    await page.getByTestId('send-backward').click();

    await expect.poll(() => tagOrder(designer)).toEqual(['Label', 'Image', 'Button']);
  });

  test('keyboard shortcuts restack the selection', async ({ page }) => {
    await designer.selectFirst('Label');

    await page.keyboard.press('Control+]');
    await expect.poll(() => tagOrder(designer)).toEqual(['Button', 'Label', 'Image']);

    await page.keyboard.press('Control+Shift+]');
    await expect.poll(() => tagOrder(designer)).toEqual(['Button', 'Image', 'Label']);

    await page.keyboard.press('Control+Shift+[');
    await expect.poll(() => tagOrder(designer)).toEqual(['Label', 'Button', 'Image']);
  });

  test('restacking is a single undo step', async ({ page }) => {
    await designer.selectFirst('Label');
    await page.getByTestId('bring-to-front').click();
    await expect.poll(() => tagOrder(designer)).toEqual(['Button', 'Image', 'Label']);

    await designer.undoButton.click();

    await expect.poll(() => tagOrder(designer)).toEqual(['Label', 'Button', 'Image']);
  });

  test('the buttons are disabled when there is nothing to restack against', async ({ page }) => {
    // A fresh canvas with a single child has no sibling to move past.
    await designer.goto();
    await expect(page.getByTestId('bring-to-front')).toBeDisabled();

    await designer.addControl('Label');
    await designer.selectFirst('Label');
    await expect(page.getByTestId('bring-to-front')).toBeDisabled();

    await designer.addControl('Button');
    await designer.selectFirst('Button');
    await expect(page.getByTestId('bring-to-front')).toBeEnabled();
  });

  test('a selected element paints in its real stacking order', async ({ page }) => {
    // Regression guard: the canvas used to lift whatever was selected to
    // z-index 9999, so sending the selection to the back left it visibly on
    // top and the feature looked broken until you clicked somewhere else.
    await designer.selectFirst('Image');
    await page.getByTestId('send-to-back').click();

    const zIndex = await page
      .locator('[data-element-type="Image"]')
      .first()
      .evaluate(node => getComputedStyle(node).zIndex);

    expect(zIndex).toBe('auto');
  });
});
