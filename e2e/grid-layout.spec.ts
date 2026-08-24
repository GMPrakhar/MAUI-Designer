import { expect, test } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

const GRID_PAGE = `<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
  <Grid x:Name="Board" ColumnDefinitions="*,*" RowDefinitions="*,*" WidthRequest="400" HeightRequest="300">
    <Label x:Name="Cell" Text="Hi" Grid.Row="0" Grid.Column="0" />
  </Grid>
</ContentPage>`;

test.describe('Grid layout', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
    await designer.applyXaml(GRID_PAGE);
  });

  test('column span stretches the child across both columns', async ({ page }) => {
    await designer.selectFirst('Label');
    const child = page.getByTestId(/^grid-child-/).first();
    const before = (await child.boundingBox())!;

    await designer.setProperty('columnSpan', '2');

    await expect.poll(async () => {
      const box = (await child.boundingBox())!;
      return box.width;
    }).toBeGreaterThan(before.width * 1.5);
  });

  test('row span stretches the child across both rows', async ({ page }) => {
    await designer.selectFirst('Label');
    const child = page.getByTestId(/^grid-child-/).first();
    const before = (await child.boundingBox())!;

    await designer.setProperty('rowSpan', '2');

    await expect.poll(async () => {
      const box = (await child.boundingBox())!;
      return box.height;
    }).toBeGreaterThan(before.height * 1.5);
  });

  test('an Absolute column is the requested number of pixels wide', async ({ page }) => {
    await designer.canvas.locator('[data-element-type="Grid"]').first().click();

    await page.getByTestId('grid-column-type-0').selectOption('Absolute');
    await expect(page.getByTestId('grid-column-value-0')).toHaveValue('80');

    const overlay = designer.canvas.locator('.grid-visualization').first();
    await expect.poll(async () => {
      return overlay.evaluate(node => {
        const first = (node.children[0] as HTMLElement | undefined)?.offsetWidth || 0;
        return first;
      });
    }).toBeCloseTo(80, 0);
  });

  test('a Star column grows when its neighbour is pinned to Absolute', async ({ page }) => {
    await designer.canvas.locator('[data-element-type="Grid"]').first().click();

    await page.getByTestId('grid-column-type-0').selectOption('Absolute');

    const overlay = designer.canvas.locator('.grid-visualization').first();
    await expect.poll(async () => {
      return overlay.evaluate(node => {
        const first = (node.children[0] as HTMLElement | undefined)?.offsetWidth || 0;
        const second = (node.children[1] as HTMLElement | undefined)?.offsetWidth || 0;
        return second - first;
      });
    }).toBeGreaterThan(100);
  });

  test('align centre on a grid child writes HorizontalOptions rather than x', async ({ page }) => {
    await designer.selectFirst('Label');
    await designer.alignButton('center').click();

    await expect(page.getByTestId('prop-horizontalOptions')).toHaveValue('Center');
    await designer.expectXamlToContain('HorizontalOptions="Center"');
  });
});
