import { test, expect } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

test.describe('Hierarchy panel', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
  });

  test('shows the root layout and an empty hint', async ({ page }) => {
    await designer.openHierarchy();
    await expect(page.getByTestId('hierarchy-node-root')).toBeVisible();
    await expect(page.locator('.empty-state')).toBeVisible();
  });

  test('lists added elements and keeps selection in sync', async ({ page }) => {
    await designer.addControl('Label');
    await designer.addControl('Button');
    await designer.openHierarchy();

    await expect(designer.hierarchyNodes()).toHaveCount(3);

    const labelNode = designer.hierarchy.locator('[data-element-type="Label"]');
    await labelNode.click();
    await expect(labelNode).toHaveAttribute('data-selected', 'true');
    await expect(page.getByTestId('selected-element-type')).toHaveText('Label');
    await expect(designer.canvasElements('Label').first()).toHaveAttribute('data-selected', 'true');
  });

  test('deletes an element from the tree', async ({ page }) => {
    await designer.addControl('Editor');
    await designer.openHierarchy();

    const node = designer.hierarchy.locator('[data-element-type="Editor"]');
    const testId = await node.getAttribute('data-testid');
    const elementId = testId!.replace('hierarchy-node-', '');

    await page.getByTestId(`hierarchy-delete-${elementId}`).click();
    await expect(designer.canvasElements('Editor')).toHaveCount(0);
    await expect(designer.hierarchyNodes()).toHaveCount(1);
  });

  test('does not offer deleting the root layout', async ({ page }) => {
    await expect(page.getByTestId('hierarchy-delete-root')).toHaveCount(0);
  });

  test('nested elements are indented under their parent', async ({ page }) => {
    await designer.addControl('Grid');
    await designer.addControl('Label');
    await designer.openHierarchy();

    const gridNode = designer.hierarchy.locator('[data-element-type="Grid"]');
    const labelNode = designer.hierarchy.locator('[data-element-type="Label"]');

    const gridPadding = await gridNode.locator('xpath=..').evaluate(node => getComputedStyle(node).paddingLeft);
    const labelPadding = await labelNode.locator('xpath=..').evaluate(node => getComputedStyle(node).paddingLeft);

    expect(parseFloat(labelPadding)).toBeGreaterThan(parseFloat(gridPadding));
  });
});
