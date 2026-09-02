import { test, expect, Page } from '@playwright/test';

import { DesignerPage } from './helpers/designer-page';

/**
 * The designer is also shipped inside Visual Studio and VS Code, where the open
 * document — not browser storage — is the source of truth. These tests stub the
 * WebView2 bridge so the hosted path is exercised in a plain browser.
 */

/** Installs a fake `window.chrome.webview` before the app boots. */
async function hostAsVisualStudio(page: Page) {
  await page.addInitScript(() => {
    const outbound: unknown[] = [];
    const webview = new EventTarget() as EventTarget & {
      postMessage(message: string): void;
    };
    webview.postMessage = (message: string) => outbound.push(JSON.parse(message));
    (window as any).__hostMessages = outbound;
    (window as any).chrome = { webview };
    (window as any).__sendToDesigner = (message: unknown) => {
      webview.dispatchEvent(new MessageEvent('message', { data: JSON.stringify(message) }));
    };
  });
}

/** Messages the designer has posted back to the host. */
function outbound(page: Page) {
  return page.evaluate(() => (window as any).__hostMessages as { type: string; xaml?: string }[]);
}

async function sendToDesigner(page: Page, message: unknown) {
  await page.evaluate(msg => (window as any).__sendToDesigner(msg), message);
}

test.describe('IDE host integration', () => {
  test('announces itself and asks the host for control manifests', async ({ page }) => {
    await hostAsVisualStudio(page);
    const designer = new DesignerPage(page);
    await designer.goto();

    await expect
      .poll(async () => (await outbound(page)).map(m => m.type))
      .toContain('designer.ready');

    await expect(page.getByText('XAML Editor', { exact: true })).toHaveCount(0);
    expect((await outbound(page)).map(m => m.type)).not.toContain('document.changed');

    await sendToDesigner(page, { type: 'host.ready', host: 'visual-studio', fileName: 'MainPage.xaml' });

    await expect
      .poll(async () => (await outbound(page)).map(m => m.type))
      .toContain('manifests.request');
  });

  test('opens the document the host pushes', async ({ page }) => {
    await hostAsVisualStudio(page);
    const designer = new DesignerPage(page);
    await designer.goto();

    await sendToDesigner(page, {
      type: 'document.load',
      fileName: 'LoginPage.xaml',
      xaml: `<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
  <AbsoluteLayout>
    <Label Text="Hosted label" />
  </AbsoluteLayout>
</ContentPage>`
    });

    await expect(designer.canvas.locator('[data-element-type="Label"]')).toHaveCount(1);
    await expect(designer.canvas.getByText('Hosted label')).toBeVisible();
  });

  test('streams every edit back to the host', async ({ page }) => {
    await hostAsVisualStudio(page);
    const designer = new DesignerPage(page);
    await designer.goto();

    await sendToDesigner(page, {
      type: 'document.load',
      fileName: 'MainPage.xaml',
      xaml: '<ContentPage><AbsoluteLayout /></ContentPage>'
    });
    await designer.addControl('Button');

    await expect
      .poll(async () => {
        const changes = (await outbound(page)).filter(m => m.type === 'document.changed');
        return changes.length > 0 && changes[changes.length - 1].xaml!.includes('<Button');
      })
      .toBe(true);
  });

  test('save writes to the host document instead of browser storage', async ({ page }) => {
    await hostAsVisualStudio(page);
    const designer = new DesignerPage(page);
    await designer.goto();

    await designer.addControl('Label');
    await designer.saveButton.click();

    await expect
      .poll(async () => (await outbound(page)).filter(m => m.type === 'document.save').length)
      .toBe(1);

    // The browser-only toast must not appear when a host owns the document
    await expect(designer.toast).toHaveCount(0);
  });

  test('registers custom controls the host discovered in NuGet packages', async ({ page }) => {
    await hostAsVisualStudio(page);
    const designer = new DesignerPage(page);
    await designer.goto();

    await sendToDesigner(page, {
      type: 'manifests.push',
      manifests: [
        {
          id: 'host.pack',
          package: 'Contoso.Maui.Controls',
          xmlns: {
            prefix: 'contoso',
            uri: 'clr-namespace:Contoso.Maui.Controls;assembly=Contoso.Maui.Controls'
          },
          controls: [
            {
              tag: 'RatingBar',
              displayName: 'Rating Bar',
              defaultWidth: 160,
              defaultHeight: 40,
              properties: []
            }
          ]
        }
      ]
    });

    await designer.openToolbox();
    await expect(page.getByTestId('custom-item-contoso-RatingBar')).toBeVisible();
  });

  test('a plain browser is unaffected and still saves locally', async ({ page }) => {
    const designer = new DesignerPage(page);
    await designer.goto();

    await designer.addControl('Label');
    await designer.saveButton.click();

    await expect(designer.toast).toContainText('Design saved to this browser');
  });
});
