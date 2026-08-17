import { test, expect } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

test.describe('Clipboard, templates and starter pages', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
    await page.evaluate(() => {
      localStorage.removeItem('maui-designer.clipboard');
      localStorage.removeItem('maui-designer.templates');
    });
  });

  test('Ctrl+C then Ctrl+V duplicates the selected element', async ({ page }) => {
    await designer.addControl('Label');
    await designer.setProperty('x', '40');
    await designer.setProperty('y', '40');
    // Move focus out of the property input: shortcuts are ignored while typing
    await designer.selectFirst('Label');

    await page.keyboard.press('Control+c');
    await page.keyboard.press('Control+v');

    await expect(designer.canvasElements('Label')).toHaveCount(2);
    // The pasted copy is selected and offset from the original
    await designer.expectPropertyNumber('x', value => value > 40);
  });

  test('the pasted element keeps the copied properties', async ({ page }) => {
    await designer.addControl('Button');
    await designer.setProperty('text', 'Save changes');
    await designer.selectFirst('Button');

    await page.keyboard.press('Control+c');
    await page.keyboard.press('Control+v');

    const xaml = await designer.xamlWhen(
      value => (value.match(/Text="Save changes"/g) || []).length === 2
    );
    expect((xaml.match(/Text="Save changes"/g) || []).length).toBe(2);
  });

  test('pasting assigns unique names so the XAML stays valid', async ({ page }) => {
    await designer.addControl('Label');
    await page.keyboard.press('Control+c');
    await page.keyboard.press('Control+v');

    const xaml = await designer.getXaml();
    const names = [...xaml.matchAll(/x:Name="([^"]+)"/g)].map(match => match[1]);
    expect(new Set(names).size).toBe(names.length);
  });

  test('Ctrl+X removes the element and pastes it back', async ({ page }) => {
    await designer.addControl('Label');
    await page.keyboard.press('Control+x');
    await expect(designer.canvasElements('Label')).toHaveCount(0);

    await page.keyboard.press('Control+v');
    await expect(designer.canvasElements('Label')).toHaveCount(1);
  });

  test('the toolbar copy and paste buttons work', async ({ page }) => {
    await designer.addControl('Image');
    await designer.selectFirst('Image');

    await page.getByTestId('toolbar-copy').click();
    await page.getByTestId('toolbar-paste').click();

    await expect(designer.canvasElements('Image')).toHaveCount(2);
  });

  test('copying a container also copies its children', async ({ page }) => {
    await designer.addControl('VerticalStackLayout');
    await designer.addControl('Label');

    await designer.selectFirst('VerticalStackLayout');
    await page.keyboard.press('Control+c');
    await page.keyboard.press('Control+v');

    await expect(designer.canvasElements('VerticalStackLayout')).toHaveCount(2);
    await expect(designer.canvasElements('Label')).toHaveCount(2);
  });

  test('a pasted copy can be undone in a single step', async ({ page }) => {
    await designer.addControl('Label');
    await page.keyboard.press('Control+c');
    await page.keyboard.press('Control+v');
    await expect(designer.canvasElements('Label')).toHaveCount(2);

    await designer.undoButton.click();
    await expect(designer.canvasElements('Label')).toHaveCount(1);
  });

  test('a selection can be saved as a reusable template and inserted again', async ({ page }) => {
    await designer.addControl('Button');
    await designer.setProperty('text', 'Primary');
    await designer.selectFirst('Button');

    page.once('dialog', dialog => dialog.accept('PrimaryButton'));
    await page.getByTestId('save-template').click();

    await designer.openToolbox();
    const template = page.getByTestId('template-PrimaryButton');
    await expect(template).toBeVisible();

    await template.click();
    await expect(designer.canvasElements('Button')).toHaveCount(2);
    const xaml = await designer.getXaml();
    expect((xaml.match(/Text="Primary"/g) || []).length).toBe(2);
  });

  test('templates survive a reload and can be deleted', async ({ page }) => {
    await designer.addControl('Button');
    await designer.selectFirst('Button');
    page.once('dialog', dialog => dialog.accept('Reusable'));
    await page.getByTestId('save-template').click();

    await page.reload();
    await designer.openToolbox();
    await expect(page.getByTestId('template-Reusable')).toBeVisible();

    await page.getByTestId('template-delete-Reusable').click();
    await expect(page.getByTestId('template-Reusable')).toHaveCount(0);
  });

  test('cancelling the template prompt saves nothing', async ({ page }) => {
    await designer.addControl('Button');
    await designer.selectFirst('Button');

    page.once('dialog', dialog => dialog.dismiss());
    await page.getByTestId('save-template').click();

    await designer.openToolbox();
    await expect(page.getByTestId('templates-section').locator('.toolbox-list-item')).toHaveCount(0);
  });

  for (const starter of ['login', 'list', 'profile', 'settings']) {
    test(`the ${starter} starter page populates the canvas`, async ({ page }) => {
      await designer.openToolbox();
      await page.getByTestId(`starter-${starter}`).click();

      await expect(designer.canvasElements().first()).toBeVisible();
      // The tree renders from an observable, so poll instead of taking a one-shot count
      await expect.poll(() => designer.canvasElements().count()).toBeGreaterThan(2);
      await designer.expectXamlToContain('<ContentPage');
    });
  }

  test('applying a starter page can be undone', async ({ page }) => {
    await designer.addControl('Label');
    await designer.openToolbox();
    await page.getByTestId('starter-login').click();
    await expect(designer.canvasElements('Entry').first()).toBeVisible();

    await designer.undoButton.click();
    await expect(designer.canvasElements('Label')).toHaveCount(1);
  });
});
