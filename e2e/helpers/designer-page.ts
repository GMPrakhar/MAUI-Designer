import { Page, Locator, expect } from '@playwright/test';

/**
 * Page object for the MAUI Designer shell. Every locator is based on
 * `data-testid` hooks so the tests survive styling changes.
 */
export class DesignerPage {
  readonly canvas: Locator;
  readonly toolbox: Locator;
  readonly toolboxSearch: Locator;
  readonly hierarchy: Locator;
  readonly xamlTextarea: Locator;
  readonly xamlStatus: Locator;
  readonly undoButton: Locator;
  readonly redoButton: Locator;
  readonly saveButton: Locator;
  readonly loadButton: Locator;
  readonly clearButton: Locator;
  readonly toast: Locator;

  constructor(private readonly page: Page) {
    this.canvas = page.getByTestId('designer-canvas');
    this.toolbox = page.getByTestId('toolbox');
    this.toolboxSearch = page.getByTestId('toolbox-search');
    this.hierarchy = page.getByTestId('hierarchy-panel');
    this.xamlTextarea = page.getByTestId('xaml-textarea');
    this.xamlStatus = page.getByTestId('xaml-status');
    this.undoButton = page.getByTestId('undo-button');
    this.redoButton = page.getByTestId('redo-button');
    this.saveButton = page.getByTestId('save-button');
    this.loadButton = page.getByTestId('load-button');
    this.clearButton = page.getByTestId('clear-button');
    this.toast = page.getByTestId('app-toast');
  }

  async goto() {
    await this.page.goto('/');
    await expect(this.canvas).toBeVisible();
    // Every spec starts from a clean, predictable viewport
    await this.page.evaluate(() => {
      localStorage.removeItem('maui-designer.viewport');
      localStorage.removeItem('maui-designer.custom-controls');
    });
  }

  /** Ctrl-clicks an element to add or remove it from the selection. */
  async toggleSelect(type: string, index = 0) {
    await this.canvasElements(type).nth(index).click({ modifiers: ['Shift'] });
  }

  /** Drags a rubber band rectangle over the canvas in canvas coordinates. */
  async marquee(from: { x: number; y: number }, to: { x: number; y: number }) {
    const box = (await this.canvas.boundingBox())!;
    await this.page.mouse.move(box.x + from.x, box.y + from.y);
    await this.page.mouse.down();
    await this.page.mouse.move(box.x + to.x, box.y + to.y, { steps: 12 });
    await this.page.mouse.up();
  }

  /** Drags an already selected element by an offset. */
  async dragElementBy(type: string, delta: { x: number; y: number }, index = 0) {
    const element = this.canvasElements(type).nth(index);
    const box = (await element.boundingBox())!;
    await this.page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await this.page.mouse.down();
    await this.page.mouse.move(box.x + box.width / 2 + delta.x, box.y + box.height / 2 + delta.y, { steps: 12 });
    await this.page.mouse.up();
  }

  selectionCount(): Locator {
    return this.page.getByTestId('selection-count');
  }

  async openToolbox() {
    await this.page.getByTestId('tab-toolbox').click();
    await expect(this.toolbox).toBeVisible();
  }

  async openHierarchy() {
    await this.page.getByTestId('tab-hierarchy').click();
    await expect(this.hierarchy).toBeVisible();
  }

  /** Adds a control by clicking its toolbox entry. */
  async addControl(type: string) {
    await this.openToolbox();
    await this.page.getByTestId(`toolbox-item-${type}`).click();
  }

  /** Adds a custom (third party) control from the toolbox by prefix and tag. */
  async addCustomControl(prefix: string, tag: string) {
    await this.openToolbox();
    await this.page.getByTestId(`custom-item-${prefix}-${tag}`).click();
  }

  /** Drags a control from the toolbox onto the canvas. */
  async dragControlToCanvas(type: string, position?: { x: number; y: number }) {
    await this.openToolbox();
    await this.page
      .getByTestId(`toolbox-item-${type}`)
      .dragTo(this.canvas, position ? { targetPosition: position } : undefined);
  }

  /** All rendered canvas elements of a given type. */
  canvasElements(type?: string): Locator {
    return type
      ? this.canvas.locator(`[data-element-type="${type}"]`)
      : this.canvas.locator('[data-element-id]');
  }

  selectedCanvasElement(): Locator {
    return this.canvas.locator('[data-selected="true"]');
  }

  hierarchyNodes(): Locator {
    return this.hierarchy.locator('[data-testid^="hierarchy-node-"]');
  }

  async selectFirst(type: string) {
    await this.canvasElements(type).first().click();
  }

  async setXaml(xaml: string) {
    await this.xamlTextarea.fill(xaml);
  }

  async applyXaml(xaml?: string) {
    if (xaml !== undefined) {
      await this.setXaml(xaml);
    }
    await this.page.getByTestId('xaml-apply').click();
  }

  async resetXaml() {
    await this.page.getByTestId('xaml-reset').click();
  }

  async getXaml(): Promise<string> {
    return this.xamlTextarea.inputValue();
  }

  /**
   * The XAML pane is regenerated from an observable, so a one-shot read can
   * race with Angular's change detection on slower machines. Poll instead.
   */
  async expectXamlToContain(...fragments: string[]) {
    for (const fragment of fragments) {
      await expect.poll(() => this.getXaml(), { timeout: 10_000 }).toContain(fragment);
    }
  }

  /** Resolves with the XAML once it satisfies the predicate. */
  async xamlWhen(predicate: (xaml: string) => boolean): Promise<string> {
    await expect.poll(() => this.getXaml().then(predicate), { timeout: 10_000 }).toBe(true);
    return this.getXaml();
  }

  /** Reads a property input from the properties panel. */
  async propertyValue(name: string): Promise<string> {
    return this.page.getByTestId(`prop-${name}`).inputValue();
  }

  async setProperty(name: string, value: string) {
    const input = this.page.getByTestId(`prop-${name}`);
    await input.fill(value);
    await input.dispatchEvent('input');
  }

  /** Waits for a property input to settle on a value. */
  async expectProperty(name: string, value: string | number) {
    await expect(this.page.getByTestId(`prop-${name}`)).toHaveValue(String(value));
  }

  async expectPropertyNumber(name: string, assertion: (value: number) => boolean) {
    await expect
      .poll(() => this.propertyValue(name).then(raw => assertion(Number(raw))), { timeout: 10_000 })
      .toBe(true);
  }

  /** Reads a numeric property input once it has settled. */
  async getPropertyNumber(name: string): Promise<number> {
    await expect(this.page.getByTestId(`prop-${name}`)).toBeVisible();
    return Number(await this.propertyValue(name));
  }

  alignButton(direction: 'left' | 'center' | 'right' | 'top' | 'middle' | 'bottom'): Locator {
    return this.page.getByTestId(`align-${direction}`);
  }
}

export const SAMPLE_XAML = `<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="YourApp.MainPage">
    <AbsoluteLayout x:Name="RootLayout" WidthRequest="800" HeightRequest="600" BackgroundColor="#ffffff">
        <Label x:Name="Title" Text="Hello MAUI" AbsoluteLayout.LayoutBounds="20,30,180,40" WidthRequest="180" HeightRequest="40" FontSize="18" TextColor="#112233" />
        <Button x:Name="Submit" Text="Send" AbsoluteLayout.LayoutBounds="20,90,120,44" WidthRequest="120" HeightRequest="44" BackgroundColor="#007acc" />
    </AbsoluteLayout>
</ContentPage>`;
