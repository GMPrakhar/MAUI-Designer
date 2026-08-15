import { test, expect } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

test.describe('Properties panel', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
  });

  test('shows a placeholder when nothing is selected', async ({ page }) => {
    await expect(page.locator('.no-selection')).toBeVisible();
  });

  test('edits text, size and colours of a label', async ({ page }) => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');

    await designer.setProperty('text', 'Welcome home');
    await designer.setProperty('width', '260');
    await designer.setProperty('height', '48');
    await designer.setProperty('fontSize', '22');

    const label = designer.canvasElements('Label').first();
    await expect(label).toContainText('Welcome home');
    await expect(label).toHaveCSS('width', '260px');
    await expect(label).toHaveCSS('height', '48px');
    await expect(label).toHaveCSS('font-size', '22px');

    await page.getByTestId('prop-textColor').fill('#ff0000');
    await page.getByTestId('prop-textColor').dispatchEvent('input');
    await expect(label).toHaveCSS('color', 'rgb(255, 0, 0)');
  });

  test('renames an element and reflects it in the hierarchy', async ({ page }) => {
    await designer.addControl('Button');
    await designer.selectFirst('Button');

    await page.getByTestId('prop-name').fill('SubmitButton');
    await page.getByTestId('prop-name').blur();

    await expect(page.getByTestId('selected-element-name')).toContainText('SubmitButton');
    await designer.openHierarchy();
    await expect(designer.hierarchy).toContainText('SubmitButton');
  });

  test('moves an element by editing X and Y', async () => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');

    await designer.setProperty('x', '150');
    await designer.setProperty('y', '90');

    const label = designer.canvasElements('Label').first();
    const transform = await label.evaluate(node => getComputedStyle(node).transform);
    expect(transform).toContain('150');
    expect(transform).toContain('90');
  });

  test('toggles visibility state', async ({ page }) => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');

    await page.getByTestId('prop-isVisible').uncheck();
    await designer.expectXamlToContain('IsVisible="False"');
    await designer.applyXaml(await designer.getXaml());
    await designer.expectXamlToContain('IsVisible="False"');
  });

  test('exposes orientation and spacing for stack layouts', async ({ page }) => {
    await designer.addControl('StackLayout');
    await designer.selectFirst('StackLayout');

    await expect(page.getByTestId('prop-orientation')).toBeVisible();
    await page.getByTestId('prop-orientation').selectOption('Horizontal');
    await page.getByTestId('prop-spacing').fill('12');
    await page.getByTestId('prop-spacing').dispatchEvent('input');

    await designer.expectXamlToContain('HorizontalStackLayout', 'Spacing="12"');
  });

  test('duplicate and delete buttons act on the selected element', async ({ page }) => {
    await designer.addControl('Entry');
    await designer.selectFirst('Entry');

    await page.getByTestId('prop-duplicate').click();
    await expect(designer.canvasElements('Entry')).toHaveCount(2);

    await page.getByTestId('prop-delete').click();
    await expect(designer.canvasElements('Entry')).toHaveCount(1);
    await expect(page.locator('.no-selection')).toBeVisible();
  });
});

test.describe('Grid editing', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
    await designer.addControl('Grid');
    await designer.selectFirst('Grid');
  });

  test('starts with a two by two definition', async ({ page }) => {
    await expect(page.getByTestId('grid-row-count')).toHaveText('Rows: 2');
    await expect(page.getByTestId('grid-column-count')).toHaveText('Columns: 2');
  });

  test('adds and removes rows and columns', async ({ page }) => {
    await page.getByTestId('grid-add-row').click();
    await expect(page.getByTestId('grid-row-count')).toHaveText('Rows: 3');

    await page.getByTestId('grid-add-column').click();
    await expect(page.getByTestId('grid-column-count')).toHaveText('Columns: 3');

    await page.getByTestId('grid-remove-row-2').click();
    await expect(page.getByTestId('grid-row-count')).toHaveText('Rows: 2');

    await page.getByTestId('grid-remove-column-2').click();
    await expect(page.getByTestId('grid-column-count')).toHaveText('Columns: 2');
  });

  test('generates the edited grid definition into XAML', async ({ page }) => {
    await page.getByTestId('grid-add-row').click();
    await page.getByTestId('grid-row-type-0').selectOption('Auto');
    await page.getByTestId('grid-column-type-1').selectOption('Absolute');
    await page.getByTestId('grid-column-value-1').fill('120');
    await page.getByTestId('grid-column-value-1').dispatchEvent('input');

    const xaml = await designer.xamlWhen(value => value.includes('<ColumnDefinition Width="120" />'));
    expect(xaml).toContain('<RowDefinition Height="Auto" />');
    expect(xaml).toContain('<ColumnDefinition Width="120" />');
    expect((xaml.match(/<RowDefinition /g) || []).length).toBe(3);
  });

  test('keeps the last row and column', async ({ page }) => {
    await page.getByTestId('grid-remove-row-1').click();
    await expect(page.getByTestId('grid-row-count')).toHaveText('Rows: 1');
    await expect(page.getByTestId('grid-remove-row-0')).toBeDisabled();
  });
});
