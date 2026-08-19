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

  test.describe('Visual Studio extension download', () => {
    test('offers the extension and warns that it is beta', async ({ page }) => {
      const link = page.getByTestId('vsix-download-link');

      await expect(link).toBeVisible();
      await expect(link).toContainText('VS Extension');
      await expect(link).toContainText('Beta');
      await expect(link).toHaveAttribute(
        'href',
        'https://github.com/GMPrakhar/MAUI-Designer/releases/latest/download/MauiDesigner.vsix'
      );
      await expect(link).toHaveAttribute('title', /beta/i);
      await expect(link).toHaveAttribute('title', /break/i);
    });

    test('restates the beta warning when the download starts', async ({ page }) => {
      // Stop the click from navigating away so the assertion runs against the page.
      await page.getByTestId('vsix-download-link').evaluate(node => node.removeAttribute('href'));
      await page.getByTestId('vsix-download-link').click();

      await expect(page.getByTestId('app-toast')).toContainText(/beta/i);
      await expect(page.getByTestId('app-toast')).toContainText(/unstable or broken/i);
    });

    test('is hidden when the designer is already hosted inside an IDE', async ({ page }) => {
      await page.addInitScript(() => {
        (window as any).chrome = { webview: { postMessage: () => {}, addEventListener: () => {} } };
      });
      await page.reload();

      await expect(page.getByTestId('save-button')).toBeVisible();
      await expect(page.getByTestId('vsix-download-link')).toHaveCount(0);
    });
  });
});
