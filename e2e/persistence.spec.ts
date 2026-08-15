import { test, expect } from '@playwright/test';
import { DesignerPage, SAMPLE_XAML } from './helpers/designer-page';

test.describe('Design persistence', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
    await page.evaluate(() => localStorage.clear());
  });

  test('saves a design and restores it after a reload', async ({ page }) => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');
    await designer.setProperty('text', 'Persisted');

    await designer.saveButton.click();
    await expect(designer.toast).toContainText('Design saved');

    await page.reload();
    await expect(designer.canvasElements('Label')).toHaveCount(0);

    await designer.loadButton.click();
    await expect(designer.canvasElements('Label')).toHaveCount(1);
    await expect(designer.canvasElements('Label').first()).toContainText('Persisted');
  });

  test('reports when there is nothing saved', async () => {
    await designer.loadButton.click();
    await expect(designer.toast).toContainText('No saved design found');
  });

  test('restores a full XAML based design', async ({ page }) => {
    await designer.applyXaml(SAMPLE_XAML);
    await designer.saveButton.click();

    await page.reload();
    await designer.loadButton.click();

    await expect(designer.canvasElements('Label').first()).toContainText('Hello MAUI');
    await expect(designer.canvasElements('Button').first()).toContainText('Send');
    await designer.expectXamlToContain('x:Name="Title"');
  });

  test('loading a design is undoable', async ({ page }) => {
    await designer.addControl('Label');
    await designer.saveButton.click();
    await page.reload();

    await designer.addControl('Button');
    await designer.loadButton.click();
    await expect(designer.canvasElements('Button')).toHaveCount(0);

    await designer.undoButton.click();
    await expect(designer.canvasElements('Button')).toHaveCount(1);
  });
});
