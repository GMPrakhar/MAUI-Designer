import { test, expect } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

test.describe('Theming with AppThemeBinding', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
  });

  test('a light and dark pair is emitted as an AppThemeBinding', async ({ page }) => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');

    await page.getByTestId('theme-light-TextColor').fill('#ffffff');
    await page.getByTestId('theme-dark-TextColor').fill('#333333');

    await designer.expectXamlToContain('TextColor="{AppThemeBinding Light=#ffffff, Dark=#333333}"');
  });

  test('the canvas previews the colour for the current theme', async ({ page }) => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');

    await page.getByTestId('theme-light-BackgroundColor').fill('#ff0000');
    await page.getByTestId('theme-dark-BackgroundColor').fill('#0000ff');

    const label = page.locator('[data-element-type="Label"]').first();
    const background = () => label.evaluate(node => getComputedStyle(node).backgroundColor);

    await expect.poll(background).toBe('rgb(255, 0, 0)');

    await page.getByTestId('toggle-theme').click();

    // Same element, same design -- only the preview theme changed.
    await expect.poll(background).toBe('rgb(0, 0, 255)');
  });

  test('clearing a theme colour restores the plain literal', async ({ page }) => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');

    await page.getByTestId('theme-light-BackgroundColor').fill('#ff0000');
    await designer.expectXamlToContain('AppThemeBinding');

    await page.getByTestId('theme-clear-BackgroundColor').click();

    await expect.poll(() => designer.getXaml()).not.toContain('AppThemeBinding');
  });

  test('the clear button is only enabled once a theme colour is set', async ({ page }) => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');

    await expect(page.getByTestId('theme-clear-TextColor')).toBeDisabled();

    await page.getByTestId('theme-light-TextColor').fill('#123456');

    await expect(page.getByTestId('theme-clear-TextColor')).toBeEnabled();
  });

  test('hand written AppThemeBinding survives a round trip', async () => {
    await designer.applyXaml(`<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
  <AbsoluteLayout>
    <Label x:Name="Themed" Text="Hi" TextColor="{AppThemeBinding Light=#FFFFFF, Dark=#333333}" />
  </AbsoluteLayout>
</ContentPage>`);

    await designer.expectXamlToContain('TextColor="{AppThemeBinding Light=#FFFFFF, Dark=#333333}"');
  });

  test('only the colours that apply to the element type are offered', async ({ page }) => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');

    // A Label has text, so it gets a text colour; it is not a Path, so no fill.
    await expect(page.getByTestId('theme-row-BackgroundColor')).toBeVisible();
    await expect(page.getByTestId('theme-row-TextColor')).toBeVisible();
    await expect(page.getByTestId('theme-row-Fill')).toHaveCount(0);
  });
});
