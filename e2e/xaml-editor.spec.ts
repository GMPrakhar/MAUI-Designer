import { test, expect } from '@playwright/test';
import { DesignerPage, SAMPLE_XAML } from './helpers/designer-page';

test.describe('XAML editor', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
  });

  test('generates XAML for the current design', async () => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');
    await designer.setProperty('text', 'Generated');

    const xaml = await designer.getXaml();
    expect(xaml).toContain('<ContentPage');
    expect(xaml).toContain('<AbsoluteLayout');
    expect(xaml).toContain('Text="Generated"');
    expect(xaml).toContain('AbsoluteLayout.LayoutBounds=');
  });

  test('applies hand written XAML to the canvas', async () => {
    await designer.applyXaml(SAMPLE_XAML);

    await expect(designer.xamlStatus).toContainText('applied successfully');
    await expect(designer.canvasElements('Label')).toHaveCount(1);
    await expect(designer.canvasElements('Button')).toHaveCount(1);
    await expect(designer.canvasElements('Label').first()).toContainText('Hello MAUI');
    await expect(designer.canvasElements('Label').first()).toHaveCSS('width', '180px');
  });

  test('reports a parse error for malformed XAML', async () => {
    await designer.applyXaml('<ContentPage><AbsoluteLayout></ContentPage>');
    await expect(designer.xamlStatus).toContainText('Parse error');
  });

  test('reports an error when no layout is present', async () => {
    await designer.applyXaml(`<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"><Label Text="lonely" /></ContentPage>`);
    await expect(designer.xamlStatus).toContainText('No valid layout element found');
  });

  test('rejects empty content', async () => {
    await designer.applyXaml('   ');
    await expect(designer.xamlStatus).toContainText('cannot be empty');
  });

  test('reset restores the XAML of the current design', async () => {
    await designer.addControl('Label');
    const generated = await designer.xamlWhen(xaml => xaml.includes('<Label '));

    await designer.setXaml('<broken');
    await designer.resetXaml();
    await expect(designer.xamlTextarea).toHaveValue(generated);
  });

  test('round trips a design through apply and regenerate', async () => {
    await designer.applyXaml(SAMPLE_XAML);
    const firstPass = await designer.xamlWhen(xaml => xaml.includes('Text="Hello MAUI"'));

    await designer.applyXaml(firstPass);
    const secondPass = await designer.xamlWhen(xaml => xaml.includes('Text="Hello MAUI"'));

    expect(secondPass).toBe(firstPass);
    expect(secondPass).toContain('Text="Hello MAUI"');
  });

  test('round trips a grid definition', async () => {
    await designer.applyXaml(`<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
    <Grid x:Name="Layout" WidthRequest="400" HeightRequest="300">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="2*" />
        </Grid.RowDefinitions>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="80" />
            <ColumnDefinition Width="*" />
            <ColumnDefinition Width="*" />
        </Grid.ColumnDefinitions>
        <Label x:Name="Cell" Text="Cell" Grid.Row="1" Grid.Column="2" />
    </Grid>
</ContentPage>`);

    const xaml = await designer.xamlWhen(value => value.includes('<RowDefinition '));
    expect(xaml).toContain('<RowDefinition Height="Auto" />');
    expect(xaml).toContain('<RowDefinition Height="2*" />');
    expect(xaml).toContain('<ColumnDefinition Width="80" />');
    expect((xaml.match(/<ColumnDefinition /g) || []).length).toBe(3);
    expect(xaml).toContain('Grid.Row="1"');
    expect(xaml).toContain('Grid.Column="2"');
  });

  test('reads the comma separated grid definition shorthand', async () => {
    // MAUI accepts RowDefinitions="Auto,2*" as shorthand for the property
    // element form. It used to be ignored, so a grid written this way - which
    // is how most real XAML writes it - imported as a default 2x2.
    await designer.applyXaml(`<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
    <Grid x:Name="Layout" WidthRequest="400" HeightRequest="300"
          RowDefinitions="Auto,2*" ColumnDefinitions="80,*,*">
        <Label x:Name="Cell" Text="Cell" Grid.Row="1" Grid.Column="2" />
    </Grid>
</ContentPage>`);

    const xaml = await designer.xamlWhen(value => value.includes('<RowDefinition '));
    expect(xaml).toContain('<RowDefinition Height="Auto" />');
    expect(xaml).toContain('<RowDefinition Height="2*" />');
    expect(xaml).toContain('<ColumnDefinition Width="80" />');
    expect((xaml.match(/<ColumnDefinition /g) || []).length).toBe(3);
    expect((xaml.match(/<RowDefinition /g) || []).length).toBe(2);
  });

  test('downloads the XAML file', async ({ page }) => {    await designer.addControl('Label');
    const downloadPromise = page.waitForEvent('download');
    await page.getByTestId('xaml-download').click();
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toBe('MainPage.xaml');
  });

  test('imports a XAML file from disk', async ({ page }) => {
    await page.getByTestId('xaml-file-input').setInputFiles({
      name: 'MainPage.xaml',
      mimeType: 'text/xml',
      buffer: Buffer.from(SAMPLE_XAML, 'utf-8')
    });

    await expect(designer.xamlStatus).toContainText('applied successfully');
    await expect(designer.canvasElements('Label').first()).toContainText('Hello MAUI');
  });

  test('converts an imported SVG icon into Path elements', async ({ page }) => {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32"><path d="M4 4h24v24H4z" fill="#336699"/></svg>`;
    await page.getByTestId('xaml-file-input').setInputFiles({
      name: 'icon.svg',
      mimeType: 'image/svg+xml',
      buffer: Buffer.from(svg, 'utf-8')
    });

    await expect(designer.xamlStatus).toContainText('applied successfully');
    await expect(designer.canvasElements('Path')).toHaveCount(1);
    await designer.expectXamlToContain('Data="M4 4h24v24H4z"');
  });
});
