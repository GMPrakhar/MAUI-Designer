import { test, expect } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

test.describe('Designer shell', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
  });

  test('renders the main layout regions', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('MAUI Designer');
    await expect(designer.canvas).toBeVisible();
    await expect(designer.xamlTextarea).toBeVisible();
    await expect(page.locator('.right-panel')).toBeVisible();
  });

  test('switches between toolbox and hierarchy tabs', async () => {
    await designer.openToolbox();
    await expect(designer.toolbox).toBeVisible();
    await expect(designer.hierarchy).toHaveCount(0);

    await designer.openHierarchy();
    await expect(designer.hierarchy).toBeVisible();
    await expect(designer.toolbox).toHaveCount(0);
  });

  test('loads without console errors', async ({ page }) => {
    const errors: string[] = [];
    page.on('pageerror', error => errors.push(error.message));
    page.on('console', message => {
      if (message.type() === 'error') {
        errors.push(message.text());
      }
    });

    await page.reload();
    await expect(designer.canvas).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('undo and redo start disabled', async () => {
    await expect(designer.undoButton).toBeDisabled();
    await expect(designer.redoButton).toBeDisabled();
  });
});
