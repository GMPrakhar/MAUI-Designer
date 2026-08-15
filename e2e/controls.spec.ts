import { test, expect } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

test.describe('Control library', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
  });

  const controls = [
    'SearchBar',
    'CheckBox',
    'Switch',
    'Slider',
    'Stepper',
    'DatePicker',
    'ProgressBar',
    'ActivityIndicator',
    'Border',
    'CollectionView'
  ];

  for (const control of controls) {
    test(`adds a ${control} and generates it into XAML`, async () => {
      await designer.addControl(control);
      await expect(designer.canvasElements(control)).toHaveCount(1);
      await designer.expectXamlToContain(`<${control} `);
    });
  }

  test('toggles a CheckBox and keeps the state in XAML', async ({ page }) => {
    await designer.addControl('CheckBox');
    await designer.selectFirst('CheckBox');

    await page.getByTestId('prop-isChecked').check();
    await designer.expectXamlToContain('IsChecked="True"');

    await page.getByTestId('prop-isChecked').uncheck();
    await designer.expectXamlToContain('IsChecked="False"');
  });

  test('edits the slider range and value', async ({ page }) => {
    await designer.addControl('Slider');
    await designer.selectFirst('Slider');

    await designer.setProperty('minimum', '10');
    await designer.setProperty('maximum', '20');
    await designer.setProperty('value', '15');

    await designer.expectXamlToContain('Minimum="10"', 'Maximum="20"', 'Value="15"');

    // The preview thumb sits half way through the range
    const fill = designer.canvasElements('Slider').locator('.slider-fill');
    await expect(fill).toHaveAttribute('style', /width: 50%/);
  });

  test('writes an Entry placeholder separately from its text', async ({ page }) => {
    await designer.addControl('Entry');
    await designer.selectFirst('Entry');

    await designer.setProperty('placeholder', 'Email address');
    await designer.setProperty('text', 'me@example.com');

    await designer.expectXamlToContain('Placeholder="Email address"', 'Text="me@example.com"');
  });

  test('configures a Border stroke and radius', async ({ page }) => {
    await designer.addControl('Border');
    await designer.selectFirst('Border');

    await designer.setProperty('borderWidth', '3');
    await designer.setProperty('cornerRadius', '12');

    await designer.expectXamlToContain('StrokeThickness="3"', 'StrokeShape="RoundRectangle 12"');
  });

  test('renders repeated items for a CollectionView', async ({ page }) => {
    await designer.addControl('CollectionView');
    await designer.selectFirst('CollectionView');

    await designer.setProperty('itemCount', '5');
    await expect(designer.canvasElements('CollectionView').locator('.collection-item')).toHaveCount(5);
  });
});

test.describe('Data bindings', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
  });

  test('binds a property and generates a Binding expression', async ({ page }) => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');

    await page.getByTestId('binding-Text').fill('UserName');
    await page.getByTestId('binding-Text').dispatchEvent('input');

    await designer.expectXamlToContain('Text="{Binding UserName}"');
  });

  test('a binding replaces the literal value', async ({ page }) => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');

    await designer.setProperty('text', 'Static');
    await designer.expectXamlToContain('Text="Static"');

    await page.getByTestId('binding-Text').fill('Title');
    await page.getByTestId('binding-Text').dispatchEvent('input');

    const xaml = await designer.xamlWhen(value => value.includes('{Binding Title}'));
    expect(xaml).not.toContain('Text="Static"');
  });

  test('round trips bindings through the XAML editor', async ({ page }) => {
    await designer.addControl('Switch');
    await designer.selectFirst('Switch');

    await page.getByTestId('binding-IsToggled').fill('IsDarkMode');
    await page.getByTestId('binding-IsToggled').dispatchEvent('input');
    await designer.expectXamlToContain('IsToggled="{Binding IsDarkMode}"');

    await designer.applyXaml(await designer.getXaml());
    await designer.selectFirst('Switch');
    await expect(page.getByTestId('binding-IsToggled')).toHaveValue('IsDarkMode');
  });

  test('clearing a binding restores the literal property', async ({ page }) => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');

    await page.getByTestId('binding-Text').fill('Name');
    await page.getByTestId('binding-Text').dispatchEvent('input');
    await designer.expectXamlToContain('{Binding Name}');

    await page.getByTestId('binding-Text').fill('');
    await page.getByTestId('binding-Text').dispatchEvent('input');

    const xaml = await designer.xamlWhen(value => !value.includes('{Binding'));
    expect(xaml).toContain('Text="Label"');
  });
});
