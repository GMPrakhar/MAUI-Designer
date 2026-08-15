import { test, expect } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

/** Adds a control and places it at an absolute position. */
async function addAt(designer: DesignerPage, type: string, x: number, y: number) {
  await designer.addControl(type);
  await designer.setProperty('x', String(x));
  await designer.setProperty('y', String(y));
}

test.describe('Alignment and layout tools', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
  });

  test('align left moves every selected element to the leftmost edge', async () => {
    await addAt(designer, 'Label', 60, 40);
    await addAt(designer, 'Button', 130, 140);

    await designer.selectFirst('Label');
    await designer.toggleSelect('Button');
    await designer.alignButton('left').click();

    await designer.selectFirst('Button');
    await designer.expectProperty('x', 60);
    await designer.selectFirst('Label');
    await designer.expectProperty('x', 60);
  });

  test('align top moves every selected element to the topmost edge', async () => {
    await addAt(designer, 'Label', 60, 90);
    await addAt(designer, 'Button', 200, 30);

    await designer.selectFirst('Label');
    await designer.toggleSelect('Button');
    await designer.alignButton('top').click();

    await designer.selectFirst('Label');
    await designer.expectProperty('y', 30);
  });

  test('align buttons are disabled without a multi selection', async ({ page }) => {
    await addAt(designer, 'Label', 60, 40);

    await expect(designer.alignButton('left')).toBeDisabled();
    await expect(page.getByTestId('distribute-horizontal')).toBeDisabled();
  });

  test('distribute horizontally spaces three elements evenly', async ({ page }) => {
    await addAt(designer, 'Label', 20, 40);
    await addAt(designer, 'Button', 60, 120);
    await addAt(designer, 'Image', 320, 200);

    await designer.selectFirst('Label');
    await designer.toggleSelect('Button');
    await designer.toggleSelect('Image');
    await expect(page.getByTestId('distribute-horizontal')).toBeEnabled();
    await page.getByTestId('distribute-horizontal').click();

    await designer.selectFirst('Label');
    const left = await designer.getPropertyNumber('x');
    await designer.selectFirst('Image');
    const right = await designer.getPropertyNumber('x');
    await designer.selectFirst('Button');
    const middleX = await designer.getPropertyNumber('x');
    const middleWidth = await designer.getPropertyNumber('width');

    // The middle element's centre should sit halfway between the two anchors
    const leftCentre = left + 100 / 2;
    const rightCentre = right + 100 / 2;
    expect(middleX + middleWidth / 2).toBeCloseTo((leftCentre + rightCentre) / 2, 0);
  });

  test('the align buttons in the properties panel work too', async ({ page }) => {
    await addAt(designer, 'Label', 60, 40);
    await addAt(designer, 'Button', 220, 140);

    await designer.selectFirst('Label');
    await designer.toggleSelect('Button');
    await page.getByTestId('panel-align-right').click();

    await designer.selectFirst('Label');
    const labelRight = await designer.getPropertyNumber('x');
    expect(labelRight).toBeGreaterThan(60);
  });

  test('aligning a multi selection is a single undo step', async () => {
    await addAt(designer, 'Label', 60, 40);
    await addAt(designer, 'Button', 220, 140);

    await designer.selectFirst('Label');
    await designer.toggleSelect('Button');
    await designer.alignButton('left').click();
    await designer.undoButton.click();

    await designer.selectFirst('Button');
    await designer.expectProperty('x', 220);
    await designer.selectFirst('Label');
    await designer.expectProperty('x', 60);
  });

  test('snap to grid rounds a dropped element to the grid size', async ({ page }) => {
    await addAt(designer, 'Label', 33, 47);

    await page.getByTestId('grid-size').fill('20');
    await page.getByTestId('grid-size').dispatchEvent('input');
    const snap = page.getByTestId('toggle-snap');
    if ((await snap.getAttribute('data-active')) !== 'true') {
      await snap.click();
    }
    await expect(snap).toHaveAttribute('data-active', 'true');

    await designer.selectFirst('Label');
    await designer.dragElementBy('Label', { x: 41, y: 33 });

    const x = await designer.getPropertyNumber('x');
    const y = await designer.getPropertyNumber('y');
    expect(x % 20).toBe(0);
    expect(y % 20).toBe(0);
  });

  test('the grid overlay can be toggled on the canvas', async ({ page }) => {
    const toggle = page.getByTestId('toggle-grid');
    const wasOn = (await toggle.getAttribute('data-active')) === 'true';
    await toggle.click();

    if (wasOn) {
      await expect(designer.canvas).not.toHaveClass(/show-grid/);
    } else {
      await expect(designer.canvas).toHaveClass(/show-grid/);
    }
  });

  test('rulers can be toggled', async ({ page }) => {
    const toggle = page.getByTestId('toggle-rulers');
    if ((await toggle.getAttribute('data-active')) === 'true') {
      await toggle.click();
    }
    await expect(page.getByTestId('ruler-horizontal')).toHaveCount(0);

    await toggle.click();
    await expect(page.getByTestId('ruler-horizontal')).toBeVisible();
    await expect(page.getByTestId('ruler-vertical')).toBeVisible();
  });

  test('smart guides appear while dragging next to a sibling', async ({ page }) => {
    await addAt(designer, 'Label', 100, 60);
    await addAt(designer, 'Button', 100, 260);

    const snap = page.getByTestId('toggle-snap');
    if ((await snap.getAttribute('data-active')) === 'true') {
      await snap.click();
    }

    await designer.selectFirst('Button');
    const element = designer.canvasElements('Button').first();
    const box = (await element.boundingBox())!;
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await page.mouse.down();
    // Drift a couple of pixels off the shared left edge: within the snap threshold
    await page.mouse.move(box.x + box.width / 2 + 3, box.y + box.height / 2 - 60, { steps: 10 });
    await page.mouse.move(box.x + box.width / 2 + 2, box.y + box.height / 2 - 60, { steps: 5 });

    await expect(page.locator('[data-testid^="alignment-guide"]').first()).toBeVisible();
    await page.mouse.up();

    await expect(page.locator('[data-testid^="alignment-guide"]')).toHaveCount(0);
  });
});
