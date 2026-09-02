import { test, expect } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

// Adapted from the MIT-licensed dotnet/maui EntryPage.xaml:
// https://github.com/dotnet/maui/blob/d2edf1972d09f6689a4621ba9ff42346ced6f1b1/src/Controls/samples/Controls.Sample/Pages/Controls/EntryPage.xaml
const COMPLEX_ENTRY_PAGE = `<views:BasePage
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:controls="clr-namespace:Maui.Controls.Sample.Pages"
    xmlns:views="clr-namespace:Maui.Controls.Sample.Pages.Base"
    xmlns:viewmodels="clr-namespace:Maui.Controls.Sample.ViewModels"
    x:Class="Maui.Controls.Sample.Pages.EntryPage"
    Title="Entry">
  <views:BasePage.Resources>
    <ResourceDictionary>
      <Style x:Key="EntryVisualStatesStyle" TargetType="Entry">
        <Setter Property="VisualStateManager.VisualStateGroups">
          <VisualStateGroupList>
            <VisualStateGroup x:Name="CommonStates">
              <VisualState x:Name="Focused">
                <VisualState.Setters>
                  <Setter Property="BackgroundColor" Value="Yellow" />
                </VisualState.Setters>
              </VisualState>
            </VisualStateGroup>
          </VisualStateGroupList>
        </Setter>
      </Style>
    </ResourceDictionary>
  </views:BasePage.Resources>
  <views:BasePage.BindingContext>
    <viewmodels:EntryViewModel />
  </views:BasePage.BindingContext>
  <views:BasePage.Content>
    <ScrollView>
      <VerticalStackLayout Padding="12">
        <Label Text="Password" Style="{StaticResource Headline}" />
        <Entry Text="Disabled" IsEnabled="False" />
        <HorizontalStackLayout>
          <CheckBox x:Name="chkIsPassword" IsChecked="true" />
          <Label Text="Is Password" VerticalOptions="Center" />
        </HorizontalStackLayout>
        <Entry IsPassword="{Binding IsChecked, Source={Reference chkIsPassword}}" />
        <Entry Text="Background">
          <Entry.Background>
            <LinearGradientBrush EndPoint="1,0">
              <GradientStop Color="Yellow" Offset="0.1" />
              <GradientStop Color="Green" Offset="1.0" />
            </LinearGradientBrush>
          </Entry.Background>
        </Entry>
        <controls:TransparentEntry />
        <HorizontalStackLayout>
          <Label Text="CursorPosition = 4" />
          <Slider x:Name="sldCursorPosition" WidthRequest="100" />
        </HorizontalStackLayout>
      </VerticalStackLayout>
    </ScrollView>
  </views:BasePage.Content>
</views:BasePage>`;

test('renders and preserves a complex official MAUI page', async ({ page }) => {
  const designer = new DesignerPage(page);
  await designer.goto();
  await page.locator('#device-select').selectOption('phone');
  await designer.applyXaml(COMPLEX_ENTRY_PAGE);

  await expect(designer.xamlStatus).toContainText('applied successfully');
  await expect(designer.canvasElements('ScrollView')).toHaveCount(1);
  await expect(designer.canvasElements('VerticalStackLayout')).toHaveCount(1);
  await expect(designer.canvasElements('StackLayout')).toHaveCount(2);
  await expect(designer.canvasElements('Label')).toHaveCount(3);
  await expect(designer.canvasElements('Entry')).toHaveCount(3);
  await expect(designer.canvasElements('CheckBox')).toHaveCount(1);
  await expect(designer.canvasElements('Slider')).toHaveCount(1);
  await expect(designer.canvasElements('ScrollView')).toHaveCSS('width', '390px');
  await expect(designer.canvasElements('ScrollView')).toHaveCSS('height', '844px');
  await expect(designer.canvasElements('VerticalStackLayout')).toHaveCSS('width', '388px');

  const horizontalStacks = designer.canvasElements('StackLayout');
  await expect(horizontalStacks.locator('.element-stack-layout')).toHaveCount(2);
  expect(await horizontalStacks.locator('.element-stack-layout')
    .evaluateAll(elements => elements.map(element => element.getAttribute('data-orientation'))))
    .toEqual(['Horizontal', 'Horizontal']);
  await expect(designer.canvasElements('CheckBox').locator('.material-icons')).toHaveText('check_box');

  const disabledEntry = designer.canvasElements('Entry').nth(0);
  await expect(disabledEntry).toHaveAttribute('data-enabled', 'false');
  await expect(disabledEntry.locator('input')).toHaveValue('Disabled');
  await expect(disabledEntry.locator('input')).toBeDisabled();

  const gradientEntry = designer.canvasElements('Entry').nth(2);
  await expect(gradientEntry.locator('input')).toHaveValue('Background');
  await expect(gradientEntry).toHaveCSS(
    'background-image',
    'linear-gradient(90deg, rgb(255, 255, 0) 10%, rgb(0, 128, 0) 100%)'
  );
  await expect(designer.canvas.locator('[data-custom-tag="TransparentEntry"]')).toHaveCount(1);
  await expect(designer.canvasElements('Slider')).toHaveCSS('width', '100px');

  const roundTripped = await designer.getXaml();
  expect(roundTripped).toContain('<views:BasePage');
  expect(roundTripped).toContain('<views:BasePage.Resources>');
  expect(roundTripped).toContain('<views:BasePage.BindingContext>');
  expect(roundTripped).toContain('IsPassword="{Binding IsChecked, Source={Reference chkIsPassword}}"');
  expect(roundTripped).toContain('<LinearGradientBrush EndPoint="1,0">');
  expect(roundTripped).toContain('<controls:TransparentEntry');
});
