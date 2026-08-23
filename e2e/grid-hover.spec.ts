import { expect, test } from '@playwright/test';

import { DesignerPage } from './helpers/designer-page';

/**
 * The cell highlighted under the pointer is the only feedback showing where a
 * drop will land, so if it points at the wrong cell the designer is actively
 * misleading. These drive the real canvas rather than the maths behind it,
 * because every bug this covers came from the wiring and not the arithmetic.
 */
test.describe('Grid cell hover', () => {
  /** A grid whose columns are deliberately unequal: 40px, then the rest. */
  const unevenGrid = `<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
  <Grid ColumnDefinitions="40,*" RowDefinitions="*,*" WidthRequest="300" HeightRequest="200" />
</ContentPage>`;

  /** Reads the row/column of whichever overlay cell is highlighted. */
  async function highlightedCell(designer: DesignerPage) {
    return designer.canvas.evaluate(canvas => {
      const overlay = canvas.querySelector('.grid-visualization');
      if (!overlay) {
        return null;
      }

      const cells = Array.from(overlay.children);
      const index = cells.findIndex(cell => cell.classList.contains('highlight-cell'));
      if (index < 0) {
        return null;
      }

      const columns = getComputedStyle(overlay).gridTemplateColumns.split(' ').length;
      return { row: Math.floor(index / columns), column: index % columns };
    });
  }

  /** Moves the pointer to a point inside the grid, in grid-local pixels. */
  async function hoverGrid(designer: DesignerPage, page: any, x: number, y: number) {
    const grid = designer.canvas.locator('.element-grid').first();
    const box = (await grid.boundingBox())!;
    await page.mouse.move(box.x + x, box.y + y);
    return box;
  }

  test('highlights the cell under the pointer', async ({ page }) => {
    const designer = new DesignerPage(page);
    await designer.goto();
    await designer.applyXaml(unevenGrid);

    const grid = designer.canvas.locator('.element-grid').first();
    await expect(grid).toBeVisible();
    const box = (await grid.boundingBox())!;

    await hoverGrid(designer, page, 10, 10);
    await expect.poll(() => highlightedCell(designer)).toEqual({ row: 0, column: 0 });

    // Bottom right quadrant, well inside the wide second column.
    await hoverGrid(designer, page, box.width - 20, box.height - 20);
    await expect.poll(() => highlightedCell(designer)).toEqual({ row: 1, column: 1 });
  });

  test('tracks the pointer as it moves within one cell', async ({ page }) => {
    const designer = new DesignerPage(page);
    await designer.goto();
    await designer.applyXaml(unevenGrid);

    const grid = designer.canvas.locator('.element-grid').first();
    await expect(grid).toBeVisible();
    const box = (await grid.boundingBox())!;

    // Entering the grid once and then moving used to leave the highlight
    // wherever it first landed, because the handler was bound to mouseover.
    await hoverGrid(designer, page, 10, 10);
    await expect.poll(() => highlightedCell(designer)).toEqual({ row: 0, column: 0 });

    await hoverGrid(designer, page, 10, box.height - 20);
    await expect.poll(() => highlightedCell(designer)).toEqual({ row: 1, column: 0 });
  });

  test('respects an uneven column boundary', async ({ page }) => {
    const designer = new DesignerPage(page);
    await designer.goto();
    await designer.applyXaml(unevenGrid);

    const grid = designer.canvas.locator('.element-grid').first();
    await expect(grid).toBeVisible();

    // The first column is 40px wide. Splitting the grid evenly would place x=70
    // in column 0, so this is the assertion that fails on equal-cell maths.
    await hoverGrid(designer, page, 70, 20);
    await expect.poll(() => highlightedCell(designer)).toEqual({ row: 0, column: 1 });

    await hoverGrid(designer, page, 20, 20);
    await expect.poll(() => highlightedCell(designer)).toEqual({ row: 0, column: 0 });
  });

  test('stays correct while the canvas is zoomed', async ({ page }) => {
    const designer = new DesignerPage(page);
    await designer.goto();
    await designer.applyXaml(unevenGrid);

    await page.getByTestId('zoom-in').click();
    await page.getByTestId('zoom-in').click();

    const grid = designer.canvas.locator('.element-grid').first();
    await expect(grid).toBeVisible();
    const box = (await grid.boundingBox())!;

    // boundingBox is in screen pixels, so it already includes the zoom. The
    // highlight is only right if the handler divides the pointer offset back
    // out; without that it drifts further from the pointer the more you zoom.
    await hoverGrid(designer, page, box.width - 15, box.height - 15);
    await expect.poll(() => highlightedCell(designer)).toEqual({ row: 1, column: 1 });

    await hoverGrid(designer, page, box.width - 15, 15);
    await expect.poll(() => highlightedCell(designer)).toEqual({ row: 0, column: 1 });
  });

  test('clears the highlight when the pointer leaves', async ({ page }) => {
    const designer = new DesignerPage(page);
    await designer.goto();
    await designer.applyXaml(unevenGrid);

    await hoverGrid(designer, page, 10, 10);
    await expect.poll(() => highlightedCell(designer)).not.toBeNull();

    await page.mouse.move(5, 5);
    await expect.poll(() => highlightedCell(designer)).toBeNull();
  });
});
