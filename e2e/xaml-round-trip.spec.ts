import { test, expect } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

const PAGE_WITH_UNKNOWN_MARKUP = `<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="YourApp.MainPage">
    <VerticalStackLayout x:Name="Root">
        <Label x:Name="Greeting" Text="Hi" Rotation="15" Opacity="0.5"
               HorizontalOptions="Center" VerticalOptions="EndAndExpand" />
    </VerticalStackLayout>
</ContentPage>`;

test.describe('XAML round trip fidelity', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
  });

  test('importing XAML does not drop attributes the designer does not model', async () => {
    await designer.applyXaml(PAGE_WITH_UNKNOWN_MARKUP);

    await designer.expectXamlToContain('Rotation="15"');
    await designer.expectXamlToContain('Opacity="0.5"');
  });

  test('importing XAML keeps layout alignment', async () => {
    await designer.applyXaml(PAGE_WITH_UNKNOWN_MARKUP);

    await designer.expectXamlToContain('HorizontalOptions="Center"');
    // The Xamarin.Forms AndExpand suffix is obsolete in MAUI, so it is normalised away.
    await designer.expectXamlToContain('VerticalOptions="End"');
  });

  test('the alignment dropdowns reflect the imported values', async ({ page }) => {
    await designer.applyXaml(PAGE_WITH_UNKNOWN_MARKUP);
    await designer.selectFirst('Label');

    await expect(page.getByTestId('prop-horizontalOptions')).toHaveValue('Center');
    await expect(page.getByTestId('prop-verticalOptions')).toHaveValue('End');
  });

  test('setting horizontal alignment writes it into the XAML', async ({ page }) => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');

    await page.getByTestId('prop-horizontalOptions').selectOption('Fill');

    await designer.expectXamlToContain('HorizontalOptions="Fill"');
  });

  test('setting vertical alignment writes it into the XAML', async ({ page }) => {
    await designer.addControl('Button');
    await designer.selectFirst('Button');

    await page.getByTestId('prop-verticalOptions').selectOption('Start');

    await designer.expectXamlToContain('VerticalOptions="Start"');
  });

  test('clearing an alignment removes the attribute again', async ({ page }) => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');

    await page.getByTestId('prop-horizontalOptions').selectOption('Center');
    await designer.expectXamlToContain('HorizontalOptions="Center"');

    await page.getByTestId('prop-horizontalOptions').selectOption('');

    await expect.poll(async () => await designer.getXaml()).not.toContain('HorizontalOptions=');
  });

  test('a freshly added control does not emit alignment attributes', async () => {
    await designer.addControl('Label');

    const xaml = await designer.getXaml();

    expect(xaml).not.toContain('HorizontalOptions=');
    expect(xaml).not.toContain('VerticalOptions=');
  });
});
