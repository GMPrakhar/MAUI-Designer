import { test, expect } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

test.describe('Fit to parent, drop clamp, hierarchy reorder', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
  });

  test('double-clicking an east handle fits the element to the parent width', async ({ page }) => {
    await designer.addControl('Button');
    await designer.selectFirst('Button');

    await page.getByTestId('resize-handle-e').dblclick();

    await designer.expectProperty('width', 800);
    await designer.expectProperty('x', 0);
    await designer.expectPropertyNumber('height', value => value < 800);
  });

  test('double-clicking a south-east handle fits both axes', async ({ page }) => {
    await designer.addControl('Label');
    await designer.selectFirst('Label');

    await page.getByTestId('resize-handle-se').dblclick();

    await designer.expectProperty('width', 800);
    await designer.expectProperty('height', 600);
    await designer.expectProperty('x', 0);
    await designer.expectProperty('y', 0);
  });

  test('adding a control into a smaller layout clamps its size', async ({ page }) => {
    await designer.applyXaml(`<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
    <AbsoluteLayout WidthRequest="800" HeightRequest="600">
        <AbsoluteLayout x:Name="Narrow" BackgroundColor="#eeeeee"
                        AbsoluteLayout.LayoutBounds="10,10,80,200"
                        WidthRequest="80" HeightRequest="200" />
    </AbsoluteLayout>
</ContentPage>`);

    await designer.canvas.locator('[data-element-name="Narrow"]').click();
    await designer.addControl('Button');

    await designer.expectPropertyNumber('width', value => value <= 80);
    const xaml = await designer.getXaml();
    expect(xaml).toMatch(/<AbsoluteLayout x:Name="Narrow"[\s\S]*?<Button/);
  });

  test('dropping an oversized control into a nested layout shrinks it', async ({ page }) => {
    await designer.applyXaml(`<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
    <AbsoluteLayout WidthRequest="800" HeightRequest="600">
        <AbsoluteLayout x:Name="Narrow" BackgroundColor="#eeeeee"
                        AbsoluteLayout.LayoutBounds="10,10,80,200"
                        WidthRequest="80" HeightRequest="200" />
        <Button x:Name="Wide" Text="Wide" WidthRequest="160" HeightRequest="40"
                AbsoluteLayout.LayoutBounds="200,20,160,40" />
    </AbsoluteLayout>
</ContentPage>`);

    const button = designer.canvas.locator('[data-element-name="Wide"]');
    const panel = designer.canvas.locator('[data-element-name="Narrow"]');
    await expect(button).toBeVisible();
    await expect(panel).toBeVisible();

    await button.click({ position: { x: 6, y: 6 } });
    await expect(button).toHaveAttribute('data-selected', 'true');

    const from = (await button.boundingBox())!;
    const to = (await panel.boundingBox())!;
    await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
    await page.mouse.down();
    await page.mouse.move(to.x + to.width / 2, to.y + 30, { steps: 12 });
    await page.mouse.up();

    await expect.poll(async () => designer.getXaml()).toMatch(
      /<AbsoluteLayout x:Name="Narrow"[\s\S]*?<Button x:Name="Wide"/
    );
    await designer.selectFirst('Button');
    await designer.expectPropertyNumber('width', value => value <= 80);
  });

  test('hierarchy move buttons change XAML child order', async ({ page }) => {
    await designer.applyXaml(`<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
    <AbsoluteLayout WidthRequest="800" HeightRequest="600">
        <Label x:Name="First" Text="First" AbsoluteLayout.LayoutBounds="10,10,80,30" WidthRequest="80" HeightRequest="30" />
        <Label x:Name="Second" Text="Second" AbsoluteLayout.LayoutBounds="10,50,80,30" WidthRequest="80" HeightRequest="30" />
    </AbsoluteLayout>
</ContentPage>`);

    await designer.openHierarchy();
    const first = designer.hierarchy.locator('[data-element-name="First"]');
    await expect(first).toBeVisible();
    const id = (await first.getAttribute('data-testid'))!.replace('hierarchy-node-', '');
    await page.getByTestId(`hierarchy-move-down-${id}`).click({ force: true });

    const xaml = await designer.xamlWhen(value =>
      value.indexOf('x:Name="Second"') < value.indexOf('x:Name="First"')
    );
    expect(xaml.indexOf('x:Name="Second"')).toBeLessThan(xaml.indexOf('x:Name="First"'));
  });
});
