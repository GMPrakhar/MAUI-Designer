import { test, expect } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

test.describe('Toolbox', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
    await designer.openToolbox();
  });

  test('lists the control categories', async ({ page }) => {
    await expect(page.getByTestId('toolbox-item-Label')).toBeVisible();
    await expect(page.getByTestId('toolbox-item-Button')).toBeVisible();
    await expect(page.getByTestId('toolbox-item-Grid')).toBeVisible();
    await expect(page.getByTestId('toolbox-item-ScrollView')).toBeVisible();
  });

  test('filters controls with the search box', async ({ page }) => {
    await designer.toolboxSearch.fill('button');
    await expect(page.getByTestId('toolbox-item-Button')).toBeVisible();
    await expect(page.getByTestId('toolbox-item-Label')).toHaveCount(0);

    await page.getByTestId('toolbox-search-clear').click();
    await expect(page.getByTestId('toolbox-item-Label')).toBeVisible();
  });

  test('shows an empty state when nothing matches', async ({ page }) => {
    await designer.toolboxSearch.fill('zzzz-not-a-control');
    await expect(page.getByTestId('toolbox-empty')).toBeVisible();
  });

  test('clicking a control adds it to the canvas', async () => {
    await designer.addControl('Label');
    await expect(designer.canvasElements('Label')).toHaveCount(1);
    await expect(designer.canvasElements('Label').first()).toContainText('Label');
  });

  test('dragging a control onto the canvas creates it at the drop point', async () => {
    await designer.dragControlToCanvas('Button', { x: 220, y: 140 });

    const button = designer.canvasElements('Button').first();
    await expect(button).toHaveCount(1);

    // Element is selected after the drop, so the properties panel shows its position
    expect(Number(await designer.propertyValue('x'))).toBeGreaterThan(100);
    expect(Number(await designer.propertyValue('y'))).toBeGreaterThan(50);
  });

  test('adds a control into the selected layout', async () => {
    await designer.addControl('VerticalStackLayout');
    await designer.addControl('Label');

    await designer.openHierarchy();
    // Label nests under the stack layout, so the tree has root > layout > label
    await expect(designer.hierarchyNodes()).toHaveCount(3);

    const stack = designer.canvasElements('VerticalStackLayout').first();
    await expect(stack.locator('[data-element-type="Label"]')).toHaveCount(1);
  });
});
