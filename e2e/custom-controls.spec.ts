import { test, expect } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

const SYNCFUSION_MANIFEST = JSON.stringify({
  id: 'syncfusion-inputs',
  package: 'Syncfusion.Maui.Inputs',
  xmlns: { prefix: 'sf', uri: 'clr-namespace:Syncfusion.Maui.Inputs;assembly=Syncfusion.Maui.Inputs' },
  controls: [
    {
      tag: 'SfComboBox',
      displayName: 'Combo box',
      icon: 'arrow_drop_down_circle',
      defaultWidth: 200,
      defaultHeight: 40,
      preview: { kind: 'box', label: '{Placeholder}' },
      properties: [
        { name: 'Placeholder', type: 'string', defaultValue: 'Select an item' },
        { name: 'IsEditable', type: 'boolean', defaultValue: false },
        { name: 'MaxDropDownHeight', type: 'number', defaultValue: 200 }
      ]
    }
  ]
});

const THIRD_PARTY_XAML = `<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:telerik="clr-namespace:Telerik.Maui.Controls;assembly=Telerik.Maui.Controls"
             x:Class="YourApp.MainPage">
    <AbsoluteLayout x:Name="RootLayout" WidthRequest="800" HeightRequest="600">
        <telerik:RadCalendar x:Name="Calendar" SelectionMode="Single" DayCellHeight="32"
                             AbsoluteLayout.LayoutBounds="20,20,280,240" WidthRequest="280" HeightRequest="240" />
    </AbsoluteLayout>
</ContentPage>`;

test.describe('Custom controls from NuGet packages', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
    await page.reload();
    await expect(designer.canvas).toBeVisible();
  });

  test('the bundled CommunityToolkit controls are in the toolbox', async ({ page }) => {
    await designer.openToolbox();

    await expect(page.getByTestId('custom-controls-section')).toBeVisible();
    await expect(page.getByTestId('custom-item-toolkit-AvatarView')).toBeVisible();
    await expect(page.getByTestId('custom-item-toolkit-Expander')).toBeVisible();
  });

  test('adding a custom control generates the prefixed tag and its namespace', async () => {
    await designer.addCustomControl('toolkit', 'AvatarView');

    await designer.expectXamlToContain(
      'xmlns:toolkit="http://schemas.microsoft.com/dotnet/2022/maui/toolkit"',
      '<toolkit:AvatarView',
      'Text="AB"',
      'CornerRadius="24"'
    );
  });

  test('the canvas renders a preview for the custom control', async ({ page }) => {
    await designer.addCustomControl('toolkit', 'AvatarView');

    const preview = page.locator('[data-custom-tag="AvatarView"]');
    await expect(preview).toBeVisible();
    await expect(preview).toContainText('AB');
  });

  test('manifest properties are editable in the properties panel', async ({ page }) => {
    await designer.addCustomControl('toolkit', 'AvatarView');

    await expect(page.getByTestId('custom-section')).toBeVisible();
    await expect(page.getByTestId('custom-package')).toHaveText('CommunityToolkit.Maui');

    const text = page.getByTestId('custom-Text');
    await text.fill('MP');
    await text.dispatchEvent('input');

    await designer.expectXamlToContain('Text="MP"');
    await expect(page.locator('[data-custom-preview="AvatarView"]')).toHaveText('MP');
  });

  test('boolean and enum manifest properties generate XAML', async ({ page }) => {
    await designer.addCustomControl('toolkit', 'MediaElement');

    await page.getByTestId('custom-ShouldAutoPlay').check();
    await page.getByTestId('custom-Aspect').selectOption('AspectFill');

    await designer.expectXamlToContain('ShouldAutoPlay="True"', 'Aspect="AspectFill"');
  });

  test('a custom control can be bound to a view model path', async ({ page }) => {
    await designer.addCustomControl('toolkit', 'AvatarView');

    const binding = page.getByTestId('binding-Text');
    await binding.fill('User.Initials');
    await binding.dispatchEvent('input');

    await designer.expectXamlToContain('Text="{Binding User.Initials}"');
    const xaml = await designer.getXaml();
    expect(xaml).not.toContain('Text="AB"');
  });

  test('a custom container accepts child controls', async ({ page }) => {
    await designer.addCustomControl('toolkit', 'Expander');
    await designer.addControl('Label');

    const xaml = await designer.xamlWhen(value => value.includes('</toolkit:Expander>'));
    expect(xaml).toMatch(/<toolkit:Expander[\s\S]*<Label[\s\S]*<\/toolkit:Expander>/);
  });

  test('imported XAML keeps unknown third party controls', async ({ page }) => {
    await designer.applyXaml(THIRD_PARTY_XAML);

    await expect(page.locator('[data-custom-tag="RadCalendar"]')).toBeVisible();
    await designer.expectXamlToContain(
      'xmlns:telerik="clr-namespace:Telerik.Maui.Controls;assembly=Telerik.Maui.Controls"',
      '<telerik:RadCalendar',
      'SelectionMode="Single"',
      'DayCellHeight="32"'
    );
  });

  test('controls learned from XAML become available in the toolbox', async ({ page }) => {
    await designer.applyXaml(THIRD_PARTY_XAML);
    await designer.openToolbox();

    await expect(page.getByTestId('custom-item-telerik-RadCalendar')).toBeVisible();

    await page.getByTestId('custom-item-telerik-RadCalendar').click();
    await expect(page.locator('[data-custom-tag="RadCalendar"]')).toHaveCount(2);
  });

  test('a learned control keeps its attributes editable', async ({ page }) => {
    await designer.applyXaml(THIRD_PARTY_XAML);
    await page.locator('[data-custom-tag="RadCalendar"]').click({ position: { x: 6, y: 6 } });

    const mode = page.getByTestId('custom-SelectionMode');
    await mode.fill('Multiple');
    await mode.dispatchEvent('input');

    await designer.expectXamlToContain('SelectionMode="Multiple"');
  });

  test('a manifest can be imported from a JSON file', async ({ page }) => {
    await designer.openToolbox();
    await page.getByTestId('manifest-file').setInputFiles({
      name: 'syncfusion.json',
      mimeType: 'application/json',
      buffer: Buffer.from(SYNCFUSION_MANIFEST)
    });

    const item = page.getByTestId('custom-item-sf-SfComboBox');
    await expect(item).toBeVisible();

    await item.click();
    await designer.expectXamlToContain(
      'xmlns:sf="clr-namespace:Syncfusion.Maui.Inputs;assembly=Syncfusion.Maui.Inputs"',
      '<sf:SfComboBox',
      'Placeholder="Select an item"'
    );
  });

  test('an invalid manifest reports an error', async ({ page }) => {
    await designer.openToolbox();
    await page.getByTestId('manifest-file').setInputFiles({
      name: 'broken.json',
      mimeType: 'application/json',
      buffer: Buffer.from('{"package":"Broken"}')
    });

    await expect(page.getByTestId('manifest-error')).toBeVisible();
  });

  test('an imported manifest survives a reload and can be removed', async ({ page }) => {
    await designer.openToolbox();
    await page.getByTestId('manifest-file').setInputFiles({
      name: 'syncfusion.json',
      mimeType: 'application/json',
      buffer: Buffer.from(SYNCFUSION_MANIFEST)
    });
    await expect(page.getByTestId('custom-item-sf-SfComboBox')).toBeVisible();

    await page.reload();
    await designer.openToolbox();
    await expect(page.getByTestId('custom-item-sf-SfComboBox')).toBeVisible();

    await page.getByTestId('manifest-remove-syncfusion-inputs').click();
    await expect(page.getByTestId('custom-item-sf-SfComboBox')).toHaveCount(0);
  });

  test('the registry can be exported as JSON', async ({ page }) => {
    await designer.openToolbox();

    const download = page.waitForEvent('download');
    await page.getByTestId('export-manifests').click();
    const file = await download;

    expect(file.suggestedFilename()).toBe('maui-designer-controls.json');
  });

  test('custom controls can be searched in the toolbox', async ({ page }) => {
    await designer.openToolbox();
    await designer.toolboxSearch.fill('avatar');

    await expect(page.getByTestId('custom-item-toolkit-AvatarView')).toBeVisible();
    await expect(page.getByTestId('custom-item-toolkit-Expander')).toHaveCount(0);
    await expect(page.getByTestId('toolbox-empty')).toHaveCount(0);
  });

  test('a custom control participates in copy, paste and undo', async ({ page }) => {
    await designer.addCustomControl('toolkit', 'AvatarView');
    await page.locator('[data-custom-tag="AvatarView"]').click({ position: { x: 4, y: 4 } });

    await page.keyboard.press('Control+c');
    await page.keyboard.press('Control+v');
    await expect(page.locator('[data-custom-tag="AvatarView"]')).toHaveCount(2);

    await designer.undoButton.click();
    await expect(page.locator('[data-custom-tag="AvatarView"]')).toHaveCount(1);
  });
});
