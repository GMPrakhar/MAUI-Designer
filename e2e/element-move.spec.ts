import { test, expect } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

/**
 * Regression coverage for dragging elements that already live on the canvas.
 * The drop position used to be read from the CDK drag transform, which is a delta and not a
 * canvas coordinate, so elements jumped to seemingly random places.
 */
test.describe('Moving elements on the canvas', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    await page.setViewportSize({ width: 1600, height: 1000 });
    designer = new DesignerPage(page);
    await designer.goto();
    await expect(designer.canvas).toBeVisible();
  });

  async function bounds(page: any, name: string) {
    const xaml = await new DesignerPage(page).getXaml();
    const match = new RegExp(`x:Name="${name}"[^>]*LayoutBounds="([^"]+)"`).exec(xaml);
    const [x, y] = (match?.[1] ?? '').split(',').map(Number);
    return { x, y };
  }

  async function drag(page: any, name: string, dx: number, dy: number) {
    const element = page.locator(`[data-element-name="${name}"]`);
    await element.click({ position: { x: 6, y: 6 } });
    const box = (await element.boundingBox())!;
    const cx = box.x + box.width / 2;
    const cy = box.y + box.height / 2;
    await page.mouse.move(cx, cy);
    await page.mouse.down();
    await page.mouse.move(cx + dx / 2, cy + dy / 2, { steps: 8 });
    await page.mouse.move(cx + dx, cy + dy, { steps: 8 });
    await page.mouse.up();
    await page.keyboard.press('Escape');
  }

  test('an element lands where it was dropped instead of at the drag distance', async ({ page }) => {
    await designer.openToolbox();
    await page.getByTestId('starter-login').click();

    const before = await bounds(page, 'SignInButton');
    expect(before).toEqual({ x: 24, y: 364 });

    await drag(page, 'SignInButton', 0, -100);

    const after = await bounds(page, 'SignInButton');
    expect(after.x).toBe(24);
    expect(Math.abs(after.y - 264)).toBeLessThanOrEqual(8);
  });

  test('dragging the same element twice does not drift', async ({ page }) => {
    await designer.openToolbox();
    await page.getByTestId('starter-login').click();

    const start = await bounds(page, 'Busy');
    await drag(page, 'Busy', 60, 40);
    const once = await bounds(page, 'Busy');
    expect(Math.abs(once.x - (start.x + 60))).toBeLessThanOrEqual(8);
    expect(Math.abs(once.y - (start.y + 40))).toBeLessThanOrEqual(8);

    await drag(page, 'Busy', -60, -40);
    const twice = await bounds(page, 'Busy');
    expect(Math.abs(twice.x - start.x)).toBeLessThanOrEqual(8);
    expect(Math.abs(twice.y - start.y)).toBeLessThanOrEqual(8);
  });

  test('a move stays accurate while the canvas is zoomed', async ({ page }) => {
    await designer.openToolbox();
    await page.getByTestId('starter-login').click();
    await page.getByTestId('zoom-out').click();
    await page.getByTestId('zoom-out').click();

    const start = await bounds(page, 'Title');
    await drag(page, 'Title', 80, 60);
    const end = await bounds(page, 'Title');

    const zoom = Number(await page.getByTestId('canvas-scale').getAttribute('data-zoom'));
    expect(Math.abs(end.x - (start.x + 80 / zoom))).toBeLessThanOrEqual(10);
    expect(Math.abs(end.y - (start.y + 60 / zoom))).toBeLessThanOrEqual(10);
  });

  test('a move is a single undo step that restores the original position', async ({ page }) => {
    await designer.openToolbox();
    await page.getByTestId('starter-login').click();

    const start = await bounds(page, 'Subtitle');
    await drag(page, 'Subtitle', 40, 120);
    expect(await bounds(page, 'Subtitle')).not.toEqual(start);

    await designer.undoButton.click();
    await expect
      .poll(async () => (await bounds(page, 'Subtitle')).y)
      .toBe(start.y);
  });

  test('an element dropped inside a nested layout is positioned relative to it', async ({ page }) => {
    await designer.applyXaml(`<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
    <AbsoluteLayout x:Name="Root" WidthRequest="600" HeightRequest="600">
        <AbsoluteLayout x:Name="Panel" AbsoluteLayout.LayoutBounds="200,200,300,300" WidthRequest="300" HeightRequest="300" BackgroundColor="#eeeeee" />
        <Label x:Name="Mover" Text="Move me" AbsoluteLayout.LayoutBounds="20,20,100,30" WidthRequest="100" HeightRequest="30" />
    </AbsoluteLayout>
</ContentPage>`);

    // Drop the label near the top left corner of the nested panel
    await drag(page, 'Mover', 210, 210);

    const xaml = await designer.getXaml();
    const nested = /<AbsoluteLayout x:Name="Panel"[\s\S]*?<Label x:Name="Mover"[^>]*LayoutBounds="([^"]+)"/.exec(xaml);
    expect(nested, 'the label should now be a child of Panel').not.toBeNull();

    const [x, y] = nested![1].split(',').map(Number);
    // Local coordinates inside the panel, not the canvas coordinates of the drop point
    expect(x).toBeLessThan(120);
    expect(y).toBeLessThan(120);
  });
});
