import { test, expect } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

test.describe('Multi selection', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
    // Position each control as it is added: a freshly added control is already
    // selected, and stacking them would make the earlier one unclickable.
    await designer.addControl('Label');
    await designer.setProperty('x', '40');
    await designer.setProperty('y', '40');
    await designer.addControl('Button');
    await designer.setProperty('x', '40');
    await designer.setProperty('y', '140');
  });

  test('shift clicking adds elements to the selection', async () => {
    await designer.selectFirst('Label');
    await expect(designer.selectionCount()).toHaveText('1 selected');

    await designer.toggleSelect('Button');
    await expect(designer.selectionCount()).toHaveText('2 selected');
    await expect(designer.selectedCanvasElement()).toHaveCount(2);
  });

  test('shift clicking a selected element removes it again', async () => {
    await designer.selectFirst('Label');
    await designer.toggleSelect('Button');
    await designer.toggleSelect('Button');

    await expect(designer.selectionCount()).toHaveText('1 selected');
  });

  test('the properties panel switches to the multi selection editor', async ({ page }) => {
    await designer.selectFirst('Label');
    await designer.toggleSelect('Button');

    await expect(page.getByTestId('multi-selection-panel')).toBeVisible();
    await expect(page.getByTestId('multi-selection-title')).toHaveText('2 elements selected');
  });

  test('a marquee selects every element it touches', async () => {
    await designer.marquee({ x: 20, y: 20 }, { x: 320, y: 260 });

    await expect(designer.selectionCount()).toHaveText('2 selected');
  });

  test('a marquee over empty space clears the selection', async () => {
    await designer.selectFirst('Label');
    await designer.marquee({ x: 200, y: 320 }, { x: 380, y: 460 });

    await expect(designer.selectionCount()).toHaveText('0 selected');
  });

  test('Ctrl+A selects every child of the root layout', async ({ page }) => {
    await page.getByTestId('designer-canvas').click({ position: { x: 300, y: 420 } });
    await page.keyboard.press('Control+a');

    await expect(designer.selectionCount()).toHaveText('2 selected');
  });

  test('editing a shared property updates every selected element', async ({ page }) => {
    await designer.selectFirst('Label');
    await designer.toggleSelect('Button');

    await page.getByTestId('multi-width').fill('180');
    await page.getByTestId('multi-width').dispatchEvent('input');

    await designer.expectXamlToContain('WidthRequest="180"');
    const xaml = await designer.getXaml();
    expect((xaml.match(/WidthRequest="180"/g) || []).length).toBe(2);
  });

  test('deleting a multi selection removes every element in one undo step', async ({ page }) => {
    await designer.selectFirst('Label');
    await designer.toggleSelect('Button');

    await page.keyboard.press('Delete');
    await expect(designer.canvasElements()).toHaveCount(1); // only the root remains

    await designer.undoButton.click();
    await expect(designer.canvasElements('Label')).toHaveCount(1);
    await expect(designer.canvasElements('Button')).toHaveCount(1);
  });

  test('arrow keys nudge every selected element', async ({ page }) => {
    await designer.selectFirst('Label');
    await designer.toggleSelect('Button');

    await page.keyboard.press('Shift+ArrowRight');

    await designer.selectFirst('Label');
    await designer.expectProperty('x', 50);
    await designer.selectFirst('Button');
    await designer.expectProperty('x', 50);
  });

  test('duplicating a multi selection copies every element', async ({ page }) => {
    await designer.selectFirst('Label');
    await designer.toggleSelect('Button');

    await page.getByTestId('multi-duplicate').click();

    await expect(designer.canvasElements('Label')).toHaveCount(2);
    await expect(designer.canvasElements('Button')).toHaveCount(2);
  });

  test('only a single selection offers resize handles', async ({ page }) => {
    await designer.selectFirst('Label');
    await expect(page.getByTestId('resize-handle-se')).toHaveCount(1);

    await designer.toggleSelect('Button');
    await expect(page.getByTestId('resize-handle-se')).toHaveCount(0);
  });
});
