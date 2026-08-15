import { test, expect } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

test.describe('Undo / redo', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
  });

  test('undoes and redoes adding an element with the toolbar', async () => {
    await designer.addControl('Label');
    await expect(designer.canvasElements('Label')).toHaveCount(1);
    await expect(designer.undoButton).toBeEnabled();

    await designer.undoButton.click();
    await expect(designer.canvasElements('Label')).toHaveCount(0);
    await expect(designer.redoButton).toBeEnabled();

    await designer.redoButton.click();
    await expect(designer.canvasElements('Label')).toHaveCount(1);
  });

  test('undoes a deletion with Ctrl+Z', async ({ page }) => {
    await designer.addControl('Button');
    await designer.selectFirst('Button');
    await page.keyboard.press('Delete');
    await expect(designer.canvasElements('Button')).toHaveCount(0);

    await page.keyboard.press('Control+z');
    await expect(designer.canvasElements('Button')).toHaveCount(1);
  });

  test('redoes with Ctrl+Y', async ({ page }) => {
    await designer.addControl('Entry');
    await page.keyboard.press('Control+z');
    await expect(designer.canvasElements('Entry')).toHaveCount(0);

    await page.keyboard.press('Control+y');
    await expect(designer.canvasElements('Entry')).toHaveCount(1);
  });

  test('undoes a property change', async () => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');
    const original = await designer.propertyValue('width');

    await designer.setProperty('width', '321');
    await expect(designer.canvasElements('Label').first()).toHaveCSS('width', '321px');

    await designer.undoButton.click();
    await expect(designer.canvasElements('Label').first()).toHaveCSS('width', `${original}px`);
  });

  test('clear removes every element and can be undone', async () => {
    await designer.addControl('Label');
    await designer.addControl('Button');

    await designer.clearButton.click();
    await expect(designer.canvasElements('Label')).toHaveCount(0);
    await expect(designer.canvasElements('Button')).toHaveCount(0);
    await expect(designer.toast).toContainText('Canvas cleared');

    await designer.undoButton.click();
    await expect(designer.canvasElements('Label')).toHaveCount(1);
    await expect(designer.canvasElements('Button')).toHaveCount(1);
  });
});
