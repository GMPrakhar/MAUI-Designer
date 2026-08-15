import { test, expect } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

test.describe('Canvas interactions', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
  });

  test('selecting an element shows resize handles and properties', async ({ page }) => {
    await designer.addControl('Button');
    await designer.selectFirst('Button');

    await expect(designer.selectedCanvasElement()).toHaveCount(1);
    await expect(page.getByTestId('resize-handle-se')).toBeVisible();
    await expect(page.getByTestId('selected-element-type')).toHaveText('Button');
  });

  test('clicking the root layout selects it and Escape clears the selection', async ({ page }) => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');
    await expect(page.getByTestId('selected-element-type')).toHaveText('Label');

    // The empty area of the canvas belongs to the root layout
    await designer.canvas.click({ position: { x: 400, y: 400 } });
    await expect(page.getByTestId('selected-element-type')).toHaveText('AbsoluteLayout');
    // The root layout is not resizable through handles
    await expect(page.getByTestId('resize-handle-se')).toHaveCount(0);

    await page.keyboard.press('Escape');
    await expect(designer.selectedCanvasElement()).toHaveCount(0);
    await expect(page.locator('.no-selection')).toBeVisible();
  });

  test('resizing with the south-east handle updates width and height', async ({ page }) => {
    await designer.addControl('Button');
    await designer.selectFirst('Button');

    const startWidth = Number(await designer.propertyValue('width'));
    const startHeight = Number(await designer.propertyValue('height'));

    const handle = page.getByTestId('resize-handle-se');
    const box = (await handle.boundingBox())!;

    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await page.mouse.down();
    await page.mouse.move(box.x + box.width / 2 + 60, box.y + box.height / 2 + 40, { steps: 10 });
    await expect(page.getByTestId('size-display')).toBeVisible();
    await page.mouse.up();

    expect(Number(await designer.propertyValue('width'))).toBe(startWidth + 60);
    expect(Number(await designer.propertyValue('height'))).toBe(startHeight + 40);
  });

  test('resizing never shrinks below the minimum size', async ({ page }) => {
    await designer.addControl('Button');
    await designer.selectFirst('Button');

    const handle = page.getByTestId('resize-handle-se');
    const box = (await handle.boundingBox())!;

    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await page.mouse.down();
    await page.mouse.move(box.x - 400, box.y - 400, { steps: 10 });
    await page.mouse.up();

    expect(Number(await designer.propertyValue('width'))).toBeGreaterThanOrEqual(20);
    expect(Number(await designer.propertyValue('height'))).toBeGreaterThanOrEqual(20);
  });

  test('arrow keys nudge the selected element', async ({ page }) => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');

    const startX = Number(await designer.propertyValue('x'));
    const startY = Number(await designer.propertyValue('y'));

    await designer.canvas.press('ArrowRight');
    await designer.canvas.press('ArrowDown');
    expect(Number(await designer.propertyValue('x'))).toBe(startX + 1);
    expect(Number(await designer.propertyValue('y'))).toBe(startY + 1);

    await page.keyboard.press('Shift+ArrowRight');
    expect(Number(await designer.propertyValue('x'))).toBe(startX + 11);
  });

  test('Delete removes the selected element and Escape clears selection', async ({ page }) => {
    await designer.addControl('Label');
    await designer.addControl('Button');
    await designer.selectFirst('Button');

    await page.keyboard.press('Escape');
    await expect(designer.selectedCanvasElement()).toHaveCount(0);

    await designer.selectFirst('Button');
    await page.keyboard.press('Delete');
    await expect(designer.canvasElements('Button')).toHaveCount(0);
    await expect(designer.canvasElements('Label')).toHaveCount(1);
  });

  test('Ctrl+D duplicates the selected element', async ({ page }) => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');

    await page.keyboard.press('Control+d');
    await expect(designer.canvasElements('Label')).toHaveCount(2);
    // The duplicate becomes the new selection
    await expect(designer.selectedCanvasElement()).toHaveCount(1);
  });
});
