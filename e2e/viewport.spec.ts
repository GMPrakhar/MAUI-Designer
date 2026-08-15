import { test, expect } from '@playwright/test';
import { DesignerPage } from './helpers/designer-page';

test.describe('Live preview viewport', () => {
  let designer: DesignerPage;

  test.beforeEach(async ({ page }) => {
    designer = new DesignerPage(page);
    await designer.goto();
    await page.reload();
    await expect(designer.canvas).toBeVisible();
  });

  test('zooming in and out updates the zoom readout and the canvas transform', async ({ page }) => {
    await expect(page.getByTestId('zoom-value')).toHaveText('100%');

    await page.getByTestId('zoom-in').click();
    await expect(page.getByTestId('zoom-value')).not.toHaveText('100%');
    const zoomed = Number(await page.getByTestId('canvas-scale').getAttribute('data-zoom'));
    expect(zoomed).toBeGreaterThan(1);

    await page.getByTestId('zoom-out').click();
    await expect
      .poll(async () => Number(await page.getByTestId('canvas-scale').getAttribute('data-zoom')))
      .toBeLessThan(zoomed);
  });

  test('reset view returns the canvas to 100%', async ({ page }) => {
    await page.getByTestId('zoom-in').click();
    await page.getByTestId('zoom-in').click();
    await page.getByTestId('zoom-reset').click();

    await expect(page.getByTestId('zoom-value')).toHaveText('100%');
    await expect(page.getByTestId('canvas-scale')).toHaveAttribute('data-zoom', '1');
  });

  test('fit to window picks a zoom that fits the design surface', async ({ page }) => {
    await page.getByTestId('zoom-fit').click();

    const zoom = Number(await page.getByTestId('canvas-scale').getAttribute('data-zoom'));
    expect(zoom).toBeGreaterThan(0.25);
    expect(zoom).toBeLessThanOrEqual(3);
  });

  test('zoom is clamped at the maximum', async ({ page }) => {
    for (let i = 0; i < 20; i++) {
      await page.getByTestId('zoom-in').click();
    }
    await expect(page.getByTestId('zoom-value')).toHaveText('300%');
  });

  test('Ctrl + wheel zooms the canvas', async ({ page }) => {
    const box = (await designer.canvas.boundingBox())!;
    await page.mouse.move(box.x + 200, box.y + 200);
    await page.keyboard.down('Control');
    await page.mouse.wheel(0, -240);
    await page.keyboard.up('Control');

    await expect
      .poll(async () => Number(await page.getByTestId('canvas-scale').getAttribute('data-zoom')))
      .toBeGreaterThan(1);
  });

  test('the zoom level is restored after a reload', async ({ page }) => {
    await page.getByTestId('zoom-in').click();
    const zoom = await page.getByTestId('zoom-value').textContent();

    await page.reload();
    await expect(page.getByTestId('zoom-value')).toHaveText(zoom!.trim());
  });

  test('choosing a device preset resizes the design surface', async ({ page }) => {
    await page.getByTestId('device-select').selectOption('tablet');

    const width = await designer.canvas.evaluate(node => (node as HTMLElement).style.width);
    expect(width).not.toBe('');

    await designer.canvas.click({ position: { x: 10, y: 10 } });
    const rootWidth = await designer.getPropertyNumber('width');
    expect(rootWidth).toBeGreaterThan(700);
    await designer.expectXamlToContain(`WidthRequest="${rootWidth}"`);
  });

  test('switching to a phone preset makes the surface narrower', async ({ page }) => {
    await page.getByTestId('device-select').selectOption('tablet');
    const tabletWidth = await designer.canvas.evaluate(node =>
      Number.parseFloat((node as HTMLElement).style.width)
    );

    await page.getByTestId('device-select').selectOption('phone');
    await expect
      .poll(async () =>
        designer.canvas.evaluate(node => Number.parseFloat((node as HTMLElement).style.width))
      )
      .toBeLessThan(tabletWidth);
  });

  test('a device change can be undone', async ({ page }) => {
    const original = await designer.canvas.evaluate(node =>
      Number.parseFloat((node as HTMLElement).style.width)
    );

    await page.getByTestId('device-select').selectOption('tablet');
    await expect
      .poll(async () =>
        designer.canvas.evaluate(node => Number.parseFloat((node as HTMLElement).style.width))
      )
      .not.toBe(original);

    await designer.undoButton.click();
    await expect
      .poll(async () =>
        designer.canvas.evaluate(node => Number.parseFloat((node as HTMLElement).style.width))
      )
      .toBe(original);
  });

  test('the dark theme preview can be toggled', async ({ page }) => {
    const container = page.getByTestId('canvas-container');
    await expect(container).toHaveAttribute('data-theme', 'light');

    await page.getByTestId('toggle-theme').click();
    await expect(container).toHaveAttribute('data-theme', 'dark');

    await page.reload();
    await expect(page.getByTestId('canvas-container')).toHaveAttribute('data-theme', 'dark');
  });

  test('panning with space and drag moves the canvas', async ({ page }) => {
    const before = await page.getByTestId('canvas-scale').evaluate(node =>
      getComputedStyle(node as HTMLElement).transform
    );

    const box = (await designer.canvas.boundingBox())!;
    await page.keyboard.down('Space');
    await page.mouse.move(box.x + 200, box.y + 200);
    await page.mouse.down();
    await page.mouse.move(box.x + 320, box.y + 260, { steps: 10 });
    await page.mouse.up();
    await page.keyboard.up('Space');

    await expect
      .poll(async () =>
        page.getByTestId('canvas-scale').evaluate(node =>
          getComputedStyle(node as HTMLElement).transform
        )
      )
      .not.toBe(before);
  });

  test('elements stay clickable while the canvas is zoomed', async ({ page }) => {
    await designer.addControl('Button');
    await designer.setProperty('x', '60');
    await designer.setProperty('y', '60');

    await page.getByTestId('zoom-in').click();
    await page.getByTestId('zoom-in').click();

    await designer.canvas.click({ position: { x: 5, y: 5 } });
    await designer.selectFirst('Button');
    await expect(designer.selectionCount()).toHaveText('1 selected');
    await designer.expectProperty('x', 60);
  });
});
